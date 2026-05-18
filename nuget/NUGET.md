# Melic.AgentFramework.Observability

OpenTelemetry-native observability for the [Microsoft Agent Framework (MAF)](https://github.com/microsoft/agent-framework).
Adds session identity tracking, per-session token aggregation, custom business tags,
and optional session-scoped parent spans — all without modifying MAF internals.

## Packages

| Package | Purpose |
|---|---|
| `Melic.AgentFramework.Observability.Abstractions` | Shared attribute constants (`SessionAttributeNames`) and interfaces |
| `Melic.AgentFramework.Observability.Sessions` | Session lifecycle tracing — enriches every `invoke_agent` span with `genai.session.*` tags |

## Quick Start

```csharp
using Melic.AgentFramework.Observability.Sessions.Extensions;

// Register the middleware (order matters: UseOpenTelemetry must be outermost)
var agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry()       // outermost — creates the invoke_agent span
    .UseSessionTelemetry()    // innermost — enriches the open span
    .Build();

// Each AgentSession gets a stable, auto-generated session ID automatically.
// Optionally assign your own:
session.AssignSessionId("order-42");

// Optionally attach business context tags:
session.SetSessionTag("tenant.id", "contoso");
session.SetSessionTag("user.tier", "premium");
```

## Key Features

- **Zero-config session IDs** — every session gets a stable `genai.session.id` automatically
- **Token aggregation** — `genai.session.input_tokens` and `genai.session.output_tokens` accumulate across all turns
- **Custom tags** — attach arbitrary `genai.*` business context to every span in a session
- **Mode B tracing** — opt-in explicit parent span via `BeginSessionTrace()` for visual trace trees
- **OTel-only dependencies** — depends only on `OpenTelemetry.Api`, no MAF internals

## Links

- [GitHub repository](https://github.com/luismanez/agent-framework-observability)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
