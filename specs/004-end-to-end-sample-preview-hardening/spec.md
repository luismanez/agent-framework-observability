# Feature Specification: End-to-End Sample and Preview Hardening

**Feature Branch**: `004-end-to-end-sample-preview-hardening`

**Created**: 2026-05-21

**Status**: Draft

**Input**: User description: "Create a new Spec Kit feature for end-to-end sample and preview hardening of Melic.AgentFramework.Observability. The repository now has Sessions, Tools, and Redaction preview packages. The goal is to add one end-to-end sample, tighten documentation, and validate clean installation and usage flows without creating a new runtime package."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Run Recommended End-to-End Sample (Priority: P1)

As a developer evaluating the preview packages, I want to run one realistic sample that combines session correlation, tool-call telemetry, and export-boundary redaction so that I can see the recommended setup working as a coherent observability story.

**Why this priority**: This is the primary proof that the three packages compose into a useful preview experience rather than separate demos.

**Independent Test**: Clone the repository, run the end-to-end sample with the default local exporter, complete a multi-turn conversation with a tool call, and verify the output shows session, tool, standard sensitive-data, and redacted telemetry signals.

**Acceptance Scenarios**:

1. **Given** a developer has the repository and required sample credentials, **When** they run the end-to-end sample without an Application Insights connection string, **Then** the sample exports telemetry locally and shows how Sessions, Tools, and Redaction compose.
2. **Given** the sample runs a multi-turn agent interaction, **When** the interaction includes at least one tool call, **Then** the exported telemetry includes stable session correlation, tool-call enrichment, and redacted sensitive payload values.
3. **Given** sensitive telemetry capture is enabled for the sample, **When** prompt, response, tool argument, or tool result values contain sensitive examples, **Then** the exported telemetry masks those sensitive examples before export while preserving useful non-sensitive context.

---

### User Story 2 - Inspect The Same Sample In Application Insights (Priority: P2)

As a developer validating a cloud observability workflow, I want the same end-to-end sample to export to Azure Application Insights when configured so that I can inspect the expected spans and custom dimensions in the backend used by many production customers.

**Why this priority**: Cloud inspection guidance turns the sample from a console-only demo into a production-testable preview validation path.

**Independent Test**: Set an Application Insights connection string, run the same sample, and verify the documentation tells the developer which spans and custom dimensions to inspect.

**Acceptance Scenarios**:

1. **Given** an Application Insights connection string is set, **When** the developer runs the sample, **Then** the sample exports telemetry to Application Insights instead of the local console exporter.
2. **Given** telemetry has been exported to Application Insights, **When** the developer follows the inspection guidance, **Then** they can find session correlation attributes, tool-call enrichment attributes, MAF/GenAI standard attributes, and redaction diagnostics.

---

### User Story 3 - Follow A Recommended Setup Guide (Priority: P3)

As a developer adopting all three preview packages, I want one recommended setup guide that explains package roles, registration locations, and safe redaction choices so that I can avoid wiring mistakes and understand the boundaries between agent middleware and export-boundary telemetry processing.

**Why this priority**: Documentation consistency reduces adoption friction and prevents Redaction from being misunderstood as runtime prompt or tool mutation.

**Independent Test**: Read the guide from a clean context and verify it explains how to combine the packages, which parts register on the agent builder, which parts register on the telemetry pipeline, and how to inspect results.

**Acceptance Scenarios**:

1. **Given** a developer wants to use Sessions, Tools, and Redaction together, **When** they follow the recommended setup guide, **Then** they can identify which package handles session correlation, tool-call enrichment, and export-boundary redaction.
2. **Given** the developer enables sensitive telemetry capture for audit or analytics, **When** they read the guide, **Then** they see explicit guidance to use the MAF sensitive-data redaction preset before export.
3. **Given** a developer is concerned about runtime behavior, **When** they read the Redaction guidance, **Then** they understand that Redaction changes exported telemetry attributes only and does not mutate agent messages, model inputs, model outputs, tool arguments, or application state.

---

### User Story 4 - Validate Clean Preview Consumption (Priority: P4)

As a maintainer preparing the preview for public use, I want a clean consumer validation path and consistent package metadata so that published packages can be installed, compiled, and understood without relying on repository internals.

**Why this priority**: Clean consumption validation catches packaging, metadata, and documentation issues that full repository tests can miss.

**Independent Test**: Create or use a fresh consumer project, install the published preview packages, compile a minimal recommended setup, and verify documentation and metadata consistently describe the preview.

**Acceptance Scenarios**:

1. **Given** a fresh consumer project, **When** the published preview packages are installed, **Then** a minimal recommended setup compiles without project references to repository internals.
2. **Given** package READMEs, root documentation, feature docs, release notes, and package metadata exist, **When** they are reviewed together, **Then** they describe package roles and boundaries consistently.
3. **Given** library package boundaries are validated, **When** dependencies are inspected, **Then** Redaction does not depend on Sessions, Tools, Azure Monitor, Application Insights, or exporter-specific packages, while samples may depend on multiple packages and exporters.

