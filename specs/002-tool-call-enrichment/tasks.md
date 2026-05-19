# Tasks: Tool Call Span Enrichment

**Input**: Design documents from `/specs/002-tool-call-enrichment/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/tools-api.md, quickstart.md

**Tests**: Included. The feature specification defines independent tests for each user story, and the constitution requires integration-first coverage for Activity/span behavior.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Every task includes an exact file path

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add the new Tools package and test project to the repository without implementing story behavior yet.

- [X] T001 Create Tools project file with package metadata, Abstractions reference, MAF/MEAI/OpenTelemetry references, and InternalsVisibleTo in src/Melic.AgentFramework.Observability.Tools/Melic.AgentFramework.Observability.Tools.csproj
- [X] T002 Create Tools test project file with xUnit, OpenTelemetry, and Tools project reference in tests/Melic.AgentFramework.Observability.Tools.Tests/Melic.AgentFramework.Observability.Tools.Tests.csproj
- [X] T003 Add Tools source and test projects to agent-framework-observability.slnx under `/src/` and `/tests/`
- [X] T004 [P] Create public namespace placeholder file with SPDX header in src/Melic.AgentFramework.Observability.Tools/ToolTelemetryOptions.cs
- [X] T005 [P] Create Abstractions invocation data placeholder file with SPDX header in src/Melic.AgentFramework.Observability.Abstractions/ToolInvocationData.cs
- [X] T006 [P] Create test helpers folder for Activity capture utilities in tests/Melic.AgentFramework.Observability.Tools.Tests/Helpers/ActivityCapture.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core constants, options, mapping, serialization, and retry primitives needed before any user story can be completed.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 Add `ToolAttributeNames` constants plus MAF adapter boundary types with XML docs in src/Melic.AgentFramework.Observability.Abstractions/ToolAttributeNames.cs, src/Melic.AgentFramework.Observability.Abstractions/ToolInvocationData.cs, and src/Melic.AgentFramework.Observability.Abstractions/ToolInvocationContextAdapter.cs
- [X] T008 Implement `ToolTelemetryOptions` defaults and validation helpers in src/Melic.AgentFramework.Observability.Tools/ToolTelemetryOptions.cs
- [X] T009 [P] Implement `ToolInvocationData` record for mapped tool call data in src/Melic.AgentFramework.Observability.Abstractions/ToolInvocationData.cs
- [X] T010 [P] Implement `ToolInvocationMapper` as the Tools adapter from `FunctionInvocationContext` into Abstractions `ToolInvocationData` in src/Melic.AgentFramework.Observability.Tools/Internal/ToolInvocationMapper.cs
- [X] T011 [P] Implement `InvocationAttemptRegistry` with parent-Activity scoped concurrent attempt counts in src/Melic.AgentFramework.Observability.Tools/Internal/InvocationAttemptRegistry.cs
- [X] T012 [P] Implement initial `ToolPayloadSerializer` shell for JSON serialization entry points in src/Melic.AgentFramework.Observability.Tools/Internal/ToolPayloadSerializer.cs
- [X] T013 Add shared ActivityListener test helper able to collect Activity name, source, kind, parent, tags, and status in tests/Melic.AgentFramework.Observability.Tools.Tests/Helpers/ActivityCapture.cs
- [X] T014 [P] Add pure tests for `ToolTelemetryOptions` default values and invalid max length/source validation in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryOptionsTests.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in priority order or in parallel where noted.

---

## Phase 3: User Story 1 - Tool Execution Visibility (Priority: P1) MVP

**Goal**: Every intercepted tool invocation emits an `agent_tool_call` span with tool name, call id when available, `ActivityKind.Internal`, status, duration, and parent relationship to the current `invoke_agent` span.

**Independent Test**: Configure tool telemetry on an agent with one or more tools and verify emitted spans under a captured parent activity without requiring input/output capture or retry detection.

### Tests for User Story 1

- [X] T015 [P] [US1] Add integration test for successful delayed single tool call child span, tool name, `ActivityKind.Internal`, Activity status OK, parent id, and wall-clock duration in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryVisibilityTests.cs
- [X] T016 [P] [US1] Add integration test for failing tool call Activity status ERROR and status description in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryVisibilityTests.cs
- [X] T017 [P] [US1] Add integration test for three sequential tool calls producing three independent `agent_tool_call` spans in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryVisibilityTests.cs
- [X] T018 [P] [US1] Add integration tests proving no spans are emitted when `.UseToolTelemetry()` is not registered and root spans are emitted when no parent Activity is current in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryVisibilityTests.cs

### Implementation for User Story 1

- [X] T019 [US1] Implement `UseToolTelemetry()` builder extension using MAF public function-invocation middleware in src/Melic.AgentFramework.Observability.Tools/ToolTelemetryAgentBuilderExtensions.cs
- [X] T020 [US1] Implement `ToolTelemetryAgent` callback orchestration to start `agent_tool_call` spans around `next(context, cancellationToken)` in src/Melic.AgentFramework.Observability.Tools/Internal/ToolTelemetryAgent.cs
- [X] T021 [US1] Add span attributes `genai.tool.name` and optional `genai.tool.call_id` during span start in src/Melic.AgentFramework.Observability.Tools/Internal/ToolTelemetryAgent.cs
- [X] T022 [US1] Set tool Activity status OK/ERROR and status description while preserving original tool result or exception in src/Melic.AgentFramework.Observability.Tools/Internal/ToolTelemetryAgent.cs
- [X] T023 [US1] Ensure all tool telemetry failures are swallowed and do not alter tool execution outcome in src/Melic.AgentFramework.Observability.Tools/Internal/ToolTelemetryAgent.cs
- [X] T024 [US1] Export public API from the intended namespace with XML docs matching contracts/tools-api.md in src/Melic.AgentFramework.Observability.Tools/ToolTelemetryAgentBuilderExtensions.cs

**Checkpoint**: User Story 1 is independently functional as the MVP.

---

## Phase 4: User Story 2 - Input and Output Capture (Priority: P2)

**Goal**: Capture tool input and output payloads as valid JSON span attributes with configurable size bounds and structured error output.

**Independent Test**: Invoke tools with typed parameters, structured return values, null results, oversized values, disabled capture, and thrown exceptions; verify `genai.tool.input` and `genai.tool.output` behavior without relying on retry detection.

### Tests for User Story 2

- [X] T025 [P] [US2] Add serializer tests for empty input `{}`, null output `null`, structured output JSON, and structured exception output `{ type, message }` in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolPayloadSerializerTests.cs
- [X] T026 [P] [US2] Add serializer tests proving oversized string values and nested payloads remain valid JSON within configured limits in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolPayloadSerializerTests.cs
- [X] T027 [P] [US2] Add integration test for input/output attributes on successful typed tool calls in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryPayloadTests.cs
- [X] T028 [P] [US2] Add integration test proving `CaptureInput=false` omits `genai.tool.input` while preserving execution in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryPayloadTests.cs
- [X] T029 [P] [US2] Add integration test proving `CaptureOutput=false` omits `genai.tool.output` while preserving execution in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryPayloadTests.cs
- [X] T030 [P] [US2] Add integration test for failing tool call structured JSON error output when output capture is enabled in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryPayloadTests.cs

### Implementation for User Story 2

- [X] T031 [US2] Implement `ToolPayloadSerializer` valid-JSON input serialization and length-bounded shrinking in src/Melic.AgentFramework.Observability.Tools/Internal/ToolPayloadSerializer.cs
- [X] T032 [US2] Implement `ToolPayloadSerializer` output serialization for success, null, and exception payloads in src/Melic.AgentFramework.Observability.Tools/Internal/ToolPayloadSerializer.cs
- [X] T033 [US2] Wire `CaptureInput`, `CaptureOutput`, `MaxInputLength`, and `MaxOutputLength` into span enrichment in src/Melic.AgentFramework.Observability.Tools/Internal/ToolTelemetryAgent.cs
- [X] T034 [US2] Add best-effort omission behavior for input/output serialization failures in src/Melic.AgentFramework.Observability.Tools/Internal/ToolTelemetryAgent.cs
- [X] T035 [US2] Update quickstart payload examples if implementation naming or behavior diverges from planned examples in specs/002-tool-call-enrichment/quickstart.md

**Checkpoint**: User Stories 1 and 2 work independently; payload capture can be adopted or disabled per option.

---

## Phase 5: User Story 3 - Retry Detection (Priority: P3)

**Goal**: Mark repeated tool call ids within the same parent invocation with retry attributes and attempt indexes while resetting between invocations.

**Independent Test**: Exercise repeated `call_id` values within one parent activity and across separate parent activities; verify retry tags appear only when a non-empty call id exists.

### Tests for User Story 3

- [X] T036 [P] [US3] Add unit tests for `InvocationAttemptRegistry` first, second, third, different-id, null-id, and different-parent Activity cases in tests/Melic.AgentFramework.Observability.Tools.Tests/InvocationAttemptRegistryTests.cs
- [X] T037 [P] [US3] Add concurrency test for simultaneous attempt recording on the same parent Activity and call id in tests/Melic.AgentFramework.Observability.Tools.Tests/InvocationAttemptRegistryTests.cs
- [X] T038 [P] [US3] Add integration test proving repeated tool call id emits `genai.tool.is_retry=true` and `genai.tool.attempt_index=2` on the second span in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryRetryTests.cs
- [X] T039 [P] [US3] Add integration test proving retry attributes are omitted when no call id is available in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryRetryTests.cs

### Implementation for User Story 3

- [X] T040 [US3] Complete `InvocationAttemptRegistry.Record()` with `ConditionalWeakTable<Activity, ConcurrentDictionary<string, int>>` semantics in src/Melic.AgentFramework.Observability.Tools/Internal/InvocationAttemptRegistry.cs
- [X] T041 [US3] Wire retry attempt data into `genai.tool.is_retry` and `genai.tool.attempt_index` attributes in src/Melic.AgentFramework.Observability.Tools/Internal/ToolTelemetryAgent.cs
- [X] T042 [US3] Ensure retry tracking is skipped without call id or parent Activity and does not affect span creation in src/Melic.AgentFramework.Observability.Tools/Internal/ToolTelemetryAgent.cs

**Checkpoint**: User Stories 1, 2, and 3 work independently; retry diagnostics are complete.

---

## Phase 6: User Story 4 - Opt-In Configuration (Priority: P4)

**Goal**: Developers can enable tool telemetry with one builder call, customize source and capture options, and combine it with Sessions without cross-package dependencies.

**Independent Test**: Build equivalent agents with and without `.UseToolTelemetry()` and with customized options; verify behavior and emitted spans while keeping tool results identical.

### Tests for User Story 4

- [X] T043 [P] [US4] Add integration test for default `.UseToolTelemetry()` activation with no options in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryConfigurationTests.cs
- [X] T044 [P] [US4] Add integration test for custom `ActivitySourceName` producing spans from the configured source in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryConfigurationTests.cs
- [X] T045 [P] [US4] Add integration test combining `.UseOpenTelemetry()`, `.UseSessionTelemetry()`, and `.UseToolTelemetry()` without compile-time Tools-to-Sessions dependency in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryConfigurationTests.cs
- [X] T046 [P] [US4] Add project dependency assertion ensuring Tools references Abstractions but not Sessions in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryConfigurationTests.cs

### Implementation for User Story 4

- [X] T047 [US4] Finalize `UseToolTelemetry()` option cloning/validation so invalid source names or non-positive max lengths fail at build configuration in src/Melic.AgentFramework.Observability.Tools/ToolTelemetryAgentBuilderExtensions.cs
- [X] T048 [US4] Ensure Tools project has no Sessions project or package reference in src/Melic.AgentFramework.Observability.Tools/Melic.AgentFramework.Observability.Tools.csproj
- [X] T049 [US4] Add package tags and description for Tools NuGet metadata in src/Melic.AgentFramework.Observability.Tools/Melic.AgentFramework.Observability.Tools.csproj

**Checkpoint**: All user stories are independently functional and package adoption is friction-light.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Repository integration, docs, release readiness, and final validation across all stories.

- [X] T050 [P] Add Tools package entry and source registration guidance to docs/features/tool-call-enrichment.md
- [X] T051 [P] Update root README package table and feature list with `Melic.AgentFramework.Observability.Tools` in README.md
- [X] T052 [P] Update NuGet shared README package table and documentation link for Tools in nuget/NUGET.md
- [X] T053 Add Tools package to release workflow pack comment/list if needed in .github/workflows/release.yml
- [X] T054 Add Tools-specific test filter note to common commands in .github/copilot-instructions.md
- [X] T055 Run `dotnet build -c Release` from repository root and fix any warnings/errors in src/Melic.AgentFramework.Observability.Tools/ and tests/Melic.AgentFramework.Observability.Tools.Tests/
- [X] T056 Run `dotnet test -c Release --filter "FullyQualifiedName~Tools"` from repository root and fix any test failures in tests/Melic.AgentFramework.Observability.Tools.Tests/
- [X] T057 Run `dotnet pack src/Melic.AgentFramework.Observability.Tools -c Release -o ./artifacts --no-build` and verify `.nupkg` and `.snupkg` outputs in artifacts/
- [X] T058 Validate quickstart examples against final public API in specs/002-tool-call-enrichment/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion - blocks all user stories.
- **User Story 1 (Phase 3)**: Depends on Foundational - MVP span visibility.
- **User Story 2 (Phase 4)**: Depends on Foundational and benefits from US1 span orchestration; payload serialization can start in parallel after Foundational.
- **User Story 3 (Phase 5)**: Depends on Foundational and US1 span orchestration; registry tests can start in parallel after Foundational.
- **User Story 4 (Phase 6)**: Depends on Setup and Foundational; configuration tests that require emitted spans depend on US1.
- **Polish (Phase 7)**: Depends on selected user stories being complete.

### User Story Dependencies

- **US1 Tool Execution Visibility**: MVP, no dependency on other stories after Foundation.
- **US2 Input and Output Capture**: Uses US1 span lifecycle but serializer work is independently testable.
- **US3 Retry Detection**: Uses US1 span lifecycle but registry work is independently testable.
- **US4 Opt-In Configuration**: Public activation story; can validate defaults after US1 and full options after US2.

### Within Each User Story

- Tests first, and they should fail before implementation.
- Internal helpers before agent wiring.
- Attribute constants before package references them.
- Core behavior before docs and release polish.

---

## Parallel Opportunities

- Setup placeholders T004-T006 can run in parallel after project files T001-T003 are planned.
- Foundational internals T009-T012 can run in parallel after T007-T008.
- US1 test tasks T015-T018 can be authored in parallel.
- US2 serializer tests T025-T026 and integration tests T027-T030 can be authored in parallel.
- US3 registry tests T036-T037 and integration tests T038-T039 can be authored in parallel.
- US4 tests T043-T046 can be authored in parallel once foundational APIs exist.
- Polish docs T050-T052 can run in parallel after public API stabilizes.

---

## Parallel Example: User Story 1

```text
Task: "T015 [P] [US1] Add integration test for successful single tool call child span, tool name, ActivityKind.Internal, status OK, and parent id in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryVisibilityTests.cs"
Task: "T016 [P] [US1] Add integration test for failing tool call Activity status ERROR and status description in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryVisibilityTests.cs"
Task: "T017 [P] [US1] Add integration test for three sequential tool calls producing three independent agent_tool_call spans in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryVisibilityTests.cs"
Task: "T018 [P] [US1] Add integration test proving no spans are emitted when .UseToolTelemetry() is not registered in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryVisibilityTests.cs"
```

## Parallel Example: User Story 2

```text
Task: "T025 [P] [US2] Add serializer tests for empty input {}, null output null, structured output JSON, and structured exception output { type, message } in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolPayloadSerializerTests.cs"
Task: "T026 [P] [US2] Add serializer tests proving oversized string values and nested payloads remain valid JSON within configured limits in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolPayloadSerializerTests.cs"
Task: "T027 [P] [US2] Add integration test for input/output attributes on successful typed tool calls in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryPayloadTests.cs"
```

## Parallel Example: User Story 3

```text
Task: "T036 [P] [US3] Add unit tests for InvocationAttemptRegistry first, second, third, different-id, null-id, and different-parent Activity cases in tests/Melic.AgentFramework.Observability.Tools.Tests/InvocationAttemptRegistryTests.cs"
Task: "T037 [P] [US3] Add concurrency test for simultaneous attempt recording on the same parent Activity and call id in tests/Melic.AgentFramework.Observability.Tools.Tests/InvocationAttemptRegistryTests.cs"
Task: "T038 [P] [US3] Add integration test proving repeated tool call id emits genai.tool.is_retry=true and genai.tool.attempt_index=2 on the second span in tests/Melic.AgentFramework.Observability.Tools.Tests/ToolTelemetryRetryTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational constants/options/mapping primitives.
3. Complete Phase 3: User Story 1.
4. Stop and validate: run `dotnet test -c Release --filter "FullyQualifiedName~ToolTelemetryVisibility"`.
5. Demo trace shape: `invoke_agent` parent span with `agent_tool_call` child spans.

