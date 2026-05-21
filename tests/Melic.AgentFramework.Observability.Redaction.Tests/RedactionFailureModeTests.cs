// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionFailureModeTests
{
    [Fact]
    public void ReplaceValueFailureModeFailsClosedForTooLongValues()
    {
        CapturedActivity captured = Export(options => options.MaxValueLength = 5);

        Assert.Equal("[REDACTED]", captured.Tags[ToolAttributeNames.ToolInput]);
    }

    [Fact]
    public void PreserveOriginalFailureModeKeepsTooLongValues()
    {
        CapturedActivity captured = Export(options =>
        {
            options.MaxValueLength = 5;
            options.FailureMode = RedactionFailureMode.PreserveOriginal;
        });

        Assert.Equal("person@example.com", captured.Tags[ToolAttributeNames.ToolInput]);
    }

    private static CapturedActivity Export(Action<RedactionOptions> configure)
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionFailureModeTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction(configure)
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, "person@example.com");
        }

        provider.ForceFlush();
        return Assert.Single(exporter.ExportedActivities);
    }
}
