// SPDX-License-Identifier: MIT
using Melic.AgentFramework.Observability.Abstractions;

namespace Melic.AgentFramework.Observability.Redaction.Internal;

internal sealed class RedactionPolicy
{
    private static readonly string[] DefaultSensitiveFieldNames =
    [
        "password",
        "passwd",
        "pwd",
        "secret",
        "token",
        "apiKey",
        "api_key",
        "accessToken",
        "access_token",
        "refreshToken",
        "refresh_token",
        "connectionString",
        "connection_string"
    ];

    private static readonly string[] DefaultTargets =
    [
        ToolAttributeNames.ToolInput,
        ToolAttributeNames.ToolOutput
    ];

    private static readonly string[] ErrorMessageTargets =
    [
        "exception.message",
        "error.message"
    ];

    private RedactionPolicy(
        string replacementText,
        int maxValueLength,
        RedactionFailureMode failureMode,
        bool enableDiagnostics,
        AttributeTargetMatcher targetMatcher,
        IReadOnlyList<RedactionRule> rules,
        IReadOnlySet<string> sensitiveFieldNames)
    {
        ReplacementText = replacementText;
        MaxValueLength = maxValueLength;
        FailureMode = failureMode;
        EnableDiagnostics = enableDiagnostics;
        TargetMatcher = targetMatcher;
        Rules = rules;
        SensitiveFieldNames = sensitiveFieldNames;
    }

    internal string ReplacementText { get; }

    internal int MaxValueLength { get; }

    internal RedactionFailureMode FailureMode { get; }

    internal bool EnableDiagnostics { get; }

    internal AttributeTargetMatcher TargetMatcher { get; }

    internal IReadOnlyList<RedactionRule> Rules { get; }

    internal IReadOnlySet<string> SensitiveFieldNames { get; }

    internal static RedactionPolicy FromOptions(RedactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ReplacementText))
        {
            throw new ArgumentException("Replacement text must not be null, empty, or whitespace.", nameof(options));
        }

        if (options.MaxValueLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.MaxValueLength, "Maximum value length must be positive.");
        }

        ValidateNoDuplicateRuleNames(options.PatternRules);

        var exactTargets = new List<string>(DefaultTargets);
        exactTargets.AddRange(options.IncludedAttributes);
        if (options.RedactExceptionMessages)
        {
            exactTargets.AddRange(ErrorMessageTargets);
        }

        var rules = new List<RedactionRule>();
        var sensitiveFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (options.EnableDefaultRules)
        {
            rules.AddRange(PatternRedactionRule.CreateDefaultRules());
            foreach (string fieldName in DefaultSensitiveFieldNames)
            {
                sensitiveFieldNames.Add(fieldName);
            }
        }

        foreach (string fieldName in options.SensitiveFieldNames)
        {
            sensitiveFieldNames.Add(fieldName);
        }

        foreach (PatternRuleDefinition customRule in options.PatternRules)
        {
            rules.Add(PatternRedactionRule.Create(customRule.Name, customRule.Pattern));
        }

        return new RedactionPolicy(
            options.ReplacementText,
            options.MaxValueLength,
            options.FailureMode,
            options.EnableDiagnostics,
            new AttributeTargetMatcher(
                exactTargets,
                options.IncludedPrefixes,
                options.ExcludedAttributes,
                options.IncludedStandardAttributes,
                options.IncludedStandardPrefixes),
            rules,
            sensitiveFieldNames);
    }

    private static void ValidateNoDuplicateRuleNames(IEnumerable<PatternRuleDefinition> patternRules)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PatternRuleDefinition rule in patternRules)
        {
            if (!names.Add(rule.Name))
            {
                throw new ArgumentException($"A redaction rule named '{rule.Name}' is already configured.");
            }
        }
    }
}
