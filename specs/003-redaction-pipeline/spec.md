# Feature Specification: Redaction Pipeline

**Feature Branch**: `003-redaction-pipeline`

**Created**: May 20, 2026

**Status**: Draft

**Input**: User description: "Create a Redaction package for Melic.AgentFramework.Observability that redacts sensitive telemetry attribute values before export while preserving observability value."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Safe Telemetry by Default (Priority: P1)

As an application owner using agent telemetry, I want sensitive values in high-risk observability payloads to be redacted before export so that I can enable detailed telemetry without leaking private data, credentials, or customer content.

**Why this priority**: This is the core product value. Sessions and Tools already produce useful telemetry; redaction makes that telemetry practical for production environments where sensitive content may appear in tool inputs, tool outputs, and session metadata.

**Independent Test**: Enable redaction with default settings, emit telemetry containing common sensitive values in package-owned telemetry attributes, and verify that exported telemetry contains replacement markers instead of the original sensitive values while preserving non-sensitive context.

**Acceptance Scenarios**:

1. **Given** default redaction is enabled and a tool input contains an email address, bearer token, and connection string, **When** telemetry is exported, **Then** those sensitive values are replaced and the rest of the payload remains readable.
2. **Given** default redaction is enabled and a telemetry attribute does not contain sensitive content, **When** telemetry is exported, **Then** the attribute value is unchanged.
3. **Given** a telemetry value is already redacted, **When** redaction is applied again, **Then** the value remains stable and is not repeatedly transformed.

---

### User Story 2 - Preserve Structured Observability Value (Priority: P2)

As a developer debugging agent behavior, I want redaction to preserve the shape of structured telemetry payloads so that I can still understand which inputs, outputs, and fields were involved without seeing sensitive values.

**Why this priority**: Redaction that destroys all structure reduces the usefulness of Tools and Sessions telemetry. The MVP must protect data while keeping enough signal for troubleshooting.

**Independent Test**: Export telemetry with JSON-looking payloads containing nested sensitive fields and verify that sensitive field values are replaced while the resulting payload remains understandable and structurally valid where practical.

**Acceptance Scenarios**:

1. **Given** a JSON-looking tool input contains `password`, `apiKey`, and non-sensitive fields, **When** telemetry is exported, **Then** sensitive field values are redacted and non-sensitive fields remain present.
2. **Given** a JSON-looking payload is malformed, **When** redaction runs, **Then** redaction falls back to safe string processing without interrupting telemetry export.
3. **Given** a payload exceeds the configured inspection limit, **When** redaction runs, **Then** processing remains bounded and the exported value follows the documented long-value behavior.

---

### User Story 3 - Configure Redaction Scope and Rules (Priority: P3)

As a library consumer with domain-specific privacy needs, I want to configure which telemetry attributes and patterns are redacted so that the package fits my organization’s data handling policy without requiring custom telemetry plumbing.

**Why this priority**: Default rules cover common risks, but production applications often have domain-specific identifiers, proprietary tokens, or stricter policies.

**Independent Test**: Configure custom field-name and pattern rules, choose target attributes, and verify that only the configured telemetry values are redacted while excluded attributes remain unchanged.

**Acceptance Scenarios**:

1. **Given** a custom rule for a domain-specific account number, **When** matching telemetry is exported, **Then** the account number is replaced with the configured replacement text.
2. **Given** official standard telemetry attributes are not explicitly opted in, **When** redaction runs, **Then** those attributes are left unchanged by default.
3. **Given** a consumer explicitly opts selected standard attributes into redaction, **When** matching telemetry is exported, **Then** only the selected standard attributes are processed.
4. **Given** error message redaction is disabled, **When** an exception message is exported, **Then** the error message is preserved; **When** error message redaction is enabled, **Then** sensitive values inside the message are redacted.

---

### User Story 4 - Operate Reliably and Transparently (Priority: P4)

As an operator, I want redaction to be reliable and observable without exposing sensitive content so that telemetry export is not broken by redaction and I can confirm that redaction is active.

**Why this priority**: Redaction sits close to export. It must fail safely and provide confidence without becoming another source of sensitive data.

**Independent Test**: Force redaction edge cases and failures, then verify telemetry export continues and non-sensitive diagnostics indicate whether redaction occurred or failed.

**Acceptance Scenarios**:

