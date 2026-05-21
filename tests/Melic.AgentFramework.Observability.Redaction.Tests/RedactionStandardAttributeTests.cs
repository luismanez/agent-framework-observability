// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionStandardAttributeTests
{
    [Fact]
    public void StandardAttributesAreUnchangedByDefault()
    {
        CapturedActivity captured = Export(null);

        Assert.Equal("person@example.com", captured.Tags["gen_ai.prompt"]);
    }

    [Fact]
    public void SelectedStandardAttributeCanBeRedacted()
    {
        CapturedActivity captured = Export(options => options.IncludeStandardAttribute("gen_ai.prompt"));

        Assert.Equal("[REDACTED]", captured.Tags["gen_ai.prompt"]);
    }

    [Fact]
    public void MafSensitiveDataAttributesPresetRedactsStandardAttributes()
    {
        CapturedActivity captured = Export(options => options.IncludeMafSensitiveDataAttributes());

        Assert.Equal("[REDACTED]", captured.Tags["gen_ai.prompt"]);
        Assert.Equal("assistant response for [REDACTED]", captured.Tags["gen_ai.response.text"]);
    }

    private static CapturedActivity Export(Action<RedactionOptions>? configure)
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionStandardAttributeTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction(configure)
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag("gen_ai.prompt", "person@example.com");
            activity?.SetTag("gen_ai.response.text", "assistant response for person@example.com");
        }

        provider.ForceFlush();
        return Assert.Single(exporter.ExportedActivities);
    }
}
