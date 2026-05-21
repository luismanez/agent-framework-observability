// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Text.Json;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

public sealed class RedactionJsonPayloadTests
{
    public static TheoryData<string> ValidJsonCorpus => new()
    {
        "{\"email\":\"person@example.com\",\"query\":\"public\"}",
        "{\"profile\":{\"password\":\"secret\",\"age\":42}}",
        "[\"person@example.com\",\"public\"]",
        "[{\"apiKey\":\"abc123\"},{\"ok\":true}]",
        "{\"maybe\":null,\"password\":\"secret\"}",
        "{\"count\":5,\"token\":\"abc123\"}",
        "{\"enabled\":true,\"secret\":\"abc123\"}",
        "{\"message\":\"Bearer abc.def\"}",
        "{\"query\":\"show invoices\"}"
    };

    [Theory]
    [MemberData(nameof(ValidJsonCorpus))]
    public void ValidJsonCorpusRemainsValidJsonAfterRedaction(string payload)
    {
        CapturedActivity captured = ExportPayload(payload);
        string redacted = Assert.IsType<string>(captured.Tags[ToolAttributeNames.ToolInput]);

        using JsonDocument document = JsonDocument.Parse(redacted);
        Assert.NotEqual(JsonValueKind.Undefined, document.RootElement.ValueKind);
        Assert.DoesNotContain("person@example.com", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc.def", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(":\"abc123\"", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(":\"secret\"", redacted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SensitiveJsonFieldsAreReplacedAndNonSensitiveFieldsRemain()
    {
        CapturedActivity captured = ExportPayload("{\"password\":\"secret\",\"query\":\"show invoices\"}");
        string redacted = Assert.IsType<string>(captured.Tags[ToolAttributeNames.ToolInput]);

        Assert.Contains("\"password\":\"[REDACTED]\"", redacted, StringComparison.Ordinal);
        Assert.Contains("\"query\":\"show invoices\"", redacted, StringComparison.Ordinal);
    }

    private static CapturedActivity ExportPayload(string payload)
    {
        var exporter = new RedactionTestExporter();
        using var source = new ActivitySource("RedactionJsonPayloadTests");
        using TracerProvider provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source.Name)
            .AddTelemetryRedaction()
            .AddProcessor(new SimpleActivityExportProcessor(exporter))
            .Build();

        using (Activity? activity = source.StartActivity("span"))
        {
            activity?.SetTag(ToolAttributeNames.ToolInput, payload);
        }

        provider.ForceFlush();
        return Assert.Single(exporter.ExportedActivities);
    }
}
