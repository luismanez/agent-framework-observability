// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionDiagnosticsTests
{
    [Fact]
    public void DiagnosticsAreAggregateOnlyWhenEnabled()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionDiagnosticsTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction(options => options.EnableDiagnostics = true)
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, "person@example.com");
        }

        provider.ForceFlush();

        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.Equal(true, captured.Tags[RedactionAttributeNames.Applied]);
        Assert.Equal(1, captured.Tags[RedactionAttributeNames.MatchCount]);
        Assert.Equal(0, captured.Tags[RedactionAttributeNames.FailureCount]);
        Assert.DoesNotContain(captured.Tags.Keys, key => key.Contains("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(captured.Tags.Values.OfType<string>(), value => value.Contains("person@example.com", StringComparison.Ordinal));
    }

    [Fact]
    public void DiagnosticsAreOmittedByDefault()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionDiagnosticsTests.Omitted");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction()
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, "person@example.com");
        }

        provider.ForceFlush();

        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.False(captured.Tags.ContainsKey(RedactionAttributeNames.Applied));
    }
}
