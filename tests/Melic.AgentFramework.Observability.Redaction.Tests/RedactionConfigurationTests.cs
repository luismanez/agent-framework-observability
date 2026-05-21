// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionConfigurationTests
{
    [Fact]
    public void CustomPatternRuleRedactsConfiguredValues()
    {
        CapturedActivity captured = Export("app.account", "acct_123456789012", options =>
        {
            options.IncludeAttribute("app.account");
            options.AddPatternRule("account", @"acct_[0-9]{12}");
        });

        Assert.Equal("[REDACTED]", captured.Tags["app.account"]);
    }

    [Fact]
    public void CustomFieldNameRuleRedactsStructuredValues()
    {
        CapturedActivity captured = Export("app.payload", "{\"customerSecret\":\"open-sesame\",\"query\":\"ok\"}", options =>
        {
            options.IncludeAttribute("app.payload");
            options.AddSensitiveFieldName("customerSecret");
        });

        string payload = Assert.IsType<string>(captured.Tags["app.payload"]);
        Assert.Contains("\"customerSecret\":\"[REDACTED]\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"query\":\"ok\"", payload, StringComparison.Ordinal);
    }

    private static CapturedActivity Export(string attributeName, string value, Action<RedactionOptions> configure)
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionConfigurationTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction(configure)
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(attributeName, value);
        }

        provider.ForceFlush();
        return Assert.Single(exporter.ExportedActivities);
    }
}
