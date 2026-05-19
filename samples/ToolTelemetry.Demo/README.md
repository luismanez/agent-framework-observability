# ToolTelemetry.Demo

Console app that runs several tool-call scenarios against a real Azure OpenAI / Foundry endpoint and exports `agent_tool_call` spans to the console or to Azure Application Insights.

The sample demonstrates `Melic.AgentFramework.Observability.Tools` with default settings, disabled payload capture, bounded payload capture with a custom `ActivitySource`, and tool failure telemetry.

## Prerequisites

- .NET 10 SDK
- An Azure OpenAI or Foundry endpoint with a deployed model that supports tool calling

## Environment variables

| Variable | Required | Description |
|---|---|---|
| `AZURE_OPENAI_ENDPOINT` | Yes | Endpoint URL |
| `AZURE_OPENAI_API_KEY` | Yes | API key |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | No | Model deployment name (default: `gpt-4o-mini`) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | No | If set, exports to App Insights instead of Console |

## Run

```powershell
# From the repo root
$env:AZURE_OPENAI_ENDPOINT        = "https://<your-resource>.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY         = "<your-key>"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4o-mini"

dotnet run --project samples/ToolTelemetry.Demo
```

## What The Sample Wires

Each scenario creates an agent with function tools and then enables tool telemetry through the agent builder:

```csharp
AIAgent agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry(configure: otel => otel.EnableSensitiveData = true)
    .UseToolTelemetry()
    .Build();
```

The tracer subscribes to these sources:

- `Experimental.Microsoft.Agents.AI` — MAF `invoke_agent` spans
- `Melic.AgentFramework.Observability.Tools` — default `agent_tool_call` spans
- `ToolTelemetry.Demo.CustomTools` — custom-source scenario
- `ToolTelemetry.Demo` — parent scenario spans created by the sample

## Scenarios

| # | Description | What to verify |
|---|---|---|
| 1 | Default tool telemetry | `agent_tool_call` span with `genai.tool.name`, `genai.tool.call_id`, `genai.tool.input`, `genai.tool.output`, and status `OK` |
| 2 | Payload capture disabled | Tool span still exists, but `genai.tool.input` and `genai.tool.output` are omitted |
| 3 | Custom source and bounded payloads | Tool span appears under `ToolTelemetry.Demo.CustomTools`; payload attributes remain valid JSON and fit the configured limits |
| 4 | Tool failure | Tool span status is `ERROR`; `genai.tool.output` contains structured `{ "type", "message" }` error JSON when output capture is enabled |

## App Insights

After running with `APPLICATIONINSIGHTS_CONNECTION_STRING` set:

- **Transaction Search** → filter by span name `agent_tool_call`
- **Transaction Search** → inspect `customDimensions["genai.tool.name"]`
- **Transaction details** → verify tool spans are children of `invoke_agent` spans when MAF OpenTelemetry is enabled

## Notes

Tool input and output payloads may contain sensitive data. The demo enables capture so the telemetry shape is visible; production apps should disable capture or apply redaction when payloads can contain secrets, personal data, or regulated content.