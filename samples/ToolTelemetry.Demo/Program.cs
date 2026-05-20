// SPDX-License-Identifier: MIT

// ─────────────────────────────────────────────────────────────────────────────
// ToolTelemetry.Demo — runs several function-tool scenarios against a real
// Foundry/Azure OpenAI endpoint and exports tool-call telemetry to the console
// or to Azure Application Insights.
//
// Required environment variables:
//   AZURE_OPENAI_ENDPOINT           Foundry or Azure OpenAI endpoint URL
//   AZURE_OPENAI_API_KEY            API key for the endpoint
//   AZURE_OPENAI_DEPLOYMENT_NAME    Model deployment name (default: gpt-4o-mini)
//
// Optional:
//   APPLICATIONINSIGHTS_CONNECTION_STRING
//       When set, traces are sent to App Insights instead of Console.
//   MAF_ENABLE_SENSITIVE_DATA
//       When true, MAF's built-in execute_tool spans include arguments/results.
//
// Scenarios:
//   1. Default capture: input/output payloads plus status and duration
//   2. Payload capture disabled: timing/status/name only
//   3. Bounded payloads on MAF execute_tool spans
//   4. Failing tool: ERROR status and structured error payload
// ─────────────────────────────────────────────────────────────────────────────

using System.ComponentModel;
using System.Diagnostics;
using Azure;
using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.Exporter;
using Melic.AgentFramework.Observability.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Trace;

const string DefaultToolSourceName = "Melic.AgentFramework.Observability.Tools";
const string DemoSourceName = "ToolTelemetry.Demo";

// ── Configuration ─────────────────────────────────────────────────────────────

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required.");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
    ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY is required.");
var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
var appInsightsConnStr = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
var enableMafSensitiveData = IsEnabled(Environment.GetEnvironmentVariable("MAF_ENABLE_SENSITIVE_DATA"));

bool useAppInsights = !string.IsNullOrWhiteSpace(appInsightsConnStr);
Banner(useAppInsights
    ? "Exporting tool telemetry to Azure Application Insights"
    : "Exporting tool telemetry to Console  (set APPLICATIONINSIGHTS_CONNECTION_STRING to switch to App Insights)");
Console.WriteLine($"MAF sensitive data telemetry: {(enableMafSensitiveData ? "enabled" : "disabled")}");
Console.WriteLine("Set MAF_ENABLE_SENSITIVE_DATA=true to also let MAF emit gen_ai.tool.call.arguments/result on execute_tool spans.");
Console.WriteLine();

// ── OpenTelemetry setup ───────────────────────────────────────────────────────

var traceBuilder = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource(DefaultToolSourceName)
    .AddSource(DemoSourceName);

if (useAppInsights)
{
    traceBuilder.AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsightsConnStr!);
}
else
{
    traceBuilder.AddConsoleExporter();
}

using var tracerProvider = traceBuilder.Build()!;
using var demoActivitySource = new ActivitySource(DemoSourceName);

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deployment)
    .AsIChatClient();

IList<AITool> tools =
[
    AIFunctionFactory.Create(GetOrderStatusAsync, name: "get_order_status"),
    AIFunctionFactory.Create(SearchCatalogAsync, name: "search_catalog"),
    AIFunctionFactory.Create(GetLargeDiagnosticPayloadAsync, name: "get_large_diagnostic_payload"),
    AIFunctionFactory.Create(FailInventoryReservationAsync, name: "fail_inventory_reservation")
];

// ═════════════════════════════════════════════════════════════════════════════
// Scenario 1 — Default tool telemetry
// ═════════════════════════════════════════════════════════════════════════════

Banner("Scenario 1 — Default tool telemetry");

AIAgent defaultAgent = BuildAgent(
    chatClient,
    tools,
    name: "ToolTelemetryDefaultAgent",
    enableMafSensitiveData,
    configureToolTelemetry: null);

using (demoActivitySource.StartActivity("scenario.default_capture"))
{
    var response = await defaultAgent.RunAsync("Use the get_order_status tool for order 42 and summarize the result in one sentence.");
    Console.WriteLine(response.Text);
}

Console.WriteLine("Check MAF execute_tool span: enriched with genai.tool.input and genai.tool.output.");
Console.WriteLine();

// ═════════════════════════════════════════════════════════════════════════════
// Scenario 2 — Payload capture disabled
// ═════════════════════════════════════════════════════════════════════════════

Banner("Scenario 2 — Payload capture disabled");

AIAgent noPayloadAgent = BuildAgent(
    chatClient,
    tools,
    name: "ToolTelemetryNoPayloadAgent",
    enableMafSensitiveData,
    configureToolTelemetry: options =>
    {
        options.CaptureInput = false;
        options.CaptureOutput = false;
    });

using (demoActivitySource.StartActivity("scenario.no_payload_capture"))
{
    var response = await noPayloadAgent.RunAsync("Use the search_catalog tool to find two coffee products, then summarize only their names.");
    Console.WriteLine(response.Text);
}

Console.WriteLine("Check span: tool name/status/duration are present; input/output attributes are omitted.");
Console.WriteLine();

// ═════════════════════════════════════════════════════════════════════════════
// Scenario 3 — Bounded payloads on MAF execute_tool spans
// ═════════════════════════════════════════════════════════════════════════════

Banner("Scenario 3 — Bounded payloads");

AIAgent boundedPayloadAgent = BuildAgent(
    chatClient,
    tools,
    name: "ToolTelemetryBoundedPayloadAgent",
    enableMafSensitiveData,
    configureToolTelemetry: options =>
    {
        options.MaxInputLength = 128;
        options.MaxOutputLength = 256;
    });

