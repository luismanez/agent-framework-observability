// SPDX-License-Identifier: MIT

// ─────────────────────────────────────────────────────────────────────────────
// SessionTelemetry.Demo — runs three scenarios against a real Foundry/Azure
// OpenAI endpoint and exports the enriched telemetry to the console or to
// Azure Application Insights.
//
// Required environment variables:
//   AZURE_OPENAI_ENDPOINT           Foundry or Azure OpenAI endpoint URL
//   AZURE_OPENAI_API_KEY            API key for the endpoint
//   AZURE_OPENAI_DEPLOYMENT_NAME    Model deployment name (default: gpt-4o-mini)
//
// Optional:
//   APPLICATIONINSIGHTS_CONNECTION_STRING
//       When set, traces and metrics are sent to App Insights instead of Console.
//       Find it in the Azure Portal → your App Insights resource → Overview → Connection string.
//
// Scenarios:
//   1. Mode A — three-turn session: stable ID, incrementing invocation_index, token aggregates
//   2. Custom session ID + business tags: tenant_id, user_tier propagated on every span
//   3. Mode B — explicit session span: all turns appear as children of a root session span
// ─────────────────────────────────────────────────────────────────────────────

using Azure;
using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.Exporter;
using Melic.AgentFramework.Observability.Sessions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// ── Configuration ─────────────────────────────────────────────────────────────

var endpoint   = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required.");
var apiKey     = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
    ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY is required.");
var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
var appInsightsConnStr = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

bool useAppInsights = !string.IsNullOrWhiteSpace(appInsightsConnStr);
Banner(useAppInsights
    ? $"Exporting telemetry to Azure Application Insights"
    : $"Exporting telemetry to Console  (set APPLICATIONINSIGHTS_CONNECTION_STRING to switch to App Insights)");

// ── OpenTelemetry setup ───────────────────────────────────────────────────────
//
// ActivitySource name  : "Melic.AgentFramework.Observability.Sessions"
// Meter name           : "Melic.AgentFramework.Observability.Sessions"
//
// In App Insights these appear as:
//   Traces  → Transaction search → filter by customDimensions["genai.session.id"]
//   Metrics → Metrics Explorer   → session.invocations, session.duration, session.active

var traceBuilder = Sdk.CreateTracerProviderBuilder()
    .AddSource("Melic.AgentFramework.Observability.Sessions");

var metricsBuilder = Sdk.CreateMeterProviderBuilder()
    .AddMeter("Melic.AgentFramework.Observability.Sessions");

if (useAppInsights)
{
    traceBuilder.AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsightsConnStr!);
    metricsBuilder.AddAzureMonitorMetricExporter(o => o.ConnectionString = appInsightsConnStr!);
}
else
{
    traceBuilder.AddConsoleExporter();
    metricsBuilder.AddConsoleExporter();
}

using var tracerProvider  = traceBuilder.Build()!;
using var meterProvider   = metricsBuilder.Build()!;

// ── Build the instrumented agent ──────────────────────────────────────────────
//
// Architecture: AzureOpenAIClient → ChatClient → AIAgent (inner)
//                                              ↑ wrapped by SessionTelemetryAgent

AIAgent innerAgent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deployment)
    .AsIChatClient()
    .AsAIAgent(
        instructions: "You are a helpful assistant. Answer in one sentence only.",
        name: "SessionDemoAgent");

AIAgent agent = new AIAgentBuilder(innerAgent)
    .UseSessionTelemetry(opt =>
    {
        opt.TrackTokenAggregates = true;   // accumulate token counts across turns
        opt.EnableSessionSpan    = true;   // allow Mode B (used in Scenario 3)
    })
    .Build();

// ═════════════════════════════════════════════════════════════════════════════
// Scenario 1 — Mode A: multi-turn session
//
// What to look for in telemetry:
//   • genai.session.id          — same GUID on all three spans
//   • genai.session.invocation_index  — 1, 2, 3
//   • genai.session.total_input_tokens  — accumulating across turns
//   • genai.session.total_output_tokens — accumulating across turns
//   • genai.session.age_seconds  — 0 on first span, positive on later spans
// ═════════════════════════════════════════════════════════════════════════════

Banner("Scenario 1 — Mode A: multi-turn session");

var session1 = await agent.CreateSessionAsync();

var r1 = await agent.RunAsync([new ChatMessage(ChatRole.User, "What is the capital of France?")], session1);
Console.WriteLine($"[Turn 1] {r1.Text}");
Console.WriteLine($"  session.id = {session1.GetSessionId()}");
Console.WriteLine();

