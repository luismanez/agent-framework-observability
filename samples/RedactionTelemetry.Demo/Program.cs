// SPDX-License-Identifier: MIT

// -----------------------------------------------------------------------------
// RedactionTelemetry.Demo - emits local Activity telemetry and exports it to the
// console or to Azure Application Insights after the Redaction processor runs.
//
// Optional:
//   APPLICATIONINSIGHTS_CONNECTION_STRING
//       When set, traces are sent to App Insights instead of Console.
//
// Scenarios:
//   1. Default tool payload redaction: genai.tool.input/output only
//   2. MAF sensitive-data preset: gen_ai.* prompt/response/tool payloads
//   3. Custom scope and rules: session business attributes and app tags
//   4. Bounded fail-closed behavior: over-limit values replaced before export
// -----------------------------------------------------------------------------

using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;

const string DemoSourceName = "RedactionTelemetry.Demo";

var appInsightsConnStr = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
bool useAppInsights = !string.IsNullOrWhiteSpace(appInsightsConnStr);

Banner(useAppInsights
    ? "Exporting redaction telemetry to Azure Application Insights"
    : "Exporting redaction telemetry to Console  (set APPLICATIONINSIGHTS_CONNECTION_STRING to switch to App Insights)");

using var activitySource = new ActivitySource(DemoSourceName);

// -----------------------------------------------------------------------------
// Scenario 1 - Default tool payload redaction
// -----------------------------------------------------------------------------

RunScenario(
    "Scenario 1 - Default tool payload redaction",
    options => options.EnableDiagnostics = true,
    source =>
    {
        using Activity? activity = source.StartActivity("redaction.default_tool_payloads");
        activity?.SetTag(SessionAttributeNames.SessionId, "conv-demo-001");
        activity?.SetTag("app.note", "person@example.com is unchanged because app.note is not targeted");
        activity?.SetTag(ToolAttributeNames.ToolInput, "{\"email\":\"customer@example.com\",\"password\":\"secret\",\"query\":\"show invoice status\"}");
        activity?.SetTag(ToolAttributeNames.ToolOutput, "Authorization: Bearer abc.def result ok");
        activity?.SetTag("gen_ai.prompt", "person@example.com remains unchanged until standard attributes are opted in");
    });

Console.WriteLine("Check span: genai.tool.input/output are redacted; app.note and gen_ai.prompt are unchanged.");
Console.WriteLine();

// -----------------------------------------------------------------------------
// Scenario 2 - MAF sensitive-data preset
// -----------------------------------------------------------------------------

RunScenario(
    "Scenario 2 - MAF sensitive-data preset",
    options =>
    {
        options.IncludeMafSensitiveDataAttributes();
        options.RedactExceptionMessages = true;
        options.EnableDiagnostics = true;
    },
    source =>
    {
        using Activity? activity = source.StartActivity("redaction.maf_sensitive_data");

        // These represent the kind of official gen_ai.* attributes MAF and
        // Microsoft.Extensions.AI may emit when EnableSensitiveData=true.
        activity?.SetTag("gen_ai.prompt", "Please help customer@example.com with invoice INV-42.");
        activity?.SetTag("gen_ai.response.text", "Customer customer@example.com has invoice INV-42 ready for review.");
        activity?.SetTag("gen_ai.tool.call.arguments", "{\"email\":\"customer@example.com\",\"apiKey\":\"secret-123\"}");
        activity?.SetTag("gen_ai.tool.call.result", "Bearer abc.def returned a successful lookup");
        activity?.SetTag("exception.message", "Downstream lookup failed for customer@example.com");
    });

Console.WriteLine("Check span: gen_ai.* prompt/response/tool attributes and exception.message are redacted.");
Console.WriteLine();

// -----------------------------------------------------------------------------
// Scenario 3 - Custom scope and custom rules
// -----------------------------------------------------------------------------

RunScenario(
    "Scenario 3 - Custom scope and rules",
    options =>
    {
        options.IncludeAttribute("app.customer.profile");
        options.IncludeAttributesWithPrefix("genai.session.customer_");
        options.AddSensitiveFieldName("customerSecret");
        options.AddPatternRule("account-number", @"acct_[0-9]{12}");
        options.EnableDiagnostics = true;
    },
    source =>
    {
        using Activity? activity = source.StartActivity("redaction.custom_policy");
        activity?.SetTag(SessionAttributeNames.SessionId, "conv-demo-002");
        activity?.SetTag("genai.session.customer_email", "customer@example.com");
        activity?.SetTag("app.customer.profile", "{\"customerSecret\":\"secret\",\"account\":\"acct_123456789012\",\"tier\":\"gold\"}");
        activity?.SetTag("app.unrelated", "acct_123456789012 is unchanged because app.unrelated is not targeted");
    });

Console.WriteLine("Check span: opted-in session/app attributes are redacted; unrelated app tags are unchanged.");
Console.WriteLine();

// -----------------------------------------------------------------------------
// Scenario 4 - Bounded fail-closed behavior
// -----------------------------------------------------------------------------

RunScenario(
    "Scenario 4 - Bounded fail-closed behavior",
    options =>
    {
        options.MaxValueLength = 48;
        options.EnableDiagnostics = true;
    },
    source =>
    {
        using Activity? activity = source.StartActivity("redaction.fail_closed_bounds");
        activity?.SetTag(ToolAttributeNames.ToolInput, "customer@example.com sent a long payload with repeated details that exceeds the configured redaction bound");
        activity?.SetTag(ToolAttributeNames.ToolOutput, "short output for customer@example.com");
    });

Console.WriteLine("Check span: over-limit genai.tool.input is replaced entirely; shorter output is redacted normally.");
Console.WriteLine();

Banner("All scenarios complete");
Console.WriteLine("Check your telemetry backend:");
Console.WriteLine("  Console      : scroll up for spans named redaction.*");
Console.WriteLine("  App Insights : Transaction Search -> filter by name startswith redaction");
Console.WriteLine($"  Diagnostics  : {RedactionAttributeNames.Applied}, {RedactionAttributeNames.MatchCount}, {RedactionAttributeNames.FailureCount}");

if (useAppInsights)
{
    Console.WriteLine();
    Console.WriteLine("Azure Monitor exporter batches telemetry; waiting briefly for in-flight sends...");
    await Task.Delay(TimeSpan.FromSeconds(5));
    Console.WriteLine("Done - data should appear in App Insights within ~1-2 minutes.");
}

void RunScenario(string name, Action<RedactionOptions>? configureRedaction, Action<ActivitySource> emitTelemetry)
{
    Banner(name);

    using TracerProvider tracerProvider = BuildTracerProvider(configureRedaction);
    emitTelemetry(activitySource);
    tracerProvider.ForceFlush(15_000);
}

TracerProvider BuildTracerProvider(Action<RedactionOptions>? configureRedaction)
{
    TracerProviderBuilder traceBuilder = Sdk.CreateTracerProviderBuilder()
        .AddSource(DemoSourceName)
        .AddTelemetryRedaction(configureRedaction);

    if (useAppInsights)
    {
        traceBuilder.AddAzureMonitorTraceExporter(options => options.ConnectionString = appInsightsConnStr!);
    }
    else
    {
        traceBuilder.AddConsoleExporter();
    }

    return traceBuilder.Build()!;
}

static void Banner(string text)
{
    string line = new('=', text.Length + 4);
    Console.WriteLine(line);
    Console.WriteLine($"  {text}");
    Console.WriteLine(line);
    Console.WriteLine();
}