using (demoActivitySource.StartActivity("scenario.custom_source_bounded_payloads"))
{
    var response = await boundedPayloadAgent.RunAsync("Use get_large_diagnostic_payload for order 42. Then say whether diagnostics were returned.");
    Console.WriteLine(response.Text);
}

Console.WriteLine("Check MAF execute_tool span: genai.tool.input/output remain valid JSON within configured limits.");
Console.WriteLine();

// ═════════════════════════════════════════════════════════════════════════════
// Scenario 4 — Failing tool
// ═════════════════════════════════════════════════════════════════════════════

Banner("Scenario 4 — Failing tool");

AIAgent failureAgent = BuildAgent(
    chatClient,
    tools,
    name: "ToolTelemetryFailureAgent",
    enableMafSensitiveData,
    configureToolTelemetry: null);

using (demoActivitySource.StartActivity("scenario.failing_tool"))
{
    try
    {
        var response = await failureAgent.RunAsync("Use the fail_inventory_reservation tool for SKU espresso-001 and order 42.");
        Console.WriteLine(response.Text);
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Tool failure was preserved for the caller: {ex.Message}");
    }
}

Console.WriteLine("Check span: status ERROR and genai.tool.output contains structured error JSON.");
Console.WriteLine();

Banner("All scenarios complete");
Console.WriteLine("Check your telemetry backend:");
Console.WriteLine("  Console      : scroll up for execute_tool spans with MAF gen_ai.tool.* identity and package genai.tool.* enrichment");
Console.WriteLine("  App Insights : Transaction Search → filter by name == execute_tool");
Console.WriteLine("                 Inspect customDimensions[\"gen_ai.tool.name\"] for identity and genai.tool.input/output for package enrichment");

if (useAppInsights)
{
    Console.WriteLine("\nFlushing telemetry to App Insights (may take a few seconds)...");
    tracerProvider.ForceFlush(15_000);
    await Task.Delay(TimeSpan.FromSeconds(5));
    Console.WriteLine("Done — data should appear in App Insights within ~1-2 minutes.");
}

// ── Agent setup ───────────────────────────────────────────────────────────────

static AIAgent BuildAgent(
    IChatClient chatClient,
    IList<AITool> tools,
    string name,
    bool enableMafSensitiveData,
    Action<ToolTelemetryOptions>? configureToolTelemetry)
{
    AIAgent innerAgent = chatClient.AsAIAgent(
        instructions: "You are a concise support assistant. When the user asks about orders, catalog, diagnostics, or inventory reservations, call the matching tool before answering.",
        name: name,
        tools: tools);

    return new AIAgentBuilder(innerAgent)
        .UseOpenTelemetry(configure: otel =>
        {
            otel.EnableSensitiveData = enableMafSensitiveData;
        })
        .UseToolTelemetry(configureToolTelemetry)
        .Build();
}

// ── Demo tools ────────────────────────────────────────────────────────────────

[Description("Get order status, shipment, and fulfillment information for an order.")]
static async Task<OrderStatus> GetOrderStatusAsync([Description("The order id to inspect.")] string orderId)
{
    await Task.Delay(150);
    return new OrderStatus(orderId, "shipped", "ZX-441", DateTimeOffset.UtcNow.AddDays(2));
}

[Description("Search the product catalog and return a small set of matching products.")]
static async Task<IReadOnlyList<ProductSuggestion>> SearchCatalogAsync(
    [Description("The product search text.")] string query,
    [Description("The maximum number of products to return.")] int maxResults = 2)
{
    await Task.Delay(100);

    ProductSuggestion[] products =
    [
        new("espresso-001", "Contoso Espresso Blend", 12.99m),
        new("mug-042", "Insulated Travel Mug", 18.50m),
        new("filter-007", "Reusable Coffee Filter", 9.25m)
    ];

    return products.Take(Math.Clamp(maxResults, 1, products.Length)).ToArray();
}

[Description("Return a deliberately large diagnostic payload for payload-size demonstrations.")]
static async Task<DiagnosticReport> GetLargeDiagnosticPayloadAsync([Description("The order id to inspect.")] string orderId)
{
    await Task.Delay(100);

    string repeatedDetails = string.Join(" ", Enumerable.Repeat(
        "Fulfillment telemetry shows normal routing, warehouse scan completion, carrier pickup, and customer notification events.",
        12));

    return new DiagnosticReport(orderId, repeatedDetails, ["routing", "warehouse", "carrier", "notification"]);
}

[Description("Simulate an inventory reservation failure for error telemetry demonstrations.")]
static Task<string> FailInventoryReservationAsync(
    [Description("The SKU to reserve.")] string sku,
    [Description("The order id requesting the reservation.")] string orderId)
    => throw new InvalidOperationException($"Inventory reservation failed for SKU '{sku}' on order '{orderId}'.");

// ── Helpers ───────────────────────────────────────────────────────────────────

static void Banner(string text)
{
    var line = new string('═', text.Length + 4);
    Console.WriteLine(line);
    Console.WriteLine($"  {text}");
    Console.WriteLine(line);
    Console.WriteLine();
}

static bool IsEnabled(string? value)
    => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
    || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

internal sealed record OrderStatus(string OrderId, string Status, string TrackingNumber, DateTimeOffset EstimatedDelivery);

internal sealed record ProductSuggestion(string Sku, string Name, decimal Price);

internal sealed record DiagnosticReport(string OrderId, string Details, IReadOnlyList<string> Signals);