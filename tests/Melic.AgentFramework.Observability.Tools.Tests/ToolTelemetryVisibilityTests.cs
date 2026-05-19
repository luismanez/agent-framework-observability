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

    [Fact]
    public async Task Successful_Delayed_Tool_Call_Emits_Child_Span_With_Status_And_Duration()
    {
        using var capture = new ActivityCapture(SourceName);
        using var parent = new Activity("invoke_agent").Start();
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);
        var context = ToolTelemetryTestHelpers.CreateContext();

        object? result = await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, context, async () =>
        {
            await Task.Delay(25).ConfigureAwait(false);
            return "ok";
        });

        Activity span = Assert.Single(capture.Completed);
        Assert.Equal("ok", result);
        Assert.Equal(ToolTelemetryAgent.ActivityName, span.OperationName);
        Assert.Equal(ActivityKind.Internal, span.Kind);
        Assert.Equal(parent.Id, span.ParentId);
        Assert.Equal(ActivityStatusCode.Ok, span.Status);
        Assert.Null(span.StatusDescription);
        Assert.Equal("get_order", span.GetTagItem(ToolAttributeNames.ToolName));
        Assert.Equal("call_123", span.GetTagItem(ToolAttributeNames.ToolCallId));
        Assert.True(span.Duration >= TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public async Task Failing_Tool_Call_Emits_Error_Status_And_Preserves_Exception()
    {
        using var capture = new ActivityCapture(SourceName);
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);
        var context = ToolTelemetryTestHelpers.CreateContext();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, context, () => throw new InvalidOperationException("Missing order id")));

        Activity span = Assert.Single(capture.Completed);
        Assert.Equal("Missing order id", exception.Message);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("Missing order id", span.StatusDescription);
    }

    [Fact]
    public async Task Three_Sequential_Tool_Calls_Produce_Three_Independent_Spans()
    {
        using var capture = new ActivityCapture(SourceName);
        using var parent = new Activity("invoke_agent").Start();
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);

        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext("first", "1"), () => new("one"));
        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext("second", "2"), () => new("two"));
        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext("third", "3"), () => new("three"));

        Assert.Equal(3, capture.Completed.Count);
        Assert.Equal(["first", "second", "third"], capture.Completed.Select(span => span.GetTagItem(ToolAttributeNames.ToolName)));
        Assert.All(capture.Completed, span => Assert.Equal(parent.Id, span.ParentId));
    }

    [Fact]
    public async Task No_Middleware_Emits_No_Spans_And_No_Parent_Emits_Root_Span()
    {
        using var capture = new ActivityCapture(SourceName);

        await new ValueTask<object?>("without telemetry");
        Assert.Empty(capture.Completed);

        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);
        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(), () => new("with telemetry"));

        Activity span = Assert.Single(capture.Completed);
        Assert.Null(span.ParentId);
    }
}