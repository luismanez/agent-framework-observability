# Tasks: Session Identity and Enrichment

**Feature Branch**: `001-session-identity-enrichment`
**Input**: plan.md · spec.md · research.md · data-model.md · contracts/sessions-api.md · quickstart.md

---

## Format: `- [ ] [ID] [P?] [Story?] Description — file path`

- **[P]**: parallelizable (different files, no in-flight dependency)
- **[US1–US4]**: user story label (maps to spec.md priorities P1–P4)
- No story label → Setup or Foundational phase

---

## Phase 1: Setup

**Purpose**: Repo scaffold — build infrastructure, solution, project files. No source code yet.

- [ ] T001 Create `.gitignore` for .NET (bin/, obj/, *.user, .vs/, TestResults/) at repo root
- [ ] T002 Create `.editorconfig` enforcing UTF-8, LF, 4-space indent, `dotnet_diagnostic.CS1591.severity = error` at repo root
- [ ] T003 [P] Create `eng/Directory.Build.props` — `<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>`, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>latest</LangVersion>`, SPDX header enforcement note
- [ ] T004 [P] Create `eng/Directory.Build.targets` — MinVer `<PackageReference Include="MinVer" Version="*" PrivateAssets="all"/>`, `<MinVerDefaultPreReleaseIdentifiers>preview</MinVerDefaultPreReleaseIdentifiers>`
- [ ] T005 [P] Create `eng/Directory.Packages.props` — centralized versions for `Microsoft.Agents.AI`, `Microsoft.Extensions.AI`, `OpenTelemetry`, `System.Diagnostics.DiagnosticSource`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`
- [ ] T006 Create `agent-framework-observability.slnx` solution file referencing all csproj files in src/ and tests/
- [ ] T007 [P] Create `src/Melic.AgentFramework.Observability.Abstractions/Melic.AgentFramework.Observability.Abstractions.csproj` — `<PackageId>`, `<Description>`, MIT license metadata, no dependencies beyond BCL
- [ ] T008 [P] Create `src/Melic.AgentFramework.Observability.Sessions/Melic.AgentFramework.Observability.Sessions.csproj` + `tests/Melic.AgentFramework.Observability.Sessions.Tests/Melic.AgentFramework.Observability.Sessions.Tests.csproj` — Sessions refs Abstractions + MAF + OTel; Tests refs Sessions + xUnit

**Checkpoint**: `dotnet build` on the empty solution succeeds for all three TFMs.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types that ALL user story phases depend on. Must be complete before US1–US4.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T009 [P] Create `src/Melic.AgentFramework.Observability.Abstractions/SessionAttributeNames.cs` — `public static class SessionAttributeNames` with all `public static readonly string` constants: `SessionId="genai.session.id"`, `InvocationIndex`, `FirstSeen`, `AgeSeconds`, `TotalInputTokens`, `TotalOutputTokens`, `MessageCount`, `TotalInvocations`, `DurationSeconds`, `Errors`, `StartTime`; XML doc on each field; file-scoped namespace; SPDX header
- [ ] T010 [P] Create `src/Melic.AgentFramework.Observability.Sessions/Internal/SessionStateBlock.cs` — `internal sealed record` with fields: `SessionId string?`, `InvocationIndex int`, `TotalInputTokens long`, `TotalOutputTokens long`, `FirstSeenUtc DateTimeOffset`, `Tags IReadOnlyDictionary<string,string>?`, `ExtensionData Dictionary<string,JsonElement>?`; `[JsonPropertyName]` on each field; `[JsonIgnore(Condition=WhenWritingDefault)]` on `FirstSeenUtc`; `[JsonIgnore(Condition=WhenWritingNull)]` on `Tags`; `[JsonExtensionData]` on `ExtensionData`; SPDX header
- [ ] T011 Create `src/Melic.AgentFramework.Observability.Sessions/Internal/SessionStateBagAccessor.cs` — `internal static class` with methods: `GetOrInitialise(AgentSessionStateBag, string key)` (thread-safe via `lock`, returns `SessionStateBlock`), `TryPersist(AgentSessionStateBag, string key, SessionStateBlock)` (returns bool, swallows exceptions), `TryRead(AgentSessionStateBag, string key, out SessionStateBlock?)` (depends on T010)
- [ ] T012 [P] Create `src/Melic.AgentFramework.Observability.Sessions/SessionTelemetryOptions.cs` — `public sealed class` with properties: `TrackTokenAggregates=true`, `EnableSessionSpan=false`, `StateBagKey="__melic_telemetry"`, `ActivitySourceName`, `MeterName`; XML docs on all; SPDX header
- [ ] T013 [P] Create `tests/Melic.AgentFramework.Observability.Sessions.Tests/Helpers/StubAIAgent.cs` — concrete `AIAgent` subclass; configurable `AgentResponse` return with `UsageDetails`; delegates `CreateSessionAsync` to return a real `AgentSession`-derived instance; supports serialize/deserialize round-trip via MAF default implementation
- [ ] T014 [P] Create `tests/Melic.AgentFramework.Observability.Sessions.Tests/Helpers/ActivityRecorder.cs` — sets up `ActivityListener` subscribed to a given source name; records all started/stopped `Activity` objects; exposes `IReadOnlyList<Activity> Completed` for assertions; implements `IDisposable`
- [ ] T015 [P] Create `src/Melic.AgentFramework.Observability.Sessions/SessionTelemetryOptions.cs` metrics instruments scaffold — add `internal` factory method `CreateInstruments(string meterName)` returning a record with `Counter<long> Invocations`, `Histogram<long> Duration`, `UpDownCounter<long> Active`, `Counter<long> StateBagWriteFailures`; instrument names: `genai.session.invocations`, `genai.session.duration`, `genai.session.active`, `session.statebag.write.failures`

