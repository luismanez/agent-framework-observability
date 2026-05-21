// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Redaction;

/// <summary>
/// Configuration options for telemetry redaction. Options are cloned and validated when
/// <see cref="RedactionTracerProviderBuilderExtensions.AddTelemetryRedaction" /> is called.
/// </summary>
public sealed class RedactionOptions
{
    internal const string DefaultReplacementText = "[REDACTED]";

    private readonly List<string> _includedAttributes = [];
    private readonly List<string> _includedPrefixes = [];
    private readonly List<string> _excludedAttributes = [];
    private readonly List<string> _includedStandardAttributes = [];
    private readonly List<string> _includedStandardPrefixes = [];
    private readonly List<string> _sensitiveFieldNames = [];
    private readonly List<PatternRuleDefinition> _patternRules = [];

    /// <summary>
    /// Gets or sets a value indicating whether built-in sensitive value rules are enabled.
    /// Default: <see langword="true" />.
    /// </summary>
    public bool EnableDefaultRules { get; set; } = true;

    /// <summary>
    /// Gets or sets the replacement text used for redacted values. Default: <c>"[REDACTED]"</c>.
    /// </summary>
    public string ReplacementText { get; set; } = DefaultReplacementText;

    /// <summary>
    /// Gets or sets the maximum string length inspected for a targeted telemetry attribute.
    /// Default: <c>8192</c> characters.
    /// </summary>
    public int MaxValueLength { get; set; } = 8192;

    /// <summary>
    /// Gets or sets how processing failures for targeted values are handled.
    /// Default: <see cref="RedactionFailureMode.ReplaceValue" />.
    /// </summary>
    public RedactionFailureMode FailureMode { get; set; } = RedactionFailureMode.ReplaceValue;

    /// <summary>
    /// Gets or sets a value indicating whether exception and error message attributes are redacted.
    /// Default: <see langword="false" />.
    /// </summary>
    public bool RedactExceptionMessages { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether aggregate non-sensitive redaction diagnostics are emitted.
    /// Default: <see langword="false" />.
    /// </summary>
    public bool EnableDiagnostics { get; set; } = false;

    /// <summary>
    /// Includes a package-owned or application-owned telemetry attribute by exact name.
    /// Official <c>gen_ai.*</c> attributes must be included with <see cref="IncludeStandardAttribute" />.
    /// </summary>
    /// <param name="attributeName">The exact telemetry attribute name to process.</param>
    /// <returns>The current options instance for fluent configuration.</returns>
    public RedactionOptions IncludeAttribute(string attributeName)
    {
        _includedAttributes.Add(RequireName(attributeName, nameof(attributeName)));
        return this;
    }

    /// <summary>
    /// Includes package-owned or application-owned telemetry attributes by name prefix.
    /// Official <c>gen_ai.*</c> prefixes must be included with <see cref="IncludeStandardAttributesWithPrefix" />.
    /// </summary>
    /// <param name="attributePrefix">The telemetry attribute prefix to process.</param>
    /// <returns>The current options instance for fluent configuration.</returns>
    public RedactionOptions IncludeAttributesWithPrefix(string attributePrefix)
    {
        _includedPrefixes.Add(RequireName(attributePrefix, nameof(attributePrefix)));
        return this;
    }

    /// <summary>
    /// Excludes a telemetry attribute by exact name. Exclusions win over inclusions.
    /// </summary>
    /// <param name="attributeName">The exact telemetry attribute name to exclude.</param>
    /// <returns>The current options instance for fluent configuration.</returns>
    public RedactionOptions ExcludeAttribute(string attributeName)
    {
        _excludedAttributes.Add(RequireName(attributeName, nameof(attributeName)));
        return this;
    }

    /// <summary>
    /// Includes an official OpenTelemetry or MAF-owned <c>gen_ai.*</c> telemetry attribute by exact name.
    /// </summary>
    /// <param name="attributeName">The exact standard telemetry attribute name to process.</param>
    /// <returns>The current options instance for fluent configuration.</returns>
    public RedactionOptions IncludeStandardAttribute(string attributeName)
    {
        _includedStandardAttributes.Add(RequireStandardName(attributeName, nameof(attributeName)));
        return this;
    }

    /// <summary>
    /// Includes official OpenTelemetry or MAF-owned <c>gen_ai.*</c> telemetry attributes by prefix.
    /// </summary>
    /// <param name="attributePrefix">The standard telemetry attribute prefix to process.</param>
    /// <returns>The current options instance for fluent configuration.</returns>
    public RedactionOptions IncludeStandardAttributesWithPrefix(string attributePrefix)
    {
        _includedStandardPrefixes.Add(RequireStandardName(attributePrefix, nameof(attributePrefix)));
        return this;
    }

    /// <summary>
    /// Includes all official OpenTelemetry or MAF-owned <c>gen_ai.*</c> telemetry attributes.
    /// This is the recommended preset when MAF OpenTelemetry sensitive data capture is enabled.
    /// </summary>
    /// <returns>The current options instance for fluent configuration.</returns>
    public RedactionOptions IncludeMafSensitiveDataAttributes()
    {
        _includedStandardPrefixes.Add("gen_ai.");
        return this;
    }

    /// <summary>
    /// Adds a structured field name whose associated value should be replaced when found in JSON payloads.
    /// </summary>
    /// <param name="fieldName">The field name to treat as sensitive.</param>
    /// <returns>The current options instance for fluent configuration.</returns>
    public RedactionOptions AddSensitiveFieldName(string fieldName)
    {
        _sensitiveFieldNames.Add(RequireName(fieldName, nameof(fieldName)));
        return this;
    }

    /// <summary>
    /// Adds a custom regular-expression rule that replaces matching substrings with
    /// <see cref="ReplacementText" />.
    /// </summary>
    /// <param name="name">A non-sensitive rule name used only for validation.</param>
    /// <param name="pattern">The regular expression pattern to apply.</param>
    /// <returns>The current options instance for fluent configuration.</returns>
    public RedactionOptions AddPatternRule(string name, string pattern)
    {
        _patternRules.Add(new PatternRuleDefinition(
            RequireName(name, nameof(name)),
            RequireName(pattern, nameof(pattern))));
        return this;
    }

    internal IReadOnlyList<string> IncludedAttributes => _includedAttributes;

    internal IReadOnlyList<string> IncludedPrefixes => _includedPrefixes;

    internal IReadOnlyList<string> ExcludedAttributes => _excludedAttributes;

    internal IReadOnlyList<string> IncludedStandardAttributes => _includedStandardAttributes;

    internal IReadOnlyList<string> IncludedStandardPrefixes => _includedStandardPrefixes;

    internal IReadOnlyList<string> SensitiveFieldNames => _sensitiveFieldNames;

    internal IReadOnlyList<PatternRuleDefinition> PatternRules => _patternRules;

    private static string RequireName(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    private static string RequireStandardName(string value, string paramName)
    {
        string name = RequireName(value, paramName);
        if (!name.StartsWith("gen_ai.", StringComparison.Ordinal))
        {
            throw new ArgumentException("Standard telemetry attributes must use the gen_ai. prefix.", paramName);
        }

        return name;
    }
}

internal sealed record PatternRuleDefinition(string Name, string Pattern);
