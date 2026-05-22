# Research: End-to-End Sample and Preview Hardening

## Decision: Create a dedicated end-to-end sample

**Rationale**: Sessions, Tools, and Redaction already have package-specific demos. The feature needs one recommended composition story, so a new `samples/EndToEndTelemetry.Demo` project keeps the integrated path discoverable without diluting the focused demos.

**Alternatives considered**: Extending `RedactionTelemetry.Demo` would overload a processor-focused sample with real agent credentials and tool execution. Extending `ToolTelemetry.Demo` would make Redaction look like a tool-only concern. Updating only docs would not satisfy the runnable proof requirement.

## Decision: Use Console by default and Application Insights by environment variable

**Rationale**: Console export is the lowest-friction validation path and satisfies local preview evaluation. `APPLICATIONINSIGHTS_CONNECTION_STRING` is already the conventional single-switch pattern for Azure Monitor/App Insights export and keeps cloud telemetry optional.

**Alternatives considered**: Separate samples for Console and App Insights would duplicate setup logic. Requiring App Insights would block local validation. Command-line flags add unnecessary surface area for a sample where environment variables are sufficient.

## Decision: Keep exporter dependencies sample-only

**Rationale**: The constitution requires package independence and stable, minimal package dependencies. Library packages should not take Azure Monitor, Application Insights, or exporter-specific dependencies; applications and samples own exporter choices.

**Alternatives considered**: Adding exporter references to Redaction or a meta-package would simplify one sample setup but violate the boundary expected by clean consumers and package independence checks.

## Decision: Register Sessions and Tools on `AIAgentBuilder`, Redaction on `TracerProviderBuilder`

**Rationale**: Sessions and Tools enrich live agent/tool spans through MAF integration points. Redaction is an OpenTelemetry processor that transforms selected string-valued span attributes before export. Showing both registration locations in the same sample directly addresses the most likely adoption mistake.

**Alternatives considered**: Wrapping Redaction in an `AIAgentBuilder` extension would blur its export-boundary semantics and imply runtime prompt/tool mutation. Registering Sessions/Tools only through OTel would not match their current package APIs.

## Decision: Demonstrate MAF sensitive telemetry through explicit Redaction preset

**Rationale**: Many consumers enable sensitive telemetry capture for audit and analytics. The sample should deliberately pair `EnableSensitiveData = true` with `IncludeMafSensitiveDataAttributes()` so sensitive prompt/response/tool data is useful for observability but masked before export.

**Alternatives considered**: Leaving sensitive telemetry disabled would not demonstrate FR-005 or FR-006. Opting in every `gen_ai.*` attribute manually would be noisier and less self-documenting than the dedicated preset.

## Decision: Validate clean preview consumption outside repository project references

**Rationale**: Repository builds do not prove that published packages restore, compile, and expose the intended public surface for consumers. A fresh project that references package IDs catches packaging metadata, dependency, and README issues.

**Alternatives considered**: Using sample project references only validates local source. Packing locally is useful for pre-release checks but does not prove NuGet consumption once preview packages are published.

## Decision: Keep the feature out of runtime package/API scope

**Rationale**: The spec is preview hardening: sample, docs, validation, and metadata consistency. Existing APIs are assumed suitable. Avoiding public API changes keeps risk low and preserves the roadmap for the later meta-package and Performance package.

**Alternatives considered**: Adding a convenience meta-package or setup helper could make the sample shorter, but would violate FR-019 and expand the release surface unnecessarily.
