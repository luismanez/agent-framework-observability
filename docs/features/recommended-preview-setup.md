# Recommended Preview Setup

This guide shows the recommended preview composition for `Melic.AgentFramework.Observability.Sessions`, `Tools`, and `Redaction`.

Use this path when you want conversation correlation, tool-call diagnostics, and export-boundary masking in the same Microsoft Agent Framework application.

## Package Roles

| Package | Registration point | Role |
| --- | --- | --- |
| `Melic.AgentFramework.Observability.Sessions` | `AIAgentBuilder` | Enriches `invoke_agent` spans with stable `genai.session.*` attributes and running aggregates. |
| `Melic.AgentFramework.Observability.Tools` | `AIAgentBuilder` / MAF tool invocation pipeline | Enriches MAF `execute_tool` spans with bounded package-owned `genai.tool.*` payload and retry attributes. |
| `Melic.AgentFramework.Observability.Redaction` | `TracerProviderBuilder` | Redacts selected string-valued telemetry attributes before exporters receive them. |

Sessions and Tools observe agent behavior while the invocation is running. Redaction observes completed span attributes at the OpenTelemetry export boundary.

## Recommended Agent Builder Registration

Register MAF OpenTelemetry first so the `invoke_agent` span is open when Sessions and Tools enrich it.

```csharp
using Melic.AgentFramework.Observability.Sessions;
using Melic.AgentFramework.Observability.Tools;
using Microsoft.Agents.AI;

AIAgent agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry(configure: options =>
    {
        options.EnableSensitiveData = true;
    })
    .UseSessionTelemetry()
    .UseToolTelemetry()
    .Build();
```

Enable MAF sensitive data only when your audit, analytics, or troubleshooting scenario requires prompt, response, tool argument, or tool result telemetry. Pair it with the Redaction preset below before export.

## Recommended Trace Pipeline Registration

Register Redaction before exporters.

```csharp
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Sessions")
    .AddSource("Melic.AgentFramework.Observability.Tools")
    .AddTelemetryRedaction(options =>
    {
        options.IncludeMafSensitiveDataAttributes();
        options.RedactExceptionMessages = true;
        options.EnableDiagnostics = true;
    })
    .AddConsoleExporter()
    .Build();
```

`IncludeMafSensitiveDataAttributes()` opts in the official `gen_ai.*` attribute surface commonly emitted when MAF `EnableSensitiveData` is enabled. Redaction also targets package-owned `genai.tool.input` and `genai.tool.output` by default.

## Application Insights Export

Applications own exporter choices. Add the Azure Monitor exporter in the app or sample project, then choose the exporter from configuration.

```csharp
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    traceBuilder.AddAzureMonitorTraceExporter(options => options.ConnectionString = appInsightsConnectionString);
}
else
{
    traceBuilder.AddConsoleExporter();
}
```

In Application Insights, use Transaction Search and inspect custom dimensions for:

- `genai.session.id`
- `genai.session.invocation_index`
- `genai.tool.input`
- `genai.tool.output`
- official `gen_ai.*` prompt, response, or tool-call attributes when sensitive capture is enabled
- `genai.redaction.applied`
- `genai.redaction.match_count`
- `genai.redaction.failure_count`

Ingestion is asynchronous. Even after `ForceFlush`, telemetry may take 1-2 minutes to appear.

## Redaction Boundary

Redaction is MAF-aware through telemetry attributes and conventions. It is not an `AIAgent` runtime middleware.

It does not mutate:

- agent messages
- prompts before model invocation
- model responses
- tool arguments
- tool outputs
- `AgentSession`
- application state

Use runtime safety middleware separately if your application needs to transform prompts, responses, or tool payloads before they are used by the agent.

## Clean Consumer Validation

Create a fresh project outside the repository source graph and install published preview packages:

```powershell
dotnet new console -n MafObservabilityPreviewSmoke
cd MafObservabilityPreviewSmoke
dotnet add package Melic.AgentFramework.Observability.Sessions --prerelease
dotnet add package Melic.AgentFramework.Observability.Tools --prerelease
dotnet add package Melic.AgentFramework.Observability.Redaction --prerelease
dotnet add package Microsoft.Agents.AI
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Console
dotnet build
```

The smoke project should contain only package references. It must not reference repository project files or package-internal implementation types.

## Package Boundary Matrix

| Project | May reference sibling packages? | May reference exporters? | Registration surface |
| --- | --- | --- | --- |
| `Abstractions` | No | No | Constants/contracts only |
| `Sessions` | Abstractions only | No | `AIAgentBuilder` |
| `Tools` | Abstractions only | No | `AIAgentBuilder` / tool invocation middleware |
| `Redaction` | Abstractions only | No | `TracerProviderBuilder` |
| Samples/applications | Yes | Yes | Owns composition and exporter choices |

Confirm Redaction's boundary with:

```powershell
dotnet list src/Melic.AgentFramework.Observability.Redaction/Melic.AgentFramework.Observability.Redaction.csproj reference
dotnet list src/Melic.AgentFramework.Observability.Redaction/Melic.AgentFramework.Observability.Redaction.csproj package
```

Expected result: Redaction references Abstractions and OpenTelemetry only; it does not reference Sessions, Tools, Azure Monitor, Application Insights, or exporter packages.

## Package README Source Of Truth

All packages currently use the shared NuGet README configured by `nuget/nuget-package.props`:

```xml
<PackageReadmeFile>NUGET.md</PackageReadmeFile>
```

Update `nuget/NUGET.md` when package-level NuGet README content must change for Sessions, Tools, or Redaction. Package-specific Markdown files under `src/` are supplemental repository docs where they exist.

## See Also

- [End-to-End Telemetry Demo](../../samples/EndToEndTelemetry.Demo/README.md)
- [Session Identity & Enrichment](session-identity-enrichment.md)
- [Tool Call Span Enrichment](tool-call-enrichment.md)
- [Redaction Pipeline](redaction-pipeline.md)