// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Redaction;

/// <summary>
/// Defines how the redaction pipeline handles targeted telemetry values that cannot be processed safely.
/// </summary>
public enum RedactionFailureMode
{
    /// <summary>
    /// Replace the entire targeted value with <see cref="RedactionOptions.ReplacementText" />.
    /// This is the default fail-closed behavior.
    /// </summary>
    ReplaceValue = 0,

    /// <summary>
    /// Preserve the original value when processing fails. Use only when diagnostic fidelity is preferred
    /// over fail-closed redaction for processing failures.
    /// </summary>
    PreserveOriginal = 1
}
