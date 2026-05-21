// SPDX-License-Identifier: MIT
using Melic.AgentFramework.Observability.Redaction.Internal;
using OpenTelemetry.Trace;

namespace Melic.AgentFramework.Observability.Redaction;

/// <summary>
/// Extension methods for registering telemetry redaction in an OpenTelemetry trace pipeline.
/// </summary>
public static class RedactionTracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds telemetry redaction to the trace pipeline. Redaction transforms targeted string
    /// Activity attributes before downstream exporters receive them.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder" /> to configure.</param>
    /// <param name="configure">Optional delegate used to configure <see cref="RedactionOptions" />.</param>
    /// <returns>The same <paramref name="builder" /> instance for fluent configuration.</returns>
    public static TracerProviderBuilder AddTelemetryRedaction(
        this TracerProviderBuilder builder,
        Action<RedactionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new RedactionOptions();
        configure?.Invoke(options);
        RedactionPolicy policy = RedactionPolicy.FromOptions(options);

        return builder.AddProcessor(new RedactionProcessor(policy));
    }
}
