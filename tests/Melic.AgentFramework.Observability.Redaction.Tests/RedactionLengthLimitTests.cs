// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionLengthLimitTests
{
    [Fact]
    public void OverLimitTargetedValueIsReplacedByDefault()
    {
        CapturedActivity captured = ExportWithOptions(options => options.MaxValueLength = 5);

        Assert.Equal("[REDACTED]", captured.Tags[ToolAttributeNames.ToolInput]);
    }

    [Fact]
    public void OverLimitTargetedValueCanBePreservedWhenConfigured()
    {
        CapturedActivity captured = ExportWithOptions(options =>
        {
            options.MaxValueLength = 5;
            options.FailureMode = RedactionFailureMode.PreserveOriginal;
        });

        Assert.Equal("person@example.com", captured.Tags[ToolAttributeNames.ToolInput]);
    }

    private static CapturedActivity ExportWithOptions(Action<RedactionOptions> configure)
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionLengthLimitTests");
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
