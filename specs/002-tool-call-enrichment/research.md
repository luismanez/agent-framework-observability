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
        // create child span
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

**Question**: Which public members expose the metadata needed for `genai.tool.*` attributes?

**Decision**: Read tool metadata from `FunctionInvocationContext.Function`, `FunctionInvocationContext.Arguments`, and `FunctionInvocationContext.CallContent`.

**Rationale**: MAF populates `FunctionInvocationContext` with the invoked `AIFunction`, the concrete `AIFunctionArguments`, and a `FunctionCallContent` carrying at least `Name`, `CallId`, and normalized arguments. This is the minimal stable surface required by the spec.

**Attribute mapping**:
- `genai.tool.name` → `context.Function.Name`, falling back to `context.CallContent.Name`
- `genai.tool.call_id` → `context.CallContent.CallId` when non-empty
- `genai.tool.input` → serialized `context.Arguments` / `context.CallContent.Arguments`

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

**Question**: How should `agent_tool_call` spans be created so they appear under `invoke_agent` when available but still work without `UseOpenTelemetry()`?

**Decision**: Start spans from a dedicated `ActivitySource` (`Melic.AgentFramework.Observability.Tools` by default) using `ActivityKind.Internal`, relying on `Activity.Current` at tool-call start as the parent when present.

**Rationale**: This matches the spec and mirrors how the Sessions package enriches the current invocation span. When `UseOpenTelemetry()` is outermost, the current activity is the `invoke_agent` span and tool spans become children automatically. If no current activity exists, `StartActivity` still creates a root span or returns `null` when there are no listeners; both outcomes are acceptable and best-effort.

**Alternatives considered**:
- Reusing the MAF `Experimental.Microsoft.Agents.AI` source for tool spans — rejected; this package needs its own source for selective subscription and package independence.

---

## R-006 — Error recording shape on failed tool calls

**Question**: What should be written to the span when the tool throws?

**Decision**: Set `otel.status_code = ERROR`, set `otel.status_description` to the exception message, and serialize `genai.tool.output` as a JSON object with at least `type` and `message` when output capture is enabled.

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
