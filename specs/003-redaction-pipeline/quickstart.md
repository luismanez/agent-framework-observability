# Quickstart: Redaction Pipeline

## Goal

Enable redaction for high-risk agent telemetry payloads before traces are exported to an OpenTelemetry-compatible backend.

## 1. Add the package

```powershell
dotnet add package Melic.AgentFramework.Observability.Redaction --prerelease
```

When developing in this repository, reference the project instead:

```xml
<ProjectReference Include="..\..\src\Melic.AgentFramework.Observability.Redaction\Melic.AgentFramework.Observability.Redaction.csproj" />
```

## 2. Register redaction in the tracing pipeline

Register redaction after sources are added and before exporters in examples. Redaction is an OpenTelemetry export-boundary processor: it is MAF-aware through span attributes, but it is not registered on `AIAgentBuilder`.

```csharp
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;

using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Tools")
    .AddTelemetryRedaction()
    .AddConsoleExporter()
    .Build();
```

With defaults, Redaction processes package-owned high-risk tool payload attributes such as `genai.tool.input` and `genai.tool.output`. Built-in session correlation and aggregate attributes are left unchanged unless you explicitly include session custom tags or a session attribute prefix.

## 3. Compose with Sessions and Tools

Redaction is configured on the OpenTelemetry pipeline. Sessions and Tools remain configured on the agent builder.

```csharp
using Melic.AgentFramework.Observability.Redaction;
using Melic.AgentFramework.Observability.Sessions;
using Melic.AgentFramework.Observability.Tools;
using Microsoft.Agents.AI;

using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Tools")
    .AddTelemetryRedaction()
    .AddConsoleExporter()
    .Build();

AIAgent innerAgent = chatClient.AsAIAgent(
    instructions: "You are a concise support assistant.",
    name: "SupportAgent",
    tools: tools);

AIAgent agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry()
    .UseSessionTelemetry()
    .UseToolTelemetry()
    .Build();
```

The resulting exported `execute_tool` spans keep useful structure while sensitive values in `genai.tool.input` and `genai.tool.output` are replaced.

## 4. Redact MAF sensitive-data telemetry

Many production applications enable MAF sensitive-data capture for audit or analytics:

```csharp
AIAgent agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry(configure: options =>
    {
        options.EnableSensitiveData = true;
    })
    .UseSessionTelemetry()
    .UseToolTelemetry()
    .Build();
```

When doing that, opt in the official `gen_ai.*` span attributes emitted by MAF and `Microsoft.Extensions.AI`:

```csharp
using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddTelemetryRedaction(options =>
    {
        options.IncludeMafSensitiveDataAttributes();
        options.RedactExceptionMessages = true;
    })
    .AddConsoleExporter()
    .Build();
```

This keeps prompt, response, tool argument, and tool result telemetry available for observability while applying Redaction rules before export. It does not change the data sent to the model or tools.

## 5. Add custom rules

```csharp
using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddTelemetryRedaction(options =>
    {
        options.AddSensitiveFieldName("customerSecret");
        options.AddPatternRule("internal-account", @"acct_[0-9]{12}");
    })
    .AddConsoleExporter()
    .Build();
```

## 6. Opt in selected standard attributes

Official `gen_ai.*` attributes are not changed by default. Use `IncludeMafSensitiveDataAttributes()` for the common MAF `EnableSensitiveData` case, or opt in only selected attributes when you want narrower coverage.

```csharp
using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddTelemetryRedaction(options =>
    {
        options.IncludeStandardAttribute("gen_ai.prompt");
    })
    .AddConsoleExporter()
    .Build();
```

## 7. Enable error message redaction and diagnostics

```csharp
using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddTelemetryRedaction(options =>
    {
        options.RedactExceptionMessages = true;
        options.EnableDiagnostics = true;
    })
    .AddConsoleExporter()
    .Build();
```

Diagnostics are aggregate only. They can report whether redaction happened and how many replacements occurred, but they never include matched values or payload fragments.

## 8. Disable built-in rules but keep custom rules

```csharp
using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddTelemetryRedaction(options =>
    {
        options.EnableDefaultRules = false;
        options.AddSensitiveFieldName("customerSecret");
        options.AddPatternRule("internal-account", @"acct_[0-9]{12}");
    })
    .AddConsoleExporter()
    .Build();
```

When default rules are disabled, built-in email, token, password, and connection-string rules are omitted. Custom rules still run.

## 9. Validate locally

Run the focused tests after implementation:

```powershell
dotnet test --filter "FullyQualifiedName~Redaction"
```

Run a full repo validation before opening a PR:

```powershell
dotnet build
dotnet test
```

## Expected Result

A tool payload like this:

```json
{"email":"person@example.com","apiKey":"secret-123","query":"show invoices"}
```

is exported as structurally useful telemetry similar to:

```json
{"email":"[REDACTED]","apiKey":"[REDACTED]","query":"show invoices"}
```

The application, agent messages, prompts before model invocation, model responses, tool arguments, and tool outputs in memory are unchanged. Only telemetry attributes are transformed before export.
