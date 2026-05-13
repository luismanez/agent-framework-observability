# Research: Session Identity and Enrichment

**Feature Branch**: `001-session-identity-enrichment`
**Created**: 2026-05-13

---

## R-001 — MAF `AIAgentBuilder` integration point

**Question**: Does MAF expose a builder API suitable for registering decorator agents?

**Decision**: Use `AIAgentBuilder.Use(Func<AIAgent, AIAgent> agentFactory)`.

**Rationale**: `AIAgentBuilder` (`Microsoft.Agents.AI`) is the canonical decorator-pipeline
builder in MAF. It accepts factory delegates via `Use()` and builds the pipeline with
`Build()`. The first `Use()` call becomes the outermost decorator. This is how MAF's own
`Purview` extension hooks in — confirmed in `PurviewExtensions.cs`.

**Implementation**:
```csharp
public static AIAgentBuilder UseSessionTelemetry(
    this AIAgentBuilder builder,
    Action<SessionTelemetryOptions>? configure = null)
{
    var options = new SessionTelemetryOptions();
    configure?.Invoke(options);
    return builder.Use(inner => new SessionTelemetryAgent(inner, options));
}
```

**Alternatives considered**:
- `IServiceCollection` extension — rejected; would require DI, violating the "no DI
  required" constraint from the spec (FR-009, SC-006).
- Direct constructor wrapping `new SessionTelemetryAgent(agent, options)` — valid fallback
  documented in quickstart, but builder pattern is the primary ergonomic API.

---

## R-002 — `AgentSessionStateBag` access pattern for `SessionStateBlock`

**Question**: How should `SessionStateBlock` be stored and retrieved?

**Decision**: Use `stateBag.TryGetValue<SessionStateBlock>(key, out var block)` and
`stateBag.SetValue<SessionStateBlock>(key, block)` with the default STJ serializer.

**Rationale**: `AgentSessionStateBag` uses a `ConcurrentDictionary` internally with
`SetValue<T>` / `TryGetValue<T>` generic methods that handle STJ serialization transparently.
Storing `SessionStateBlock` as a typed object means MAF's own session serialization round-trip
(`SerializeSessionAsync` / `DeserializeSessionAsync`) preserves the block without any extra
converter.

**Forward-compatibility requirement** (FR-021): `SessionStateBlock` must use
`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]` on optional fields so that
blocks serialized by older library versions are restored with sane defaults, and
`JsonExtensionData` on a `Dictionary<string, JsonElement>?` to capture unknown fields from
newer versions.

**Alternatives considered**:
- Manual `JsonSerializer.Serialize` to a raw string — rejected; duplicates what StateBag
  already does, and loses type-safe round-trip.

---

## R-003 — Thread safety for `SessionId` assignment (FR-018)

**Question**: How do we guarantee idempotent session-id generation under concurrent access?

**Decision**: Use `lock` on `session.StateBag` as the synchronisation primitive. Read,
check, assign, and write the `SessionStateBlock` atomically within the lock.

**Rationale**: `AgentSessionStateBag` is a `ConcurrentDictionary`, but the
read-check-write sequence (check if `SessionId` is null, then generate and store) is not
atomic across separate `TryGetValue` + `SetValue` calls. A `lock` object stored alongside
the agent (or keyed to `session`) makes the critical section explicit. The lock is held
only during the initial assignment check — subsequent reads skip the lock since `SessionId`
is immutable after first write.

**Implementation note**: `SessionStateBagAccessor` will expose a `GetOrInitialise` method
that takes the lock internally.

**Alternatives considered**:
- `Interlocked.CompareExchange` on a field — not applicable; the state is JSON-serialized
  and lives in the StateBag, not an in-memory field.

---

## R-004 — Token usage source: `AgentResponse.Usage` vs span tags (FR-022)

**Question**: What is the concrete .NET type of `AgentResponse.Usage`?

**Decision**: `AgentResponse.Usage` is `UsageDetails?` from `Microsoft.Extensions.AI`.
Use `response.Usage?.InputTokenCount` and `response.Usage?.OutputTokenCount` (both `long?`).

**Rationale**: Confirmed by reading `AgentResponse.cs` — the `Usage` property is copied
from `ChatResponse.Usage` in the MEAI constructor. The property type is `UsageDetails?`.
MEAI's `UsageDetails` exposes `InputTokenCount: long?` and `OutputTokenCount: long?`.

**Fallback path** (when `response.Usage` is null): read `genai.input_tokens` and
`genai.output_tokens` from `Activity.Current?.Tags` before the span ends.

---

## R-005 — Mode B (`BeginSessionTrace`) and `AsyncLocal` propagation (FR-014/015/016)

**Question**: How does the session span become the `Activity.Current` parent for all
invocation spans within the `using` block?

**Decision**: `BeginSessionTrace` starts an `Activity` via the Sessions `ActivitySource`
and returns it as `IDisposable`. The `Activity` API propagates via `AsyncLocal<Activity?>` 
natively — any `ActivitySource.StartActivity` call inside the same async context inherits it
as parent automatically. No manual `AsyncLocal` management needed.

**Rationale**: `System.Diagnostics.Activity` uses `AsyncLocal<Activity?>` internally for
`Activity.Current`. Starting an `Activity` sets it on the current async context; the
`DelegatingAIAgent.RunCoreAsync` call (which starts the `invoke_agent` span) runs as a
continuation in the same async flow, so it inherits the parent automatically.

**Constraint**: Mode B is only meaningful within a single process and async call chain.
Cross-process or cross-thread usage relies on Mode A (`genai.session.id` correlation).

---

## R-006 — `session.statebag.write.failures` metric (FR-024)

**Question**: What metric instrument should `session.statebag.write.failures` use?

**Decision**: `Counter<long>` on the Sessions meter (`Melic.AgentFramework.Observability.Sessions`).
Instrument name: `session.statebag.write.failures`. Tag: `gen_ai.agent.name`.

**Rationale**: A monotonically-increasing failure count is the correct primitive — operators
alert when the rate rises above zero. `Histogram` would be wasteful for a binary event.

---

## R-007 — `ChatHistoryProvider` / message count (FR note in TDD §5.7)

**Question**: Is `TryGetInMemoryChatHistory` or equivalent accessible via the public MAF API?

**Decision**: Access via `session.GetService<ChatHistoryProvider>()`. Emit
`genai.session.message_count` only when the service is non-null.

**Rationale**: `AgentSession.GetService<TService>()` is the MAF-endorsed service-locator
pattern (stable public API). `ChatHistoryProvider` is the base class for history providers.
The field is silently omitted when not resolvable — no exception, no config needed.

**Constraint**: This is a best-effort tag. Its absence does not affect any SC or FR.
