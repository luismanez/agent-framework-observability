# Feature Specification: Session Identity and Enrichment

**Feature Branch**: `001-session-identity-enrichment`

**Created**: 2026-05-13

**Status**: Draft

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Stable Session Identity Across Turns (Priority: P1)

As a developer, I want each agent invocation span to carry a stable session identifier that survives session serialization and deserialization, so I can correlate all turns of a multi-turn conversation in my observability backend without writing custom correlation code.

**Why this priority**: Without a stable identifier, every multi-turn conversation is invisible as a unit in any telemetry backend. This is the foundational capability on which every other session telemetry feature depends; none of the stories below deliver value without it.

**Independent Test**: Can be fully tested by configuring session telemetry on an agent, serializing and restoring the session between invocations, and verifying that the same session identifier tag appears on every invocation span — delivering the core correlation value with no other stories required.

**Acceptance Scenarios**:

1. **Given** an agent with session telemetry active and a fresh session, **When** the agent is invoked for the first time, **Then** an auto-generated GUID-format identifier is assigned to the session and recorded on the invocation span as `genai.session.id`.
2. **Given** the same session used for multiple sequential invocations, **When** I query my observability backend by that `genai.session.id` value, **Then** all invocation spans for that conversation appear together.
3. **Given** a session that has been saved to external storage and restored, **When** the agent is invoked with the restored session, **Then** the `genai.session.id` on the new span is identical to the one on all prior spans.
4. **Given** a developer who wants to supply their own identifier (e.g., a platform-assigned conversation ID), **When** they assign a custom value before the first invocation, **Then** every subsequent span uses that custom value instead of an auto-generated one.
5. **Given** a new session with no prior history, **When** session ID assignment is triggered concurrently from two threads, **Then** only one identifier is generated and both threads observe the same value.

---

### User Story 2 - Session Aggregate Enrichment on Invocation Spans (Priority: P2)

As a developer, I want session-scoped aggregates — total input and output tokens consumed, the current invocation index, and the session's age — automatically enriched onto every invocation span, so I can analyse multi-turn session behaviour in my observability backend without building custom aggregation logic.

**Why this priority**: Once session identity (P1) is in place, aggregate enrichment turns raw correlated spans into immediately useful analytical data. A developer can answer "how expensive was this conversation?" or "how deep into the session did this error occur?" with zero additional code.

**Independent Test**: Can be tested independently by running three sequential invocations on the same session and confirming the span tags show an incrementing invocation index, accumulating token totals, and a non-zero age after the first invocation.

**Acceptance Scenarios**:

1. **Given** session telemetry with aggregate tracking enabled and a third invocation on the same session, **When** that invocation completes, **Then** the span carries `genai.session.invocation_index = 3`, `genai.session.total_input_tokens` equal to the sum of all three turns, and `genai.session.total_output_tokens` similarly accumulated.
2. **Given** any invocation after the first on a session, **When** it completes, **Then** `genai.session.age_seconds` reflects the elapsed time since the first recorded invocation, and `genai.session.first_seen` carries the ISO 8601 timestamp of that first invocation.
3. **Given** aggregate tracking is disabled in the configuration, **When** an invocation completes, **Then** the token total tags are absent from the span; invocation index and age tags are still present.
4. **Given** a session saved and restored between invocations, **When** the agent runs after restoration, **Then** token totals continue accumulating from the persisted values and are not reset.
5. **Given** the model response for one turn contains no usage data, **When** the span is recorded, **Then** token totals are unchanged from the previous invocation and no error is raised.

---

### User Story 3 - Custom Business Tag Propagation (Priority: P3)

As a developer, I want to attach custom key-value tags to a session (for example, user tier or tenant identifier) that automatically appear on every subsequent invocation span, so I can add business context to my telemetry without re-attaching those tags on every call.

**Why this priority**: Business segmentation — filtering telemetry by tenant, environment, or user tier — is a standard operational requirement. Write-once propagation prevents inconsistencies across a multi-turn conversation and eliminates boilerplate.

