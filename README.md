# Agent Framework Observability

Observability library for the [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) (MAF).

Enriches AI agent sessions with stable identity, invocation aggregates, custom tags, and OpenTelemetry instrumentation — with zero changes to your agent logic.

## Packages

| Package | Description | Status |
|---|---|---|
| `Melic.AgentFramework.Observability.Sessions` | Session identity & telemetry enrichment | In development |
| `Melic.AgentFramework.Observability.Abstractions` | Shared constants and interfaces | In development |

## Quick start

```csharp
var agent = new AIAgentBuilder()
    .UseSessionTelemetry()
    .Build();
```

## Requirements

- .NET 8, 9, or 10
- Microsoft Agent Framework

## License

MIT
