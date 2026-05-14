# SessionTelemetry.Demo

Console app that runs three scenarios against a real Azure OpenAI / Foundry endpoint
and exports enriched session telemetry to the console or to Azure Application Insights.

## Prerequisites

- .NET 10 SDK
- An Azure OpenAI or Foundry endpoint with a deployed model

## Environment variables

| Variable | Required | Description |
|---|---|---|
| `AZURE_OPENAI_ENDPOINT` | ✅ | Endpoint URL |
| `AZURE_OPENAI_API_KEY` | ✅ | API key |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | | Model deployment name (default: `gpt-4o-mini`) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | | If set, exports to App Insights instead of Console |

## Run

```powershell
# From the repo root
$env:AZURE_OPENAI_ENDPOINT        = "https://<your-resource>.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY         = "<your-key>"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4o-mini"

dotnet run --project samples/SessionTelemetry.Demo
```

## Scenarios

| # | Description | Key attributes to verify |
|---|---|---|
| 1 | **Mode A — multi-turn** | `genai.session.id` stable across 3 turns, `invocation_index` increments, token totals accumulate |
| 2 | **Custom ID + business tags** | `genai.session.id = conv-demo-contoso-001`, `tenant_id`, `user_tier`, `region` on every span |
| 3 | **Mode B — explicit session span** | Root `agent_session` span with two child `invoke_agent` spans; final aggregates on dispose |

## App Insights

After running with `APPLICATIONINSIGHTS_CONNECTION_STRING` set:

- **Transaction Search** → filter by `customDimensions["genai.session.id"]`
- **Metrics Explorer** → Namespace: `Melic.AgentFramework.Observability.Sessions`
