# Tasks: End-to-End Sample and Preview Hardening

**Input**: Design documents from `/specs/004-end-to-end-sample-preview-hardening/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: The feature specification requires independent validation for each user story, but does not request TDD. Tasks include story-level validation commands and final repository validation rather than pre-implementation test tasks.

**Organization**: Tasks are grouped by user story to enable independent implementation and validation of the recommended end-to-end preview experience.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the new sample project and documentation entry points that all stories build on.

- [x] T001 Create the end-to-end sample project file with project references and package references in samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj
- [x] T002 Add the end-to-end sample project to the samples folder in agent-framework-observability.slnx
- [x] T003 [P] Create the initial sample README shell in samples/EndToEndTelemetry.Demo/README.md
- [x] T004 [P] Create the recommended preview setup guide shell in docs/features/recommended-preview-setup.md
- [x] T005 [P] Add the feature quickstart link and planned validation commands to specs/004-end-to-end-sample-preview-hardening/quickstart.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build shared sample infrastructure that must exist before any user story can be completed.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T006 Create Program.cs with configuration constants, environment variable loading, and banner/output helpers in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T007 Implement Azure OpenAI chat client creation using existing sample environment variables in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T008 Implement shared OpenTelemetry trace provider setup with MAF, Sessions, Tools, and demo ActivitySource registration in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T009 Implement shared demo tool models and deterministic tool functions for order lookup and support diagnostics in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T010 Add SPDX header and confirm sample code follows repository C# style in samples/EndToEndTelemetry.Demo/Program.cs

**Checkpoint**: The sample project exists, compiles structurally, and shared configuration/tooling is ready for story implementation.

---

## Phase 3: User Story 1 - Run Recommended End-to-End Sample (Priority: P1) MVP

**Goal**: A developer can run one realistic customer-support order lookup sample that composes Sessions, Tools, and Redaction with Console export by default.

**Independent Test**: Run the sample without `APPLICATIONINSIGHTS_CONNECTION_STRING`, complete a multi-turn session with a tool call, and verify console telemetry shows session correlation, tool enrichment, official sensitive telemetry when enabled, and redacted targeted values.

### Implementation for User Story 1

- [x] T011 [US1] Implement agent construction with UseOpenTelemetry, UseSessionTelemetry, and UseToolTelemetry in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T012 [US1] Configure MAF sensitive data capture and Redaction IncludeMafSensitiveDataAttributes in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T013 [US1] Add default Console exporter behavior when APPLICATIONINSIGHTS_CONNECTION_STRING is absent in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T014 [US1] Implement the multi-turn AgentSession scenario with a stable session id in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T015 [US1] Implement at least one required tool-call turn that emits genai.tool.input and genai.tool.output in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T016 [US1] Add sensitive example values to prompt, response, tool argument, or tool result paths so Redaction has visible work in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T017 [US1] Add sample console guidance describing expected spans and attributes in samples/EndToEndTelemetry.Demo/README.md
- [x] T018 [US1] Document local prerequisites and required Azure OpenAI environment variables in samples/EndToEndTelemetry.Demo/README.md
- [x] T019 [US1] Validate the local sample build with `dotnet build samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj -c Release` for samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj
- [x] T020 [US1] Validate the local sample run path and record under-10-minute timing from specs/004-end-to-end-sample-preview-hardening/quickstart.md

**Checkpoint**: User Story 1 is independently functional as the MVP and the local Console path demonstrates all three packages together.

---

## Phase 4: User Story 2 - Inspect The Same Sample In Application Insights (Priority: P2)

**Goal**: The same end-to-end sample exports to Application Insights when configured, with clear backend inspection guidance.

**Independent Test**: Set `APPLICATIONINSIGHTS_CONNECTION_STRING`, run the same sample, and use documentation to inspect expected spans and custom dimensions in Application Insights.

### Implementation for User Story 2

- [x] T021 [US2] Add Azure Monitor trace exporter selection when APPLICATIONINSIGHTS_CONNECTION_STRING is set in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T022 [US2] Add ForceFlush and ingestion-delay messaging for Application Insights runs in samples/EndToEndTelemetry.Demo/Program.cs
- [x] T023 [US2] Ensure Application Insights exporter dependency remains sample-only in samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj
- [x] T024 [US2] Add Application Insights run instructions and connection string setup in samples/EndToEndTelemetry.Demo/README.md
- [x] T025 [US2] Add App Insights inspection guidance with span filters and at least five dimensions in samples/EndToEndTelemetry.Demo/README.md
- [x] T026 [US2] Add the same App Insights inspection guidance to docs/features/recommended-preview-setup.md
- [x] T027 [US2] Validate the App Insights configuration path from specs/004-end-to-end-sample-preview-hardening/quickstart.md

**Checkpoint**: User Story 2 works independently on top of the MVP; changing one environment variable switches the same sample to Application Insights.

---

## Phase 5: User Story 3 - Follow A Recommended Setup Guide (Priority: P3)

**Goal**: A developer can follow one guide that explains package roles, registration points, safe redaction choices, and runtime boundaries.

**Independent Test**: Read the guide from a clean context and verify it identifies which package handles sessions, tools, and export-boundary redaction, plus where each package is registered.

### Implementation for User Story 3

- [x] T028 [US3] Document Sessions, Tools, and Redaction roles in docs/features/recommended-preview-setup.md
- [x] T029 [US3] Add recommended agent builder registration sample for UseOpenTelemetry, UseSessionTelemetry, and UseToolTelemetry in docs/features/recommended-preview-setup.md
- [x] T030 [US3] Add recommended telemetry pipeline registration sample for AddTelemetryRedaction before exporters in docs/features/recommended-preview-setup.md
- [x] T031 [US3] Add explicit MAF EnableSensitiveData plus IncludeMafSensitiveDataAttributes guidance in docs/features/recommended-preview-setup.md
- [x] T032 [US3] Add Redaction runtime boundary wording to docs/features/recommended-preview-setup.md
- [x] T033 [P] [US3] Add See Also links to the recommended setup guide in docs/features/session-identity-enrichment.md
- [x] T034 [P] [US3] Add See Also links to the recommended setup guide in docs/features/tool-call-enrichment.md
- [x] T035 [P] [US3] Add See Also links and boundary wording alignment in docs/features/redaction-pipeline.md
- [x] T036 [US3] Update the package comparison and recommended composition guidance in docs/design/technical-reference.md
- [x] T037 [US3] Update root quick start and sample links for the end-to-end preview path in README.md
- [x] T038 [US3] Update package overview/readme content for all three preview packages and confirm nuget/NUGET.md is the package README source of truth for Sessions and Tools in nuget/NUGET.md

**Checkpoint**: User Story 3 is independently reviewable through documentation and accurately explains composition without implying runtime redaction.

---

## Phase 6: User Story 4 - Validate Clean Preview Consumption (Priority: P4)

**Goal**: Maintainers can validate published preview packages from a fresh consumer project and confirm package metadata/boundaries are consistent.

**Independent Test**: Create a fresh consumer project, install published preview packages, compile a minimal recommended setup, and inspect dependencies so Redaction remains independent.

### Implementation for User Story 4

- [x] T039 [US4] Add clean consumer validation steps using published package references in docs/features/recommended-preview-setup.md
- [x] T040 [US4] Add a package boundary matrix for library packages and samples in docs/features/recommended-preview-setup.md
- [x] T041 [US4] Add Redaction dependency boundary validation commands in docs/features/recommended-preview-setup.md
- [x] T042 [US4] Update Redaction package README boundary wording in src/Melic.AgentFramework.Observability.Redaction/README.md
- [x] T043 [US4] Update Sessions feature documentation links for clean preview consumption and package README source-of-truth clarity in docs/features/session-identity-enrichment.md
- [x] T044 [US4] Update Tools feature documentation links for clean preview consumption and package README source-of-truth clarity in docs/features/tool-call-enrichment.md
- [x] T045 [US4] Review and update if needed package metadata descriptions and tags for Sessions in src/Melic.AgentFramework.Observability.Sessions/Melic.AgentFramework.Observability.Sessions.csproj
- [x] T046 [US4] Review and update if needed package metadata descriptions and tags for Tools in src/Melic.AgentFramework.Observability.Tools/Melic.AgentFramework.Observability.Tools.csproj
- [x] T047 [US4] Review and update if needed package metadata descriptions and tags for Redaction in src/Melic.AgentFramework.Observability.Redaction/Melic.AgentFramework.Observability.Redaction.csproj
- [x] T048 [US4] Run Redaction project reference inspection and record expected result in specs/004-end-to-end-sample-preview-hardening/quickstart.md
- [x] T049 [US4] Run clean consumer restore/build validation and record expected result in specs/004-end-to-end-sample-preview-hardening/quickstart.md
- [x] T050 [US4] Update release notes for the end-to-end sample and preview hardening in CHANGELOG.md

**Checkpoint**: User Story 4 confirms clean package consumption and validates that package boundaries match the preview story.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency pass, repository validation, and readiness checks across all user stories.

- [x] T051 [P] Verify all touched documentation consistently describes Redaction as export-boundary telemetry redaction in README.md
- [x] T052 [P] Verify all touched documentation consistently describes Redaction as export-boundary telemetry redaction in docs/features/recommended-preview-setup.md
- [x] T053 [P] Verify all touched documentation consistently describes Redaction as export-boundary telemetry redaction in samples/EndToEndTelemetry.Demo/README.md
- [x] T054 Run full repository build with `dotnet build` from agent-framework-observability.slnx
- [x] T055 Run full repository tests with `dotnet test` from agent-framework-observability.slnx
- [x] T056 Run final sample build validation with `dotnet build samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj -c Release` for samples/EndToEndTelemetry.Demo/EndToEndTelemetry.Demo.csproj
- [x] T057 Run final quickstart validation steps from specs/004-end-to-end-sample-preview-hardening/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion; blocks all user stories.
- **User Stories (Phase 3+)**: Depend on Foundational completion.
- **Polish (Phase 7)**: Depends on selected user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational; no dependency on other user stories; MVP.
- **User Story 2 (P2)**: Starts after Foundational and works best after US1 because it reuses the same sample scenario.
- **User Story 3 (P3)**: Starts after Foundational; can proceed in parallel with US1/US2 documentation once sample registration shape is stable.
- **User Story 4 (P4)**: Starts after Foundational; can proceed in parallel with US3 after docs paths are established, but final validation should run after US1/US2 sample work.

### Within Each User Story

- Sample project wiring precedes sample behavior.
- Shared configuration and tools precede story scenarios.
- Documentation guidance follows the code path it describes.
- Validation commands run after the relevant sample or documentation path exists.

### Parallel Opportunities

- T003, T004, and T005 can run in parallel after T001/T002 ownership is clear.
- T033, T034, and T035 can run in parallel because they touch separate feature docs.
- T045, T046, and T047 can run in parallel because they inspect separate package projects.
- T051, T052, and T053 can run in parallel because they verify separate docs.
- US3 documentation work can proceed in parallel with US2 exporter guidance once the sample registration shape is known.

---

## Parallel Example: User Story 3

```text
Task: "T033 [P] [US3] Add See Also links to the recommended setup guide in docs/features/session-identity-enrichment.md"
Task: "T034 [P] [US3] Add See Also links to the recommended setup guide in docs/features/tool-call-enrichment.md"
Task: "T035 [P] [US3] Add See Also links and boundary wording alignment in docs/features/redaction-pipeline.md"
```

## Parallel Example: User Story 4

```text
Task: "T045 [US4] Review package metadata descriptions and tags for Sessions in src/Melic.AgentFramework.Observability.Sessions/Melic.AgentFramework.Observability.Sessions.csproj"
Task: "T046 [US4] Review package metadata descriptions and tags for Tools in src/Melic.AgentFramework.Observability.Tools/Melic.AgentFramework.Observability.Tools.csproj"
Task: "T047 [US4] Review package metadata descriptions and tags for Redaction in src/Melic.AgentFramework.Observability.Redaction/Melic.AgentFramework.Observability.Redaction.csproj"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 setup.
2. Complete Phase 2 foundational sample infrastructure.
3. Complete Phase 3 User Story 1.
4. Stop and validate the local Console sample path from [quickstart.md](quickstart.md).

### Incremental Delivery

1. Add User Story 1 to prove the packages compose locally.
2. Add User Story 2 to validate the same sample in Application Insights.
3. Add User Story 3 to make the recommended setup understandable and repeatable.
4. Add User Story 4 to validate public preview consumption and package boundaries.
5. Run Phase 7 final validation before considering the feature ready.

### Parallel Team Strategy

1. One developer owns `samples/EndToEndTelemetry.Demo/Program.cs` through US1 and US2 to avoid code conflicts.
2. Another developer owns docs/features guidance for US3.
3. A maintainer owns package metadata and clean consumer validation for US4.
4. Final validation is serialized in Phase 7.

---

## Format Validation

- All implementation tasks use `- [ ] T###` checklist format.
- All user story tasks include `[US1]`, `[US2]`, `[US3]`, or `[US4]` labels.
- All parallelizable tasks use `[P]` only when they touch separate files and have no dependency on incomplete tasks.
- Every task description includes at least one concrete file path.
