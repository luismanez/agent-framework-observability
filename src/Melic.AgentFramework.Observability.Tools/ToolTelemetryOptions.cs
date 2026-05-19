// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Tools;

/// <summary>
/// Configuration options for the tool telemetry middleware.
/// Resolved once at agent build time and applied to all subsequent tool invocations.
/// </summary>
public sealed class ToolTelemetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether serialized tool input is emitted as
    /// <c>genai.tool.input</c>. Default: <see langword="true"/>.
    /// </summary>
    public bool CaptureInput { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether serialized tool output is emitted as
    /// <c>genai.tool.output</c>. Default: <see langword="true"/>.
    /// </summary>
    public bool CaptureOutput { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum final character length for the JSON payload written to
    /// <c>genai.tool.input</c>. The emitted value remains valid JSON. Default: <c>2048</c>.
    /// </summary>
    public int MaxInputLength { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the maximum final character length for the JSON payload written to
    /// <c>genai.tool.output</c>. The emitted value remains valid JSON. Default: <c>2048</c>.
    /// </summary>
    public int MaxOutputLength { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the <see cref="System.Diagnostics.ActivitySource"/> name used for <c>agent_tool_call</c> spans.
    /// Default: <c>"Melic.AgentFramework.Observability.Tools"</c>.
    /// </summary>
    public string ActivitySourceName { get; set; } = "Melic.AgentFramework.Observability.Tools";

    internal ToolTelemetryOptions CloneAndValidate()
    {
        if (MaxInputLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxInputLength), MaxInputLength, "Maximum input length must be positive.");
        }

        if (MaxOutputLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxOutputLength), MaxOutputLength, "Maximum output length must be positive.");
        }

        if (string.IsNullOrWhiteSpace(ActivitySourceName))
        {
            throw new ArgumentException("Activity source name must not be null, empty, or whitespace.", nameof(ActivitySourceName));
        }

        return new ToolTelemetryOptions
        {
            CaptureInput = CaptureInput,
            CaptureOutput = CaptureOutput,
            MaxInputLength = MaxInputLength,
            MaxOutputLength = MaxOutputLength,
            ActivitySourceName = ActivitySourceName
        };
    }
}