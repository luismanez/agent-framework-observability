// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionDisabledBehaviorTests
{
    [Fact]
    public void NoEffectiveRulesLeavesTelemetryUnchanged()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionDisabledBehaviorTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction(options => options.EnableDefaultRules = false)
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, "person@example.com");
        }

        provider.ForceFlush();

        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.Equal("person@example.com", captured.Tags[ToolAttributeNames.ToolInput]);
    }
}