**Checkpoint**: `dotnet build` clean; `SessionStateBlock` round-trips correctly via STJ.

---

## Phase 3: User Story 1 — Stable Session Identity (Priority: P1) 🎯 MVP

**Goal**: Every `invoke_agent` span carries a stable `genai.session.id` that survives serialization. Covers FR-001, FR-002, FR-003, FR-008, FR-009, FR-017, FR-018, FR-021.

**Independent Test**: Configure session telemetry on `StubAIAgent`; run three invocations with serialize+restore between each; verify all spans share the same `genai.session.id` value.

- [ ] T016 [US1] Create `src/Melic.AgentFramework.Observability.Sessions/Internal/SessionTelemetryAgent.cs` — `internal sealed class SessionTelemetryAgent : DelegatingAIAgent`; constructor accepts `AIAgent inner, SessionTelemetryOptions options`; `RunCoreAsync` override: (1) call `SessionStateBagAccessor.GetOrInitialise`, (2) assign `SessionId` via `Guid.NewGuid().ToString()` if null, (3) call `TryPersist`, (4) delegate to `InnerAgent.RunAsync`, (5) set `Activity.Current?.SetTag(SessionAttributeNames.SessionId, block.SessionId)` after delegation; wrap all telemetry logic in `try/catch` — agent response MUST be returned even on exception (FR-017)
- [ ] T017 [P] [US1] Create `src/Melic.AgentFramework.Observability.Sessions/SessionTelemetryExtensions.cs` — `public static class` with `GetOrAssignSessionId(this AgentSession)` calling `SessionStateBagAccessor.GetOrInitialise` + lock; `AssignSessionId(this AgentSession, string sessionId)` writing only if `SessionId` is currently null; XML docs on both; argument validation (`ArgumentException` on null/whitespace for `AssignSessionId`); SPDX header
- [ ] T018 [P] [US1] Create `src/Melic.AgentFramework.Observability.Sessions/SessionTelemetryAgentBuilderExtensions.cs` — `public static class`; `UseSessionTelemetry(this AIAgentBuilder, Action<SessionTelemetryOptions>?)` calling `builder.Use(inner => new SessionTelemetryAgent(inner, options))`; XML doc; SPDX header
- [ ] T019 [P] [US1] Create `tests/.../SessionIdentityTests.cs` — integration tests using `ActivityRecorder` + `StubAIAgent`; cover: auto-assign on first run (FR-001), same id on subsequent runs (FR-002/003), custom id via `AssignSessionId` (FR-008), concurrent assignment idempotency via `Parallel.For` (FR-018), `genai.session.id` present on span (FR-003), `UseSessionTelemetry` single-call builder (FR-009)
- [ ] T020 [P] [US1] Create `tests/.../SessionSchemaTests.cs` — unit tests for `SessionStateBlock` STJ round-trip; cover: missing fields default-init on deserialize (FR-021), unknown JSON properties preserved in `ExtensionData` (FR-021), `InvocationIndex` and `TotalInputTokens` survive full `SerializeSessionAsync`/`DeserializeSessionAsync` cycle

**Checkpoint**: All `SessionIdentityTests` and `SessionSchemaTests` pass. US1 is fully functional and independently testable.

---

## Phase 4: User Story 2 — Session Aggregate Enrichment (Priority: P2)

**Goal**: Every span carries `genai.session.invocation_index`, `first_seen`, `age_seconds`, and (when enabled) `total_input_tokens`/`total_output_tokens`. Covers FR-004, FR-005, FR-006, FR-007, FR-022.

