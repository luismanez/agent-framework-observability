// SPDX-License-Identifier: MIT
using Melic.AgentFramework.Observability.Sessions.Internal;
using Microsoft.Agents.AI;

namespace Melic.AgentFramework.Observability.Sessions;

/// <summary>
/// Extension methods for <see cref="AIAgentBuilder"/> to register the session telemetry decorator.
/// </summary>
public static class SessionTelemetryAgentBuilderExtensions
{
    /// <summary>
    /// Wraps the agent being built with a <see cref="SessionTelemetryAgent"/> decorator that
    /// enriches every invocation span with stable session identity and aggregate token counts.
    /// </summary>
    /// <param name="builder">The <see cref="AIAgentBuilder"/> instance.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="SessionTelemetryOptions"/> for this agent.
    /// When omitted, default options are used.
    /// </param>
    /// <returns>The same <paramref name="builder"/> instance for fluent chaining.</returns>
    public static AIAgentBuilder UseSessionTelemetry(
        this AIAgentBuilder builder,
        Action<SessionTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new SessionTelemetryOptions();
        configure?.Invoke(options);

        return builder.Use(inner => new SessionTelemetryAgent(inner, options));
    }
}
