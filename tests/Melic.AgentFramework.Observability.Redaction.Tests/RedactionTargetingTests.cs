// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionTargetingTests
{
    [Fact]
    public void IncludeAttributeTargetsCustomAttribute()
    {
        CapturedActivity captured = Export(options => options.IncludeAttribute("app.private"));

        Assert.Equal("[REDACTED]", captured.Tags["app.private"]);
    }

    [Fact]
    public void ExcludeAttributeWinsOverInclusion()
    {
        CapturedActivity captured = Export(options =>
        {
            options.IncludeAttribute("app.private");
            options.ExcludeAttribute("app.private");
        });

        Assert.Equal("person@example.com", captured.Tags["app.private"]);
    }

    [Fact]
    public void BuiltInSessionAttributesAreUnchangedByDefault()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionTargetingTests.SessionDefault");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction()
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(SessionAttributeNames.SessionId, "person@example.com");
        }

        provider.ForceFlush();
        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.Equal("person@example.com", captured.Tags[SessionAttributeNames.SessionId]);
    }

    [Fact]
    public void SessionPrefixCanBeExplicitlyTargetedForCustomValues()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionTargetingTests.SessionOptIn");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction(options => options.IncludeAttributesWithPrefix("genai.session."))
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag("genai.session.customer_email", "person@example.com");
        }

        provider.ForceFlush();
        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.Equal("[REDACTED]", captured.Tags["genai.session.customer_email"]);
    }

    private static CapturedActivity Export(Action<RedactionOptions> configure)
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionTargetingTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction(configure)
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag("app.private", "person@example.com");
        }

        provider.ForceFlush();
        return Assert.Single(exporter.ExportedActivities);
    }
}
