# Quickstart: Session Identity and Enrichment

**Package**: `Melic.AgentFramework.Observability.Sessions`
**Feature Branch**: `001-session-identity-enrichment`
**Created**: 2026-05-13

---

## Installation

```xml
<PackageReference Include="Melic.AgentFramework.Observability.Sessions" Version="1.*" />
```

---

## Scenario 1 — Minimal setup (Mode A enrichment only)

One line in your agent builder chain. Every `invoke_agent` span automatically gets
`genai.session.id`, `genai.session.invocation_index`, `genai.session.age_seconds`, and
running token totals.

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Sessions;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseSessionTelemetry()
    .Build();

AgentSession session = await agent.CreateSessionAsync();

// First invocation — session id is auto-assigned
AgentResponse r1 = await agent.RunAsync("Hello", session);
// span tags: genai.session.id=<guid>, genai.session.invocation_index=1, ...

// Second invocation — same id, accumulated totals
AgentResponse r2 = await agent.RunAsync("How are you?", session);
// span tags: genai.session.id=<same guid>, genai.session.invocation_index=2, ...
```

---

## Scenario 2 — Custom session identifier

Assign your own identifier (e.g., a platform conversation ID) before the first invocation.

```csharp
AgentSession session = await agent.CreateSessionAsync();
session.AssignSessionId("conv-abc-123");

AgentResponse r = await agent.RunAsync("Hello", session);
// genai.session.id = "conv-abc-123"
```

---

## Scenario 3 — Custom business context tags

Tags are set once and propagate to every subsequent span automatically.

```csharp
AgentSession session = await agent.CreateSessionAsync();
session.SetSessionTag("tenant.id", "acme-corp");
session.SetSessionTag("user.tier", "premium");

// All spans from here forward carry tenant.id and user.tier as attributes.
AgentResponse r1 = await agent.RunAsync("First message", session);
AgentResponse r2 = await agent.RunAsync("Second message", session);
```

> **Note**: Custom tags are written to span attributes without filtering.
> Ensure tag values do not contain sensitive or personal data.

---

## Scenario 4 — Session persistence across invocations

Session state (including `SessionId` and token totals) survives serialization.

```csharp
// --- Process A ---
AgentSession session = await agent.CreateSessionAsync();
AgentResponse r1 = await agent.RunAsync("First message", session);

// Serialize and store externally
JsonElement serialized = await agent.SerializeSessionAsync(session);
string json = serialized.GetRawText(); // save to DB or cache

// --- Process B (later) ---
JsonElement restored = JsonDocument.Parse(json).RootElement;
AgentSession session2 = await agent.DeserializeSessionAsync(restored);

// session2 has the same session id and accumulated totals from Process A
AgentResponse r2 = await agent.RunAsync("Second message", session2);
// genai.session.invocation_index = 2, totals continue from Process A
```

---

## Scenario 5 — Mode B (explicit session span)

Opt in to a parent session span that groups all invocation spans under one trace root.

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
} // session span closes here; final aggregates are recorded

// In your APM: one root span "agent_session <agent.name>" with three children
```

---

## Scenario 6 — Disable token aggregate tracking

When you only need session identity and invocation index, disable token tracking to
avoid the extra StateBag write.

```csharp
AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseSessionTelemetry(o => o.TrackTokenAggregates = false)
    .Build();
```

---

## Telemetry emitted

### Span tags (on every `invoke_agent` span)

| Tag | Example value |
|---|---|
| `genai.session.id` | `"3f2504e0-4f89-11d3-9a0c-0305e82c3301"` |
| `genai.session.invocation_index` | `3` |
| `genai.session.first_seen` | `"2026-05-13T10:00:00Z"` |
| `genai.session.age_seconds` | `142` |
| `genai.session.total_input_tokens` | `1250` |
| `genai.session.total_output_tokens` | `430` |
| `genai.session.message_count` | `6` *(when history provider available)* |

### Metrics

| Metric | Type |
|---|---|
| `genai.session.invocations` | Counter |
| `genai.session.duration` | Histogram (ms) |
| `genai.session.active` | UpDownCounter |
| `session.statebag.write.failures` | Counter *(alert if > 0)* |

---

## Troubleshooting

**`genai.session.id` not appearing on spans**

- Confirm an `ActivityListener` subscribed to source `"Melic.AgentFramework.Observability.Sessions"` is registered, or that an OTel tracer provider includes the source.
- Verify the agent is built with `.UseSessionTelemetry()` before `.Build()`.

**Token totals not accumulating after restore**

- Ensure you restore the session via `agent.DeserializeSessionAsync()` rather than `agent.CreateSessionAsync()`.

**`session.statebag.write.failures` counter is non-zero**

- The StateBag is being created in a read-only mode or the STJ serializer is failing on `SessionStateBlock`. Check that no other code writes to the `"__melic_telemetry"` StateBag key.
