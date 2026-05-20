// SPDX-License-Identifier: MIT
using System.Diagnostics;
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

    internal static Activity StartMafExecuteToolActivity(ActivitySource activitySource, string toolName = "get_order", string? callId = "call_123")
    {
        Activity activity = activitySource.StartActivity(toolName, ActivityKind.Internal)
            ?? throw new InvalidOperationException("The test MAF execute_tool activity was not created.");

        activity.SetTag(ToolTelemetryAgent.MafOperationNameAttribute, ToolTelemetryAgent.MafExecuteToolOperationName);
        activity.SetTag("gen_ai.tool.name", toolName);
        if (callId is not null)
        {
            activity.SetTag("gen_ai.tool.call.id", callId);
        }

        return activity;
    }

    internal static ValueTask<object?> InvokeAsync(
        ToolTelemetryAgent telemetryAgent,
        FunctionInvocationContext context,
        Func<ValueTask<object?>> next)
        => telemetryAgent.InvokeAsync(new StubAIAgent(), context, (_, _) => next(), CancellationToken.None);
}