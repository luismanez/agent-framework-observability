// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionExceptionMessageTests
{
    [Fact]
    public void ExceptionMessageIsPreservedByDefault()
    {
        CapturedActivity captured = Export(null);

        Assert.Equal("failed for person@example.com", captured.Tags["exception.message"]);
    }

    [Fact]
    public void ExceptionMessageIsRedactedWhenEnabled()
    {
        CapturedActivity captured = Export(options => options.RedactExceptionMessages = true);

        Assert.Equal("failed for [REDACTED]", captured.Tags["exception.message"]);
    }

    private static CapturedActivity Export(Action<RedactionOptions>? configure)
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionExceptionMessageTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction(configure)
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag("exception.message", "failed for person@example.com");
        }

        provider.ForceFlush();
        return Assert.Single(exporter.ExportedActivities);
    }
}
