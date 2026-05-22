# Implementation Plan: End-to-End Sample and Preview Hardening

**Branch**: `004-end-to-end-sample-preview-hardening` | **Date**: 2026-05-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-end-to-end-sample-preview-hardening/spec.md`

## Summary

Add one recommended end-to-end console sample that composes Sessions, Tools, and Redaction in a realistic multi-turn Microsoft Agent Framework flow. The sample will export to Console by default and to Azure Application Insights when `APPLICATIONINSIGHTS_CONNECTION_STRING` is present, while documentation and validation artifacts harden the preview consumption story without adding a new runtime package or changing existing public APIs.

Phase 0 research selects a new `EndToEndTelemetry.Demo` sample rather than extending a package-specific demo, keeps Azure Monitor exporter dependencies sample-only, and validates clean package consumption through a fresh external project using published preview packages.

## Technical Context

**Language/Version**: C# latest stable as configured by repository; nullable enabled; file-scoped namespaces; sample targets the repository default `net8.0`, `net9.0`, and `net10.0` conventions where applicable.

**Primary Dependencies**: `Microsoft.Agents.AI`, existing `Melic.AgentFramework.Observability.Sessions`, `Tools`, `Redaction`, `Abstractions`, `OpenTelemetry`, `OpenTelemetry.Exporter.Console`, and sample-only `Azure.Monitor.OpenTelemetry.Exporter`. Existing model-provider packages and environment variables should follow current agent sample conventions.

**Storage**: N/A. The sample uses in-memory `AgentSession` state and does not introduce persistence.

**Testing**: xUnit for existing package tests; `dotnet build`; `dotnet test`; sample build/run validation; clean consumer restore/build validation using published preview packages; dependency inspection for package boundaries.

**Target Platform**: .NET console sample runnable from the repository on Windows, Linux, and macOS; Application Insights inspection is optional and cloud-backed.

**Project Type**: .NET library suite with console samples and documentation; this feature is sample/docs/validation work, not a new package.

**Performance Goals**: Sample starts and emits the expected local telemetry within the spec's under-10-minute developer validation window after prerequisites are available. Redaction remains bounded by existing processor options.

**Constraints**: No new runtime package; no performance/cost/token-budget feature work; no runtime prompt/message/tool mutation; no Azure Monitor/Application Insights/exporter dependency in library packages; Redaction remains export-boundary telemetry processing; no public API changes expected.

**Scale/Scope**: One new end-to-end sample, one recommended setup guide, consistency updates to touched public docs/package metadata, one clean consumer validation path, and dependency-boundary checks.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Stable OTel APIs only — planned implementation uses existing public OTel registration APIs; Azure Monitor exporter is sample-only.
- [x] Attribute naming — no new package-owned attributes are expected. Any doc/sample references to `gen_ai.*` are official MAF/OpenTelemetry attributes used for interoperability and explicit opt-in guidance.
- [x] Package dependency graph — no library package dependency changes are planned; sample may reference multiple packages and exporters, while Redaction remains independent from Sessions, Tools, Azure Monitor, Application Insights, and exporter packages.
- [x] Integration-first tests — validation uses real sample execution, real OTel pipeline/exporter paths where possible, and existing xUnit package tests rather than mocked MAF internals.
- [x] Public API docs — no new public API is expected. If implementation uncovers a necessary public member, it must include XML documentation and be re-evaluated here before tasks proceed.
- [x] MAF update resilience — sample and docs use public MAF APIs only. Package behavior remains behind existing adapters and public extension points.
- [x] Best-effort telemetry — sample demonstrates telemetry failures and exporter selection without changing agent behavior; package behavior remains best-effort.

## Project Structure

### Documentation (this feature)

```text
specs/004-end-to-end-sample-preview-hardening/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── sample-runtime-contract.md
│   └── preview-validation-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
samples/
├── EndToEndTelemetry.Demo/
│   ├── EndToEndTelemetry.Demo.csproj
│   ├── Program.cs
│   └── README.md
├── SessionTelemetry.Demo/
├── ToolTelemetry.Demo/
└── RedactionTelemetry.Demo/

docs/
├── features/
│   ├── recommended-preview-setup.md
│   ├── session-identity-enrichment.md
│   ├── tool-call-enrichment.md
│   └── redaction-pipeline.md
└── design/
    └── technical-reference.md

src/
├── Melic.AgentFramework.Observability.Abstractions/
├── Melic.AgentFramework.Observability.Sessions/
├── Melic.AgentFramework.Observability.Tools/
└── Melic.AgentFramework.Observability.Redaction/

tests/
├── Melic.AgentFramework.Observability.Sessions.Tests/
├── Melic.AgentFramework.Observability.Tools.Tests/
└── Melic.AgentFramework.Observability.Redaction.Tests/

nuget/
└── NUGET.md

README.md
CHANGELOG.md
agent-framework-observability.slnx
```

**Structure Decision**: Add a dedicated `samples/EndToEndTelemetry.Demo` project because the feature is a recommended composition story, not a replacement for package-specific demos. Keep reusable behavior in existing packages, keep exporter dependencies in the sample project, and update docs/metadata in place so package roles and boundaries remain consistent.

## Phase 0: Research

Completed in [research.md](research.md).

Key decisions:

- Create a new end-to-end sample rather than extending Sessions, Tools, or Redaction demos.
- Use Console exporter by default and Application Insights only via `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- Register Sessions and Tools through `AIAgentBuilder`; register Redaction on `TracerProviderBuilder` before exporters.
- Use `IncludeMafSensitiveDataAttributes()` when demonstrating MAF sensitive telemetry capture.
- Validate clean preview consumption with a fresh external project using published package references, not repository project references.
- Keep package boundary validation explicit, especially for Redaction's dependency independence.

## Phase 1: Design

Completed artifacts:

- [data-model.md](data-model.md): end-to-end sample, setup guide, validation flow, package boundary matrix, and inspection targets.
- [contracts/sample-runtime-contract.md](contracts/sample-runtime-contract.md): sample CLI/environment behavior, exporter selection, and telemetry expectations.
- [contracts/preview-validation-contract.md](contracts/preview-validation-contract.md): clean consumer and package-boundary validation expectations.
- [quickstart.md](quickstart.md): planned run commands, Application Insights configuration, consumer validation, and repository validation.

## Post-Design Constitution Check

- [x] Stable OTel APIs only — design uses `TracerProviderBuilder`, existing package APIs, and sample-only exporters.
- [x] Attribute naming — no new custom attributes; `gen_ai.*` appears only as official MAF/OpenTelemetry telemetry in docs/sample validation.
- [x] Package dependency graph — design does not change library package references; sample-only composition is allowed.
- [x] Integration-first tests — design validates actual sample build/run, real OTel export paths, and clean package restore/build.
- [x] Public API docs — no new public API planned, so no XML doc work is required unless implementation discovers a gap.
- [x] MAF update resilience — design uses public MAF sample APIs and keeps package source changes out of scope.
- [x] Best-effort telemetry — sample observes telemetry and redaction effects without changing runtime agent behavior.

## Complexity Tracking

No constitution violations or justified complexity exceptions.
