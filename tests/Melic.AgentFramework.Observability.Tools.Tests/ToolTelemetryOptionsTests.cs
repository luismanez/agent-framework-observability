// SPDX-License-Identifier: MIT
using Melic.AgentFramework.Observability.Tools;
using Xunit;

namespace Melic.AgentFramework.Observability.Tools.Tests;

public sealed class ToolTelemetryOptionsTests
{
    [Fact]
    public void Defaults_Are_Enabled_And_Bounded()
    {
        var options = new ToolTelemetryOptions();

        Assert.True(options.CaptureInput);
        Assert.True(options.CaptureOutput);
        Assert.Equal(2048, options.MaxInputLength);
        Assert.Equal(2048, options.MaxOutputLength);
        Assert.Equal("Melic.AgentFramework.Observability.Tools", options.ActivitySourceName);
    }

    [Fact]
    public void Invalid_Max_Length_Fails_During_Builder_Configuration()
    {
        var options = new ToolTelemetryOptions { MaxInputLength = 0 };

        Assert.Throws<ArgumentOutOfRangeException>(() => options.CloneAndValidate());
    }

    [Fact]
    public void Invalid_Source_Name_Fails_During_Builder_Configuration()
    {
        var options = new ToolTelemetryOptions { ActivitySourceName = " " };

        Assert.Throws<ArgumentException>(() => options.CloneAndValidate());
    }
}