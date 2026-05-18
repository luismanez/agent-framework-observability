<!--
SYNC IMPACT REPORT
==================
Version Change: (unversioned template) → 1.0.0
Bump Rationale: MINOR — initial population of all principles and sections; no prior version exists.

Added Sections:
  - Core Principles (6 principles: Package Independence, OTel-Native, Zero Breaking Changes,
    Integration Tests, Attribute Naming, Code Style)
  - Technology Stack & Constraints
  - Development Workflow (including Constitution Check gate list)
  - Governance

Removed Sections: None (template placeholders replaced)

Modified Principles: None (first-time authoring)

Templates Requiring Updates:
  ✅ .specify/memory/constitution.md — written (this file)
  ✅ .specify/templates/plan-template.md — Constitution Check placeholder is intentionally
     generic ([Gates determined based on constitution file]); the concrete gate list now lives
     in the Development Workflow section of this constitution and is injected by /speckit.plan.
  ✅ .specify/templates/spec-template.md — no structural changes required; user story format
     is compatible with all six principles.
  ✅ .specify/templates/tasks-template.md — task phases are compatible; principle-driven task
     types (observability pipeline, attribute naming, redaction) will be expressed in individual
     tasks.md files, not in the template itself.

Deferred TODOs: None — all placeholders resolved.
-->

# Melic.AgentFramework.Observability Constitution

## Core Principles

### I. Package Independence
Each of the six packages (Abstractions, Performance, Tools, Sessions, Redaction, and the
meta-package) MUST be independently publishable, versionable, and testable. The meta-package
contains zero source code and exists solely to group consumer-facing package references.
No package MUST depend on a sibling package except through Abstractions. Cross-cutting logic
MUST live in Abstractions; duplication across sibling packages is PROHIBITED.

### II. OTel-Native, No Platform Internals
All packages MUST depend only on stable, publicly documented OpenTelemetry .NET APIs:
`OpenTelemetry`, `System.Diagnostics.Activity`, and `Microsoft.Extensions.AI`.
Reflection-based discovery, Source Generator output, and use of internal Microsoft Agent
Framework (MAF) types are PROHIBITED. Dependency on MAF is permitted only through its
stable public API surface. Preview or pre-release OTel packages MUST NOT appear in any
stable release.

### III. Zero Breaking Changes on MAF Updates
Packages MUST NOT break when the underlying Microsoft Agent Framework updates its public
API, provided that API remains stable. All MAF-dependent code MUST be isolated behind
adapter types defined in the Abstractions package. When MAF introduces a breaking change,
only the affected adapter(s) require updating; all other packages MUST remain
source-compatible without modification.

### IV. Integration Tests Over Unit Tests (NON-NEGOTIABLE)
xUnit is the mandatory test framework. Mocking of internal MAF types is PROHIBITED.
Integration tests are PREFERRED over unit tests wherever behavior crosses a package
boundary, an Activity/Span lifecycle, or an OpenTelemetry pipeline. Unit tests are
acceptable only for pure algorithmic logic with zero external dependencies (e.g.,
attribute name validation, redaction rule evaluation). No PR may remove integration test
coverage in favor of mocked equivalents.

### V. Telemetry Attribute Naming Discipline
All custom telemetry attributes emitted by this library MUST use the `genai.` prefix.
The `gen_ai.` prefix is RESERVED for the official OpenTelemetry Semantic Conventions
and MUST NOT be used anywhere in this codebase. Attribute names MUST be lowercase,
dot-separated, and self-describing. Any new attribute key MUST be declared as a
`public static readonly string` constant in Abstractions before it is referenced in
any other package.

### VI. Code Style & API Surface Completeness
All code MUST target the latest stable C# language version with `<Nullable>enable</Nullable>`
and file-scoped namespaces. Every public type, method, property, and field MUST carry an
XML documentation comment; no public API may be merged without its doc. Code MUST compile
without warnings under `<TreatWarningsAsErrors>true`. No suppression of nullable or
documentation warnings is permitted on public-facing members.

