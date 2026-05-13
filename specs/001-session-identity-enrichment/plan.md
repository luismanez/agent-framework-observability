# Implementation Plan: Session Identity and Enrichment

**Branch**: `001-session-identity-enrichment` | **Date**: 2026-05-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-session-identity-enrichment/spec.md`

## Summary

Implement `Melic.AgentFramework.Observability.Sessions` — a `DelegatingAIAgent` decorator that provides stable per-session telemetry: a persistent `SessionId`, per-invocation enrichment tags (`genai.session.*`), running token aggregates, custom tag propagation, and an optional Mode B session span. All state is persisted in `AgentSession.StateBag` via `System.Text.Json`. Integration point is a single `AIAgentBuilder.UseSessionTelemetry()` extension call.

## Technical Context

**Language/Version**: C# 13 (latest stable), targeting `net8.0;net9.0;net10.0`

**Primary Dependencies**:
- `Microsoft.Agents.AI` (≥ current stable) — `AIAgent`, `AIAgentBuilder`, `DelegatingAIAgent`, `AgentSession`, `AgentSessionStateBag`, `AgentResponse`
- `Microsoft.Extensions.AI` — `UsageDetails`, `ChatMessage`
- `System.Diagnostics.DiagnosticSource` — `Activity`, `ActivitySource`, `ActivityListener`
- `OpenTelemetry` (stable) — `Meter`, `Counter<T>`, `Histogram<T>`, `UpDownCounter<T>`
- `System.Text.Json` — `SessionStateBlock` serialization within StateBag
- `MinVer` (build-time, no runtime dependency) — version from git tag

**Storage**: `AgentSessionStateBag.SetValue<T>` / `TryGetValue<T>` under reserved key `"__melic_telemetry"` (configurable via `StateBagKey`). The block is a `SessionStateBlock` record serialized by STJ on every write.

**Testing**: xUnit (mandatory per constitution). Integration tests preferred — spin up a real `AIAgentBuilder` pipeline with a stub `AIAgent`, verify `Activity` tags via `ActivityListener`, verify metrics via `MetricCollector<T>`. No mocking of internal MAF types.

**Target Platform**: NuGet library, no runtime host dependency, no DI required.

**Project Type**: library (one of six in the `Melic.AgentFramework.Observability.*` suite)

**Performance Goals**: Read path (StateBag lookup + tag writes) must not add measurable latency to the agent invocation; telemetry is best-effort and all failure paths swallow exceptions.

**Constraints**:
- No reflection, no source generators, no internal MAF types
- Stable OTel and MEAI APIs only (no `[Experimental]` surfaces)
- No sibling package dependency (only `Abstractions`)
- `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true`, file-scoped namespaces, XML docs on all public members
- `SPDX-License-Identifier: MIT` header on every source file

**Scale/Scope**: Sessions package only. Abstractions constants (`SessionAttributeNames`) are the cross-cutting concern; all other logic is self-contained in the Sessions package.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- [x] Does the feature touch only stable OTel APIs? → **PASS** — `ActivitySource`, `Activity.SetTag`, `Meter`, `Counter<long>`, `Histogram<long>`, `UpDownCounter<long>` are all stable. No preview OTel APIs used.
- [x] Are all new attributes declared in Abstractions and using the `genai.` prefix? → **PASS** — All `genai.session.*` attribute name string constants will be declared as `public static readonly string` fields in `SessionAttributeNames` in Abstractions, before being referenced in Sessions.
- [x] Is the package dependency graph acyclic and rooted at Abstractions? → **PASS** — Sessions → Abstractions only. No sibling dependency.
- [x] Does the test plan prefer integration tests over mocked unit tests? → **PASS** — Integration tests use a real `AIAgentBuilder` + stub `AIAgent`. Only `SessionStateBlock` validation logic (pure algorithmic) may use unit tests.
- [x] Does every new public member have an XML doc comment? → **PASS** — enforced as constitution requirement and warnings-as-errors gate.
- [x] Could a MAF version bump silently break this feature? → **MITIGATED** — Only touches `AgentSession.StateBag` (`SetValue<T>`/`TryGetValue<T>`), `AgentResponse.Usage`, `DelegatingAIAgent.InnerAgent`, and `AIAgentBuilder.Use()` — all stable public API. `SessionTelemetryAgent` wraps the entire MAF surface; only that class needs updating if MAF changes.

**Constitution Check post-design**: Re-evaluate after Phase 1. No violations at planning time.

## Project Structure

### Documentation (this feature)

```text
specs/001-session-identity-enrichment/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── sessions-api.md  # Phase 1 output — public API contract
└── tasks.md             # Phase 2 output (speckit.tasks — NOT created here)
```

### Source Code (repository root)

```text
src/
├── Melic.AgentFramework.Observability.Abstractions/
│   ├── Melic.AgentFramework.Observability.Abstractions.csproj
│   └── SessionAttributeNames.cs        ← genai.session.* constants
│
└── Melic.AgentFramework.Observability.Sessions/
    ├── Melic.AgentFramework.Observability.Sessions.csproj
    ├── SessionTelemetryAgentBuilderExtensions.cs
    ├── SessionTelemetryExtensions.cs
    ├── SessionTelemetryOptions.cs
    └── Internal/
        ├── SessionTelemetryAgent.cs    ← DelegatingAIAgent decorator
        ├── SessionStateBlock.cs        ← STJ-serializable record
        └── SessionStateBagAccessor.cs  ← read/write/lock helpers

tests/
└── Melic.AgentFramework.Observability.Sessions.Tests/
    ├── Melic.AgentFramework.Observability.Sessions.Tests.csproj
    ├── SessionIdentityTests.cs         ← FR-001/002/003/008/018
    ├── SessionAggregateTests.cs        ← FR-004/005/006/007/022
    ├── SessionTagTests.cs              ← FR-010/011/012/013/020/023
    ├── SessionSpanTests.cs             ← FR-014/015/016 (Mode B)
    ├── SessionErrorHandlingTests.cs    ← FR-017/024
    ├── SessionSchemaTests.cs           ← FR-021 forward-compat
    └── Helpers/
        ├── StubAIAgent.cs
        └── ActivityRecorder.cs
```

**Structure Decision**: Single-package layout (Option 1 variant). Sessions package only; Abstractions is a sibling that already exists in the suite plan. The `Internal/` subfolder groups types that are `internal` by access modifier — no separate assembly boundary needed.

## Complexity Tracking

> No constitution violations to justify.

