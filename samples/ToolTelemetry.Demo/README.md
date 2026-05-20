# ToolTelemetry.Demo

Console app that runs several tool-call scenarios against a real Azure OpenAI / Foundry endpoint and enriches MAF `execute_tool` spans with bounded payload and retry attributes.

The sample demonstrates `Melic.AgentFramework.Observability.Tools` with default settings, disabled payload capture, bounded payload capture, and tool failure telemetry.

## Prerequisites

- .NET 10 SDK
- An Azure OpenAI or Foundry endpoint with a deployed model that supports tool calling

## Environment variables

| Variable | Required | Description |
| --- | --- | --- |
| `AZURE_OPENAI_ENDPOINT` | Yes | Endpoint URL |
| `AZURE_OPENAI_API_KEY` | Yes | API key |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | No | Model deployment name (default: `gpt-4o-mini`) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | No | If set, exports to App Insights instead of Console |
| `MAF_ENABLE_SENSITIVE_DATA` | No | If `true`, MAF's built-in `execute_tool` spans also emit `gen_ai.tool.call.arguments` and `gen_ai.tool.call.result` |

## Run

```powershell
# From the repo root
$env:AZURE_OPENAI_ENDPOINT        = "https://<your-resource>.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY         = "<your-key>"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4o-mini"

dotnet run --project samples/ToolTelemetry.Demo
```

By default, the sample leaves MAF sensitive-data telemetry disabled so the bounded payload attributes you see on `execute_tool` spans come from `Melic.AgentFramework.Observability.Tools`. Set `MAF_ENABLE_SENSITIVE_DATA=true` only when you also want to inspect MAF's built-in GenAI payload telemetry.

## What The Sample Wires

Each scenario creates an agent with function tools and then enables tool telemetry through the agent builder:

```csharp
AIAgent agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry(configure: otel => otel.EnableSensitiveData = enableMafSensitiveData)
    .UseToolTelemetry()
    .Build();
```

The tracer subscribes to these sources:

- `Experimental.Microsoft.Agents.AI` — MAF `invoke_agent` spans
- `Melic.AgentFramework.Observability.Tools` — fallback `agent_tool_call` spans if no MAF `execute_tool` span is current
- `ToolTelemetry.Demo` — parent scenario spans created by the sample

## Scenarios

| # | Description | What to verify |
| --- | --- | --- |
| 1 | Default tool telemetry | MAF `execute_tool` span keeps standard `gen_ai.tool.name` / `gen_ai.tool.call.id` identity and is enriched with `genai.tool.input`, `genai.tool.output`, and status `OK` |
| 2 | Payload capture disabled | MAF `execute_tool` span still exists, but `genai.tool.input` and `genai.tool.output` are omitted |
| 3 | Bounded payloads | MAF `execute_tool` span gets payload attributes that remain valid JSON and fit the configured limits |
| 4 | Tool failure | MAF `execute_tool` span status is `ERROR`; `genai.tool.output` contains structured `{ "type", "message" }` error JSON when output capture is enabled |

## App Insights: MAF `execute_tool` Enrichment

With `UseOpenTelemetry()` enabled, MAF emits its own `execute_tool` span using OpenTelemetry GenAI semantic-convention attributes such as `gen_ai.tool.name`, `gen_ai.tool.call.arguments`, and `gen_ai.tool.call.result`.

This package enriches that existing `execute_tool` span with custom attributes using the `genai.` prefix, for example `genai.tool.input`, `genai.tool.output`, `genai.tool.is_retry`, and `genai.tool.attempt_index`. It does not duplicate MAF's `gen_ai.tool.name` or `gen_ai.tool.call.id` attributes on that span. It creates a fallback `agent_tool_call` span only when no MAF `execute_tool` span is current, and fallback spans carry `genai.tool.name` plus optional `genai.tool.call_id` because MAF identity attributes are not present there.

In Azure Application Insights, the **Generative AI Properties** and **Messages** sections are rendered from MAF's `gen_ai.*` attributes on the `execute_tool` span. They are not emitted by `Melic.AgentFramework.Observability.Tools`. If `MAF_ENABLE_SENSITIVE_DATA=true`, those sections may look redundant with `gen_ai.tool.call.result`; that is App Insights presenting the same MAF semantic payload in a friendlier view. Inspect `customDimensions["genai.tool.*"]` on the same `execute_tool` span to see this package's enrichment.

## App Insights

After running with `APPLICATIONINSIGHTS_CONNECTION_STRING` set:

- **Transaction Search** → filter by span name `execute_tool`
- **Transaction Search** → inspect `customDimensions["gen_ai.tool.name"]` for MAF identity
- **Transaction details** → verify MAF `execute_tool` spans carry MAF `gen_ai.*` attributes and this package's bounded `genai.tool.input` / `genai.tool.output` attributes

## Notes

Tool input and output payloads may contain sensitive data. The demo enables capture so the telemetry shape is visible; production apps should disable capture or apply redaction when payloads can contain secrets, personal data, or regulated content.
