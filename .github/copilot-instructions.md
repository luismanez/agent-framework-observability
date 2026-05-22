# Melic.AgentFramework.Observability

A C# observability library for the [Microsoft Agent Framework (MAF)](https://github.com/microsoft/agent-framework)
that adds OpenTelemetry-native session tracking, performance instrumentation, tool-call span enrichment,
and PII redaction. All packages target `net8.0`, `net9.0`, and `net10.0`.

## Package Map

| Package | Purpose |
|---|---|
| `Melic.AgentFramework.Observability.Abstractions` | Shared attribute constants (`SessionAttributeNames`, etc.), interfaces |
| `Melic.AgentFramework.Observability.Sessions` | Conversation/session lifecycle tracing |
| `Melic.AgentFramework.Observability.Performance` | _(planned)_ CPU/memory/latency instrumentation |
| `Melic.AgentFramework.Observability.Tools` | Tool-call span enrichment |
| `Melic.AgentFramework.Observability.Redaction` | _(planned)_ PII redaction pipeline |
| `Melic.AgentFramework.Observability` | Meta-package — no code, aggregates the above |

Source lives under `src/`, tests under `tests/`. Solution file: `agent-framework-observability.slnx`.

## Technology Stack

- **Language**: C# 13 (latest stable), `<Nullable>enable</Nullable>`, file-scoped namespaces
- **Frameworks**: `net8.0`, `net9.0`, `net10.0` (multi-targeted via `<TargetFrameworks>`)
- **MAF**: `Microsoft.Agents.AI` — `AIAgent`, `DelegatingAIAgent`, `AIAgentBuilder`, `AgentSession`, `AgentSessionStateBag`
- **OTel**: `OpenTelemetry.Api` — `Meter`, `Counter<T>`, `Histogram<T>`, `UpDownCounter<T>`, `ActivitySource`
- **Testing**: `xUnit` + `Microsoft.NET.Test.Sdk`; CPM (`ManagePackageVersionsCentrally=true`) in `eng/Directory.Packages.props`
- **Versioning**: MinVer driven from git tags (`v<MAJOR>.<MINOR>.<PATCH>`); no manual version properties

## Non-Negotiable Rules (always apply)

1. **OTel-only dependencies** — depend only on `OpenTelemetry.Api`, `System.Diagnostics.Activity`, `Microsoft.Extensions.AI`. No MAF internal types, no reflection-heavy libs, no preview OTel packages in stable releases.
2. **Attribute prefix** — package-owned custom telemetry attributes use the `genai.` prefix. The `gen_ai.` prefix is **reserved for OTel Semantic Conventions** and MAF-owned attributes; references to official `gen_ai.*` attributes are allowed only for interoperability, tests, documentation, or explicit consumer opt-in behavior.
3. **Declare before use** — every new attribute key must be declared as `public static readonly string` in the Abstractions package before being referenced anywhere else.
4. **Package independence** — no package may depend on a sibling except through Abstractions. Cross-cutting logic lives in Abstractions; duplication across siblings is prohibited.
5. **Integration tests preferred** — mocking internal MAF types is prohibited. Use real `AgentSession`, `AIAgent`, `ActivitySource`. Unit tests are acceptable only for pure algorithmic logic with zero external dependencies.
6. **SPDX header** — every `.cs` file must begin with `// SPDX-License-Identifier: MIT`.
7. **Zero warnings** — `TreatWarningsAsErrors=true`. Every public type/method/property/field must have an XML doc comment. No suppression of CS1591 or nullable warnings on public members.

## Common Build Commands

```powershell
# From repo root
dotnet build                          # all TFMs, zero warnings expected
dotnet test                           # xUnit across net8/net9/net10
dotnet test --filter "FullyQualifiedName~Sessions"   # single package
dotnet test --filter "FullyQualifiedName~Tools"      # Tools package
```

## Feature Specs

Feature work follows the Spec Kit convention. Each spec lives under `specs/<NNN>-<feature-name>/`
and contains `spec.md`, `plan.md`, `tasks.md`, and a `contracts/` directory for public API surface.
The constitution at `.specify/memory/constitution.md` is the authoritative source for principles.

## Observability Decorator Pattern

Most observability packages intercept agent invocations by subclassing `DelegatingAIAgent` and
registering via `AIAgentBuilder.Use()`. The Tools package uses MAF's public function-invocation
middleware to intercept tool execution around `FunctionInvocationContext`. Cross-invocation state is stored in
`AgentSession.StateBag` under a **configurable string key** (default: `"__melic_telemetry"`),
serialised as a JSON record using `System.Text.Json`. Each package's state record must be
independently typed and stored under its own key to avoid collisions between packages.

The Sessions package establishes the design template for all future observability packages:

- **Mode A** (default): Enrich every `invoke_agent` span with structured `genai.*` tags.
  Consumers correlate sessions by filtering on those tags in their observability backend.
  No configuration beyond `UseSessionTelemetry()` is required.
- **Mode B** (opt-in): Open an explicit parent span via `BeginSessionTrace()` so all
  invocations in a scope appear as children of a single trace tree. Useful for
  single-process, bounded sessions where visual trace trees are more valuable than filtered
  queries. Enabled via `SessionTelemetryOptions.EnableSessionSpan = true`.

---

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
at specs/004-end-to-end-sample-preview-hardening/plan.md
<!-- SPECKIT END -->
