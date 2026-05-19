// SPDX-License-Identifier: MIT
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Tools.Tests.Helpers;
using Xunit;

namespace Melic.AgentFramework.Observability.Tools.Tests;

public sealed class ToolTelemetryConfigurationTests
{
    [Fact]
    public async Task Default_Activation_Uses_Default_Source_Name()
    {
        using var capture = new ActivityCapture("Melic.AgentFramework.Observability.Tools");
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent();

        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(), () => new("ok"));

        Assert.Single(capture.Completed);
    }

    [Fact]
    public async Task Custom_ActivitySourceName_Produces_Spans_From_Configured_Source()
    {
        const string sourceName = "custom-tool-source";
        using var capture = new ActivityCapture(sourceName);
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(sourceName);

        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(), () => new("ok"));

        Assert.Equal(sourceName, Assert.Single(capture.Completed).Source.Name);
    }

    [Fact]
    public void Tools_Project_Does_Not_Reference_Sessions()
    {
        string projectFile = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Melic.AgentFramework.Observability.Tools",
            "Melic.AgentFramework.Observability.Tools.csproj"));

        Assert.Contains("Melic.AgentFramework.Observability.Abstractions", projectFile, StringComparison.Ordinal);
        Assert.DoesNotContain("Melic.AgentFramework.Observability.Sessions", projectFile, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tool_Telemetry_Remains_Independent_From_Session_Attributes()
    {
        const string sourceName = "session-combo-source";
        using var capture = new ActivityCapture(sourceName);
        var telemetryAgent = ToolTelemetryTestHelpers.CreateAgent(sourceName);

        await ToolTelemetryTestHelpers.InvokeAsync(telemetryAgent, ToolTelemetryTestHelpers.CreateContext(), () => new("ok"));

        Assert.Equal("get_order", Assert.Single(capture.Completed).GetTagItem(ToolAttributeNames.ToolName));
    }
}