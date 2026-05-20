# Research: Tool Call Span Enrichment

**Feature Branch**: `002-tool-call-enrichment`
**Created**: 2026-05-19

---

## R-001 — Public MAF integration point for tool interception

**Question**: Does MAF expose a stable public hook for intercepting tool execution, or would the package need internal MAF types?

**Decision**: Use the public function-invocation middleware exposed by `AIAgentBuilder.Use(Func<AIAgent, FunctionInvocationContext, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>>, CancellationToken, ValueTask<object?>>)`.

**Rationale**: `Microsoft.Agents.AI` already exposes `FunctionInvocationDelegatingAgentBuilderExtensions.Use(...)`, which wraps `AIFunction` tools inside the `FunctionInvokingChatClient` loop. This is the correct public integration point for a tool telemetry package: it runs exactly around tool execution, receives the current `FunctionInvocationContext`, and does not require internal MAF types.

**Implementation shape**:
```csharp
public static AIAgentBuilder UseToolTelemetry(
    this AIAgentBuilder builder,
    Action<ToolTelemetryOptions>? configure = null)
{
    var options = new ToolTelemetryOptions();
    configure?.Invoke(options);

    return builder.Use(async (agent, context, next, cancellationToken) =>
    {
        // map context -> snapshot
        // enrich current MAF execute_tool span, or create fallback span
        // call next(context, cancellationToken)
        // enrich result/error
    });
}
```

**Alternatives considered**:
- Wrapping `DelegatingAIAgent.RunCoreAsync` only — rejected; too late and too coarse, because tool execution happens inside the inner `FunctionInvokingChatClient` loop.
- Reflection over internal `FunctionInvokingChatClient` state — rejected by constitution.

---

## R-002 — Source of tool metadata (name, call id, input)

**Question**: Which public members expose the metadata needed for package enrichment attributes?

**Decision**: Read tool metadata from `FunctionInvocationContext.Function`, `FunctionInvocationContext.Arguments`, and `FunctionInvocationContext.CallContent`.

**Rationale**: MAF populates `FunctionInvocationContext` with the invoked `AIFunction`, the concrete `AIFunctionArguments`, and a `FunctionCallContent` carrying at least `Name`, `CallId`, and normalized arguments. This is the minimal stable surface required by the spec.

**Attribute mapping**:
- Fallback `genai.tool.name` → `context.Function.Name`, falling back to `context.CallContent.Name`
- Fallback `genai.tool.call_id` and retry tracking id → `context.CallContent.CallId` when non-empty
- `genai.tool.input` → serialized `context.Arguments` / `context.CallContent.Arguments`

MAF `execute_tool` spans already carry standard `gen_ai.tool.name` and `gen_ai.tool.call.id`, so the package must not duplicate those identity attributes under `genai.*` on MAF spans.

**Alternatives considered**:
- Reading from raw `ChatMessage` content later in the pipeline — rejected; loses direct execution context and increases coupling to message-shape details.

---

## R-003 — Retry tracking scope and concurrency model

**Question**: How should retry attempts be counted safely when multiple tools execute concurrently within one `invoke_agent` span?

**Decision**: Scope retry state to the current parent `Activity` and track call-id counts in a `ConcurrentDictionary<string, int>` stored in a `ConditionalWeakTable<Activity, ConcurrentDictionary<string, int>>`.

**Rationale**: The retry semantics are explicitly per `invoke_agent` span, not per session. Using the current parent `Activity` as the registry key matches the spec directly and avoids leaking retry state across invocations. `ConditionalWeakTable` ties lifetime to the parent span object so state disappears automatically when the invocation is no longer referenced.

**Implementation sketch**:
```csharp
internal sealed class InvocationAttemptRegistry
{
    private readonly ConditionalWeakTable<Activity, ConcurrentDictionary<string, int>> _counts = new();

    public (bool IsRetry, int AttemptIndex)? Record(Activity? parent, string? callId)
    {
        if (parent is null || string.IsNullOrWhiteSpace(callId))
        {
            return null;
        }

        var perInvocation = _counts.GetOrCreateValue(parent);
        int attempt = perInvocation.AddOrUpdate(callId, 1, static (_, current) => current + 1);
        return (attempt > 1, attempt);
    }
}
```

**Alternatives considered**:
- Store attempt counters in `AgentSession.StateBag` — rejected; wrong lifetime and unnecessary persistence.
- Key by `TraceId` string — rejected; more allocation and weaker lifetime semantics than the actual parent `Activity` instance.

---

## R-004 — Valid JSON truncation strategy

**Question**: How can input/output be truncated to a maximum length while keeping the final attribute value as valid JSON?

**Decision**: Introduce an internal `ToolPayloadSerializer` that converts the payload to a JSON DOM, truncates oversized string values before final serialization, and, when needed, replaces oversized nested subtrees with short JSON-safe sentinel strings until the final serialized payload fits the configured limit.

**Rationale**: Arbitrarily clipping the final serialized string would produce invalid JSON and break downstream parsing. Pre-serialization truncation alone is insufficient for deep objects or large arrays. A DOM-based best-effort shrink pass preserves valid JSON while keeping the payload diagnostically useful.

**Rules**:
- Strings are truncated first.
- Error outputs are serialized as `{ "type": "...", "message": "..." }`.
- Empty inputs serialize as `{}`.
- If serialization still cannot fit the limit after shrinking, the payload is replaced with a compact sentinel JSON string value (still valid JSON).

**Alternatives considered**:
- Raw string clipping — rejected; invalid JSON.
- Envelope object like `{ truncated, preview }` — rejected; changes the payload shape for every truncated value and makes normal success/error queries less direct.

---

## R-005 — Span creation and parent relationship

**Question**: How should tool telemetry avoid duplicating MAF `execute_tool` spans while still working without MAF OpenTelemetry?

**Decision**: If `Activity.Current` is MAF's `execute_tool` span (`gen_ai.operation.name = execute_tool`), enrich that existing span. Otherwise, start a fallback `agent_tool_call` span from a dedicated `ActivitySource` (`Melic.AgentFramework.Observability.Tools` by default) using `ActivityKind.Internal`, relying on `Activity.Current` at tool-call start as the parent when present.

**Rationale**: MAF already emits an `execute_tool` span that Application Insights understands. The package should add differentiated value to that span — bounded payloads, retry markers, and future redaction hooks — rather than visually duplicating the trace. If no current MAF tool span exists, `StartActivity` still creates a fallback span or returns `null` when there are no listeners; both outcomes are acceptable and best-effort.

**Alternatives considered**:
- Always creating a nested `agent_tool_call` span — rejected; it duplicates MAF telemetry and makes App Insights traces noisier.

---

## R-006 — Error recording shape on failed tool calls

**Question**: What should be written to the span when the tool throws?

**Decision**: Set the Activity/OpenTelemetry span status to `ERROR`, set the status description to the exception message, and serialize `genai.tool.output` as a JSON object with at least `type` and `message` when output capture is enabled.

**Rationale**: This keeps the error surface queryable and consistent with the clarified spec. A structured payload is more useful than a plain string when diagnosing repeated failures in KQL/App Insights.

**Example**:
```json
{
  "type": "InvalidOperationException",
  "message": "Missing order id"
}
```

**Alternatives considered**:
- Omit `genai.tool.output` for errors — rejected; loses the payload channel developers explicitly asked for.
- Store only the message string — rejected; less structured and less query-friendly.
