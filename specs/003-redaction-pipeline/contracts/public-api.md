# Public API Contract: Redaction Pipeline

## Package

`Melic.AgentFramework.Observability.Redaction`

The package is independently installable and depends only on Abstractions plus stable OpenTelemetry APIs required to register a trace processor.

## Registration Contract

```csharp
namespace Melic.AgentFramework.Observability.Redaction;

public static class RedactionTracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddTelemetryRedaction(
        this TracerProviderBuilder builder,
        Action<RedactionOptions>? configure = null);
}
```

**Behavior**:

- Registers one redaction processor in the trace pipeline.
- Applies defaults when `configure` is null.
- Validates options at registration time.
- Throws standard argument/validation exceptions for invalid configuration.
- Does not throw from telemetry processing after registration succeeds.
- Should be registered before exporters in examples so transformed attributes reach every exporter.

## Options Contract

```csharp
namespace Melic.AgentFramework.Observability.Redaction;

public sealed class RedactionOptions
{
    public bool EnableDefaultRules { get; set; } = true;
    public string ReplacementText { get; set; } = "[REDACTED]";
    public int MaxValueLength { get; set; } = 8192;
    public RedactionFailureMode FailureMode { get; set; } = RedactionFailureMode.ReplaceValue;
    public bool RedactExceptionMessages { get; set; } = false;
    public bool EnableDiagnostics { get; set; } = false;

    public RedactionOptions IncludeAttribute(string attributeName);
    public RedactionOptions IncludeAttributesWithPrefix(string attributePrefix);
    public RedactionOptions ExcludeAttribute(string attributeName);
    public RedactionOptions IncludeStandardAttribute(string attributeName);
    public RedactionOptions IncludeStandardAttributesWithPrefix(string attributePrefix);
    public RedactionOptions IncludeMafSensitiveDataAttributes();
    public RedactionOptions AddSensitiveFieldName(string fieldName);
    public RedactionOptions AddPatternRule(string name, string pattern);
}
```

**Behavior**:

- Exact attributes and prefixes are matched using ordinal comparison.
- Exclusions override inclusions.
- Standard `gen_ai.*` attributes are ignored unless explicitly selected through the standard-attribute methods or the `IncludeMafSensitiveDataAttributes()` preset.
- `IncludeMafSensitiveDataAttributes()` includes the `gen_ai.` prefix and is the recommended opt-in when MAF `EnableSensitiveData` telemetry is enabled for audit, analytics, or troubleshooting.
- Custom pattern rules are applied after built-in rules.
- Custom field names are matched case-insensitively for structured payload redaction.
- The option object is cloned and validated during registration; later consumer mutations do not affect the active processor.

## Failure Mode Contract

```csharp
namespace Melic.AgentFramework.Observability.Redaction;

public enum RedactionFailureMode
{
    ReplaceValue = 0,
    PreserveOriginal = 1
}
```

**Behavior**:

- `ReplaceValue`: default fail-closed behavior. A targeted value that cannot be processed is replaced entirely with `ReplacementText`.
- `PreserveOriginal`: opt-in behavior for consumers who prefer diagnostic fidelity over fail-closed redaction on processing failures.
- Processing failures are swallowed and never interrupt telemetry export.

## Attribute Constants Contract

```csharp
namespace Melic.AgentFramework.Observability.Abstractions;

public static class RedactionAttributeNames
{
    public static readonly string Applied = "genai.redaction.applied";
    public static readonly string MatchCount = "genai.redaction.match_count";
    public static readonly string FailureCount = "genai.redaction.failure_count";
}
```

**Behavior**:

- Constants are declared in Abstractions before Redaction references them.
- These attributes are emitted only when diagnostics are enabled.
- Values are aggregate and non-sensitive.

## Default Target Contract

Default redaction targets:

- `genai.tool.input`
- `genai.tool.output`

Default excluded behavior:

- Built-in session correlation, counter, timestamp, token, duration, and error-count attributes remain unchanged.
- Session custom tags and future user-provided session attributes are processed only when included by exact attribute name or prefix.
- Official `gen_ai.*` attributes remain unchanged.
- Official `gen_ai.*` attributes are processed when selected individually, by prefix, or through `IncludeMafSensitiveDataAttributes()`.
- Exception and error message attributes remain unchanged unless `RedactExceptionMessages` is true.
- Non-string attribute values remain unchanged.

## Default Rule Contract

When default rules are enabled, the processor redacts:

- Email addresses.
- Phone-number-like values.
- Bearer token values.
- API-token-like key/value fragments.
- Connection-string secrets.
- Password-like key/value fragments.
- JSON string values whose property names match common secret fields: `password`, `secret`, `token`, `apiKey`, `accessToken`, `refreshToken`, `connectionString`.

## JSON Payload Contract

For string values that appear to be JSON objects or arrays:

- Valid JSON is parsed and recursively redacted.
- Sensitive property values are fully replaced.
- Other string values still receive pattern-based redaction.
- Numbers, booleans, and nulls remain unchanged.
- Invalid JSON falls back to string redaction.
- Valid JSON in the acceptance corpus remains valid JSON after redaction. The corpus includes a flat object, nested object, array, array of objects, null values, numbers, booleans, sensitive strings, and non-sensitive strings.

## Processor Safety Contract

- Redaction does not mutate business objects, agent messages, tool arguments, tool outputs, or application state.
- Redaction only transforms telemetry attribute values.
- Redaction never throws during export processing.
- Diagnostic metadata never includes original values, matched fragments, or field values.