---

### Edge Cases

- Application Insights connection string is missing: the sample must still run with local console export by default.
- Application Insights ingestion is delayed: documentation must set expectations and provide inspection guidance that does not imply immediate backend availability.
- Sensitive telemetry capture is disabled: the sample must still show package-owned tool payload redaction and explain what additional coverage appears when sensitive capture is enabled.
- A clean consumer project has only published packages available: validation must not rely on project references, local source paths, or package-internal dependencies.
- Documentation mentions Redaction: wording must preserve its value as MAF-aware telemetry redaction without implying `AIAgent` runtime redaction.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide one end-to-end sample that composes Sessions, Tools, and Redaction in a single recommended scenario.
- **FR-002**: The sample MUST demonstrate a realistic multi-turn agent conversation with at least one tool call.
- **FR-003**: The sample MUST demonstrate stable session correlation attributes in exported telemetry.
- **FR-004**: The sample MUST demonstrate tool-call enrichment attributes in exported telemetry.
- **FR-005**: The sample MUST demonstrate MAF/GenAI standard sensitive-data telemetry when sensitive telemetry capture is enabled.
- **FR-006**: The sample MUST demonstrate Redaction before export using the MAF sensitive-data redaction preset.
- **FR-007**: The sample MUST demonstrate redaction of package-owned tool payload attributes and selected official standard attributes.
- **FR-008**: The sample MUST export to a local console exporter by default.
- **FR-009**: The sample MUST export to Azure Application Insights when an Application Insights connection string is configured.
- **FR-010**: The sample documentation MUST provide clear inspection guidance for both local console output and Application Insights.
- **FR-011**: The recommended setup guide MUST explain how Sessions, Tools, and Redaction compose.
- **FR-012**: The recommended setup guide MUST explain that Sessions and Tools integrate through the agent builder while Redaction integrates through the telemetry pipeline.
- **FR-013**: The Redaction documentation MUST describe Redaction as MAF-aware export-boundary telemetry redaction, not as `AIAgent` runtime redaction.
- **FR-014**: The documentation MUST state that Redaction does not mutate agent messages, model inputs, model outputs, tool arguments, tool outputs, or application state.
- **FR-015**: The preview hardening work MUST validate clean installation and compilation from a fresh consumer project using published preview packages.
- **FR-016**: The validation path MUST confirm that Console and Application Insights examples do not require package-internal dependencies.
- **FR-017**: The validation path MUST confirm that Redaction has no dependency on Sessions, Tools, Azure Monitor, Application Insights, or exporter-specific packages.
- **FR-018**: Root documentation, package READMEs, feature documentation, release notes, and package metadata MUST tell a consistent story about package roles, registration points, and boundaries.
- **FR-019**: The feature MUST NOT create a new runtime package.
- **FR-020**: The feature MUST NOT add performance telemetry, cost telemetry, token budget behavior, runtime prompt/message/tool mutation, or Azure-specific dependencies to library packages.

### Key Entities

- **End-to-End Sample**: A runnable developer experience that composes the three preview packages, supports local and Application Insights export, and demonstrates realistic telemetry output.
- **Recommended Setup Guide**: Human-facing documentation explaining package roles, registration points, exporter configuration, sensitive-data redaction, and inspection steps.
- **Clean Consumer Validation**: A validation exercise proving published preview packages can be installed and compiled without repository-internal references.
- **Package Boundary Matrix**: A reviewable set of expectations describing which packages may depend on agent builder APIs, telemetry pipeline APIs, exporters, and sibling packages.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can run the end-to-end sample with the default local exporter in under 10 minutes after prerequisites are available.
- **SC-002**: The end-to-end sample emits at least one multi-turn session trace and at least one tool-call trace that demonstrate all three packages working together.
- **SC-003**: The same sample can be configured for Application Insights by setting one connection string, with no source code changes required.
- **SC-004**: Application Insights inspection guidance identifies the expected span names or filters and at least five relevant telemetry attributes or dimensions to inspect.
- **SC-005**: A fresh consumer project can install the published preview packages and compile a minimal recommended setup without repository project references.
- **SC-006**: Documentation review confirms 100% of touched public docs describe Redaction as export-boundary telemetry redaction rather than runtime agent mutation.
- **SC-007**: Dependency validation confirms Redaction has zero references to Sessions, Tools, Azure Monitor, Application Insights, or exporter-specific packages.
- **SC-008**: Full repository build and test validation complete successfully before the feature is considered ready.

## Assumptions

- The existing package APIs remain suitable for the recommended setup and no public API changes are expected.
- The end-to-end sample may require the same model-provider credentials pattern used by existing agent demos, but Application Insights must remain optional.
- Published preview packages are available from the configured package source by the time clean consumer validation is run.
- Console export is the default local validation path because it avoids requiring cloud resources.
- Application Insights validation may require a short ingestion delay before telemetry appears in the backend.
