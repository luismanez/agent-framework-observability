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

## When does this add value over MAF's built-in telemetry?

MAF already emits `invoke_agent` spans with `gen_ai.*` attributes (model, tokens, tool calls) following OTel Semantic Conventions. The `Sessions` package adds value **whenever a logical conversation spans more than one `invoke_agent` call** — especially when those calls live in different requests or processes and cannot be correlated by `traceId` alone.

| Your scenario | Does this package help? |
|---|---|
| Single-shot stateless API (one HTTP request = one invocation) | ❌ No real gain — MAF's `traceId` already covers it. |
| Stateless REST API with persisted chat history + `conversationId` per request, **but no anchoring of `session.id`** | ❌ Each request gets a random `session.id` → useless. |
| Stateless REST API that **anchors `session.id` to your `conversationId`** (Level 1) | ✅ All spans of a conversation share `genai.session.id` → cross-request correlation in your APM. |
| Level 1 + **rehydrate `StateBag` from your persistence store** (Level 2) | ✅✅ Plus running totals: `invocation_index`, `total_*_tokens`, `age_seconds` per conversation. |
| In-process / long-lived `AgentSession` (SignalR, Blazor, desktop) | ✅✅ Full feature set out of the box. |
| Background workers / batch jobs invoking the agent N times per logical unit | ✅ Mode B (`BeginSessionTrace`) gives a visual trace tree. |

See [Deployment patterns](#deployment-patterns) for concrete pseudocode of each scenario.

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

## Deployment patterns

### ⚠️ Pipeline order when combining with `UseOpenTelemetry()`

`UseSessionTelemetry()` enriches the **currently active** `Activity` — the `invoke_agent` span opened by MAF's own `UseOpenTelemetry()`. For the enrichment to land on the right span, `UseOpenTelemetry()` must be registered **first** in the builder chain so it becomes the outermost wrapper.

`AIAgentBuilder` rule: **first `.Use()` registered = outermost wrapper = executes first on the way in, last on the way out**.

```csharp
// ✅ Correct — OpenTelemetry opens the span; SessionTelemetry enriches it while it is still open
AIAgent agent = new AIAgentBuilder(inner)
    .UseOpenTelemetry()        // 1) outermost: opens `invoke_agent` Activity
    .UseSessionTelemetry()     // 2) innermost: SetTag on Activity.Current works
    .Build();

// ❌ Wrong — SessionTelemetry runs after OpenTelemetry has already closed the span
AIAgent agent = new AIAgentBuilder(inner)
    .UseSessionTelemetry()     // outermost: Activity.Current is null → tags are silently lost
    .UseOpenTelemetry()        // innermost: span closes before enrichment runs
    .Build();
```

Also register both `ActivitySource`s in your `TracerProvider`:

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")              // MAF invoke_agent spans
    .AddSource("Melic.AgentFramework.Observability.Sessions")   // Mode B agent_session span
    ...
```

---

### Stateless REST API (recommended pattern)

In a stateless API (ASP.NET Core, Azure Functions, etc.) the `AgentSession` is recreated on every request from persisted history. The `StateBag` — where this library stores its telemetry state — is **not** automatically persisted, so `invocation_index` and token totals reset to zero on every request.

The recommended pattern is to **anchor the session ID to your existing thread/conversation identifier** so all spans from the same conversation remain correlated in your observability backend:

```csharp
// POST /api/chat/{threadId}
app.MapPost("/api/chat/{threadId}", async (
    string threadId,
    ChatRequest request,
    AIAgent agent,
    IChatHistoryProvider historyProvider) =>
{
    // Restore chat history from your persistence layer (SQL, Cosmos, etc.)
    var history = await historyProvider.LoadAsync(threadId);
    var session = await agent.CreateSessionAsync(history);

    // Anchor telemetry to the caller's conversation ID
    session.AssignSessionId(threadId);

    // Optional: business tags from the JWT / claims / request headers
    session.SetSessionTag("tenant_id", request.TenantId);
    session.SetSessionTag("user_id",   request.UserId);

    var response = await agent.RunAsync(request.Messages, session);
    return Results.Ok(response);
});
```

**What you get** with this pattern:

| Telemetry signal | Value |
|---|---|
| `genai.session.id` | Your `threadId` — all spans of a conversation share this ID |
| Custom tags (`tenant_id`, `user_id`) | Present on every span automatically |
| `genai.session.invocation_index` | Always `1` per request — no cross-request accumulation |
| `genai.session.total_*_tokens` | Tokens for the current request only — not accumulated |

**What you do NOT get** (inherent stateless limitation):

- Running token totals across the lifetime of a conversation
- Meaningful `age_seconds` / `first_seen` across requests

If cross-request token accumulation is required, persist the raw StateBag JSON alongside the chat history and restore it by calling `DeserializeSessionAsync` (see Scenario 4 above).

#### Level 2 — Stateless API with cross-request aggregates

If you want `invocation_index`, `total_*_tokens` and `age_seconds` to grow across HTTP requests of the same conversation, persist and rehydrate the full `AgentSession` (which includes the package's StateBag entry under `__melic_telemetry`).

```csharp
// POST /api/chat/{threadId}
app.MapPost("/api/chat/{threadId}", async (
    string threadId,
    ChatRequest request,
    AIAgent agent,
    ISessionStore sessionStore) =>
{
    // 1. Try to restore a previously serialized AgentSession for this conversation.
    AgentSession session;
    string? savedJson = await sessionStore.LoadAsync(threadId);
    if (savedJson is not null)
    {
        JsonElement restored = JsonDocument.Parse(savedJson).RootElement;
        session = await agent.DeserializeSessionAsync(restored);
    }
    else
    {
        session = await agent.CreateSessionAsync();
        session.AssignSessionId(threadId);              // anchor for Level 1 correlation
        session.SetSessionTag("tenant_id", request.TenantId);
    }

    // 2. Invoke — invocation_index, token totals and age_seconds keep growing.
    var response = await agent.RunAsync(request.Messages, session);

    // 3. Persist the updated session (chat history + telemetry StateBag) back to storage.
    JsonElement updated = await agent.SerializeSessionAsync(session);
    await sessionStore.SaveAsync(threadId, updated.GetRawText());

    return Results.Ok(response);
});
```

With this pattern an APM query like

```kusto
dependencies
| where customDimensions["genai.session.id"] == "thread-abc-123"
| order by timestamp asc
```

returns every turn of the conversation across every process that handled it, with cumulative token counts and a meaningful `age_seconds` per span.

---

### In-process / long-lived session (full feature set)

When the `AgentSession` object can be kept alive for the duration of a user's conversation (e.g. SignalR hub, Blazor circuit, desktop app), all features work without extra configuration:

```csharp
// Session created once per user, reused for every message
_session = await agent.CreateSessionAsync();
_session.AssignSessionId(userId);          // optional correlation anchor
_session.SetSessionTag("tenant_id", tid); // set once, propagated forever

// Each subsequent user message:
var response = await agent.RunAsync(messages, _session);
// invocation_index increments, token totals accumulate, age_seconds grows
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
