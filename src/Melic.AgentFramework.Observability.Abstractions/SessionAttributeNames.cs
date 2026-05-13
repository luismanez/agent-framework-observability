// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Abstractions;

/// <summary>
/// Defines the standard OpenTelemetry attribute names used by the session telemetry enrichment library.
/// All attributes follow the <c>genai.session.*</c> naming convention.
/// </summary>
public static class SessionAttributeNames
{
    /// <summary>Stable session identifier. Value: <c>genai.session.id</c>.</summary>
    public static readonly string SessionId = "genai.session.id";

    /// <summary>1-based index of the current invocation within the session. Value: <c>genai.session.invocation_index</c>.</summary>
    public static readonly string InvocationIndex = "genai.session.invocation_index";

    /// <summary>ISO 8601 UTC timestamp of the first invocation in this session. Value: <c>genai.session.first_seen</c>.</summary>
    public static readonly string FirstSeen = "genai.session.first_seen";

    /// <summary>Elapsed seconds since the first invocation in this session. Value: <c>genai.session.age_seconds</c>.</summary>
    public static readonly string AgeSeconds = "genai.session.age_seconds";

    /// <summary>Running total of input tokens consumed across all invocations in this session. Value: <c>genai.session.total_input_tokens</c>.</summary>
    public static readonly string TotalInputTokens = "genai.session.total_input_tokens";

    /// <summary>Running total of output tokens produced across all invocations in this session. Value: <c>genai.session.total_output_tokens</c>.</summary>
    public static readonly string TotalOutputTokens = "genai.session.total_output_tokens";

    /// <summary>Number of messages in the current session history. Value: <c>genai.session.message_count</c>.</summary>
    public static readonly string MessageCount = "genai.session.message_count";

    /// <summary>Total number of invocations recorded in this session. Value: <c>genai.session.total_invocations</c>.</summary>
    public static readonly string TotalInvocations = "genai.session.total_invocations";

    /// <summary>Duration in seconds of the current invocation. Value: <c>genai.session.duration_seconds</c>.</summary>
    public static readonly string DurationSeconds = "genai.session.duration_seconds";

    /// <summary>Number of errors recorded during this session. Value: <c>genai.session.errors</c>.</summary>
    public static readonly string Errors = "genai.session.errors";

    /// <summary>ISO 8601 UTC timestamp when the session span started. Value: <c>genai.session.start_time</c>.</summary>
    public static readonly string StartTime = "genai.session.start_time";
}
