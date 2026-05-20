// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Tools.Tests.Helpers;
using Xunit;

namespace Melic.AgentFramework.Observability.Tools.Tests;

public sealed class ToolTelemetryRetryTests
{
    private const string SourceName = "retry-source";
    private const string MafSourceName = "maf-retry-source";

    [Fact]
    public async Task Repeated_Call_Id_Enriches_Maf_ExecuteTool_With_Retry_Attributes()
    {
        using var mafCapture = new ActivityCapture(MafSourceName);
        using var fallbackCapture = new ActivityCapture(SourceName);
        using var mafSource = new ActivitySource(MafSourceName);
        using var parent = new Activity("invoke_agent").Start();
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);
        var context = ToolTelemetryTestHelpers.CreateContext(callId: "repeat");

        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource))
        {
            await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, context, () => new("first"));
        }

        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource))
        {
            await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, context, () => new("second"));
        }

        Assert.Empty(fallbackCapture.Completed);
        Assert.False((bool)mafCapture.Completed[0].GetTagItem(ToolAttributeNames.IsRetry)!);
        Assert.Equal(1, mafCapture.Completed[0].GetTagItem(ToolAttributeNames.AttemptIndex));
        Assert.True((bool)mafCapture.Completed[1].GetTagItem(ToolAttributeNames.IsRetry)!);
        Assert.Equal(2, mafCapture.Completed[1].GetTagItem(ToolAttributeNames.AttemptIndex));
    }

    [Fact]
    public async Task Retry_Attributes_Are_Omitted_When_Call_Id_Is_Absent()
    {
        using var mafCapture = new ActivityCapture(MafSourceName);
        using var fallbackCapture = new ActivityCapture(SourceName);
        using var mafSource = new ActivitySource(MafSourceName);
        using var parent = new Activity("invoke_agent").Start();
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);

        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource))
        {
            await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(callId: null), () => new("ok"));
        }

        Activity span = Assert.Single(mafCapture.Completed);
        Assert.Empty(fallbackCapture.Completed);
        Assert.Null(span.GetTagItem(ToolAttributeNames.IsRetry));
        Assert.Null(span.GetTagItem(ToolAttributeNames.AttemptIndex));
    }
}