**Independent Test**: Can be tested by attaching a `tenant_id` tag to a session before any invocations and running two turns, confirming the tag appears on both spans without any per-call attachment code.

**Acceptance Scenarios**:

1. **Given** a developer attaches `user_tier = premium` to a session before the first invocation, **When** the agent runs, **Then** the invocation span contains the tag `user_tier = premium`.
2. **Given** a new custom tag is attached to a session mid-conversation (after at least one prior invocation), **When** the next invocation runs, **Then** the new tag appears on that span and every span thereafter.
3. **Given** a session with custom tags is serialized and later restored, **When** an invocation runs with the restored session, **Then** all previously attached custom tags appear on the span.
4. **Given** the same tag key is set twice with different values, **When** the next invocation runs, **Then** only the most recently set value appears on the span.
5. **Given** a developer attempts to attach a tag whose key matches a reserved tag name (e.g., `genai.session.id`), **When** the invocation runs, **Then** the reserved tag retains its system-assigned value and the user-supplied value is not written.

---

### User Story 4 - Optional Session Span as Trace Root (Mode B) (Priority: P4)

As a developer, I want an opt-in mode that opens an explicit parent span grouping all invocation spans within a defined conversation scope, so I can view a complete multi-turn session as a single trace tree in my APM tool.

**Why this priority**: Mode A enrichment (stories 1–3) already enables correlation via the session identifier. This story adds structural convenience for single-process, short-lived sessions where a visual trace tree is more useful than a filtered query. It is opt-in and does not alter the behaviour of the other stories.

**Independent Test**: Can be tested by enabling session span mode, wrapping two agent invocations in a session trace scope, and confirming that both invocation spans appear as children of a single session-level parent span in the exported trace data.

**Acceptance Scenarios**:

1. **Given** session span mode is enabled and a developer opens a session trace scope, **When** two agent invocations run inside that scope, **Then** both invocation spans appear as children of a single `agent_session <agent name>` span.
2. **Given** an active session trace scope, **When** the scope is disposed, **Then** the session span records final aggregates — total invocations, total input and output tokens, session duration, and error count — before closing.
3. **Given** an invocation within the scope raises an exception, **When** the scope is later disposed, **Then** the error is counted in the session span's error total without suppressing the original exception.
4. **Given** session span mode is disabled (the default), **When** invocations run, **Then** no parent session span is created and trace structure is identical to Mode A.
5. **Given** a developer opens a session trace scope without enabling session span mode in configuration, **When** invocations run, **Then** no session span is created and no error is raised; the call is silently ignored.

---

### Edge Cases

