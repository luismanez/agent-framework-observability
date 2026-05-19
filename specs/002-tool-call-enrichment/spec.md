# Feature Specification: Tool Call Span Enrichment

**Feature Branch**: `002-tool-call-enrichment`

**Created**: 2026-05-18

**Status**: Draft

## Clarifications

### Session 2026-05-19

- Q: How should captured JSON be truncated for `genai.tool.input` and `genai.tool.output`? → A: Truncate long values before serialization so the final attribute remains valid JSON.
- Q: What `ActivityKind` should `agent_tool_call` spans use? → A: Use `ActivityKind.Internal` for all tool-call spans.
- Q: When a tool fails, what shape should `genai.tool.output` use? → A: Emit a JSON object with at least `type` and `message`.

---

## User Scenarios & Testing *(mandatory)*

<!--
  User stories are PRIORITIZED as independent, independently testable journeys.
  Each story delivers standalone value — implementing only P1 still produces a usable MVP.
-->

### User Story 1 — Tool Execution Visibility (Priority: P1)

As a developer, I want each tool invocation made by a MAF agent to appear as a distinct child span of the enclosing `invoke_agent` span, so I can see the full breakdown of time and outcomes for every tool call within a single agent turn in my observability backend without adding any custom instrumentation code to my tools.

**Why this priority**: Without per-tool span data, an `invoke_agent` span is an opaque block of time. Identifying which tools ran, how long each took, and whether each succeeded or failed is impossible from the parent span alone. This story delivers the foundational visibility from which all other tool telemetry stories build; none of the stories below deliver value without it.

**Independent Test**: Can be fully tested by configuring tool telemetry on an agent that has one registered tool, triggering an agent invocation that exercises that tool, and verifying that a child span named `agent_tool_call` appears in the exported trace as a child of `invoke_agent` carrying the correct tool name and a success or error status.

**Acceptance Scenarios**:

1. **Given** an agent with tool telemetry active and one registered tool, **When** the agent invokes that tool during an invocation, **Then** a child span named `agent_tool_call` appears in the trace as a direct child of the `invoke_agent` span, carrying `genai.tool.name` set to the tool's registered name.
2. **Given** a tool call that completes without error, **When** the span is recorded, **Then** `otel.status_code` is `"OK"` and `otel.status_description` is absent from the child span.
3. **Given** a tool call that throws an exception, **When** the span is recorded, **Then** `otel.status_code` is `"ERROR"` and `otel.status_description` carries the exception message on the child span.
4. **Given** an agent invocation that calls three distinct tools in sequence, **When** the invocation completes, **Then** three separate `agent_tool_call` child spans exist under the same `invoke_agent` span, each carrying its own tool name and independent status.
5. **Given** a tool call for which a tool call identifier is available, **When** the span is recorded, **Then** `genai.tool.call_id` is set to that identifier on the child span.
6. **Given** a tool call for which no tool call identifier is available, **When** the span is recorded, **Then** `genai.tool.call_id` is omitted rather than written with a null or empty value.
7. **Given** tool telemetry is not configured on the agent, **When** the agent invokes a tool, **Then** no `agent_tool_call` span appears and tool execution is unaffected.
8. **Given** a tool call span is emitted, **When** the span is inspected in the trace, **Then** its `ActivityKind` is `Internal`.

---

### User Story 2 — Input and Output Capture (Priority: P2)

As a developer, I want the input parameters and result of each tool call captured on the corresponding span in a structured, readable form, so I can diagnose incorrect tool behaviour or unexpected inputs without adding custom logging to every tool implementation.

**Why this priority**: Span existence and timing (P1) tell me that a tool was called. Input and output capture turns each span into a diagnostic record that explains what happened inside the call and why it may have succeeded or failed.

**Independent Test**: Can be tested independently by invoking an agent whose tool accepts typed parameters and returns a structured result, then confirming the span carries `genai.tool.input` as a JSON-serialized representation of the call parameters and `genai.tool.output` as the serialized return value — without any other story being required.

**Acceptance Scenarios**:

1. **Given** input capture is enabled (the default) and a tool is called with named parameters, **When** the span is recorded, **Then** `genai.tool.input` contains a JSON object whose keys are parameter names and whose values are the serialized parameter values.
2. **Given** input capture is enabled and the captured input would exceed the configured maximum input length, **When** the span is recorded, **Then** `genai.tool.input` remains valid JSON, fits within that maximum length, and the span is still recorded successfully.
3. **Given** output capture is enabled (the default) and a tool returns a result, **When** the span is recorded, **Then** `genai.tool.output` contains the serialized result and, if truncation is required, remains valid JSON while fitting within the configured maximum output length.
4. **Given** a tool call that raises an exception, **When** the span is recorded, **Then** `genai.tool.output` carries a valid JSON object containing at least the exception `type` and `message` (subject to truncation), and `otel.status_code` is `"ERROR"`.
5. **Given** input capture is disabled in configuration, **When** a tool is invoked, **Then** `genai.tool.input` is absent from the span and the tool still executes and returns its result normally.
6. **Given** output capture is disabled in configuration, **When** a tool completes, **Then** `genai.tool.output` is absent from the span and the tool result is still returned to the agent normally.
7. **Given** a tool that returns a null result and output capture is enabled, **When** the span is recorded, **Then** `genai.tool.output` reflects the null result without raising an error or omitting the attribute unexpectedly.
8. **Given** JSON serialization of the input parameters fails for any reason, **When** the span is recorded, **Then** `genai.tool.input` is omitted, the tool still executes, and no exception propagates to the caller.

