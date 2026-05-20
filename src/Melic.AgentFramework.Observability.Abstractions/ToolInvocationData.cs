// SPDX-License-Identifier: MIT
using System.Diagnostics;

namespace Melic.AgentFramework.Observability.Abstractions;

/// <summary>
/// Stable tool-invocation data extracted from a framework-specific tool invocation context.
/// </summary>
public sealed record ToolInvocationData
{
    /// <summary>Gets the non-empty name of the invoked tool.</summary>
    public required string ToolName { get; init; }

    /// <summary>Gets the provider tool-call identifier when one is available.</summary>
    public string? CallId { get; init; }

    /// <summary>Gets the raw input payload to serialize as <c>genai.tool.input</c>.</summary>
    public required object? InputPayload { get; init; }

    /// <summary>Gets the current parent activity captured immediately before the tool span starts.</summary>
    public required Activity? ParentActivity { get; init; }

    /// <summary>Gets the UTC timestamp captured immediately before the tool invocation starts.</summary>
    public required DateTimeOffset StartedUtc { get; init; }
}