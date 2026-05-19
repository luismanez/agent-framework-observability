// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Tools.Tests.Helpers;
using Xunit;

namespace Melic.AgentFramework.Observability.Tools.Tests;

public sealed class ToolTelemetryRetryTests
{
    private const string SourceName = "retry-source";

    [Fact]
    public async Task Repeated_Call_Id_Emits_Retry_Attributes_On_Second_Span()
    {
        using var capture = new ActivityCapture(SourceName);
        using var parent = new Activity("invoke_agent").Start();
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);
        var context = ToolTelemetryTestHelpers.CreateContext(callId: "repeat");

        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, context, () => new("first"));
        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, context, () => new("second"));

        Assert.False((bool)capture.Completed[0].GetTagItem(ToolAttributeNames.IsRetry)!);
        Assert.Equal(1, capture.Completed[0].GetTagItem(ToolAttributeNames.AttemptIndex));
        Assert.True((bool)capture.Completed[1].GetTagItem(ToolAttributeNames.IsRetry)!);
        Assert.Equal(2, capture.Completed[1].GetTagItem(ToolAttributeNames.AttemptIndex));
    }

    [Fact]
    public async Task Retry_Attributes_Are_Omitted_When_Call_Id_Is_Absent()
    {
        using var capture = new ActivityCapture(SourceName);
        using var parent = new Activity("invoke_agent").Start();
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);

        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(callId: null), () => new("ok"));

        Activity span = Assert.Single(capture.Completed);
        Assert.Null(span.GetTagItem(ToolAttributeNames.IsRetry));
        Assert.Null(span.GetTagItem(ToolAttributeNames.AttemptIndex));
    }
}