var r2 = await agent.RunAsync([new ChatMessage(ChatRole.User, "What is its approximate population?")], session1);
Console.WriteLine($"[Turn 2] {r2.Text}");
Console.WriteLine($"  session.id = {session1.GetSessionId()}  (same — stable)");
Console.WriteLine();

var r3 = await agent.RunAsync([new ChatMessage(ChatRole.User, "Name one famous landmark there.")], session1);
Console.WriteLine($"[Turn 3] {r3.Text}");
Console.WriteLine($"  session.id = {session1.GetSessionId()}  (same — stable)");
Console.WriteLine($"  [Check spans: invocation_index=3, token totals accumulated over 3 turns]");
Console.WriteLine();

// ═════════════════════════════════════════════════════════════════════════════
// Scenario 2 — Custom session ID + business tags
//
// What to look for in telemetry:
//   • genai.session.id = "conv-demo-contoso-001"  (your value, not a GUID)
//   • tenant_id = contoso
//   • user_tier = premium
//   • region    = westeurope
//   All four attributes appear on every span for this session.
// ═════════════════════════════════════════════════════════════════════════════

Banner("Scenario 2 — Custom session ID + business tags");

var session2 = await agent.CreateSessionAsync();

// Pre-assign a caller-provided correlation ID before the first invocation.
session2.AssignSessionId("conv-demo-contoso-001");

// Attach business tags once — they propagate automatically on every span.
session2.SetSessionTag("tenant_id", "contoso");
session2.SetSessionTag("user_tier", "premium");
session2.SetSessionTag("region",    "westeurope");

Console.WriteLine($"Assigned session ID: {session2.GetSessionId()}");
Console.WriteLine();

var r4 = await agent.RunAsync([new ChatMessage(ChatRole.User, "Hello, I'm a premium user from Contoso.")], session2);
Console.WriteLine($"[Turn 1] {r4.Text}");
Console.WriteLine($"  [Check span: custom tags tenant_id, user_tier, region all present]");
Console.WriteLine();

var r5 = await agent.RunAsync([new ChatMessage(ChatRole.User, "What Azure regions do you know?")], session2);
Console.WriteLine($"[Turn 2] {r5.Text}");
Console.WriteLine($"  [Check span: same 3 custom tags still propagated — no re-attachment needed]");
Console.WriteLine();

// ═════════════════════════════════════════════════════════════════════════════
// Scenario 3 — Mode B: explicit session span as trace root
//
// What to look for in telemetry:
//   • A root span named "agent_session SessionDemoAgent" (the session span)
//   • Two child spans "invoke_agent" — one per turn — parented to the root span
//   • On session span Dispose: a "session.ended" span event + final aggregates:
//       genai.session.total_invocations = 2
//       genai.session.duration_seconds  = elapsed time
//       genai.session.total_input_tokens  (accumulated)
//       genai.session.total_output_tokens (accumulated)
//
// In App Insights end-to-end transaction view you will see the full trace tree.
// ═════════════════════════════════════════════════════════════════════════════

Banner("Scenario 3 — Mode B: explicit session span (trace root)");

var session3 = await agent.CreateSessionAsync();

// Options must have EnableSessionSpan = true to open the root span.
var modeBOptions = new SessionTelemetryOptions { EnableSessionSpan = true };

using (agent.BeginSessionTrace(session3, modeBOptions))
{
    Console.WriteLine("[Root session span opened — invocations below are children in the trace tree]");
    Console.WriteLine();

    var r6 = await agent.RunAsync([new ChatMessage(ChatRole.User, "Tell me a fun fact about Spain.")], session3);
    Console.WriteLine($"[Turn 1] {r6.Text}");

    var r7 = await agent.RunAsync([new ChatMessage(ChatRole.User, "And one about Portugal.")], session3);
    Console.WriteLine($"[Turn 2] {r7.Text}");

    Console.WriteLine();
    Console.WriteLine("[Disposing root span — final aggregates written: total_invocations=2, session_duration, error_count=0]");
}

Console.WriteLine();
Banner("All scenarios complete");
Console.WriteLine("Check your telemetry backend:");
Console.WriteLine("  Console : scroll up for the OTel span output above");
Console.WriteLine("  App Insights: Transaction Search → filter by customDimensions[\"genai.session.id\"]");
Console.WriteLine("                Metrics Explorer  → Namespace: Melic.AgentFramework.Observability.Sessions");

// ── Helpers ───────────────────────────────────────────────────────────────────

static void Banner(string text)
{
    var line = new string('═', text.Length + 4);
    Console.WriteLine(line);
    Console.WriteLine($"  {text}");
    Console.WriteLine(line);
    Console.WriteLine();
}
