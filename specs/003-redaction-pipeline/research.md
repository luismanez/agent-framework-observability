# Phase 0 Research: Redaction Pipeline

## Decision 1: Use an OpenTelemetry trace processor as the primary integration point

**Decision**: Implement redaction as a trace processor registered on `TracerProviderBuilder` through a Redaction package extension method.

**Rationale**: Redaction must happen after Sessions and Tools have written attributes but before exporters send data to backends. A processor sits at the right boundary: it sees completed `Activity` data, requires no changes to agent code, and keeps Redaction independent from Sessions, Tools, MAF internals, Azure Monitor, or Application Insights.

**Alternatives considered**:

- Agent middleware: rejected because it would only see MAF agent/tool flows and would not protect arbitrary telemetry attributes.
- Exporter-specific adapters: rejected because they would create Azure/App Insights coupling and duplicate work for every backend.
- Pre-serialization redaction in Tools/Sessions: rejected because it violates package independence and mutates behavior before telemetry export.

## Decision 2: Create a new independent Redaction package and test project

**Decision**: Add `src/Melic.AgentFramework.Observability.Redaction` and `tests/Melic.AgentFramework.Observability.Redaction.Tests`; the Redaction package references Abstractions and stable OpenTelemetry packages only.

**Rationale**: The constitution requires sibling package independence. Redaction must compose with Sessions and Tools without depending on either package. Shared attribute constants for diagnostics belong in Abstractions before Redaction references them.

**Alternatives considered**:

- Add redaction into Tools: rejected because Sessions custom tags and future packages also need protection.
- Put redaction engine in Abstractions: rejected because Abstractions should stay a small shared contract package, not contain OTel processing logic.
- Depend on Sessions/Tools to discover attributes: rejected because default targets can reference shared constants in Abstractions.

## Decision 3: Do not use external compliance/redaction libraries in the MVP

**Decision**: Implement the MVP with `System.Text.Json`, `System.Text.RegularExpressions`, `System.Diagnostics.Activity`, and stable OpenTelemetry APIs.

**Rationale**: The repository constitution limits package dependencies to stable OTel and related public APIs. A small internal rule engine keeps the NuGet package dependency surface predictable and avoids pulling in platform-specific or broader compliance libraries.

**Alternatives considered**:

- `Microsoft.Extensions.Compliance.Redaction`: rejected for MVP because it adds an additional dependency family outside the current package constraints.
- AI-based sensitive data classification: rejected by the spec as out of scope and would be costly, non-deterministic, and backend-dependent.
- Reflection-based redaction of arbitrary objects: rejected because Redaction must not mutate application objects or inspect runtime business state.

## Decision 4: Redact exact high-risk package attributes by default, not every `genai.*` attribute

**Decision**: Default targets are `genai.tool.input` and `genai.tool.output`. Session attributes remain mostly pass-through by default because current built-in `genai.session.*` constants are correlation, counters, timestamps, durations, and token totals. Consumers can target additional `genai.session.*` custom tags or prefixes explicitly.

**Rationale**: Tools payloads are the high-risk surface. Built-in session attributes are low-risk operational fields. Broadly redacting all `genai.session.*` by default would risk destroying correlation value while protecting little in the current package shape.

**Alternatives considered**:

- Redact all `genai.*` attributes by default: rejected because it would redact stable correlation IDs, metrics, and low-risk operational values.
- Redact all `genai.session.*` attributes by default: rejected because built-in session attributes are not user content today.
- Require users to configure every target manually: rejected because safe default protection for tool payloads is the primary MVP value.

## Decision 5: Official `gen_ai.*` attributes are opt-in only

**Decision**: Redaction never changes official OTel/MAF `gen_ai.*` attributes unless the consumer explicitly includes selected attribute names or prefixes.

**Rationale**: `gen_ai.*` belongs to semantic conventions and MAF. Changing those fields by default could surprise consumers, break backend queries, or alter the meaning of standard telemetry. Explicit opt-in gives teams control when a standard attribute carries sensitive content in their environment.

**Alternatives considered**:

- Redact all `gen_ai.*` strings by default: rejected due to semantic convention compatibility risk.
- Never allow standard attribute redaction: rejected because some applications may need to protect selected standard attributes.

## Decision 6: Preserve JSON shape when possible and fail closed for unprocessable targeted values

**Decision**: For JSON-looking strings, parse with `System.Text.Json.Nodes`, redact recursively, and serialize back to JSON. If parsing fails, fall back to string redaction. If a targeted value exceeds the configured inspection limit or processing fails, replace the whole targeted value by default.

**Rationale**: JSON payloads from Tools should remain useful for debugging after sensitive fields are removed. Fail-closed behavior is safer for a package whose purpose is preventing leakage. Long values must be bounded so telemetry export cannot be delayed by redaction.

**Alternatives considered**:

- Always treat payloads as raw strings: rejected because it loses field-name redaction and structured observability value.
- Preserve original values on failures by default: rejected because the spec requires the default failure posture to avoid leaking targeted values.
- Drop attributes on failure: rejected for MVP because replacing with a marker is easier to understand in backends and preserves attribute presence.

## Decision 7: Use deterministic pattern and field-name rules with time-bounded regex processing

**Decision**: Built-in and custom pattern rules run deterministically, use culture-invariant regex matching with bounded execution time, and replace matched substrings with the configured replacement text. Field-name rules redact the full value associated with sensitive JSON property names.

**Rationale**: Determinism and idempotency are acceptance criteria. Regex processing must be constrained to avoid catastrophic backtracking or long export delays. Field-name redaction handles common JSON secret patterns even when the value itself is not recognizable by a generic regex.

**Alternatives considered**:

- Entropy-based secret detection: deferred because it can produce false positives and needs careful tuning.
- Type-specific redactor interface as the main public API: deferred because the MVP can expose simpler pattern and field-name configuration first.
- Partial redaction of secret field values: rejected for MVP because full replacement is safer and idempotent.

## Decision 8: MVP diagnostics are non-sensitive Activity attributes, not detailed logs

**Decision**: When diagnostics are enabled, Redaction may write aggregate `genai.redaction.*` attributes such as applied status, match count, and failure count. It must never emit rule matches, original fragments, field values, or payload snippets. Dedicated metrics can be added later if needed.

**Rationale**: Operators need a way to confirm redaction is active, but diagnostic data must not become another leak path. Activity-level aggregate counters are enough for the MVP and fit the trace processor shape.

**Alternatives considered**:

- Emit per-rule diagnostic events: rejected because rule names and event volume can reveal sensitive context.
- Emit redaction logs: rejected because logs create another telemetry surface requiring redaction.
- Add Meter instruments in MVP: deferred to keep this feature focused on trace redaction and avoid designing metric cardinality before real consumer feedback.
