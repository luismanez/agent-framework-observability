// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Microsoft.Extensions.AI;

namespace Melic.AgentFramework.Observability.Tools.Internal;

internal sealed class ToolInvocationMapper : ToolInvocationContextAdapter<FunctionInvocationContext>
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyInput = new Dictionary<string, object?>();

    public ToolInvocationData ToToolInvocationData(FunctionInvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string toolName = FirstNonWhiteSpace(context.Function?.Name, context.CallContent?.Name) ?? "unknown_tool";
        string? callId = string.IsNullOrWhiteSpace(context.CallContent?.CallId) ? null : context.CallContent.CallId;
        object? inputPayload = (object?)context.CallContent?.Arguments ?? context.Arguments ?? EmptyInput;

        return new ToolInvocationData
        {
            ToolName = toolName,
            CallId = callId,
            InputPayload = inputPayload,
            ParentActivity = Activity.Current,
            StartedUtc = DateTimeOffset.UtcNow
        };
    }

    private static string? FirstNonWhiteSpace(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}