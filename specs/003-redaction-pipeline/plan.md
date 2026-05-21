# Implementation Plan: Redaction Pipeline

**Branch**: `003-redaction-pipeline` | **Date**: May 20, 2026 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-redaction-pipeline/spec.md`

## Summary

Add an independently installable `Melic.AgentFramework.Observability.Redaction` package that redacts sensitive string telemetry attribute values before OpenTelemetry trace export. The technical approach is a stable OpenTelemetry trace processor registered on `TracerProviderBuilder`, with default protection for high-risk package-owned tool payload attributes, configurable target/rule selection, JSON-preserving redaction where practical, and fail-closed behavior for targeted values that cannot be safely processed.

## Technical Context

**Language/Version**: C# 13 / latest stable, nullable enabled, file-scoped namespaces.

**Primary Dependencies**: `OpenTelemetry` 1.15.3 for `TracerProviderBuilder` and processor APIs; `OpenTelemetry.Api` / `System.Diagnostics.Activity` through the OTel stack; `System.Text.Json`; `System.Text.RegularExpressions`; project reference to `Melic.AgentFramework.Observability.Abstractions` only.

**Storage**: N/A. Redaction is stateless per telemetry item and stores no application data.

**Testing**: xUnit + `Microsoft.NET.Test.Sdk`; integration-style tests with real `ActivitySource`, `TracerProvider`, and test exporters/processors; unit tests only for pure redaction engine/rule evaluation.

**Target Platform**: NuGet package multi-targeting `net8.0`, `net9.0`, and `net10.0` through repo-wide `TargetFrameworks`.

**Project Type**: .NET library package plus test project and a console sample demonstrating composition with Sessions and Tools.

**Performance Goals**: Redaction work is bounded per targeted string attribute. Default `MaxValueLength` is 8192 characters; values over the limit are handled by configured failure behavior rather than unbounded inspection. Regex rules use bounded execution time.

**Constraints**: No Azure-specific dependency; no Application Insights-specific API; no dependency on Sessions or Tools; no MAF internal types; no mutation of runtime business objects, agent messages, tool arguments, or tool results; no sensitive data in diagnostics.

**Scale/Scope**: MVP processes trace `Activity` string attributes only. Default targets are `genai.tool.input` and `genai.tool.output`. Built-in `genai.session.*` correlation and aggregate attributes are not default targets; session custom tags or future user-provided session attributes are covered through explicit exact-attribute or prefix targeting. Additional package-owned, custom, exception, and selected standard attributes are opt-in through configuration. Logs, baggage, metrics redaction, DLP classification, and AI-based detection are out of scope.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Does the feature touch only stable OTel APIs? (Principle II) — Uses stable OpenTelemetry processor APIs and `System.Diagnostics.Activity`; no MAF internals or preview OTel APIs.
- [x] Are all new attributes declared in Abstractions and using the `genai.` prefix? (Principle V) — Plan adds `RedactionAttributeNames` constants for optional `genai.redaction.*` diagnostics before Redaction references them.
- [x] Is the package dependency graph acyclic and rooted at Abstractions? (Principle I) — Redaction depends on Abstractions and OpenTelemetry only; no Sessions/Tools dependency.
- [x] Does the test plan prefer integration tests over mocked unit tests? (Principle IV) — Processor behavior is tested with real OTel primitives; pure redaction algorithms may use focused unit tests.
- [x] Does every new public member have an XML doc comment? (Principle VI) — Public options, extension methods, enums, and constants require XML docs.
- [x] Could a MAF version bump silently break this feature? (Principle III) — No compile-time MAF dependency; standard `gen_ai.*` processing is opt-in and attribute-name based.
- [x] Does telemetry failure avoid affecting agent/application behavior? (Principle VII) — Processor catches processing failures and applies configured fail-safe behavior without interrupting export or invocation flow.

## Project Structure

### Documentation (this feature)

```text
specs/003-redaction-pipeline/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── public-api.md
└── tasks.md             # Created later by /speckit.tasks
```

### Source Code (repository root)

```text
src/
├── Melic.AgentFramework.Observability.Abstractions/
│   └── RedactionAttributeNames.cs
├── Melic.AgentFramework.Observability.Redaction/
│   ├── Melic.AgentFramework.Observability.Redaction.csproj
│   ├── RedactionOptions.cs
│   ├── RedactionFailureMode.cs
│   ├── RedactionTracerProviderBuilderExtensions.cs
│   └── Internal/
│       ├── RedactionProcessor.cs
│       ├── RedactionPolicy.cs
│       ├── RedactionEngine.cs
│       ├── AttributeTargetMatcher.cs
│       ├── RedactionRule.cs
│       ├── JsonPayloadRedactor.cs
│       └── PatternRedactionRule.cs

tests/
└── Melic.AgentFramework.Observability.Redaction.Tests/
    ├── Melic.AgentFramework.Observability.Redaction.Tests.csproj
    ├── RedactionProcessorTests.cs
    ├── RedactionDefaultsTests.cs
    ├── RedactionJsonPayloadTests.cs
    ├── RedactionConfigurationTests.cs
    ├── RedactionFailureModeTests.cs
    └── RedactionTestExporter.cs

samples/
└── RedactionTelemetry.Demo/
    ├── RedactionTelemetry.Demo.csproj
    ├── Program.cs
    └── README.md
```

**Structure Decision**: Create a fourth production package under `src/` that matches the existing Sessions and Tools package layout, plus a matching test project and sample. Update `agent-framework-observability.slnx` to include the new source, test, and sample projects. Keep reusable constants in Abstractions and all redaction processing in the Redaction package.

## Phase 0: Research

Completed in [research.md](research.md).

Key decisions:

- OpenTelemetry trace processor is the primary integration point.
- Redaction package is independent and references only Abstractions plus stable OTel APIs.
- No external compliance/redaction package dependency in MVP.
- Default targets are high-risk tool payload attributes, not every `genai.*` attribute.
- Official `gen_ai.*` attributes are opt-in only.
- JSON shape is preserved when practical; targeted failures fail closed by default.
- Pattern and field-name rules are deterministic and bounded.
- Diagnostics are aggregate `genai.redaction.*` attributes only in MVP.

## Phase 1: Design

Completed artifacts:

- [data-model.md](data-model.md): redaction options, policy, targets, rules, results, processor, and diagnostics.
- [contracts/public-api.md](contracts/public-api.md): public registration API, options contract, failure mode enum, attribute constants, default targets, and processor safety contract.
- [quickstart.md](quickstart.md): package usage, OTel registration, composition with Sessions/Tools, custom rules, standard attribute opt-in, diagnostics, and validation commands.

## Post-Design Constitution Check

- [x] Stable OTel APIs only — public API is a `TracerProviderBuilder` extension plus package options; implementation uses `BaseProcessor<Activity>`-style processing and the Redaction project must not reference Azure, Application Insights, or exporter-specific packages.
- [x] Attribute naming — new diagnostic attributes are `genai.redaction.*` and declared in Abstractions.
- [x] Package independence — Redaction has no Sessions/Tools dependency and can run against any `Activity` source.
- [x] Integration-first tests — processor/export behavior uses real OTel primitives; pure string/JSON rule behavior may use unit tests.
- [x] Public API docs — all public types in the contract require XML documentation.
- [x] MAF update resilience — no MAF compile-time surface is consumed.
- [x] Best-effort telemetry — failures do not throw from export processing and do not alter agent/application behavior.

## Complexity Tracking

No constitution violations or justified complexity exceptions.