---

### User Story 3 — Retry Detection (Priority: P3)

As a developer, I want tool call spans to be automatically marked as retries when the same tool call identifier appears more than once within a single agent invocation, so I can identify unstable or looping tool calls in my telemetry without writing any custom detection logic.

**Why this priority**: Retry and re-invocation loops are a common source of unexpected cost and latency in agentic systems. Automatic retry detection provides immediate diagnostic value once P1 spans exist, and requires no developer action beyond the initial one-time builder configuration.

**Independent Test**: Can be tested by running an agent scenario where the model issues the same tool call id twice within one `invoke_agent` span (simulating a retry) and confirming that the second span carries `genai.tool.is_retry = true` and `genai.tool.attempt_index = 2`.

**Acceptance Scenarios**:

1. **Given** a tool is invoked for the first time within an `invoke_agent` span using a given call id, **When** the span is recorded, **Then** `genai.tool.is_retry` is `false` and `genai.tool.attempt_index` is `1`.
2. **Given** the same tool call id is observed a second time within the same `invoke_agent` span, **When** the span is recorded, **Then** `genai.tool.is_retry` is `true` and `genai.tool.attempt_index` is `2`.
3. **Given** a third occurrence of the same tool call id within the same invocation, **When** the span is recorded, **Then** `genai.tool.attempt_index` is `3` and `genai.tool.is_retry` remains `true`.
4. **Given** two different tool call ids appear within the same invocation, **When** both spans are recorded, **Then** each is tracked independently; neither is marked as a retry of the other.
5. **Given** a new `invoke_agent` span begins for a subsequent invocation, **When** a tool call id that was seen in the previous invocation appears again, **Then** the attempt counter resets: `genai.tool.is_retry` is `false` and `genai.tool.attempt_index` is `1`.
6. **Given** a tool call for which no call identifier is available, **When** the span is recorded, **Then** retry tracking is skipped and both `genai.tool.is_retry` and `genai.tool.attempt_index` are omitted.

---

### User Story 4 — Opt-In Configuration (Priority: P4)

As a developer, I want to enable tool call telemetry with a single call in the agent builder chain and no changes to any tool implementation, so I can adopt full tool visibility with minimal friction and without coupling my instrumentation to business logic.

**Why this priority**: Adoption friction is the primary barrier to observability coverage. A one-statement activation model ensures developers do not need to understand internals to gain immediate benefits.

**Independent Test**: Can be tested by building two equivalent agents — one without `.UseToolTelemetry()` and one with it — invoking the same tool on both, and confirming that only the configured agent emits `agent_tool_call` spans while both agents produce identical tool execution outcomes.

**Acceptance Scenarios**:

1. **Given** an agent builder configured with `.UseToolTelemetry()` and no options supplied, **When** the agent is built, **Then** tool telemetry is active with all default settings and the agent behaves identically to one without telemetry from the caller's perspective.
2. **Given** a developer supplies `ToolTelemetryOptions` with `CaptureInput = false`, **When** the agent is built, **Then** `genai.tool.input` is absent from all tool spans while all other attributes are still captured normally.
3. **Given** `.UseToolTelemetry()` is chained after `.UseSessionTelemetry()` in the same builder, **When** tool spans are emitted, **Then** tool spans are children of the enclosing `invoke_agent` span regardless of the registration order of the two packages.
4. **Given** a developer supplies a custom `ActivitySourceName` in `ToolTelemetryOptions`, **When** tool spans are emitted, **Then** all `agent_tool_call` spans originate from an `ActivitySource` with that name, allowing consumers to subscribe selectively.
5. **Given** `.UseToolTelemetry()` is not called, **When** the agent invokes tools, **Then** no `agent_tool_call` spans appear in the trace and tool execution is completely unaffected.

---

### Edge Cases

