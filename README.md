# Agent Framework Observability

Observability library for the [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) (MAF).

Enriches AI agent sessions with stable identity, invocation aggregates, custom tags, and OpenTelemetry instrumentation — with zero changes to your agent logic.

## Packages

| Package | NuGet | Description |
|---|---|---|
| `Melic.AgentFramework.Observability.Sessions` | *(coming soon)* | Session identity & telemetry enrichment |
| `Melic.AgentFramework.Observability.Abstractions` | *(coming soon)* | Shared attribute-name constants |

## Installation

```xml
<PackageReference Include="Melic.AgentFramework.Observability.Sessions" Version="1.*" />
```

## Quick start (Scenario 1 — Mode A enrichment)

One line in your agent builder chain. Every `invoke_agent` span automatically gets
`genai.session.id`, `genai.session.invocation_index`, `genai.session.age_seconds`,
and running token totals.

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

### ⚠️ Pipeline order matters when combining with `UseOpenTelemetry()`

`UseSessionTelemetry()` enriches the **currently active** `Activity` (the `invoke_agent`
span emitted by MAF's `UseOpenTelemetry()`). For the enrichment to land on the right span,
`UseOpenTelemetry()` must be registered **before** `UseSessionTelemetry()` so it ends up
as the outermost wrapper and its span is still open when the session decorator runs.

`AIAgentBuilder` rule: **first `.Use()` registered = outermost wrapper**.

```csharp
// ✅ Correct — OpenTelemetry outermost, span open during enrichment
AIAgent agent = new AIAgentBuilder(inner)
    .UseOpenTelemetry()        // 1) outermost: opens `invoke_agent` activity
    .UseSessionTelemetry()     // 2) innermost: SetTag on Activity.Current works
    .Build();

// ❌ Wrong — session enrichment runs AFTER the span is closed; tags are lost
AIAgent agent = new AIAgentBuilder(inner)
    .UseSessionTelemetry()     // outermost
    .UseOpenTelemetry()        // innermost — closes span before enrichment runs
    .Build();
```

If you only use `UseSessionTelemetry()` without `UseOpenTelemetry()`, no `invoke_agent`
span is emitted at all and only Mode B (`BeginSessionTrace`) will produce traces.

Don't forget to subscribe both `ActivitySource`s in your `TracerProvider`:

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")          // MAF invoke_agent spans
    .AddSource("Melic.AgentFramework.Observability.Sessions") // Mode B session span
    .AddConsoleExporter()
    .Build();
```

See the [full quickstart](specs/001-session-identity-enrichment/quickstart.md) for more scenarios:

- Scenario 2 — Assign your own session identifier
- Scenario 3 — Custom business context tags
- Scenario 4 — Session persistence across serialization/restore
- Scenario 5 — Mode B session-level span (`BeginSessionTrace`)

## Features

- [Session Identity & Enrichment](docs/features/session-identity-enrichment.md) — stable session ID, aggregate token counts, custom tags, optional session span (Mode B)

## Requirements

- .NET 8, 9, or 10
- `Microsoft.Agents.AI` 1.5+
- `OpenTelemetry.Api` 1.9+

## License

MIT

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
