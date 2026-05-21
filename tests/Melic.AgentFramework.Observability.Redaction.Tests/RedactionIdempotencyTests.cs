// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionIdempotencyTests
{
    [Fact]
    public void AlreadyRedactedValuesRemainStable()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionIdempotencyTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction()
            .AddTelemetryRedaction()
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, "contact [REDACTED]");
        }

        provider.ForceFlush();

        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.Equal("contact [REDACTED]", captured.Tags[ToolAttributeNames.ToolInput]);
    }
}
