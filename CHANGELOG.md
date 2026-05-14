# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Session Identity** — Every invocation span is automatically enriched with a stable `genai.session.id` that persists across session serialization and restore, enabling conversation-level correlation in any OpenTelemetry backend without custom code. (see specs/001-session-identity-enrichment/)
- **Session Aggregate Enrichment** — Running totals for input and output tokens, a 1-based invocation index, session age in seconds, and first-seen timestamp are accumulated in the session state and emitted on every `invoke_agent` span. (see specs/001-session-identity-enrichment/)
- **Custom Business Tag Propagation** — Developers can attach arbitrary key-value tags to a session (e.g. tenant ID, user tier) via `SetSessionTag`; tags propagate to all subsequent spans and survive serialization. (see specs/001-session-identity-enrichment/)
- **Mode B Session Span** — Opt-in explicit parent span (`BeginSessionTrace`) groups all invocation spans under a single `agent_session <agent.name>` root span; final aggregates and a `session.ended` event are recorded on dispose. (see specs/001-session-identity-enrichment/)
- New package `Melic.AgentFramework.Observability.Sessions` — session telemetry decorator, builder extension (`UseSessionTelemetry`), and session extension methods (`AssignSessionId`, `GetSessionId`, `SetSessionTag`, `BeginSessionTrace`).
- New package `Melic.AgentFramework.Observability.Abstractions` — shared `SessionAttributeNames` constants for all `genai.session.*` attribute names.
- OTel metrics: `genai.session.invocations` (counter), `genai.session.duration` (histogram, ms), `genai.session.active` (up-down counter), `session.statebag.write.failures` (counter).
- Multi-target support: .NET 8, .NET 9, .NET 10.
