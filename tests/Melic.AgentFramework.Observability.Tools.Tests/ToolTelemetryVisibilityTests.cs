// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Tools.Internal;
using Melic.AgentFramework.Observability.Tools.Tests.Helpers;
using Xunit;

namespace Melic.AgentFramework.Observability.Tools.Tests;

public sealed class ToolTelemetryVisibilityTests
{
    private const string SourceName = "visibility-source";
    private const string MafSourceName = "maf-visibility-source";

    [Fact]
    public async Task Successful_Delayed_Tool_Call_Enriches_Maf_ExecuteTool_Activity()
    {
        using var mafCapture = new ActivityCapture(MafSourceName);
        using var fallbackCapture = new ActivityCapture(SourceName);
        using var mafSource = new ActivitySource(MafSourceName);
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);
        var context = ToolTelemetryTestHelpers.CreateContext();
        object? result;

        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource))
        {
            result = await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, context, async () =>
            {
                await Task.Delay(25).ConfigureAwait(false);
                return "ok";
            });
        }

        Activity span = Assert.Single(mafCapture.Completed);
        Assert.Empty(fallbackCapture.Completed);
        Assert.Equal("ok", result);
        Assert.Equal(ActivityKind.Internal, span.Kind);
        Assert.Equal("get_order", span.OperationName);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Equal("get_order", span.GetTagItem("gen_ai.tool.name"));
        Assert.Equal("call_123", span.GetTagItem("gen_ai.tool.call.id"));
        Assert.Null(span.GetTagItem(ToolAttributeNames.ToolName));
        Assert.Null(span.GetTagItem(ToolAttributeNames.ToolCallId));
        Assert.True(span.Duration >= TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public async Task Failing_Tool_Call_Enriches_Maf_ExecuteTool_And_Preserves_Exception()
    {
        using var mafCapture = new ActivityCapture(MafSourceName);
        using var fallbackCapture = new ActivityCapture(SourceName);
        using var mafSource = new ActivitySource(MafSourceName);
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);
        var context = ToolTelemetryTestHelpers.CreateContext();

        InvalidOperationException exception;
        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource))
        {
            exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, context, () => throw new InvalidOperationException("Missing order id")));
        }

        Activity span = Assert.Single(mafCapture.Completed);
        Assert.Empty(fallbackCapture.Completed);
        Assert.Equal("Missing order id", exception.Message);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("Missing order id", span.StatusDescription);
    }

    [Fact]
    public async Task Three_Sequential_Tool_Calls_Enrich_Three_Independent_Maf_Activities()
    {
        using var mafCapture = new ActivityCapture(MafSourceName);
        using var fallbackCapture = new ActivityCapture(SourceName);
        using var mafSource = new ActivitySource(MafSourceName);
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);

        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource, "first", "1"))
        {
            await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext("first", "1"), () => new("one"));
        }

        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource, "second", "2"))
        {
            await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext("second", "2"), () => new("two"));
        }

        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource, "third", "3"))
        {
            await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext("third", "3"), () => new("three"));
        }

        Assert.Equal(3, mafCapture.Completed.Count);
        Assert.Empty(fallbackCapture.Completed);
        Assert.Equal(["first", "second", "third"], mafCapture.Completed.Select(span => span.GetTagItem("gen_ai.tool.name")));
        Assert.All(mafCapture.Completed, span => Assert.Null(span.GetTagItem(ToolAttributeNames.ToolName)));
    }

    [Fact]
    public async Task No_Middleware_Emits_No_Spans_And_No_Maf_Activity_Emits_Fallback_Root_Span()
    {
        using var capture = new ActivityCapture(SourceName);

        await new ValueTask<object?>("without telemetry");
        Assert.Empty(capture.Completed);

        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);
        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(), () => new("with telemetry"));

        Activity span = Assert.Single(capture.Completed);
        Assert.Null(span.ParentId);
        Assert.Equal("get_order", span.GetTagItem(ToolAttributeNames.ToolName));
        Assert.Equal("call_123", span.GetTagItem(ToolAttributeNames.ToolCallId));
    }
}