// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Abstractions;

/// <summary>
/// Defines OpenTelemetry attribute names used by the redaction pipeline diagnostics.
/// All attributes follow the <c>genai.redaction.*</c> naming convention.
/// </summary>
public static class RedactionAttributeNames
{
    /// <summary>Whether redaction changed at least one telemetry attribute value. Value: <c>genai.redaction.applied</c>.</summary>
    public static readonly string Applied = "genai.redaction.applied";

    /// <summary>Total number of sensitive value replacements made on the telemetry item. Value: <c>genai.redaction.match_count</c>.</summary>
    public static readonly string MatchCount = "genai.redaction.match_count";

    /// <summary>Total number of redaction failures handled on the telemetry item. Value: <c>genai.redaction.failure_count</c>.</summary>
    public static readonly string FailureCount = "genai.redaction.failure_count";
}
