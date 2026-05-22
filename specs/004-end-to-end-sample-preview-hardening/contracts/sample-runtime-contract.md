# Contract: End-to-End Sample Runtime

## Scope

This contract defines the externally visible behavior of `samples/EndToEndTelemetry.Demo`. It is a sample/application contract, not a library API contract.

## Invocation

```powershell
dotnet run --project samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj
```

The sample must run from the repository root after the same model-provider prerequisites used by existing agent samples are configured.

## Environment Variables

| Name | Required | Behavior |
| --- | --- | --- |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | No | When present and non-empty, traces are exported to Azure Application Insights through Azure Monitor exporter. When absent, traces are exported to Console. |
| Existing model-provider variables used by current agent samples | Yes for real model execution | Used to create the inner agent/model client. The end-to-end sample should follow existing repository sample conventions rather than inventing new names. |

## Required Runtime Behavior

- The sample starts without an Application Insights connection string and selects Console export.
- The same source selects Application Insights export when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set.
- The sample performs a multi-turn interaction using one `AgentSession`.
- The interaction includes at least one tool call.
- The agent builder registration shows `UseOpenTelemetry`, `UseSessionTelemetry`, and `UseToolTelemetry` together.
- The telemetry pipeline registration shows `AddTelemetryRedaction` before exporter registration.
- Redaction configuration includes `IncludeMafSensitiveDataAttributes()` for the sensitive telemetry scenario.
- The sample force-flushes telemetry before process exit.

## Required Telemetry Signals

The emitted telemetry must allow developers to inspect at least these categories:

| Category | Expected Signals |
| --- | --- |
| Session correlation | `genai.session.id`, invocation index or session aggregate attributes emitted by Sessions |
| Tool enrichment | `genai.tool.input`, `genai.tool.output`, retry or attempt metadata when applicable |
| MAF/GenAI sensitive telemetry | Official `gen_ai.*` prompt, response, or tool-call attributes when sensitive capture is enabled |
| Redaction | Sensitive values masked in targeted attributes before export |
| Diagnostics | Aggregate `genai.redaction.*` attributes when enabled |

## Non-Behavior

- The sample must not imply that Redaction mutates prompts before model invocation.
- The sample must not imply that Redaction mutates model responses, tool arguments, tool outputs, `AgentSession`, or application state.
- The sample must not require Application Insights for local success.
- The sample must not introduce a new runtime package.