**Independent Test**: Run three invocations on the same session; verify span tags show invocation index = 3, token totals equal the sum of all three turns' `UsageDetails`, and non-zero age after the first.

- [ ] T021 [US2] Extend `SessionTelemetryAgent.cs` — in `RunCoreAsync`: after reading the block, (1) capture `now = DateTimeOffset.UtcNow`, (2) set `FirstSeenUtc = now` if default, (3) increment `InvocationIndex`, (4) persist updated block, (5) after delegation set `Activity.Current` tags: `genai.session.invocation_index`, `genai.session.first_seen` (ISO 8601), `genai.session.age_seconds` (FR-004, FR-005); also emit `genai.session.invocations` counter + `genai.session.active` UpDownCounter increment before delegation and decrement after
- [ ] T022 [US2] Extend `SessionTelemetryAgent.cs` — token aggregate logic: after delegation read `response.Usage?.InputTokenCount` and `response.Usage?.OutputTokenCount`; fall back to span tags `genai.input_tokens`/`genai.output_tokens` only when response usage is null (FR-022); add to `TotalInputTokens`/`TotalOutputTokens`; persist; set span tags `genai.session.total_input_tokens` and `genai.session.total_output_tokens` only when `options.TrackTokenAggregates == true` (FR-006); record `genai.session.duration` histogram with elapsed ms
- [ ] T023 [P] [US2] Create `tests/.../SessionAggregateTests.cs` — integration tests: invocation index increments (FR-004), first_seen set once and age_seconds grows (FR-005), token totals accumulate across runs (FR-006), totals survive serialize+restore (FR-007), totals absent when `TrackTokenAggregates=false` (FR-006 config), no-usage-data turn leaves totals unchanged (FR-022 fallback to zero), `AgentResponse.Usage` takes priority over span tags (FR-022 priority)

**Checkpoint**: All `SessionAggregateTests` pass. Metrics emit correctly (verify via `MetricCollector`).

---

## Phase 5: User Story 3 — Custom Business Tag Propagation (Priority: P3)

**Goal**: Developer-attached session tags appear on every invocation span and survive serialization. Covers FR-010, FR-011, FR-012, FR-013, FR-020, FR-023.

**Independent Test**: Attach two custom tags to a fresh session; serialize and restore; run two invocations; verify both spans carry the tags, that overwriting a key updates the value, and that a tag exceeding limits is silently rejected.

- [ ] T024 [US3] Extend `SessionTelemetryExtensions.cs` — add `SetSessionTag(this AgentSession, string key, string value)`: validate key not null/whitespace/`>128 chars`/starts with `"genai.session."` → silent reject; validate value not `>512 chars` → silent reject; validate `block.Tags?.Count >= 50` → silent reject; else upsert in a new `Dictionary<string,string>` copy and persist via `SessionStateBagAccessor.TryPersist` (FR-010, FR-012, FR-013, FR-020, FR-023)
- [ ] T025 [US3] Extend `SessionTelemetryAgent.cs` — after setting identity/aggregate tags, iterate `block.Tags` and call `Activity.Current?.SetTag(key, value)` for each entry (FR-010, FR-011); custom tags propagated from StateBag means they survive serialization automatically (no extra code needed — FR-011 is satisfied by T010 `[JsonPropertyName("tags")]`)
- [ ] T026 [P] [US3] Create `tests/.../SessionTagTests.cs` — integration tests: tag appears on span (FR-010), tag survives serialize+restore (FR-011), overwrite updates value (FR-012), reserved prefix rejected silently (FR-013), 51st tag silently rejected (FR-020), key >128 rejected (FR-020), value >512 rejected (FR-020), empty key rejected (FR-020), custom tags not filtered — value written as-is (FR-023 documentation verified via XML doc check)

**Checkpoint**: All `SessionTagTests` pass. Custom tags visible in `ActivityRecorder.Completed[*].Tags`.

---

## Phase 6: User Story 4 — Session Span Mode B (Priority: P4)

**Goal**: Developer can open an explicit session span that becomes the trace parent for all invocations within the scope. Covers FR-014, FR-015, FR-016.

**Independent Test**: Enable `EnableSessionSpan=true`; call `BeginSessionTrace`; run three invocations inside the `using` block; verify a root span named `agent_session <agent.name>` exists in `ActivityRecorder` with all three invocation spans as children, and that the root span carries final aggregate tags on close.

