// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Text.Json;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Tools.Tests.Helpers;
using Xunit;

namespace Melic.AgentFramework.Observability.Tools.Tests;

public sealed class ToolTelemetryPayloadTests
{
    private const string SourceName = "payload-source";
    private const string MafSourceName = "maf-payload-source";

    [Fact]
    public async Task Successful_Typed_Tool_Call_Enriches_Maf_ExecuteTool_With_Input_And_Output()
    {
        using var mafCapture = new ActivityCapture(MafSourceName);
        using var fallbackCapture = new ActivityCapture(SourceName);
        using var mafSource = new ActivitySource(MafSourceName);
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);

        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource))
        {
            await ToolTelemetryTestHelpers.InvokeAsync(
                telemetryAgent,
                ToolTelemetryTestHelpers.CreateContext(arguments: new Dictionary<string, object?> { ["orderId"] = "42" }),
                () => new(new { status = "shipped" }));
        }

        Activity span = Assert.Single(mafCapture.Completed);
        Assert.Empty(fallbackCapture.Completed);
        using JsonDocument input = JsonDocument.Parse((string)span.GetTagItem(ToolAttributeNames.ToolInput)!);
        using JsonDocument output = JsonDocument.Parse((string)span.GetTagItem(ToolAttributeNames.ToolOutput)!);

        Assert.Equal("42", input.RootElement.GetProperty("orderId").GetString());
        Assert.Equal("shipped", output.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CaptureInput_False_Omits_Input_And_Preserves_Result()
    {
        using var mafCapture = new ActivityCapture(MafSourceName);
        using var fallbackCapture = new ActivityCapture(SourceName);
        using var mafSource = new ActivitySource(MafSourceName);
        var telemetryAgent = new Internal.ToolTelemetryAgent(new ToolTelemetryOptions { ActivitySourceName = SourceName, CaptureInput = false });

        object? result;
        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource))
        {
            result = await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(), () => new("ok"));
        }

        Activity span = Assert.Single(mafCapture.Completed);
        Assert.Empty(fallbackCapture.Completed);
        Assert.Equal("ok", result);
        Assert.Null(span.GetTagItem(ToolAttributeNames.ToolInput));
        Assert.NotNull(span.GetTagItem(ToolAttributeNames.ToolOutput));
    }

    [Fact]
    public async Task CaptureOutput_False_Omits_Output_And_Preserves_Result()
    {
        using var mafCapture = new ActivityCapture(MafSourceName);
        using var fallbackCapture = new ActivityCapture(SourceName);
        using var mafSource = new ActivitySource(MafSourceName);
        var telemetryAgent = new Internal.ToolTelemetryAgent(new ToolTelemetryOptions { ActivitySourceName = SourceName, CaptureOutput = false });

        object? result;
        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource))
        {
            result = await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(), () => new("ok"));
        }

        Activity span = Assert.Single(mafCapture.Completed);
        Assert.Empty(fallbackCapture.Completed);
        Assert.Equal("ok", result);
        Assert.NotNull(span.GetTagItem(ToolAttributeNames.ToolInput));
        Assert.Null(span.GetTagItem(ToolAttributeNames.ToolOutput));
    }

    [Fact]
    public async Task Failing_Tool_Call_Emits_Structured_Error_Output_When_Enabled()
    {
        using var mafCapture = new ActivityCapture(MafSourceName);
        using var fallbackCapture = new ActivityCapture(SourceName);
        using var mafSource = new ActivitySource(MafSourceName);
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(SourceName);

        using (ToolTelemetryTestHelpers.StartMafExecuteToolActivity(mafSource))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(), () => throw new InvalidOperationException("boom")));
        }

        Activity span = Assert.Single(mafCapture.Completed);
        Assert.Empty(fallbackCapture.Completed);
        using JsonDocument output = JsonDocument.Parse((string)span.GetTagItem(ToolAttributeNames.ToolOutput)!);

        Assert.Equal("InvalidOperationException", output.RootElement.GetProperty("type").GetString());
        Assert.Equal("boom", output.RootElement.GetProperty("message").GetString());
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }
}