1. **Given** redaction encounters a processing failure for a targeted attribute, **When** telemetry is exported, **Then** export continues and the attribute follows the configured fail-safe behavior.
2. **Given** redaction changes one or more values on a telemetry item, **When** diagnostics are enabled, **Then** only aggregate non-sensitive redaction counts or status markers are emitted.
3. **Given** redaction is disabled, **When** telemetry is exported, **Then** no redaction transformations or redaction diagnostics are applied.

---

### Edge Cases

- A telemetry value contains multiple sensitive values of different types in a single string.
- A value contains an already-redacted marker such as `[REDACTED]`.
- A JSON-looking payload contains nested objects, arrays, nulls, numbers, booleans, and string values.
- A structured payload is malformed or only partially parseable.
- A telemetry attribute value exceeds the configured inspection limit.
- A sensitive value appears in a field name rather than in a value.
- Attribute targeting configuration includes both broad package-owned targets and narrower exclusions.
- A custom rule overlaps with a default rule.
- A non-string telemetry value is present on a targeted attribute.
- Official standard telemetry attributes are present and have not been explicitly opted in.
- Exception and error attributes contain credentials or personal data.
- Redaction processing fails unexpectedly while telemetry export is in progress.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a public Redaction package that can be installed and used independently of Sessions and Tools.
- **FR-002**: The system MUST provide a single, discoverable registration experience for applying redaction to trace telemetry before export.
- **FR-003**: The system MUST redact sensitive values from targeted string telemetry attributes before those attributes are exported.
- **FR-004**: The system MUST target package-owned high-risk telemetry attributes by default, including `genai.tool.input` and `genai.tool.output`.
- **FR-005**: The system MUST support opt-in redaction for `genai.session.*` attributes when those attributes can contain user-provided or application-provided values.
- **FR-006**: The system MUST leave low-risk correlation identifiers and official standard telemetry attributes unchanged by default.
- **FR-007**: The system MUST allow consumers to explicitly opt selected official standard telemetry attributes into redaction.
- **FR-008**: The system MUST provide default rules for email addresses, phone numbers, bearer/API tokens, connection strings, password-like key/value pairs, and common secret field names.
- **FR-009**: The system MUST allow consumers to enable or disable default rules.
- **FR-010**: The system MUST allow consumers to add custom pattern-based redaction rules.
- **FR-011**: The system MUST allow consumers to add custom field-name redaction rules for structured or JSON-looking payloads.
- **FR-012**: The system MUST allow consumers to configure replacement text, with `[REDACTED]` as the default replacement.
- **FR-013**: The system MUST allow consumers to configure a maximum inspected value length to keep redaction processing bounded.
- **FR-014**: The system MUST support attribute allowlist or denylist targeting so consumers can control the telemetry scope that redaction processes.
- **FR-015**: The system MUST process JSON-looking string values in a way that preserves valid JSON shape where practical.
- **FR-016**: The system MUST fall back to safe string processing when structured parsing is not possible.
- **FR-017**: The system MUST preserve non-sensitive telemetry attributes unchanged.
- **FR-018**: The system MUST avoid mutating business objects, agent messages, tool arguments, application state, or any data before it becomes telemetry.
- **FR-019**: The system MUST be deterministic and idempotent so that applying redaction multiple times does not further alter already-redacted values.
- **FR-020**: The system MUST NOT throw during telemetry export because of redaction processing.
- **FR-021**: The system MUST provide documented fail-safe behavior for targeted attributes when redaction processing fails.
- **FR-022**: The default fail-safe behavior for a targeted attribute MUST avoid exporting the original unprocessed value when redaction cannot be completed.
- **FR-023**: The system MUST support optional redaction of exception and error message attributes, disabled by default.
- **FR-024**: The system MUST expose only non-sensitive redaction diagnostics when diagnostics are enabled, such as whether redaction occurred and aggregate counts of redacted values.
- **FR-025**: The system MUST declare any new `genai.redaction.*` telemetry attributes in the shared abstractions contract before use.
- **FR-026**: The system MUST NOT require Azure-specific or Application Insights-specific behavior.
- **FR-027**: The system MUST NOT depend on Sessions or Tools packages to function.
- **FR-028**: The system MUST include examples that show composition with existing session and tool telemetry.
- **FR-029**: The system MUST include validation coverage for default rules, custom rules, structured payloads, opted-in standard attributes, idempotency, long values, failure behavior, non-target attributes, and package independence.

### Configuration Model

