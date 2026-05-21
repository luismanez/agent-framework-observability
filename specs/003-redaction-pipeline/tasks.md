# Tasks: Redaction Pipeline

**Input**: Design documents from `/specs/003-redaction-pipeline/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: Tests are included because the specification explicitly requires validation coverage for default rules, custom rules, structured payloads, opted-in standard attributes, idempotency, long values, failure behavior, non-target attributes, and package independence.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Every task includes exact file paths

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the package, test project, sample project placeholders, and solution wiring.

- [x] T001 Create Redaction package project in src/Melic.AgentFramework.Observability.Redaction/Melic.AgentFramework.Observability.Redaction.csproj
- [x] T002 Create Redaction test project in tests/Melic.AgentFramework.Observability.Redaction.Tests/Melic.AgentFramework.Observability.Redaction.Tests.csproj
- [x] T003 [P] Create Redaction sample project in samples/RedactionTelemetry.Demo/RedactionTelemetry.Demo.csproj
- [x] T004 Update solution entries for Redaction source, tests, and sample in agent-framework-observability.slnx
- [x] T005 [P] Create Redaction package folder documentation placeholder in src/Melic.AgentFramework.Observability.Redaction/README.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core contracts and infrastructure that MUST be complete before any user story can be implemented.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T006 Add redaction diagnostic attribute constants in src/Melic.AgentFramework.Observability.Abstractions/RedactionAttributeNames.cs
- [x] T007 [P] Define public failure mode enum in src/Melic.AgentFramework.Observability.Redaction/RedactionFailureMode.cs
- [x] T008 Define public RedactionOptions with defaults and XML documentation in src/Melic.AgentFramework.Observability.Redaction/RedactionOptions.cs
- [x] T009 [P] Create internal RedactionResult value type in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionResult.cs
- [x] T010 [P] Create internal RedactionRule model in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionRule.cs
- [x] T011 Create immutable RedactionPolicy factory and option validation in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionPolicy.cs
- [x] T012 Create attribute target matcher skeleton in src/Melic.AgentFramework.Observability.Redaction/Internal/AttributeTargetMatcher.cs
- [x] T013 Create OpenTelemetry processor skeleton in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionProcessor.cs
- [x] T014 Create public TracerProviderBuilder registration extension skeleton in src/Melic.AgentFramework.Observability.Redaction/RedactionTracerProviderBuilderExtensions.cs
- [x] T015 [P] Create Activity capture test helper in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionTestExporter.cs

**Checkpoint**: Package compiles structurally and user story implementation can start.

---

## Phase 3: User Story 1 - Safe Telemetry by Default (Priority: P1) MVP

**Goal**: Default Redaction protects high-risk package-owned tool payload attributes before export while preserving safe telemetry values.

**Independent Test**: Enable default redaction, export Activity telemetry containing sensitive values in `genai.tool.input` and `genai.tool.output`, and verify sensitive values are replaced while non-targeted and non-sensitive values remain unchanged.

### Tests for User Story 1

- [x] T016 [P] [US1] Add default sensitive value redaction integration tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionDefaultsTests.cs
- [x] T017 [P] [US1] Add non-target attribute preservation tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionProcessorTests.cs
- [x] T018 [P] [US1] Add idempotency tests for already-redacted values in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionIdempotencyTests.cs

### Implementation for User Story 1

- [x] T019 [US1] Implement default exact targets for `genai.tool.input` and `genai.tool.output` in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionPolicy.cs
- [x] T020 [US1] Implement built-in pattern rules for email, phone, bearer token, API-token-like fragments, connection-string secrets, and password-like fragments in src/Melic.AgentFramework.Observability.Redaction/Internal/PatternRedactionRule.cs
- [x] T021 [US1] Implement raw string redaction pipeline and idempotent replacement handling in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionEngine.cs
- [x] T022 [US1] Implement Activity tag enumeration and targeted string attribute replacement in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionProcessor.cs
- [x] T023 [US1] Complete AddTelemetryRedaction registration with validated cloned options in src/Melic.AgentFramework.Observability.Redaction/RedactionTracerProviderBuilderExtensions.cs
- [x] T024 [US1] Run focused US1 tests with `dotnet test --filter "FullyQualifiedName~RedactionDefaults|FullyQualifiedName~RedactionProcessor|FullyQualifiedName~RedactionIdempotency"`

**Checkpoint**: User Story 1 is fully functional and independently testable as the MVP.

---

## Phase 4: User Story 2 - Preserve Structured Observability Value (Priority: P2)

**Goal**: Redaction preserves useful JSON payload structure while redacting sensitive fields and keeping processing bounded.

**Independent Test**: Export telemetry with JSON-looking payloads containing nested sensitive fields, malformed JSON, and long values; verify structured values are redacted, malformed values fall back safely, and long values use documented bounded behavior.

### Tests for User Story 2

- [x] T025 [P] [US2] Add valid JSON payload redaction tests for the acceptance corpus (flat object, nested object, array, array of objects, nulls, numbers, booleans, sensitive strings, non-sensitive strings) in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionJsonPayloadTests.cs
- [x] T026 [P] [US2] Add malformed JSON fallback tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionMalformedJsonTests.cs
- [x] T027 [P] [US2] Add max value length behavior tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionLengthLimitTests.cs

### Implementation for User Story 2

- [x] T028 [US2] Implement JSON object and array detection in src/Melic.AgentFramework.Observability.Redaction/Internal/JsonPayloadRedactor.cs
- [x] T029 [US2] Implement recursive JSON property value redaction for sensitive field names in src/Melic.AgentFramework.Observability.Redaction/Internal/JsonPayloadRedactor.cs
- [x] T030 [US2] Integrate JSON redaction before raw string fallback in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionEngine.cs
- [x] T031 [US2] Implement malformed JSON fallback to string redaction in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionEngine.cs
- [x] T032 [US2] Implement MaxValueLength handling and whole-value replacement for over-limit targeted values in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionEngine.cs
- [x] T033 [US2] Run focused US2 tests with `dotnet test --filter "FullyQualifiedName~RedactionJsonPayload|FullyQualifiedName~RedactionMalformedJson|FullyQualifiedName~RedactionLengthLimit"`

**Checkpoint**: User Story 2 works independently on top of the MVP and preserves structured observability value.

---

## Phase 5: User Story 3 - Configure Redaction Scope and Rules (Priority: P3)

**Goal**: Consumers can configure custom rules, target scope, selected standard attributes, and exception message redaction without custom telemetry plumbing.

**Independent Test**: Configure custom pattern and field-name rules, include and exclude attributes, opt selected `gen_ai.*` attributes into processing, and enable exception message redaction; verify only selected values are redacted.

### Tests for User Story 3

- [x] T034 [P] [US3] Add custom pattern and field-name rule tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionConfigurationTests.cs
- [x] T035 [P] [US3] Add attribute include/exclude targeting tests, including explicit `genai.session.` prefix targeting for session custom/user-provided values and unchanged built-in session attributes by default, in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionTargetingTests.cs
- [x] T036 [P] [US3] Add opt-in standard attribute tests for selected `gen_ai.*` attributes in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionStandardAttributeTests.cs
- [x] T037 [P] [US3] Add exception message opt-in redaction tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionExceptionMessageTests.cs
- [x] T038 [P] [US3] Add invalid options validation tests and `EnableDefaultRules = false` behavior tests showing built-in rules are disabled while custom rules still run in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionOptionsValidationTests.cs

### Implementation for User Story 3

- [x] T039 [US3] Implement IncludeAttribute, IncludeAttributesWithPrefix, ExcludeAttribute, IncludeStandardAttribute, and IncludeStandardAttributesWithPrefix in src/Melic.AgentFramework.Observability.Redaction/RedactionOptions.cs
- [x] T040 [US3] Implement AddSensitiveFieldName and AddPatternRule in src/Melic.AgentFramework.Observability.Redaction/RedactionOptions.cs
- [x] T041 [US3] Implement exact, prefix, exclusion, and standard opt-in matching in src/Melic.AgentFramework.Observability.Redaction/Internal/AttributeTargetMatcher.cs
- [x] T042 [US3] Implement default-rule enable/disable behavior, custom rule normalization, duplicate detection, and regex validation in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionPolicy.cs
- [x] T043 [US3] Implement RedactExceptionMessages targeting for exception and error message attributes in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionPolicy.cs
- [x] T044 [US3] Run focused US3 tests with `dotnet test --filter "FullyQualifiedName~RedactionConfiguration|FullyQualifiedName~RedactionTargeting|FullyQualifiedName~RedactionStandardAttribute|FullyQualifiedName~RedactionExceptionMessage|FullyQualifiedName~RedactionOptionsValidation"`

**Checkpoint**: User Story 3 is independently testable and supports domain-specific policy needs.

---

## Phase 6: User Story 4 - Operate Reliably and Transparently (Priority: P4)

**Goal**: Redaction remains best-effort, fails safely, and can expose aggregate non-sensitive diagnostics when enabled.

**Independent Test**: Force processing failures, enable diagnostics, and disable effective redaction; verify export continues, sensitive content is not leaked under default failure behavior, diagnostics are aggregate only, and disabled/no-rule configuration does not transform telemetry.

### Tests for User Story 4

- [x] T045 [P] [US4] Add fail-closed and preserve-original failure mode tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionFailureModeTests.cs
- [x] T046 [P] [US4] Add aggregate diagnostics tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionDiagnosticsTests.cs
- [x] T047 [P] [US4] Add no-rules/no-effective-redaction tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionDisabledBehaviorTests.cs
- [x] T048 [P] [US4] Add package independence tests verifying no Sessions, Tools, Azure, Application Insights, or exporter-specific references in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionPackageIndependenceTests.cs

### Implementation for User Story 4

- [x] T049 [US4] Implement ReplaceValue and PreserveOriginal failure behavior in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionEngine.cs
- [x] T050 [US4] Implement processor-level catch boundaries that never interrupt export in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionProcessor.cs
- [x] T051 [US4] Emit optional aggregate diagnostics with RedactionAttributeNames in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionProcessor.cs
- [x] T052 [US4] Ensure diagnostics never include rule names, matched values, original fragments, field values, or payload snippets in src/Melic.AgentFramework.Observability.Redaction/Internal/RedactionProcessor.cs
- [x] T053 [US4] Run focused US4 tests with `dotnet test --filter "FullyQualifiedName~RedactionFailureMode|FullyQualifiedName~RedactionDiagnostics|FullyQualifiedName~RedactionDisabledBehavior|FullyQualifiedName~RedactionPackageIndependence"`

**Checkpoint**: User Story 4 is independently testable and confirms operational safety.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, sample, packaging, and whole-repo validation.

- [x] T054 [P] Create Redaction sample program composing Sessions, Tools, and Redaction in samples/RedactionTelemetry.Demo/Program.cs
- [x] T055 [P] Create Redaction sample README with setup, expected output, and App Insights/console inspection notes in samples/RedactionTelemetry.Demo/README.md
- [x] T056 Update solution sample entry for RedactionTelemetry.Demo if not already complete in agent-framework-observability.slnx
- [x] T057 [P] Update root package overview and quick start in README.md
- [x] T058 [P] Update technical reference Redaction section and package comparison table in docs/design/technical-reference.md
- [x] T059 [P] Add consumer feature documentation in docs/features/redaction-pipeline.md
- [x] T060 [P] Update release notes for Redaction in CHANGELOG.md
- [x] T061 Review package metadata description and tags for Redaction in src/Melic.AgentFramework.Observability.Redaction/Melic.AgentFramework.Observability.Redaction.csproj
- [x] T062 Run Redaction-focused validation with `dotnet test --filter "FullyQualifiedName~Redaction"`
- [x] T063 Run sample build validation with `dotnet build samples/RedactionTelemetry.Demo/RedactionTelemetry.Demo.csproj -c Release`
- [x] T064 Run full repository validation with `dotnet build` and `dotnet test`
- [x] T065 Validate quickstart commands and examples in specs/003-redaction-pipeline/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion; blocks all user stories.
- **User Stories (Phase 3+)**: Depend on Foundational completion.
- **Polish (Phase 7)**: Depends on all selected user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Starts after Foundational; no dependency on other stories; MVP.
- **User Story 2 (P2)**: Starts after Foundational but integrates most cleanly after US1 because it extends the redaction engine.
- **User Story 3 (P3)**: Starts after Foundational and can proceed in parallel with US2 after US1 target/rule basics are stable.
- **User Story 4 (P4)**: Starts after Foundational and can proceed once the processor and engine exist; diagnostics depend on Abstractions constants.

### Within Each User Story

- Tests should be written first and fail before implementation.
- Policy and target matching precede processor behavior.
- Engine/rule behavior precedes processor integration.
- Processor integration precedes sample and documentation validation.

---

## Parallel Opportunities

- T003 and T005 can run in parallel with T001/T002 after directory intent is clear.
- T007, T009, T010, and T015 can run in parallel after project files exist.
- US1 tests T016, T017, and T018 can be written in parallel because they use separate files.
- US2 tests T025, T026, and T027 can be written in parallel because they use separate files.
- US3 tests T034 through T038 can be written in parallel because they use separate files.
- US4 tests T045 through T048 can be written in parallel because they use separate files.
- Polish documentation tasks T057 through T060 can run in parallel after APIs stabilize.

---

## Parallel Example: User Story 1

```text
Task: "T016 [P] [US1] Add default sensitive value redaction integration tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionDefaultsTests.cs"
Task: "T017 [P] [US1] Add non-target attribute preservation tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionProcessorTests.cs"
Task: "T018 [P] [US1] Add idempotency tests for already-redacted values in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionIdempotencyTests.cs"
```

## Parallel Example: User Story 2

```text
Task: "T025 [P] [US2] Add valid JSON payload redaction tests for the acceptance corpus (flat object, nested object, array, array of objects, nulls, numbers, booleans, sensitive strings, non-sensitive strings) in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionJsonPayloadTests.cs"
Task: "T026 [P] [US2] Add malformed JSON fallback tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionMalformedJsonTests.cs"
Task: "T027 [P] [US2] Add max value length behavior tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionLengthLimitTests.cs"
```

## Parallel Example: User Story 3

```text
Task: "T034 [P] [US3] Add custom pattern and field-name rule tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionConfigurationTests.cs"
Task: "T035 [P] [US3] Add attribute include/exclude targeting tests, including explicit `genai.session.` prefix targeting for session custom/user-provided values and unchanged built-in session attributes by default, in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionTargetingTests.cs"
Task: "T036 [P] [US3] Add opt-in standard attribute tests for selected `gen_ai.*` attributes in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionStandardAttributeTests.cs"
Task: "T037 [P] [US3] Add exception message opt-in redaction tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionExceptionMessageTests.cs"
Task: "T038 [P] [US3] Add invalid options validation tests and `EnableDefaultRules = false` behavior tests showing built-in rules are disabled while custom rules still run in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionOptionsValidationTests.cs"
```

## Parallel Example: User Story 4

```text
Task: "T045 [P] [US4] Add fail-closed and preserve-original failure mode tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionFailureModeTests.cs"
Task: "T046 [P] [US4] Add aggregate diagnostics tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionDiagnosticsTests.cs"
Task: "T047 [P] [US4] Add no-rules/no-effective-redaction tests in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionDisabledBehaviorTests.cs"
Task: "T048 [P] [US4] Add package independence tests verifying no Sessions, Tools, Azure, Application Insights, or exporter-specific references in tests/Melic.AgentFramework.Observability.Redaction.Tests/RedactionPackageIndependenceTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational contracts and processor skeleton.
3. Complete Phase 3: User Story 1 default safe telemetry behavior.
4. Stop and validate with the US1 focused tests.
5. Demo default redaction on `genai.tool.input` and `genai.tool.output` before expanding structured/configurable behavior.

### Incremental Delivery

1. Complete Setup + Foundational to establish the package and public contract.
2. Add US1 to deliver default protection for high-risk tool payloads.
3. Add US2 to preserve JSON structure and bounded long-value behavior.
4. Add US3 to support consumer-specific policy and standard attribute opt-in.
5. Add US4 to harden failure behavior and diagnostics.
6. Complete sample, docs, package metadata, and full validation.

### Parallel Team Strategy

1. One developer creates project/solution scaffolding while another prepares test helper infrastructure.
2. After Foundational is complete, one developer implements US1 while others prepare US2/US3/US4 tests in separate files.
3. Documentation and sample work begin after the public API stabilizes.

---

## Notes

- [P] tasks use separate files and can be worked independently once their phase prerequisites are met.
- [US1] is the MVP and should be completed before expanding to structured JSON or custom policies.
- Keep all new public members XML-documented and every `.cs` file headed with `// SPDX-License-Identifier: MIT`.
- Do not add dependencies on Sessions or Tools from the Redaction package.
- Do not use `gen_ai.*` for package-owned attributes; use `genai.redaction.*` constants from Abstractions.
- Validate tests fail before implementing each story's behavior.
