// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionProcessorTests
{
    [Fact]
    public void NonTargetAttributesArePreserved()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionProcessorTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction()
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag("app.note", "person@example.com");
        }

        provider.ForceFlush();

        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.Equal("person@example.com", captured.Tags["app.note"]);
    }

    [Fact]
    public void NonStringTargetAttributesArePreserved()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionProcessorTests.NonString");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction()
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, 42);
        }

        provider.ForceFlush();

        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.Equal(42, captured.Tags[ToolAttributeNames.ToolInput]);
    }
}