- **Default rules**: Enabled by default and configurable as a group.
- **Replacement text**: Defaults to `[REDACTED]` and can be customized.
- **Attribute targeting**: Defaults to high-risk package-owned telemetry attributes and supports explicit inclusion or exclusion of additional attributes.
- **Standard attributes**: Official standard telemetry attributes are excluded by default and can be selected explicitly.
- **Error attributes**: Exception and error message attributes are excluded by default and can be enabled explicitly.
- **Custom rules**: Consumers can add custom pattern rules and custom field-name rules.
- **Length bounds**: Consumers can set the maximum length inspected per value.
- **Failure mode**: Defaults to fail-closed for targeted attributes and is documented so consumers understand whether the original value or a replacement is exported if processing fails.
- **Diagnostics**: Optional and limited to non-sensitive status or aggregate counts.

### Telemetry Behavior and Attribute Targeting

- Package-owned `genai.*` attributes are the primary redaction target.
- `genai.tool.input` and `genai.tool.output` are targeted by default because they can contain user prompts, tool arguments, tool results, customer records, credentials, and other sensitive content.
- Built-in `genai.session.*` correlation, counter, timestamp, token, duration, and error-count attributes are not targeted by default.
- Session custom tags, future session attributes, or other `genai.session.*` values that contain user-provided or application-provided content are processed only when consumers explicitly include the exact attribute or a session attribute prefix.
- Official standard `gen_ai.*` attributes are not changed by default to avoid surprising consumers or altering semantic convention data.
- Exception and error message attributes are not changed by default because redacting errors can reduce diagnostic value, but consumers can opt them in.
- Non-string values are not redacted in the MVP unless a future requirement identifies a clear sensitive case.
- Redaction count diagnostics are in scope for the MVP only as aggregate non-sensitive telemetry. Rule names, matched values, field values, and original payload fragments are never emitted as diagnostics.

### Key Entities *(include if feature involves data)*

- **Redaction Policy**: Consumer-selected behavior that defines target attributes, enabled rules, replacement text, length limits, diagnostics, and failure mode.
- **Redaction Rule**: A default or custom rule that identifies sensitive content by value pattern or structured field name.
- **Attribute Target**: A telemetry attribute name or name pattern that determines whether a string value is eligible for redaction.
- **Redaction Result**: The outcome of processing one telemetry value, including the transformed value and non-sensitive metadata such as whether a change occurred.
- **Sensitive Field Name**: A field name that indicates its corresponding value should be redacted when found in structured or JSON-looking payloads.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In the acceptance test suite, 100% of default sensitive examples in targeted attributes are replaced before export.
- **SC-002**: In the acceptance test suite, 0 original sensitive values from targeted attributes appear in exported telemetry after redaction succeeds.
- **SC-003**: 100% of non-targeted attributes in the acceptance test suite remain unchanged.
- **SC-004**: 100% of already-redacted values in the acceptance test suite remain stable after repeated redaction.
- **SC-005**: 100% of valid JSON payloads in the acceptance corpus remain valid JSON after redaction. The corpus MUST include a flat object, nested object, array, array of objects, null values, numbers, booleans, sensitive strings, and non-sensitive strings.
- **SC-006**: 100% of simulated redaction processing failures allow telemetry export to continue.
- **SC-007**: A new consumer can enable default redaction and observe redacted tool telemetry in a sample application within 5 minutes.
- **SC-008**: Redaction diagnostics, when enabled, expose no matched sensitive values, original values, or payload fragments in 100% of diagnostic validation cases.

## Assumptions

- The primary users are application developers and platform engineers who already export agent telemetry to an OpenTelemetry-compatible backend.
- Redaction applies only to telemetry attribute values at or near export time; it does not change runtime data passed to agents, tools, models, or application code.
- Default targeting focuses on high-risk package-owned tool payload attributes because those are most likely to contain tool arguments, tool results, credentials, or application-provided values.
- Built-in session telemetry attributes are treated as low-risk correlation and aggregate fields by default; session custom tags or future user-provided session attributes require explicit target configuration.
- Official standard telemetry attributes remain unchanged by default to preserve compatibility and avoid altering semantic convention data unexpectedly.
- The MVP treats non-string telemetry values as out of scope unless they are represented inside a string payload.
- The MVP includes aggregate redaction diagnostics but excludes detailed audit logs of matched content.
- The default replacement text is `[REDACTED]`.
- The default failure posture for targeted attributes is fail-closed so that a redaction processing failure does not leak a value that the consumer asked the package to protect.
- Long values are processed only up to a configured inspection limit to keep telemetry export predictable.
- The package remains independent of Sessions and Tools while providing examples that show how all observability packages compose.
