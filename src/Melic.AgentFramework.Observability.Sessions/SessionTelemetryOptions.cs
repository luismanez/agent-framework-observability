// SPDX-License-Identifier: MIT
using System.Diagnostics.Metrics;

namespace Melic.AgentFramework.Observability.Sessions;

/// <summary>
/// Configuration options for the session telemetry decorator.
/// Resolved once at agent build time and applied to all subsequent invocations.
/// </summary>
public sealed class SessionTelemetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether input and output token totals are
    /// accumulated and emitted as session span tags. Default: <see langword="true"/>.
    /// </summary>
    public bool TrackTokenAggregates { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Mode B (explicit session span) is
    /// available via <c>SessionTelemetryExtensions.BeginSessionTrace</c>. Default: <see langword="false"/>.
    /// </summary>
    public bool EnableSessionSpan { get; set; } = false;

    /// <summary>
    /// Gets or sets the key used to store the session telemetry state block in the
    /// session's <c>StateBag</c>. Must not be written to by application code.
    /// Default: <c>"__melic_telemetry"</c>.
    /// </summary>
    public string StateBagKey { get; set; } = "__melic_telemetry";

    /// <summary>
    /// Gets or sets the <see cref="System.Diagnostics.ActivitySource"/> name for session spans (Mode B).
    /// Default: <c>"Melic.AgentFramework.Observability.Sessions"</c>.
    /// </summary>
    public string ActivitySourceName { get; set; } = "Melic.AgentFramework.Observability.Sessions";

    /// <summary>
    /// Gets or sets the meter name for session metrics.
    /// Default: <c>"Melic.AgentFramework.Observability.Sessions"</c>.
    /// </summary>
    public string MeterName { get; set; } = "Melic.AgentFramework.Observability.Sessions";

    /// <summary>Creates the set of OTel metric instruments for the Sessions package.</summary>
    internal SessionInstruments CreateInstruments(Meter meter) => new(
        Invocations: meter.CreateCounter<long>("genai.session.invocations", description: "Total agent invocations per session."),
        Duration: meter.CreateHistogram<long>("genai.session.duration", unit: "ms", description: "Duration of each agent invocation."),
        Active: meter.CreateUpDownCounter<long>("genai.session.active", description: "Number of currently active sessions."),
        StateBagWriteFailures: meter.CreateCounter<long>("session.statebag.write.failures", description: "Number of times persisting session state to the StateBag failed.")
    );
}

/// <summary>OTel metric instruments used by <see cref="SessionTelemetryOptions"/>.</summary>
internal sealed record SessionInstruments(
    Counter<long> Invocations,
    Histogram<long> Duration,
    UpDownCounter<long> Active,
    Counter<long> StateBagWriteFailures);
