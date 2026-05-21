// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionMalformedJsonTests
{
    [Fact]
    public void MalformedJsonFallsBackToStringRedaction()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionMalformedJsonTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction()
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, "{\"email\":\"person@example.com\"");
        }

        provider.ForceFlush();

        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        string redacted = Assert.IsType<string>(captured.Tags[ToolAttributeNames.ToolInput]);
        Assert.DoesNotContain("person@example.com", redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
    }
}
