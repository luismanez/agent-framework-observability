# Contract: Preview Validation

## Scope

This contract defines validation outcomes for preview readiness. It covers package consumption, package boundaries, and public documentation consistency.

## Clean Consumer Validation

### Setup

Create a fresh console app outside the repository source graph and add published preview package references.

```powershell
dotnet new console -n MafObservabilityPreviewSmoke
cd MafObservabilityPreviewSmoke
dotnet add package Melic.AgentFramework.Observability.Sessions --prerelease
dotnet add package Melic.AgentFramework.Observability.Tools --prerelease
dotnet add package Melic.AgentFramework.Observability.Redaction --prerelease
```

Additional public packages such as `Microsoft.Agents.AI`, `OpenTelemetry`, `OpenTelemetry.Exporter.Console`, and `Azure.Monitor.OpenTelemetry.Exporter` may be added as needed by the sample code. Repository project references are not allowed.

### Expected Outcome

- `dotnet restore` succeeds using configured public/package sources.
- `dotnet build` succeeds.
- The project file contains package references, not repository project references.
- Minimal setup code compiles for agent builder registration and telemetry pipeline registration.

## Package Boundary Validation

### Redaction Boundary

Redaction must not depend on:

- `Melic.AgentFramework.Observability.Sessions`
- `Melic.AgentFramework.Observability.Tools`
- Azure Monitor exporter packages
- Application Insights packages
- Console exporter packages

Redaction may depend on:

- `Melic.AgentFramework.Observability.Abstractions`
- Stable OpenTelemetry APIs already allowed by the constitution

### Sample Boundary

Samples may depend on multiple observability packages and exporters because applications own composition and export choices.

## Documentation Consistency Validation

Review touched docs and metadata for consistent statements:

- Sessions enriches agent invocation/session telemetry through agent builder registration.
- Tools enriches tool-call telemetry through agent builder/function invocation integration.
- Redaction redacts selected telemetry attributes at the export boundary through OpenTelemetry pipeline registration.
- Redaction is MAF-aware through telemetry attributes and conventions.
- Redaction does not mutate runtime prompts, model responses, tool arguments, tool outputs, `AgentSession`, or application state.
- Exporter dependencies belong in applications/samples, not library packages.

## Repository Validation

Before completion, run:

```powershell
dotnet build
dotnet test
dotnet build samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj -c Release
```

Sample run validation should be performed with Console export by default and, when credentials are available, with Application Insights configured.
