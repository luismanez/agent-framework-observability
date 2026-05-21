// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;

const string DemoSourceName = "RedactionTelemetry.Demo";

Console.WriteLine("Redaction telemetry demo");
Console.WriteLine("Console exporter output should show redacted tool payloads and MAF sensitive-data attributes.");
Console.WriteLine();

using var activitySource = new ActivitySource(DemoSourceName);
using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(DemoSourceName)
    .AddTelemetryRedaction(options =>
    {
        options.EnableDiagnostics = true;
        options.RedactExceptionMessages = true;
        options.IncludeMafSensitiveDataAttributes();
        options.IncludeAttributesWithPrefix("genai.session.customer_");
    })
    .AddConsoleExporter()
    .Build();

using (Activity? activity = activitySource.StartActivity("redaction.default_tool_payloads"))
{
    activity?.SetTag(SessionAttributeNames.SessionId, "conv-demo-001");
    activity?.SetTag("genai.session.customer_email", "customer@example.com");
    activity?.SetTag(ToolAttributeNames.ToolInput, "{\"email\":\"customer@example.com\",\"password\":\"secret\",\"query\":\"show invoice status\"}");
    activity?.SetTag(ToolAttributeNames.ToolOutput, "Authorization: Bearer abc.def result ok");
    activity?.SetTag("gen_ai.prompt", "Please help customer@example.com with invoice INV-42.");
    activity?.SetTag("gen_ai.response.text", "Customer customer@example.com has invoice INV-42 ready for review.");
    activity?.SetTag("exception.message", "Downstream lookup failed for customer@example.com");
}

using (Activity? activity = activitySource.StartActivity("redaction.default_scope"))
{
    activity?.SetTag(SessionAttributeNames.SessionId, "safe-session-id");
    activity?.SetTag("app.note", "person@example.com is unchanged because app.note is not targeted");
    activity?.SetTag(ToolAttributeNames.ToolInput, "person@example.com is redacted because tool input is targeted");
}

tracerProvider.ForceFlush();

Console.WriteLine();
Console.WriteLine("Look for these exported tags:");
Console.WriteLine($"  {ToolAttributeNames.ToolInput} / {ToolAttributeNames.ToolOutput}: sensitive values replaced");
Console.WriteLine("  gen_ai.prompt / gen_ai.response.text: redacted because IncludeMafSensitiveDataAttributes() was enabled");
Console.WriteLine($"  {RedactionAttributeNames.Applied}, {RedactionAttributeNames.MatchCount}, {RedactionAttributeNames.FailureCount}: aggregate diagnostics only");
