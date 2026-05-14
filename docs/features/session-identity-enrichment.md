# Session Identity & Enrichment

**Package**: `Melic.AgentFramework.Observability.Sessions`
**Targets**: .NET 8, .NET 9, .NET 10

---

## Overview

Session telemetry enrichment for [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) agents. Decorates every `invoke_agent` span with a **stable session identifier**, **running token aggregates**, **custom business tags**, and an optional **session-level parent span** — with a single line in your agent builder chain and zero changes to your agent logic.

Two enrichment modes are available:

- **Mode A** (default): per-invocation span enrichment. Works across processes, serialization boundaries, and long-lived sessions.
- **Mode B** (opt-in): explicit parent span grouping all invocation spans in a single trace tree. Ideal for in-process, short-lived conversations.

---

## Installation

```xml
<PackageReference Include="Melic.AgentFramework.Observability.Sessions" Version="1.*" />
```

---

## Usage

### Scenario 1 — Minimal setup (Mode A)

One line in your agent builder chain. Every `invoke_agent` span automatically gets `genai.session.id`, `genai.session.invocation_index`, `genai.session.age_seconds`, and running token totals.

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Sessions;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseSessionTelemetry()
    .Build();

AgentSession session = await agent.CreateSessionAsync();

AgentResponse r1 = await agent.RunAsync("Hello", session);
// span tags: genai.session.id=<guid>, genai.session.invocation_index=1, ...

AgentResponse r2 = await agent.RunAsync("How are you?", session);
// span tags: genai.session.id=<same guid>, genai.session.invocation_index=2, ...
```

### Scenario 2 — Custom session identifier

Assign your own identifier (e.g., a platform conversation ID) before the first invocation.

```csharp
AgentSession session = await agent.CreateSessionAsync();
session.AssignSessionId("conv-abc-123");

AgentResponse r = await agent.RunAsync("Hello", session);
// genai.session.id = "conv-abc-123"
```

### Scenario 3 — Custom business context tags

Tags are set once and propagate to every subsequent span automatically.

```csharp
AgentSession session = await agent.CreateSessionAsync();
session.SetSessionTag("tenant.id", "acme-corp");
session.SetSessionTag("user.tier", "premium");

AgentResponse r1 = await agent.RunAsync("First message", session);
AgentResponse r2 = await agent.RunAsync("Second message", session);
// Both spans carry tenant.id and user.tier
```

> **Security note**: Custom tags are written to span attributes without filtering. Ensure tag values do not contain PII or sensitive data.

### Scenario 4 — Session persistence across processes

Session state (session ID, token totals, custom tags) survives serialization and restore.

```csharp
// Process A
AgentSession session = await agent.CreateSessionAsync();
AgentResponse r1 = await agent.RunAsync("First message", session);

JsonElement serialized = await agent.SerializeSessionAsync(session);
string json = serialized.GetRawText(); // store in DB or cache

// Process B (later)
JsonElement restored = JsonDocument.Parse(json).RootElement;
AgentSession session2 = await agent.DeserializeSessionAsync(restored);

AgentResponse r2 = await agent.RunAsync("Second message", session2);
// genai.session.invocation_index = 2, totals continue from Process A
```

### Scenario 5 — Mode B (explicit session span)

Opt in to a parent span that groups all invocation spans under one trace root.

```csharp
AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseSessionTelemetry(o => o.EnableSessionSpan = true)
    .Build();

AgentSession session = await agent.CreateSessionAsync();

using (agent.BeginSessionTrace(session))
{
    await agent.RunAsync("Turn 1", session);
    await agent.RunAsync("Turn 2", session);
    await agent.RunAsync("Turn 3", session);
} // session span closes; final aggregates are recorded on the root span

// APM view: one root span "agent_session <agent.name>" with three children
```

---

## Public API Reference

### `UseSessionTelemetry` (builder extension)

```csharp
public static AIAgentBuilder UseSessionTelemetry(
    this AIAgentBuilder builder,
    Action<SessionTelemetryOptions>? configure = null);
