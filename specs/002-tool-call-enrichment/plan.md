# Implementation Plan: Tool Call Span Enrichment

**Branch**: `002-tool-call-enrichment` | **Date**: 2026-05-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-tool-call-enrichment/spec.md`

## Summary

Implement `Melic.AgentFramework.Observability.Tools` as a tool-invocation telemetry package for Microsoft Agent Framework. The package will register through `AIAgentBuilder.UseToolTelemetry()` and use MAF's public function-invocation middleware (`AIAgentBuilder.Use(Func<AIAgent, FunctionInvocationContext, ...>)`) behind Abstractions-defined adapter contracts to create an `agent_tool_call` span per tool execution, capture tool name/call id/input/output, mark retries within the current `invoke_agent` span, and keep all telemetry logic best-effort and isolated from business behavior.

## Technical Context

**Language/Version**: C# 13 (latest stable), targeting `net8.0;net9.0;net10.0`

**Primary Dependencies**:

- `Microsoft.Agents.AI` — `AIAgent`, `AIAgentBuilder`, `FunctionInvocationDelegatingAgentBuilderExtensions`, `OpenTelemetryAgentBuilderExtensions`, `ChatClientAgentRunOptions`
- `Microsoft.Extensions.AI` — `FunctionInvocationContext`, `FunctionCallContent`, `AIFunction`, `AIFunctionArguments`, `FunctionInvokingChatClient`
- `System.Diagnostics.DiagnosticSource` — `Activity`, `ActivitySource`, `ActivityKind`
- `OpenTelemetry.Api` — tracing API consumption only; no custom exporter dependency
- `System.Text.Json` — input/output/error payload serialization and valid-JSON truncation
- `MinVer` (build-time only) — version from git tags

**Storage**: In-memory invocation-scoped retry state only. No persistence. Retry counters keyed by the current parent `Activity` and tool `call_id` via a GC-safe cache (`ConditionalWeakTable<Activity, ConcurrentDictionary<string, int>>` or equivalent).

**Testing**: xUnit integration tests preferred — real `ChatClientAgent`/`AIAgentBuilder` pipeline with `AIFunction` tools, `ActivityListener` assertions for trace shape, and limited pure unit tests only for JSON truncation helpers.

**Target Platform**: NuGet library, no host dependency, no DI required.

**Project Type**: Library package in the `Melic.AgentFramework.Observability.*` suite.

**Performance Goals**: One child span per tool invocation with low constant overhead; no extra agent round-trip; no observable change in tool results or invocation completion when telemetry fails.

**Constraints**:

- Public APIs only — no internal MAF types, reflection, or source generators
- Custom attributes must use `genai.tool.*` and be declared in Abstractions before use
- Tool spans must use `ActivityKind.Internal`
- Captured input/output must remain valid JSON after truncation and stay within configured limits
- Package must not depend on `Sessions` or other siblings beyond `Abstractions`
- All failures in telemetry path must be swallowed silently
- XML docs on all public members; zero warnings; SPDX header on every `.cs` file

**Scale/Scope**: New `Tools` package, new Tools test project, new attribute constants in Abstractions, and design docs for one feature. No changes to the existing Sessions behavior.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] Does the feature touch only stable OTel APIs? → **PASS** — `ActivitySource`, `Activity`, `ActivityKind`, and standard OTel tracing consumption only.
- [x] Are all new attributes declared in Abstractions and using the `genai.` prefix? → **PASS** — introduce `ToolAttributeNames` in Abstractions for every `genai.tool.*` key before Tools references them.
- [x] Is the package dependency graph acyclic and rooted at Abstractions? → **PASS** — `Tools` depends only on `Abstractions`; no sibling dependencies.
- [x] Does the test plan prefer integration tests over mocked unit tests? → **PASS** — trace shape, retry tracking, and MAF middleware coverage will be exercised with a real agent pipeline.
- [x] Does every new public member have an XML doc comment? → **PASS** — public API limited to builder extension and options type, all documented.
- [x] Could a MAF version bump silently break this feature? → **PASS** — Abstractions defines the adapter contract and tool invocation data shape; the Tools package's only MAF-specific code is the adapter implementation around `FunctionInvocationContext`.

## Project Structure

### Documentation (this feature)

```text
specs/002-tool-call-enrichment/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── tools-api.md
└── tasks.md
```

### Source Code (repository root)

```text
src/
├── Melic.AgentFramework.Observability.Abstractions/
│   ├── ToolAttributeNames.cs
│   ├── ToolInvocationData.cs
│   └── ToolInvocationContextAdapter.cs
├── Melic.AgentFramework.Observability.Sessions/
└── Melic.AgentFramework.Observability.Tools/
    ├── ToolTelemetryAgentBuilderExtensions.cs
    ├── ToolTelemetryOptions.cs
    └── Internal/
        ├── ToolTelemetryAgent.cs
        ├── ToolInvocationMapper.cs
        ├── ToolPayloadSerializer.cs
        └── InvocationAttemptRegistry.cs

tests/
├── Melic.AgentFramework.Observability.Sessions.Tests/
└── Melic.AgentFramework.Observability.Tools.Tests/
    ├── ToolTelemetryIntegrationTests.cs
    └── ToolPayloadSerializerTests.cs
```

**Structure Decision**: Follow the existing Sessions package shape: a thin public surface (`UseToolTelemetry`, `ToolTelemetryOptions`), all execution logic inside `Internal/`, and all shared attribute names plus MAF adapter contracts in Abstractions. The Tools package implements the Abstractions adapter for `FunctionInvocationContext`, then the telemetry pipeline works over `ToolInvocationData`. Testing mirrors this split with integration-first coverage plus one narrow pure helper test class for truncation logic.

## Post-Design Constitution Check

- [x] Stable OTel APIs only — unchanged after design.
- [x] Abstractions-first attribute declaration — `ToolAttributeNames` is the single source of truth.
- [x] Package independence preserved — no direct reference to Sessions.
- [x] Integration tests remain primary — design centers on full agent pipeline tests.
- [x] Public API remains small and fully documented.
- [x] MAF dependency isolated — Abstractions owns the adapter contract and data shape; only the Tools adapter implementation reads MAF/MEAI-specific members.

## Complexity Tracking

No constitution violations or exceptional complexity accepted for this feature.
