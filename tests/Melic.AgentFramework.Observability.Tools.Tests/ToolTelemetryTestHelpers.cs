// SPDX-License-Identifier: MIT
using Melic.AgentFramework.Observability.Tools.Internal;
using Melic.AgentFramework.Observability.Tools.Tests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Melic.AgentFramework.Observability.Tools.Tests;

internal static class ToolTelemetryTestHelpers
{
    internal static FunctionInvocationContext CreateContext(
        string toolName = "get_order",
        string? callId = "call_123",
        IDictionary<string, object?>? arguments = null)
    {
        arguments ??= new Dictionary<string, object?> { ["orderId"] = "42" };

        return new FunctionInvocationContext
        {
            Function = AIFunctionFactory.Create(() => "unused", toolName, "Test tool"),
            Arguments = new AIFunctionArguments(arguments),
            CallContent = new FunctionCallContent(callId ?? string.Empty, toolName, arguments)
        };
    }

    internal static ToolTelemetryAgent CreateAgent(string sourceName = "Melic.AgentFramework.Observability.Tools")
        => new(new ToolTelemetryOptions { ActivitySourceName = sourceName });

    internal static ValueTask<object?> InvokeAsync(
        ToolTelemetryAgent telemetryAgent,
        FunctionInvocationContext context,
        Func<ValueTask<object?>> next)
        => telemetryAgent.InvokeAsync(new StubAIAgent(), context, (_, _) => next(), CancellationToken.None);
}