- What happens when a tool call span cannot be created because the `ActivitySource` has no active listeners? The tool executes normally; no span is emitted; no error is raised.
- What happens when JSON serialization of tool input parameters fails? `genai.tool.input` is omitted from the span; the tool executes normally and the span is still recorded with all other attributes.
- What happens when JSON serialization of the tool output fails? `genai.tool.output` is omitted from the span; the tool result is still returned to the agent normally.
- What happens when a tool accepts no parameters (empty input) and input capture is enabled? `genai.tool.input` is recorded as `"{}"` (an empty JSON object).
- What happens when the captured tool output exceeds `MaxOutputLength`? Long values are truncated before serialization so the final `genai.tool.output` attribute remains valid JSON and fits within the configured limit; no error is raised and the span is still recorded.
- What happens when retry call tracking state cannot be initialised or updated (e.g., a concurrent write collision)? Retry attributes are omitted for that span; the tool still executes and the span is still recorded.
- What happens if multiple tool calls run concurrently within the same `invoke_agent` span? Each tool call gets its own independent child span; retry state tracking handles concurrent updates safely without data races or incorrect attempt counts.
- What happens when an `invoke_agent` span is not active at the time a tool call begins? The `agent_tool_call` span is still created; it has no parent span and floats as a root span for that call.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The library MUST create an OpenTelemetry child span named `agent_tool_call` for each tool call intercepted during an `invoke_agent` span.
- **FR-002**: Each `agent_tool_call` span MUST be parented to the `Activity` that is current at the moment the tool call begins.
- **FR-002A**: Each `agent_tool_call` span MUST use `ActivityKind.Internal`.
- **FR-003**: Each `agent_tool_call` span MUST carry `genai.tool.name` set to the name of the invoked tool.
- **FR-004**: When a tool call identifier is available from MAF at the time of interception, the span MUST carry `genai.tool.call_id` set to that identifier. When no identifier is available, `genai.tool.call_id` MUST be omitted — never written as null or empty.
- **FR-005**: When input capture is enabled (the default), the span MUST carry `genai.tool.input` containing a JSON-serialized representation of the call parameters. When serialization fails for any reason, the attribute MUST be omitted and the tool MUST still execute normally.
- **FR-006**: `genai.tool.input` MUST fit within `ToolTelemetryOptions.MaxInputLength` characters (default: 2048) and remain valid JSON after truncation. When the captured payload would exceed the limit, the implementation MUST truncate oversized values before final serialization rather than clipping the serialized payload arbitrarily.
- **FR-007**: When output capture is enabled (the default), the span MUST carry `genai.tool.output` containing the serialized result of the tool call. If truncation is required, the final attribute value MUST fit within `ToolTelemetryOptions.MaxOutputLength` characters (default: 2048) and remain valid JSON.
- **FR-008**: When a tool call raises an exception, `otel.status_code` MUST be set to `"ERROR"`, `otel.status_description` MUST carry the exception message, and `genai.tool.output` MUST carry a valid JSON object containing at least `type` and `message` (subject to `MaxOutputLength` truncation) when output capture is enabled.
- **FR-009**: When a tool call completes without error, `otel.status_code` MUST be set to `"OK"` and `otel.status_description` MUST be omitted.
- **FR-010**: The `agent_tool_call` span duration MUST equal the wall-clock execution time of the tool: timing MUST begin immediately before the tool call and end immediately after the result is received or the exception is caught.
- **FR-011**: When a tool call identifier is available and the same identifier is observed more than once within the same `invoke_agent` span, the second and all subsequent `agent_tool_call` spans for that id MUST carry `genai.tool.is_retry = true` and `genai.tool.attempt_index` equal to the 1-based occurrence count for that id.
- **FR-012**: The first occurrence of any tool call id within an `invoke_agent` span MUST carry `genai.tool.is_retry = false` and `genai.tool.attempt_index = 1`.
- **FR-013**: Retry tracking state MUST be scoped to a single agent invocation; the per-call-id attempt counter MUST be initialised at the start of each invocation and discarded when the invocation ends.
- **FR-014**: When no tool call identifier is available, retry tracking MUST be skipped; `genai.tool.is_retry` and `genai.tool.attempt_index` MUST be omitted from the span.
- **FR-015**: Retry tracking state MUST be safe for concurrent access; simultaneous tool call interceptions within the same invocation MUST NOT produce data races or incorrect attempt counts.
- **FR-016**: Tool telemetry MUST be activated via a single optional `.UseToolTelemetry()` call on the agent builder chain; no modifications to tool implementations or to any other part of the application are required.
- **FR-017**: All attribute keys used by this package (`genai.tool.*`) MUST be declared as `public static readonly string` constants in `Melic.AgentFramework.Observability.Abstractions` before being referenced in the Tools package.
- **FR-018**: The `Tools` package MUST NOT carry a compile-time or runtime dependency on `Melic.AgentFramework.Observability.Sessions` or any other sibling package other than `Abstractions`.
- **FR-019**: Any failure in tool telemetry logic — span creation, attribute writing, retry state management, or serialization — MUST NOT propagate to the caller or alter the outcome of the tool call. Telemetry is best-effort and all errors MUST be swallowed silently.
- **FR-020**: Input capture and output capture MUST each be individually configurable via `ToolTelemetryOptions`. When capture is disabled for either, the corresponding attribute MUST be absent from all tool spans.
- **FR-021**: The `ActivitySource` name used to create `agent_tool_call` spans MUST default to `"Melic.AgentFramework.Observability.Tools"` and MUST be configurable via `ToolTelemetryOptions.ActivitySourceName`. Consumers MUST be able to subscribe to or filter spans by this source name.
- **FR-022**: Every public type, method, property, and field in the Tools package MUST carry an XML documentation comment. The package MUST compile without warnings under `TreatWarningsAsErrors=true`.
- **FR-023**: Every `.cs` file in the Tools package MUST begin with `// SPDX-License-Identifier: MIT`.
- **FR-024**: The Tools package MUST target `net8.0`, `net9.0`, and `net10.0` via `<TargetFrameworks>`, consistent with the rest of the library suite.