### Incremental Delivery

1. Setup + Foundational -> Tools package compiles and can host internals.
2. US1 -> MVP trace visibility.
3. US2 -> payload diagnostics.
4. US3 -> retry awareness.
5. US4 -> polished opt-in configuration and package independence checks.
6. Polish -> docs, pack, full build/test.

### Parallel Team Strategy

With multiple developers:

1. Pair on Setup + Foundational to avoid project-file conflicts.
2. Developer A: US1 span lifecycle.
3. Developer B: US2 payload serializer and tests.
4. Developer C: US3 retry registry and tests.
5. Rejoin for US4 configuration and release polish.

---

## Independent Test Criteria Summary

- **US1**: Captured trace contains one `agent_tool_call` child span per tool call with correct name, call id behavior, `ActivityKind.Internal`, OK/ERROR status, and no spans when disabled.
- **US2**: Captured spans contain valid JSON input/output when enabled, omit them when disabled or serialization fails, represent errors as `{ type, message }`, and preserve tool results.
- **US3**: Repeated call ids within one parent invocation produce retry markers and attempt indexes; different ids, missing ids, and new parent invocations do not leak counts.
- **US4**: `.UseToolTelemetry()` works with defaults, custom source names are honored, Tools remains independent from Sessions, and combined package registration works.

---

## Notes

- [P] tasks operate on different files or independent tests and can be parallelized.
- [US1]-[US4] labels map directly to prioritized user stories in spec.md.
- Do not reference `gen_ai.*` for custom attributes; use `ToolAttributeNames` constants from Abstractions.
- Keep all source files SPDX-headed and every public member XML-documented.
- Avoid internal MAF types; the only tool interception surface is the public `FunctionInvocationContext` middleware described in research.md.
