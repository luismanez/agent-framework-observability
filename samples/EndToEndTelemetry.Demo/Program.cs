// SPDX-License-Identifier: MIT

// EndToEndTelemetry.Demo - runs a customer-support order lookup scenario
// against a real Foundry/Azure OpenAI endpoint and exports composed Sessions,
// Tools, and Redaction telemetry to Console or Azure Application Insights.

using System.ComponentModel;
using System.Diagnostics;
using Azure;
using Azure.AI.OpenAI;
using Azure.Monitor.OpenTelemetry.Exporter;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Redaction;
using Melic.AgentFramework.Observability.Sessions;
using Melic.AgentFramework.Observability.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenTelemetry;
using OpenTelemetry.Trace;

const string MafSourceName = "Experimental.Microsoft.Agents.AI";
const string SessionsSourceName = "Melic.AgentFramework.Observability.Sessions";
const string ToolsSourceName = "Melic.AgentFramework.Observability.Tools";
const string DemoSourceName = "EndToEndTelemetry.Demo";

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is required.");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
    ?? throw new InvalidOperationException("AZURE_OPENAI_API_KEY is required.");
var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";
var appInsightsConnStr = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

bool useAppInsights = !string.IsNullOrWhiteSpace(appInsightsConnStr);
Banner(useAppInsights
    ? "Exporting end-to-end telemetry to Azure Application Insights"
    : "Exporting end-to-end telemetry to Console  (set APPLICATIONINSIGHTS_CONNECTION_STRING to switch to App Insights)");

using var tracerProvider = BuildTracerProvider(useAppInsights, appInsightsConnStr);
using var demoActivitySource = new ActivitySource(DemoSourceName);

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deployment)
    .AsIChatClient();

IList<AITool> tools =
[
    AIFunctionFactory.Create(GetOrderStatusAsync, name: "get_order_status"),
    AIFunctionFactory.Create(GetReturnOptionsAsync, name: "get_return_options")
];

AIAgent agent = BuildAgent(chatClient, tools);
AgentSession session = await agent.CreateSessionAsync();

session.AssignSessionId("support-order-42");
session.SetSessionTag("tenant_id", "contoso-retail");
session.SetSessionTag("customer_email", "customer@example.com");

Banner("Customer support order lookup");

using (demoActivitySource.StartActivity("scenario.customer_support_order_lookup"))
{
    Console.WriteLine("Turn 1: order status lookup");
    AgentResponse statusResponse = await agent.RunAsync(
        [new ChatMessage(ChatRole.User, "Customer customer@example.com asks about order 42. Use get_order_status and summarize shipment status.")],
        session);
    Console.WriteLine(statusResponse.Text);
    Console.WriteLine($"session.id = {session.GetSessionId()}");
    Console.WriteLine();

    Console.WriteLine("Turn 2: return options follow-up");
    AgentResponse returnResponse = await agent.RunAsync(
        [new ChatMessage(ChatRole.User, "For the same customer, use get_return_options for order 42 and explain the safest next action.")],
        session);
    Console.WriteLine(returnResponse.Text);
}

Banner("All scenarios complete");
Console.WriteLine("Check your telemetry backend:");
Console.WriteLine("  Console      : scroll up for invoke_agent and execute_tool spans");
Console.WriteLine("  App Insights : Transaction Search -> filter by customDimensions[\"genai.session.id\"] == support-order-42");
Console.WriteLine("  Session      : genai.session.id, genai.session.invocation_index, genai.session.total_input_tokens");
Console.WriteLine("  Tool         : genai.tool.input, genai.tool.output, genai.tool.attempt_index");
Console.WriteLine("  MAF standard : gen_ai.* prompt/response/tool attributes when sensitive capture is enabled");
Console.WriteLine($"  Redaction    : {RedactionAttributeNames.Applied}, {RedactionAttributeNames.MatchCount}, {RedactionAttributeNames.FailureCount}");

tracerProvider.ForceFlush(15_000);

if (useAppInsights)
{
    Console.WriteLine();
    Console.WriteLine("Azure Monitor exporter batches telemetry; waiting briefly for in-flight sends...");
    await Task.Delay(TimeSpan.FromSeconds(5));
    Console.WriteLine("Done - data should appear in App Insights within ~1-2 minutes.");
}

static TracerProvider BuildTracerProvider(bool useAppInsights, string? appInsightsConnStr)
{
    TracerProviderBuilder traceBuilder = Sdk.CreateTracerProviderBuilder()
        .AddSource(MafSourceName)
        .AddSource(SessionsSourceName)
        .AddSource(ToolsSourceName)
        .AddSource(DemoSourceName)
        .AddTelemetryRedaction(options =>
        {
            options.IncludeMafSensitiveDataAttributes();
            options.IncludeAttribute("customer_email");
            options.RedactExceptionMessages = true;
            options.EnableDiagnostics = true;
        });

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

static AIAgent BuildAgent(IChatClient chatClient, IList<AITool> tools)
{
    AIAgent innerAgent = chatClient.AsAIAgent(
        instructions: "You are a concise customer-support assistant. Always call the relevant order tool before answering questions about order status or returns. Do not expose secrets or raw customer contact details in the final answer.",
        name: "EndToEndTelemetryDemoAgent",
        tools: tools);

    return new AIAgentBuilder(innerAgent)
        .UseOpenTelemetry(configure: options =>
        {
            options.EnableSensitiveData = true;
        })
        .UseSessionTelemetry(options =>
        {
            options.TrackTokenAggregates = true;
            options.EnableSessionSpan = true;
        })
        .UseToolTelemetry(options =>
        {
            options.MaxInputLength = 1024;
            options.MaxOutputLength = 1024;
        })
        .Build();
}

[Description("Get order status, shipment, and customer contact information for an order.")]
static async Task<OrderStatus> GetOrderStatusAsync([Description("The order id to inspect.")] string orderId)
{
    await Task.Delay(100);

    return new OrderStatus(
        orderId,
        "shipped",
        "ZX-441",
        "customer@example.com",
        "Bearer support-demo-token-42",
        DateTimeOffset.UtcNow.AddDays(2));
}

[Description("Get return and escalation options for a support order.")]
static async Task<ReturnOptions> GetReturnOptionsAsync([Description("The order id to inspect.")] string orderId)
{
    await Task.Delay(100);

    return new ReturnOptions(
        orderId,
        "Return window is open for 21 more days.",
        "Use standard return authorization unless the delivery is delayed past the estimated date.",
        "customer@example.com");
}

static void Banner(string text)
{
    string line = new('=', text.Length + 4);
    Console.WriteLine(line);
    Console.WriteLine($"  {text}");
    Console.WriteLine(line);
    Console.WriteLine();
}

internal sealed record OrderStatus(
    string OrderId,
    string Status,
    string TrackingNumber,
    string CustomerEmail,
    string AccessToken,
    DateTimeOffset EstimatedDelivery);

internal sealed record ReturnOptions(
    string OrderId,
    string Eligibility,
    string RecommendedAction,
    string CustomerEmail);