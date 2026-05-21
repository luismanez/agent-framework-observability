// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionDefaultsTests
{
    [Fact]
    public void DefaultRulesRedactSensitiveToolPayloadValuesBeforeExport()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionDefaultsTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction()
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("tool"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, "email person@example.com Authorization: Bearer abc.def AccountKey=secret-key; query safe");
            activity?.SetTag(ToolAttributeNames.ToolOutput, "password=super-secret result ok");
        }

        provider.ForceFlush();

        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        string input = Assert.IsType<string>(captured.Tags[ToolAttributeNames.ToolInput]);
        string output = Assert.IsType<string>(captured.Tags[ToolAttributeNames.ToolOutput]);
        Assert.DoesNotContain("person@example.com", input, StringComparison.Ordinal);
        Assert.DoesNotContain("abc.def", input, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key", input, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", output, StringComparison.Ordinal);
        Assert.Contains("query safe", input, StringComparison.Ordinal);
        Assert.Contains("result ok", output, StringComparison.Ordinal);
    }

    [Fact]
    public void NonSensitiveTargetedValuesRemainUnchanged()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionDefaultsTests.Safe");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction()
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("tool"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, "show public invoice count");
        }

        provider.ForceFlush();

        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.Equal("show public invoice count", captured.Tags[ToolAttributeNames.ToolInput]);
    }
}
