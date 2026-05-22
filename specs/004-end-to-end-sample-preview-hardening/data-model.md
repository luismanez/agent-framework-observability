# Data Model: End-to-End Sample and Preview Hardening

## Entity: End-to-End Sample

**Purpose**: Runnable sample that composes Sessions, Tools, and Redaction in one recommended Microsoft Agent Framework observability scenario.

**Fields**:

- `projectPath`: `samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj`
- `defaultExporter`: Console
- `optionalExporter`: Azure Application Insights via `APPLICATIONINSIGHTS_CONNECTION_STRING`
- `agentRegistration`: `UseOpenTelemetry`, `UseSessionTelemetry`, `UseToolTelemetry`
- `redactionRegistration`: `AddTelemetryRedaction` before exporter registration
- `conversationShape`: multi-turn interaction with at least one tool call
- `sensitiveTelemetryMode`: enabled for the scenario that demonstrates official `gen_ai.*` prompt/response/tool attributes
- `redactionPreset`: `IncludeMafSensitiveDataAttributes()` plus default package-owned tool payload targets

**Validation Rules**:

- Must run without Application Insights configuration and export locally.
- Must switch to Application Insights without source changes when the connection string is set.
- Must emit session correlation, tool enrichment, official MAF/GenAI sensitive attributes, and redaction diagnostics or redacted values.
- Must not require a new runtime package.

## Entity: Recommended Setup Guide

**Purpose**: Human-facing adoption guide for using all three preview packages together.

**Fields**:

- `packageRoles`: Sessions, Tools, Redaction responsibilities
- `registrationPoints`: agent builder versus telemetry pipeline
- `exporterChoices`: Console and Application Insights
- `sensitiveDataGuidance`: when to enable MAF sensitive telemetry and how to mask it before export
- `runtimeBoundary`: explicit statement that Redaction does not mutate messages, prompts, model responses, tool arguments, tool outputs, or app state
- `inspectionGuidance`: local output and Application Insights dimensions/spans to inspect

**Validation Rules**:

- Must consistently describe Redaction as MAF-aware export-boundary telemetry redaction.
- Must not imply runtime safety or mutation behavior.
- Must identify at least five relevant telemetry attributes/dimensions for App Insights inspection.

## Entity: Clean Consumer Validation

**Purpose**: Maintainer validation that published preview packages can be installed and compiled from a fresh project without repository internals.

**Fields**:

- `consumerProject`: temporary console app outside the repository source graph
- `packageReferences`: published preview package IDs for Sessions, Tools, Redaction, and required external dependencies
- `sourceReferences`: none to repository projects
- `validationCommands`: restore/build/run or compile-only commands
- `expectedOutcome`: successful restore/build and no repository project reference dependency

**Validation Rules**:

- Must use package IDs, not local project references.
- Must compile a minimal recommended setup.
- Must cover Console and Application Insights examples without requiring package-internal dependencies.

## Entity: Package Boundary Matrix

**Purpose**: Reviewable expectations for package dependencies and ownership.

**Fields**:

- `package`: package or sample name
- `mayReferenceSiblings`: whether references to sibling packages are allowed
- `mayReferenceExporters`: whether exporter packages are allowed
- `registrationSurface`: agent builder, telemetry builder, or sample/application only
- `notes`: boundary-specific constraints

**Validation Rules**:

- Redaction must reference Abstractions and OpenTelemetry only among library dependencies.
- Redaction must not reference Sessions, Tools, Azure Monitor, Application Insights, or exporter-specific packages.
- Samples may reference multiple library packages and exporters.
- No new package-owned attributes may be introduced unless declared in Abstractions with `genai.` prefix.

## Entity: Telemetry Inspection Target

**Purpose**: Expected observable output used by sample docs and App Insights guidance.

**Fields**:

- `spanCategory`: invocation, session, tool, redaction, or standard MAF/GenAI sensitive telemetry
- `spanNameOrFilter`: expected span name or query filter
- `attributes`: custom dimensions or tags to inspect
- `redactionExpectation`: redacted, preserved, or diagnostic-only

**Validation Rules**:

- Must include session correlation attributes.
- Must include tool-call enrichment attributes.
- Must include official `gen_ai.*` sensitive telemetry when enabled.
- Must include package-owned `genai.tool.*` redaction coverage.
- Must include Redaction diagnostics only when diagnostics are enabled and must not expose matched values.

## State Transitions

### Sample Exporter Selection

1. `NoConnectionString` -> Console exporter selected.
2. `ConnectionStringPresent` -> Azure Monitor trace exporter selected.
3. `TelemetryEmitted` -> provider force flushes before process exit.

### Clean Consumer Validation

1. `FreshProjectCreated` -> package references added.
2. `PackagesRestored` -> minimal recommended setup compiled.
3. `BoundaryChecked` -> project references and forbidden dependencies inspected.
4. `Validated` -> results recorded in docs/release readiness notes.