```

Adds the session telemetry decorator to the agent pipeline. Call once during agent construction.

### `SessionTelemetryOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `TrackTokenAggregates` | `bool` | `true` | Accumulate and emit total input/output token counts per session. |
| `EnableSessionSpan` | `bool` | `false` | Enable Mode B — explicit parent session span via `BeginSessionTrace`. |
| `StateBagKey` | `string` | `"__melic_telemetry"` | Key used in `AgentSession.StateBag` to persist session state. Must not be used by application code. |
| `ActivitySourceName` | `string` | `"Melic.AgentFramework.Observability.Sessions"` | `ActivitySource` name for Mode B session spans. |
| `MeterName` | `string` | `"Melic.AgentFramework.Observability.Sessions"` | Meter name for OTel metrics. |

### `AgentSession` extension methods

| Method | Description |
|---|---|
| `GetSessionId(session)` | Returns the current session identifier, or `null` if not yet assigned. |
| `AssignSessionId(session, id)` | Assigns a custom session identifier. Must be called before the first invocation. Ignored if an ID is already persisted. Throws `ArgumentException` for null/empty/whitespace. |
| `SetSessionTag(session, key, value)` | Attaches a custom tag to the session. Propagated to all subsequent spans. Silently rejected if key exceeds 128 chars, starts with `genai.session.`, value exceeds 512 chars, or the session already has 50 tags. |

### `AIAgent` extension methods

| Method | Description |
|---|---|
| `BeginSessionTrace(agent, session)` | Opens a Mode B session span. Returns an `IDisposable` — dispose to close the span and record final aggregates. Returns a no-op disposable when `EnableSessionSpan = false`. |

---

## Telemetry Schema

### Span attributes (all modes)

| Attribute | Type | Description |
|---|---|---|
| `genai.session.id` | `string` | Stable session identifier (GUID or custom). |
| `genai.session.invocation_index` | `int` | 1-based invocation counter within the session. |
| `genai.session.first_seen` | `string` | ISO 8601 UTC timestamp of the first invocation. |
| `genai.session.age_seconds` | `double` | Elapsed seconds since first invocation. |
| `genai.session.total_input_tokens` | `long` | Running total of input tokens (requires `TrackTokenAggregates = true`). |
| `genai.session.total_output_tokens` | `long` | Running total of output tokens (requires `TrackTokenAggregates = true`). |
| *custom tags* | `string` | Any tags set via `SetSessionTag`. |

### Session span attributes (Mode B only, recorded on dispose)

| Attribute | Type | Description |
|---|---|---|
| `genai.session.total_invocations` | `int` | Total invocations completed within the scope. |
| `genai.session.total_input_tokens` | `long` | Final accumulated input tokens. |
| `genai.session.total_output_tokens` | `long` | Final accumulated output tokens. |
| `genai.session.duration_seconds` | `double` | Wall-clock duration from `BeginSessionTrace` to dispose. |
| `genai.session.errors` | `int` | Number of invocations that raised an exception. |
| `genai.session.start_time` | `string` | ISO 8601 UTC timestamp when `BeginSessionTrace` was called. |

### OTel metrics

| Metric | Instrument | Unit | Description |
|---|---|---|---|
| `genai.session.invocations` | Counter | `{invocation}` | Total invocations recorded by the decorator. |
| `genai.session.duration` | Histogram | `ms` | Delegation duration per invocation. |
| `genai.session.active` | UpDownCounter | `{session}` | Number of sessions with an invocation currently in flight. |
| `session.statebag.write.failures` | Counter | `{failure}` | Times session state could not be persisted to the StateBag. |

---

## See Also

- [Full quickstart with all scenarios](../../specs/001-session-identity-enrichment/quickstart.md)
- [Public API contract](../../specs/001-session-identity-enrichment/contracts/sessions-api.md)
- [Feature specification](../../specs/001-session-identity-enrichment/spec.md)
