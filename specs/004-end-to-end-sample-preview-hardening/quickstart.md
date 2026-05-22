# Quickstart: End-to-End Sample and Preview Hardening

This quickstart describes the planned validation flow for the end-to-end sample and preview hardening work.

## 1. Build the Repository

```powershell
dotnet build
```

## 2. Run the End-to-End Sample Locally

Configure the same model-provider credentials used by the existing agent samples, then run the sample with Console export:

```powershell
dotnet run --project samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj
```

To validate the under-10-minute local run criterion, use the same command with timing:

```powershell
Measure-Command { dotnet run --project samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj }
```

Expected local behavior:

- The sample reports that Console export is active.
- A multi-turn conversation runs with one stable session.
- At least one tool call occurs.
- Console output includes session correlation, tool-call enrichment, official MAF/GenAI sensitive telemetry when enabled, and redacted sensitive values before export.
- The elapsed time is under 10 minutes after required credentials and model-provider prerequisites are already configured.

## 3. Run the Same Sample with Application Insights

Set the connection string and rerun the same command:

```powershell
$env:APPLICATIONINSIGHTS_CONNECTION_STRING = "InstrumentationKey=...;IngestionEndpoint=..."
dotnet run --project samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj
```

Expected cloud behavior:

- The sample reports that Application Insights export is active.
- No source changes are required.
- Telemetry may take a short time to appear in Application Insights.

Inspect spans/custom dimensions for at least:

- `genai.session.id`
- session invocation or aggregate attributes emitted by Sessions
- `genai.tool.input`
- `genai.tool.output`
- official `gen_ai.*` prompt/response/tool attributes when sensitive capture is enabled
- `genai.redaction.applied`
- `genai.redaction.match_count`
- `genai.redaction.failure_count`

## 4. Validate Clean Preview Consumption

Use a directory outside the repository source graph:

```powershell
dotnet new console -n MafObservabilityPreviewSmoke
cd MafObservabilityPreviewSmoke
dotnet add package Melic.AgentFramework.Observability.Sessions --prerelease
dotnet add package Melic.AgentFramework.Observability.Tools --prerelease
dotnet add package Melic.AgentFramework.Observability.Redaction --prerelease
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.Console
dotnet build
```

The smoke project must use package references only. It must not reference repository project files or internal package implementation types.

## 5. Validate Package Boundaries

Confirm Redaction remains independent from sibling packages and exporters:

```powershell
dotnet list src/Melic.AgentFramework.Observability.Redaction/Melic.AgentFramework.Observability.Redaction.csproj reference
dotnet list src/Melic.AgentFramework.Observability.Redaction/Melic.AgentFramework.Observability.Redaction.csproj package
```

Expected result:

- Project references include Abstractions only.
- Package references do not include Sessions, Tools, Azure Monitor, Application Insights, or exporter packages.
- The Redaction package remains usable through `AddTelemetryRedaction()` without installing Sessions or Tools.

## 6. Run Final Repository Validation

```powershell
dotnet build
dotnet test
dotnet build samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj -c Release
```

Completion requires the repository build and tests to pass, plus successful sample build validation.

Record the final validation outcome in the PR or release notes, including whether the clean consumer project restored from published preview packages or from a configured preview feed.
