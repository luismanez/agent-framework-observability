// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Abstractions;

/// <summary>
/// Converts a framework-specific tool invocation context into stable tool telemetry data.
/// </summary>
/// <typeparam name="TContext">The framework-specific invocation context type.</typeparam>
public interface ToolInvocationContextAdapter<in TContext>
{
    /// <summary>Converts <paramref name="context"/> into stable tool invocation data.</summary>
    /// <param name="context">The framework-specific invocation context.</param>
    /// <returns>The stable tool invocation data used by telemetry packages.</returns>
    ToolInvocationData ToToolInvocationData(TContext context);
}