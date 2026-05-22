# End-to-End Telemetry Demo

This sample runs a customer-support order lookup flow that composes:

- `Melic.AgentFramework.Observability.Sessions` for conversation/session correlation
- `Melic.AgentFramework.Observability.Tools` for bounded tool-call payload enrichment
- `Melic.AgentFramework.Observability.Redaction` for export-boundary telemetry redaction

It uses Console export by default and switches to Azure Application Insights when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set. The Redaction package is registered on the OpenTelemetry pipeline, not on the `AIAgent` builder.

## Prerequisites

Set the same model-provider environment variables used by the package-specific demos:

```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://<resource>.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY = "<api-key>"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4o-mini"
```

## Run Locally

```powershell
dotnet run --project samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj
```

To time the under-10-minute acceptance criterion after prerequisites are configured:

```powershell
Measure-Command { dotnet run --project samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj }
```

## Run With Application Insights

```powershell
$env:APPLICATIONINSIGHTS_CONNECTION_STRING = "InstrumentationKey=...;IngestionEndpoint=..."
dotnet run --project samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj
```

Azure Monitor exporter batches telemetry. The sample force-flushes before exit and waits briefly, but Application Insights ingestion may still take 1-2 minutes.

## What To Inspect

Console output should include spans from MAF and the package ActivitySources. In Application Insights, use Transaction Search and filter by:

```text
customDimensions["genai.session.id"] == "support-order-42"
```

Inspect custom dimensions for:

- `genai.session.id`
- `genai.session.invocation_index`
- `genai.session.total_input_tokens`
- `genai.tool.input`
- `genai.tool.output`
- official `gen_ai.*` prompt, response, or tool-call attributes when MAF sensitive data capture is enabled
- `genai.redaction.applied`
- `genai.redaction.match_count`
- `genai.redaction.failure_count`

Sensitive examples such as `customer@example.com`, bearer tokens, and sensitive JSON fields should be masked in targeted exported attributes.

## Registration Shape

Sessions and Tools are agent middleware:

```csharp
var agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry(configure: options => options.EnableSensitiveData = true)
    .UseSessionTelemetry()
    .UseToolTelemetry()
    .Build();
```

Redaction is an OpenTelemetry processor before exporters:

```csharp
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
```

Redaction changes exported telemetry attributes only. It does not mutate prompts before model invocation, model responses, tool arguments, tool outputs, `AgentSession`, or application state.