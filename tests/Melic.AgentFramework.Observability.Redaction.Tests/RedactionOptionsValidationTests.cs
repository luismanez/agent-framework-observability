// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionOptionsValidationTests
{
    [Fact]
    public void EmptyReplacementTextIsRejectedAtRegistration()
    {
        using var source = new ActivitySource("RedactionOptionsValidationTests.EmptyReplacement");

        Assert.ThrowsAny<ArgumentException>(() =>
        {
            using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddSource(source.Name)
                .AddTelemetryRedaction(options => options.ReplacementText = "")
                .Build();
        });
    }

    [Fact]
    public void NonPositiveMaxValueLengthIsRejectedAtRegistration()
    {
        using var source = new ActivitySource("RedactionOptionsValidationTests.MaxLength");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddSource(source.Name)
                .AddTelemetryRedaction(options => options.MaxValueLength = 0)
                .Build();
        });
    }

    [Fact]
    public void DuplicateCustomRuleNamesAreRejectedAtRegistration()
    {
        using var source = new ActivitySource("RedactionOptionsValidationTests.Duplicate");

        Assert.Throws<ArgumentException>(() =>
        {
            using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddSource(source.Name)
                .AddTelemetryRedaction(options =>
                {
                    options.AddPatternRule("same", "a");
                    options.AddPatternRule("same", "b");
                })
                .Build();
        });
    }

    [Fact]
    public void InvalidCustomRegexIsRejectedAtRegistration()
    {
        using var source = new ActivitySource("RedactionOptionsValidationTests.InvalidRegex");

        Assert.ThrowsAny<ArgumentException>(() =>
        {
            using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
                .AddSource(source.Name)
                .AddTelemetryRedaction(options => options.AddPatternRule("bad", "["))
                .Build();
        });
    }

    [Fact]
    public void DisablingDefaultRulesOmitsBuiltInsButKeepsCustomRules()
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionOptionsValidationTests.DisableDefaults");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction(options =>
            {
                options.EnableDefaultRules = false;
                options.AddPatternRule("account", @"acct_[0-9]{12}");
            })
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, "person@example.com acct_123456789012");
        }

        provider.ForceFlush();
        CapturedActivity captured = Assert.Single(exporter.ExportedActivities);
        Assert.Equal("person@example.com [REDACTED]", captured.Tags[ToolAttributeNames.ToolInput]);
    }
}
