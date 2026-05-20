// SPDX-License-Identifier: MIT
using Melic.AgentFramework.Observability.Tools.Internal;
using Microsoft.Agents.AI;

namespace Melic.AgentFramework.Observability.Tools;

/// <summary>
/// Extension methods for configuring tool-call telemetry on an <see cref="AIAgentBuilder"/> pipeline.
/// </summary>
public static class ToolTelemetryAgentBuilderExtensions
{
    /// <summary>
    /// Adds tool-call telemetry to the agent pipeline. Each intercepted tool execution enriches
    /// MAF's current <c>execute_tool</c> span with structured <c>genai.tool.*</c> attributes, or
    /// emits a fallback <c>agent_tool_call</c> span when no MAF tool span is current.
    /// </summary>
    /// <param name="builder">The <see cref="AIAgentBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="ToolTelemetryOptions"/>. When
    /// <see langword="null"/>, defaults are applied.
    /// </param>
    /// <returns>The same <see cref="AIAgentBuilder"/> instance for chaining.</returns>
    public static AIAgentBuilder UseToolTelemetry(
        this AIAgentBuilder builder,
        Action<ToolTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new ToolTelemetryOptions();
        configure?.Invoke(options);

        var telemetryAgent = new ToolTelemetryAgent(options.CloneAndValidate());
        return builder.Use(telemetryAgent.InvokeAsync);
    }
}