### VII. Best-Effort Telemetry — Never Affect Agent Behavior
All telemetry logic MUST be fully encapsulated within a try/catch boundary that prevents
any telemetry failure from propagating to the caller or altering the outcome of the agent
invocation. Telemetry is best-effort: a failed span write, a StateBag serialisation error,
or a metric recording failure MUST be swallowed silently. Telemetry failures MAY be counted
via a dedicated counter metric (e.g., `session.statebag.write.failures`) but MUST NOT throw
exceptions or alter control flow visible to the caller. The agent invocation MUST complete —
or fail for its own reasons — regardless of the health of the telemetry pipeline. This
principle applies to every package in the suite (Sessions, Performance, Tools, Redaction).

## Technology Stack & Constraints

**Target Frameworks**: `net8.0`, `net9.0`, `net10.0` (multi-targeted via `<TargetFrameworks>`).

**Package Naming Convention**: `Melic.AgentFramework.Observability.*`

| Package | Purpose |
|---|---|
| `Melic.AgentFramework.Observability.Abstractions` | Shared contracts, attribute constants, interfaces |
| `Melic.AgentFramework.Observability.Performance` | CPU/memory/latency instrumentation |
| `Melic.AgentFramework.Observability.Tools` | Tool-call span enrichment |
| `Melic.AgentFramework.Observability.Sessions` | Conversation/session lifecycle tracing |
| `Melic.AgentFramework.Observability.Redaction` | PII redaction pipeline for telemetry |
| `Melic.AgentFramework.Observability` | Meta-package, no code, aggregates the above |

**License**: MIT. Every source file MUST include `// SPDX-License-Identifier: MIT`.

**Versioning**: MinVer driven from git tags (`v<MAJOR>.<MINOR>.<PATCH>`). No manual version
properties or AssemblyInfo version files. All packages in a release share one version tag.

**NuGet Publishing**: All packages MUST be published to NuGet.org. Package metadata
(description, tags, repository URL, readme, icon) MUST be complete before any public release.

**Prohibited Dependencies**: Reflection-heavy libraries, source generators, internal MAF
assemblies, preview or pre-release OTel packages in stable releases, and any dependency
that transitively brings in platform-specific native binaries without an opt-in mechanism.

## Development Workflow

**Branch Strategy**: Feature branches off `main`; PRs require at least one maintainer approval.

**Build Gate**: Every PR MUST pass:
- `dotnet build` (all three TFMs, zero warnings-as-errors)
- `dotnet test` (xUnit, no skipped integration tests without documented reason)
- XML doc completeness (no CS1591 suppression on public members)

**Constitution Check** (MUST appear in every `plan.md` before Phase 0 research):

- [ ] Does the feature touch only stable OTel APIs? (Principle II)
- [ ] Are all new attributes declared in Abstractions and using the `genai.` prefix? (Principle V)
- [ ] Is the package dependency graph acyclic and rooted at Abstractions? (Principle I)
- [ ] Does the test plan prefer integration tests over mocked unit tests? (Principle IV)
- [ ] Does every new public member have an XML doc comment? (Principle VI)
- [ ] Could a MAF version bump silently break this feature? (Principle III)

**Release Process**: Tag `vX.Y.Z` on `main` → MinVer resolves the version → CI publishes all
packages to NuGet.org atomically. Release notes MUST accompany every tag.

## Governance

This constitution supersedes all other development practices for this repository.
Amendments MUST:

1. Increment the constitution version following semantic versioning:
   - MAJOR: backward-incompatible principle removal or redefinition.
   - MINOR: new principle or section added, or materially expanded guidance.
   - PATCH: clarifications, wording corrections, non-semantic refinements.
2. Update dependent templates (`plan-template.md`, `spec-template.md`, `tasks-template.md`)
   to reflect the amendment within the same PR.
3. Be ratified by at least one project maintainer via PR review and merge.

All PRs and code reviews MUST verify compliance with all six Core Principles before approval.
Complexity MUST be justified; prefer fewer abstractions over more flexible but
harder-to-reason-about designs. When in doubt, defer to Principle II (OTel-Native).

**Version**: 1.0.0 | **Ratified**: 2026-05-13 | **Last Amended**: 2026-05-13
