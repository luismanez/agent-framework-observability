# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-07-05

### Added

- **Convenience Metapackage** — New `Melic.AgentFramework.Observability` package installs `Sessions`, `Tools`, and `Redaction` together as the recommended entrypoint for new consumers.
- **Redaction Pipeline** — New `Melic.AgentFramework.Observability.Redaction` package adds `.AddTelemetryRedaction()` for OpenTelemetry trace pipelines, redacting selected string attributes before export. (see specs/003-redaction-pipeline/)
- **Default Tool Payload Protection** — Redacts `genai.tool.input` and `genai.tool.output` by default with built-in rules for email, phone-like values, bearer tokens, API-token fragments, connection-string secrets, password-like fragments, and sensitive JSON field names. (see specs/003-redaction-pipeline/)
- **Configurable Redaction Scope** — Consumers can include or exclude attributes by exact name or prefix, opt selected official `gen_ai.*` attributes into processing, add custom regex rules, add sensitive JSON field names, and disable default rules while keeping custom rules. (see specs/003-redaction-pipeline/)
- **MAF Sensitive Data Redaction Preset** — `IncludeMafSensitiveDataAttributes()` opts in the official `gen_ai.*` attribute surface commonly emitted when MAF `EnableSensitiveData` is enabled for audit, analytics, or troubleshooting. (see specs/003-redaction-pipeline/)
- **Operational Redaction Diagnostics** — Optional aggregate diagnostics emit `genai.redaction.applied`, `genai.redaction.match_count`, and `genai.redaction.failure_count` without exposing rule names, matched values, or payload fragments. (see specs/003-redaction-pipeline/)
- **Tool Call Span Enrichment** — New `Melic.AgentFramework.Observability.Tools` package adds `.UseToolTelemetry()` to enrich MAF `execute_tool` spans in place, with fallback `agent_tool_call` spans only when no MAF tool span is current. (see specs/002-tool-call-enrichment/)
- **Bounded Tool Payload Capture** — Tool inputs, outputs, and structured error payloads can be captured as valid JSON via `genai.tool.input` and `genai.tool.output`, with independent capture switches and max-length bounds. (see specs/002-tool-call-enrichment/)
- **Tool Retry Markers** — Repeated tool call ids within one invocation are marked with `genai.tool.is_retry` and `genai.tool.attempt_index` to surface retries and loops in trace data. (see specs/002-tool-call-enrichment/)
- **Opt-in Tool Telemetry Configuration** — `ToolTelemetryOptions` controls payload capture, payload bounds, and fallback `ActivitySource` naming without requiring changes to tool implementations. (see specs/002-tool-call-enrichment/)
- **Session Identity** — Every invocation span is automatically enriched with a stable `genai.session.id` that persists across session serialization and restore, enabling conversation-level correlation in any OpenTelemetry backend without custom code. (see specs/001-session-identity-enrichment/)
- **Session Aggregate Enrichment** — Running totals for input and output tokens, a 1-based invocation index, session age in seconds, and first-seen timestamp are accumulated in the session state and emitted on every `invoke_agent` span. (see specs/001-session-identity-enrichment/)
- **Custom Business Tag Propagation** — Developers can attach arbitrary key-value tags to a session (e.g. tenant ID, user tier) via `SetSessionTag`; tags propagate to all subsequent spans and survive serialization. (see specs/001-session-identity-enrichment/)
- **Mode B Session Span** — Opt-in explicit parent span (`BeginSessionTrace`) groups all invocation spans under a single `agent_session <agent.name>` root span; final aggregates and a `session.ended` event are recorded on dispose. (see specs/001-session-identity-enrichment/)
- New package `Melic.AgentFramework.Observability` — metapackage that depends on `Sessions`, `Tools`, and `Redaction`.
- New package `Melic.AgentFramework.Observability.Sessions` — session telemetry decorator, builder extension (`UseSessionTelemetry`), and session extension methods (`AssignSessionId`, `GetSessionId`, `SetSessionTag`, `BeginSessionTrace`).
- New package `Melic.AgentFramework.Observability.Abstractions` — shared `SessionAttributeNames` constants for all `genai.session.*` attribute names.
- OTel metrics: `genai.session.invocations` (counter), `genai.session.duration` (histogram, ms), `genai.session.active` (up-down counter), `session.statebag.write.failures` (counter).
- Multi-target support: .NET 8, .NET 9, .NET 10.
