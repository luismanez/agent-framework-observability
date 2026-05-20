// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Abstractions;

/// <summary>
/// Defines the standard OpenTelemetry attribute names used by the tool telemetry enrichment library.
/// All attributes follow the <c>genai.tool.*</c> naming convention.
/// </summary>
public static class ToolAttributeNames
{
    /// <summary>Name of the invoked tool on fallback tool-call spans. Value: <c>genai.tool.name</c>.</summary>
    public static readonly string ToolName = "genai.tool.name";

    /// <summary>Provider tool-call identifier on fallback tool-call spans, when available. Value: <c>genai.tool.call_id</c>.</summary>
    public static readonly string ToolCallId = "genai.tool.call_id";

    /// <summary>JSON-serialized tool input payload. Value: <c>genai.tool.input</c>.</summary>
    public static readonly string ToolInput = "genai.tool.input";

    /// <summary>JSON-serialized tool output or error payload. Value: <c>genai.tool.output</c>.</summary>
    public static readonly string ToolOutput = "genai.tool.output";

    /// <summary>Whether this tool call repeats a previous call id in the same invocation. Value: <c>genai.tool.is_retry</c>.</summary>
    public static readonly string IsRetry = "genai.tool.is_retry";

    /// <summary>1-based attempt index for the tool call id in the current invocation. Value: <c>genai.tool.attempt_index</c>.</summary>
    public static readonly string AttemptIndex = "genai.tool.attempt_index";
}