- What happens when session state storage is corrupted or inaccessible at invocation time? A new session identifier must be assigned and the agent run must complete normally; telemetry enrichment continues on a best-effort basis.
- What happens when two invocations begin concurrently on the same session? The session identifier must be assigned atomically so both invocations observe the same value.
- What happens when token usage data is absent from the model response? Token aggregate tags reflect the last known totals; no aggregate tag is written with a misleading value and no error is produced.
- What happens when a custom tag key is empty or consists only of whitespace? The tag must be silently rejected rather than written to the span with an invalid key.
- What happens when the session trace scope wraps invocations on different agents? Each invocation span still records the agent's own tags; the session span reflects aggregates across all agents within the scope.
- What happens when a developer attempts to set a custom tag that exceeds the count, key-length, or value-length limits? The tag is silently rejected; no exception is raised, no partial write occurs, and existing tags are unaffected.
- What happens when writing the Session State Block to the StateBag fails mid-invocation? The invocation completes without session enrichment on that span; no exception propagates; the `session.statebag.write.failures` counter is incremented once.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When session telemetry is active, the library MUST auto-generate a stable GUID-format session identifier on the first invocation and persist it within the session's state storage under a reserved key.
- **FR-002**: The persisted session identifier MUST survive a full serialize-then-restore cycle of the session object without modification.
- **FR-003**: Every invocation span MUST include a `genai.session.id` tag containing the stable session identifier.
- **FR-004**: Every invocation span MUST include a `genai.session.invocation_index` tag reflecting a 1-based counter incremented once per invocation, persisted across serialization.
- **FR-005**: Every invocation span MUST include `genai.session.first_seen` (ISO 8601 timestamp) and `genai.session.age_seconds` derived from the first recorded invocation.
- **FR-006**: When token aggregate tracking is enabled, every invocation span MUST include `genai.session.total_input_tokens` and `genai.session.total_output_tokens` as running totals across all invocations in the session.
- **FR-007**: Token aggregate totals MUST survive serialization and continue accumulating from the persisted values after restoration; they MUST NOT reset on restore.
- **FR-008**: Developers MUST be able to provide a custom session identifier before the first invocation; once set, it MUST be treated identically to an auto-generated one.
- **FR-009**: Session telemetry MUST be activated via a single, optional configuration call added to the existing agent builder chain; no other code changes to the calling application are required.
- **FR-010**: Developers MUST be able to attach arbitrary string key-value custom tags to a session; each tag MUST be propagated to every subsequent invocation span without per-call attachment code.
- **FR-011**: Custom tags MUST persist through serialization and restoration of the session.
- **FR-012**: The most recently set value for a given custom tag key MUST overwrite any previously set value.
- **FR-013**: Custom tags MUST NOT overwrite reserved `genai.session.*` tag names on the span.
- **FR-014**: Developers MUST be able to opt in to a session span mode that creates a named parent span enclosing all invocation spans within a developer-delimited scope.
- **FR-015**: When the session span scope closes, the session span MUST record final aggregates: total invocations, total input tokens, total output tokens, total elapsed duration, and count of invocations that raised an error.
- **FR-016**: The session span mode MUST be disabled by default; when disabled, no parent session span is created under any circumstances.
- **FR-017**: Any failure in session telemetry logic MUST NOT propagate to the agent run; the agent response MUST be returned normally regardless of telemetry errors.
- **FR-018**: Session identifier assignment under concurrent access MUST be idempotent — only one value is generated and all concurrent callers observe the same result.
- **FR-019**: The library MUST work with any agent implementation supported by the standard agent builder; it MUST NOT depend on internals not exposed through the public builder API.
- **FR-020**: A session MUST NOT hold more than 50 custom tags. Each tag key MUST NOT exceed 128 characters and each tag value MUST NOT exceed 512 characters. Attempts to set a tag that violates any of these limits MUST be silently rejected — no exception is raised and no partial write occurs.
- **FR-021**: The Session State Block schema MUST be forward-compatible across library versions. When a block persisted by an older version is restored, any fields absent in the stored data MUST be initialised to their defined defaults. When a block persisted by a newer version is restored by an older library, unrecognised fields MUST be preserved without modification and MUST NOT cause a deserialization error.
- **FR-022**: When deriving token usage for a turn, the library MUST read usage data from the `AgentResponse` first. The invocation span tags MUST be used as a fallback only when the response carries no usage data. Both sources MUST NOT be summed together.
- **FR-023**: Custom session tags written to spans by the application MUST NOT be processed or filtered by the Sessions package. The Sessions package MUST document that developers assume full responsibility for ensuring custom tag values do not contain sensitive data.
- **FR-024**: When writing the Session State Block to the StateBag fails (e.g., read-only StateBag, serialization exception), the current invocation MUST complete without any session enrichment on that span. No exception MUST be propagated to the caller. The library MUST increment the `session.statebag.write.failures` counter metric once per failed write.

### Key Entities

