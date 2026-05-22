# Melic.AgentFramework.Observability

OpenTelemetry-native observability for the [Microsoft Agent Framework (MAF)](https://github.com/microsoft/agent-framework).
Adds session identity tracking, per-session token aggregation, custom business tags,
optional session-scoped parent spans, tool-call span enrichment, and export-boundary redaction - all without modifying MAF internals.

## Packages

| Package | Purpose |
|---|---|
| `Melic.AgentFramework.Observability.Abstractions` | Shared attribute constants (`SessionAttributeNames`, `ToolAttributeNames`) and adapter contracts |
| `Melic.AgentFramework.Observability.Sessions` | Session lifecycle tracing — enriches every `invoke_agent` span with `genai.session.*` tags |
| `Melic.AgentFramework.Observability.Tools` | Tool-call tracing — enriches MAF `execute_tool` spans with bounded payload and retry attributes |
| `Melic.AgentFramework.Observability.Redaction` | MAF-aware OpenTelemetry trace-attribute redaction before export |

This shared README is the NuGet package README source of truth for Sessions, Tools, and Redaction. Package-specific Markdown files in the repository are supplemental docs where they exist.

## Quick Start

```csharp
using Melic.AgentFramework.Observability.Redaction;
using Melic.AgentFramework.Observability.Sessions;
using Melic.AgentFramework.Observability.Tools;
using OpenTelemetry;
using OpenTelemetry.Trace;

// Register the middleware (order matters: UseOpenTelemetry must be outermost)
var agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry()       // outermost — creates the invoke_agent span
    .UseSessionTelemetry()    // innermost — enriches the open span
    .UseToolTelemetry()       // enriches execute_tool spans for tools
    .Build();

// Register Redaction on the OTel trace pipeline before exporters.
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Sessions")
    .AddSource("Melic.AgentFramework.Observability.Tools")
    .AddTelemetryRedaction(options =>
    {
        options.IncludeMafSensitiveDataAttributes();
        options.EnableDiagnostics = true;
    })
    .AddConsoleExporter()
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
- **Tool-call enrichment** — adds stable `genai.tool.*` attributes to MAF `execute_tool` spans
- **Payload diagnostics** — optional valid-JSON input and output capture with bounded size
- **Retry detection** — repeated tool call ids are marked with retry attributes within the current invocation
- **Export-boundary redaction** — masks selected MAF/GenAI and package-owned span attributes before export
- **OTel-only dependencies** — depends only on `OpenTelemetry.Api`, no MAF internals

## Documentation

Full technical documentation — including deployment patterns, stateless REST API guidance, public API reference, and the complete telemetry schema — lives in the GitHub repository:

- [Sessions package — full reference](https://github.com/luismanez/agent-framework-observability/blob/main/docs/features/session-identity-enrichment.md)
- [Tools package — full reference](https://github.com/luismanez/agent-framework-observability/blob/main/docs/features/tool-call-enrichment.md)
- [Redaction package — full reference](https://github.com/luismanez/agent-framework-observability/blob/main/docs/features/redaction-pipeline.md)
- [Recommended preview setup](https://github.com/luismanez/agent-framework-observability/blob/main/docs/features/recommended-preview-setup.md)

## Links

- [GitHub repository](https://github.com/luismanez/agent-framework-observability)
- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
