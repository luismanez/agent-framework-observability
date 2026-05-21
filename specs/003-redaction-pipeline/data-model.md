# Data Model: Redaction Pipeline

## Redaction Options

Represents consumer-provided configuration before validation.

**Fields**:

- `EnableDefaultRules`: whether built-in pattern and field-name rules are active. Default: `true`. When `false`, built-in rules are omitted but custom pattern and field-name rules still run.
- `ReplacementText`: text used to replace sensitive content. Default: `[REDACTED]`.
- `MaxValueLength`: maximum string length inspected for a targeted attribute. Default: `8192` characters.
- `FailureMode`: behavior when redaction cannot safely process a targeted value. Default: replace the whole value.
- `RedactExceptionMessages`: whether exception and error message attributes are targeted. Default: `false`.
- `EnableDiagnostics`: whether aggregate non-sensitive `genai.redaction.*` attributes are added. Default: `false`.
- `TargetedAttributes`: exact custom attribute names to process.
- `TargetedPrefixes`: custom attribute prefixes to process.
- `ExcludedAttributes`: exact attribute names that are never processed.
- `OptedInStandardAttributes`: official standard attribute names or prefixes selected by the consumer.
- `CustomPatternRules`: custom regex-based rules.
- `SensitiveFieldNames`: custom structured field names whose values should be replaced.

**Validation Rules**:

- `ReplacementText` must not be null or empty.
- `MaxValueLength` must be positive.
- Attribute names and prefixes must not be null, empty, or whitespace.
- Custom rule names must be non-empty and unique.
- Custom regex patterns must compile with a bounded timeout.
- Official `gen_ai.*` attributes can only be processed when explicitly included.

## Redaction Policy

Immutable, validated runtime form of `RedactionOptions` used by the processor and engine.

**Fields**:

- Normalized replacement text.
- Resolved target matchers.
- Built-in and custom rule sequence.
- Sensitive field-name set using case-insensitive comparison.
- Maximum inspected value length.
- Failure mode.
- Diagnostics flag.

**Relationships**:

- Created from `RedactionOptions` at registration time.
- Used by `RedactionProcessor` and `RedactionEngine` for every processed Activity.

## Attribute Target

Determines whether an Activity tag is eligible for redaction.

**Fields**:

- `Name`: exact attribute name, when applicable.
- `Prefix`: attribute prefix, when applicable.
- `IsStandardAttributeOptIn`: whether the target is an explicit `gen_ai.*` selection.
- `IsExcluded`: whether the target suppresses processing even when another target matches.

**Validation Rules**:

- Exact name and prefix matching use ordinal comparison.
- Exclusions win over inclusions.
- Default targets include only high-risk package-owned payload attributes.
- Built-in session correlation and aggregate attributes are not default targets; session attributes that can contain user-provided values require exact-attribute or prefix opt-in.

## Redaction Rule

Identifies sensitive content and transforms it to the configured replacement text.

**Fields**:

- `Name`: non-sensitive rule identifier for validation and internal ordering.
- `Kind`: pattern or field-name.
- `Pattern`: regex pattern for value matching, if pattern-based.
- `FieldNames`: names that trigger full-value replacement in structured payloads.
- `Timeout`: maximum regex execution time.

**Relationships**:

- A `RedactionPolicy` contains built-in rules and custom rules.
- Pattern rules apply to raw strings and JSON string values.
- Field-name rules apply to JSON object property values.

## Redaction Result

Result of processing one telemetry string value.

**Fields**:

- `Value`: transformed string value to export.
- `Changed`: whether any replacement occurred.
- `MatchCount`: aggregate number of replacements.
- `FailureCount`: aggregate number of processing failures for the value.
- `WasTooLong`: whether the value exceeded the inspection limit.

**Validation Rules**:

- `Value` never contains original matched sensitive substrings after successful redaction.
- Reprocessing a value that already contains only replacement markers does not change it again.
- Failure results follow the configured failure mode.

## Redaction Processor

OpenTelemetry processor that mutates targeted Activity attributes before export.

**Fields**:

- `Policy`: immutable redaction policy.
- `Engine`: pure redaction engine.

**Relationships**:

- Registered on a `TracerProviderBuilder`.
- Reads completed Activity tags at export time.
- Replaces targeted string tag values on the Activity.
- Adds optional aggregate diagnostics using `RedactionAttributeNames` constants.

**State Transitions**:

1. Activity completed.
2. Processor enumerates tags and resolves eligible targets.
3. Untargeted or non-string values remain unchanged.
4. Targeted string values move to one of: unchanged, redacted, whole-value replacement, or preserve-original failure mode.
5. Optional aggregate diagnostics are attached.
6. Export continues regardless of redaction outcome.

## Redaction Diagnostics

Optional non-sensitive metadata describing redaction activity.

**Fields**:

- `genai.redaction.applied`: whether at least one attribute value changed.
- `genai.redaction.match_count`: total number of replacements made on the Activity.
- `genai.redaction.failure_count`: total number of redaction failures handled on the Activity.

**Validation Rules**:

- Diagnostics never include original values, matched substrings, rule-specific payloads, or field values.
- Diagnostics are omitted unless explicitly enabled.