### Key Entities

- **Tool Call Span** (`agent_tool_call`): An OpenTelemetry child span created for each intercepted tool invocation. Uses `ActivityKind.Internal`; carries the tool name, optional call identifier, JSON-serialized input and output (each subject to a configurable maximum length while remaining valid JSON). When a tool fails, the output payload is a JSON object containing at least the exception `type` and `message`. The span also carries execution status (`otel.status_code`) and retry indicators. Its duration equals the wall-clock execution time of the tool. Parented to the current activity at the point the tool call begins.
- **Tool Telemetry Configuration** (`ToolTelemetryOptions`): Developer-supplied options resolved once at agent build time. Controls input/output capture flags, maximum serialized lengths for input and output, and the `ActivitySource` name. Applied uniformly to every tool call intercepted by the configured agent.
- **Retry Tracking State**: An invocation-scoped data structure that maps each tool call identifier to its 1-based occurrence count within the current `invoke_agent` span. Initialised at the start of each invocation and discarded at the end. Must be safe for concurrent access when multiple tool calls run in parallel within the same invocation.
- **Tool Attribute Constants**: `public static readonly string` declarations in `Melic.AgentFramework.Observability.Abstractions` for every attribute key in the `genai.tool.*` namespace. These are the single source of truth for attribute names across the entire library suite and MUST be declared in Abstractions before being referenced in the Tools package.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can see the complete list of tool calls made during any agent invocation — name, timing, and success/error status — in any OpenTelemetry-compatible backend with zero per-tool instrumentation code beyond the one-time builder configuration.
- **SC-002**: Every `agent_tool_call` span carries accurate wall-clock timing: the span duration matches the actual execution time of the tool, enabling immediate identification of slow tools without additional profiling setup.
- **SC-003**: Input and output data are present on every tool call span where capture is enabled and serialization succeeds, enabling full reconstruction of tool behavior from trace data alone.
- **SC-004**: Retry and re-invocation loops are automatically surfaced in telemetry: any repeated tool call id within an invocation is flagged with zero additional developer code.
- **SC-005**: Any failure in tool telemetry — span creation, serialization, retry state management — has zero observable impact on the agent's response; tool results are always delivered to the caller.
- **SC-006**: Adding tool call telemetry to an existing agent requires exactly one new statement in the builder chain and no changes to any tool implementation or other application code.

---

## Assumptions

- The MAF public API exposes a mechanism within `DelegatingAIAgent` to intercept tool invocations before and after execution; the library relies solely on that stable public interception surface.
- Tool call parameters are accessible as a collection of named values at the point of interception; JSON serialization via `System.Text.Json` is applied to the full collection without pre-filtering.
- The MAF tool call representation exposes at minimum a tool name and, when available, a tool call identifier; both are accessed only through the documented public API surface.
- The standard agent telemetry setup that creates the `invoke_agent` span is already active on the agent builder before tool telemetry is added; this is a prerequisite for the child-span parent relationship.
- The `Redaction` package (a planned future sibling) is the designated owner of PII filtering for tool inputs and outputs; the Tools package writes unfiltered values and is explicitly not responsible for redaction of sensitive data.
- `System.Text.Json` is the sole serialization mechanism for input and output; no secondary serialization dependency is introduced.
- Retry detection is only meaningful when tool call identifiers are available; the absence of an identifier is treated as non-retryable by design rather than as an error condition.
- Tool telemetry operates correctly whether or not the Sessions package is registered on the same agent; the two packages are independent and do not depend on each other.
- The `ActivitySource` consumer (the OpenTelemetry export pipeline) is registered externally by the application; the Tools package creates spans and attributes but does not configure, start, or stop the export pipeline.
- A `ToolTelemetryOptions` instance with all defaults is a fully valid and safe configuration; developers who want the default behavior need not supply any options.