- [ ] T027 [US4] Extend `SessionTelemetryExtensions.cs` — add `BeginSessionTrace(this AIAgent, AgentSession)`: if `options.EnableSessionSpan == false` return `NullDisposable`; else start an `Activity` named `"agent_session {agent.Name}"` via the Sessions `ActivitySource` with initial tags `genai.session.id`, `gen_ai.agent.name`, `genai.session.start_time`; return `IDisposable` that on `Dispose` sets final tags `genai.session.total_invocations`, `genai.session.total_input_tokens`, `genai.session.total_output_tokens`, `genai.session.duration_seconds`, `genai.session.errors` from the `SessionStateBlock`, adds a span event `session.ended`, then calls `activity.Stop()` (FR-014, FR-015, FR-016)
- [ ] T028 [US4] Extend `SessionTelemetryAgent.cs` — track error count: in the `try/catch` wrapping delegation, increment an in-memory `_errorCount` field (int) on exception before rethrowing to outer layers — this counter is read by `BeginSessionTrace`'s dispose to set `genai.session.errors`; alternatively read from the block and persist error count per invocation in `SessionStateBlock` (add `ErrorCount int` field) — use persisted approach so it survives serialize+restore (FR-015)
- [ ] T029 [P] [US4] Create `tests/.../SessionSpanTests.cs` — integration tests: Mode B disabled by default — `BeginSessionTrace` returns no-op (FR-016); Mode B enabled — root span created with correct name (FR-014); invocation spans are children of session span (FR-014); final aggregate tags present on session span after dispose (FR-015); `session.ended` event emitted (FR-015); session span not created when `EnableSessionSpan=false` even if called (FR-016)

**Checkpoint**: All `SessionSpanTests` pass. Trace tree visible in `ActivityRecorder` with correct parent-child relationships.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Error isolation verification, NuGet metadata, documentation completeness.

- [ ] T030 [P] Create `tests/.../SessionErrorHandlingTests.cs` — integration tests: telemetry exception does NOT propagate to caller — agent response returned normally (FR-017); StateBag write failure increments `session.statebag.write.failures` counter (FR-024); no session enrichment on span when write fails (FR-024); simulate write failure by providing a StateBag accessor that throws on `SetValue`
- [ ] T031 XML doc completeness pass — run `dotnet build -p:GenerateDocumentationFile=true` on Sessions and Abstractions; fix any CS1591 warnings on public members; verify no suppressions added
- [ ] T032 [P] Complete NuGet metadata in both csproj files — `<PackageId>`, `<Description>`, `<Authors>`, `<PackageTags>`, `<PackageLicenseExpression>MIT</PackageLicenseExpression>`, `<RepositoryUrl>`, `<PackageReadmeFile>README.md</PackageReadmeFile>`
- [ ] T033 [P] Create `README.md` at repo root — installation, one-liner quickstart (Scenario 1 from quickstart.md), links to full quickstart and NuGet packages

---

## Dependency Graph

```
Phase 1 (Setup)
    └── Phase 2 (Foundational: T009–T015)
            ├── Phase 3 (US1: T016–T020) ← MVP
            │       ├── Phase 4 (US2: T021–T023)
            │       ├── Phase 5 (US3: T024–T026)  [independent of US2]
            │       └── Phase 6 (US4: T027–T029)  [reads US2 aggregates at span close]
            │               └── Phase 7 (Polish: T030–T033)
```

**US2, US3, US4 are independent of each other** — can be implemented in any order after US1.
**US4 reads US2 aggregate values on span close** — implement US2 before US4 for complete final tags.

---

## Parallel Execution Examples

**After Phase 2 is complete**, these can run concurrently:
- T016 + T017 + T018 (all different files in Phase 3)
- T019 + T020 (different test files)

**After US1 is complete**, these can run concurrently:
- All of Phase 4 (US2) and Phase 5 (US3) simultaneously
- Phase 6 (US4) can start while US2/US3 are in progress (T027 skeleton, T028 error count)

---

## Implementation Strategy

**MVP scope** = Phase 1 + Phase 2 + Phase 3 (US1 only)

US1 alone satisfies SC-001 (session correlation) and SC-005 (zero agent impact) — the two highest-value success criteria. It is a complete, shippable increment.

Recommended delivery order: US1 → US2 → US3 → US4 (priority order from spec.md).

---

## Summary

| Phase | Story | Tasks | Parallel opportunities |
|---|---|---|---|
| 1 — Setup | — | T001–T008 (8) | T003, T004, T005, T007, T008 |
| 2 — Foundational | — | T009–T015 (7) | T009, T010, T012, T013, T014, T015 |
| 3 — US1 (P1) 🎯 | Session Identity | T016–T020 (5) | T017, T018, T019, T020 |
| 4 — US2 (P2) | Aggregates | T021–T023 (3) | T023 |
| 5 — US3 (P3) | Custom Tags | T024–T026 (3) | T026 |
| 6 — US4 (P4) | Session Span | T027–T029 (3) | T029 |
| 7 — Polish | — | T030–T033 (4) | T030, T032, T033 |
| **Total** | | **33 tasks** | |