- **Session State Block**: A unit of data persisted within the session's state storage under a reserved key. Contains: session identifier, invocation counter, running input and output token totals, first-seen timestamp, and the custom tag dictionary. Designed to survive serialization cycles without data loss. The schema is forward-compatible: missing fields default to their initial values on restore; unrecognised fields from newer versions are preserved.
- **Session Telemetry Configuration**: Developer-supplied options resolved once at agent build time. Controls whether token aggregate tracking is active and whether the session span mode is available for use. Applies uniformly to every invocation made through the configured agent.
- **Session Tag**: A named string value attached to a session by the developer. Stored in the Session State Block, propagated to every subsequent invocation span, and survives serialization cycles. A session holds at most 50 custom tags; each key is at most 128 characters and each value is at most 512 characters. Tags violating these limits are silently rejected.
- **Session Span** (Mode B): An explicit, developer-scoped telemetry span opened via an opt-in call and closed when the developer's scope exits. Serves as the structural trace parent for all invocation spans within the scope. Carries session-level summary aggregates at close.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can correlate all turns of a multi-turn conversation in any OpenTelemetry-compatible backend by filtering on a single tag value, without writing any custom correlation code beyond the initial one-time configuration.
- **SC-002**: Session aggregates — invocation index, token totals, and session age — appear on 100% of invocation spans with no additional per-invocation developer code after the initial setup.
- **SC-003**: A custom tag attached to a session once appears on 100% of subsequent invocation spans for that session, including after serialization and restoration of the session.
- **SC-004**: When session span mode is active, a complete multi-turn session (all invocation spans within the scope) is visible as a single trace tree in any OpenTelemetry-compatible APM tool.
- **SC-005**: Any failure in session telemetry logic has zero impact on the agent response — the run completes normally and the response is delivered to the caller.
- **SC-006**: Adding session telemetry to an existing agent requires exactly one new statement in the builder chain; no other modifications to the application are needed.

---

## Assumptions

- `AgentSession` exposes a publicly accessible, string-keyed state storage (StateBag) that participates in standard MAF serialization patterns; the library reads and writes to this storage as its persistence mechanism.
- Token usage data for a given turn is read from `AgentResponse.Usage` with priority; the invocation span tags serve as fallback only when the response carries no usage data. The two sources are never summed. When neither provides usage data, that turn contributes zero to the running totals without raising an error.
- Custom session tags are developer-supplied business context values. The Sessions package treats them as opaque strings and writes them to span attributes without any filtering. Developers are explicitly responsible for ensuring custom tag values are safe for telemetry export.
- The session span mode (Mode B) is designed for single-process, in-memory session lifecycles; restoring a session across process boundaries and correlating it via Mode A enrichment (the `genai.session.id` tag) is the expected pattern for distributed or long-lived sessions.
- The developer is responsible for correctly persisting and restoring the session object between invocations; the library trusts the session state it receives.
- The standard agent telemetry setup (the step that creates the `invoke_agent` span) is already active on the agent builder before session telemetry is added; this is a prerequisite for span enrichment.
- Chat history message count enrichment is emitted only when a compatible in-memory history provider is accessible via the session; it is silently omitted when not available.
- The reserved key used to store the Session State Block within the StateBag is documented and stable; application code must not write to that key directly.
- The Session State Block schema evolves in a forward-compatible manner across library versions: fields added in newer versions initialise to defaults when absent, and fields written by newer versions are preserved (not discarded) when read by an older version.

---

## Clarifications

### Session 2026-05-13

- Q: How many custom tags can be attached per session, and is there a maximum key or value length? → A: Max 50 tags per session; key ≤ 128 chars; value ≤ 512 chars. Tags violating any limit are silently rejected with no exception.
- Q: When a session persisted by library v1 is restored in an application running library v2, what is the expected behavior for the Session State Block schema? → A: Forward-compatible — new fields default-initialised on restore; unrecognised fields from newer versions preserved without error.
- Q: When both AgentResponse and the invocation span carry token usage data for the same turn, which source takes priority? → A: AgentResponse.Usage takes priority; span tags are used as fallback only when the response carries no usage data; the two sources are never summed.
- Q: Should custom session tags be processed by the Redaction package, or are they excluded and passed through as-is? → A: Explicitly excluded — developers assume full responsibility for custom tag values; the Sessions package writes them to spans without filtering.
- Q: When writing the Session State Block to the StateBag fails, should the current invocation span be enriched from in-memory state, or should enrichment be skipped entirely? → A: Skip enrichment entirely; emit a `session.statebag.write.failures` counter metric; never propagate the exception.
