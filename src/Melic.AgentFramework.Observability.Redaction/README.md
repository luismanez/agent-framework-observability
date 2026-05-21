# Melic.AgentFramework.Observability.Redaction

OpenTelemetry-native telemetry redaction for Melic Agent Framework Observability.

This package redacts sensitive values from selected trace attributes before export. It is independent from the Sessions and Tools packages and composes with them through OpenTelemetry.

## Usage

```csharp
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddTelemetryRedaction()
    .AddConsoleExporter()
    .Build();
```

Default targets are `genai.tool.input` and `genai.tool.output`. Official `gen_ai.*` attributes and custom session payload attributes are opt-in.

```csharp
builder.AddTelemetryRedaction(options =>
{
    // Recommended when MAF UseOpenTelemetry has EnableSensitiveData=true.
    options.IncludeMafSensitiveDataAttributes();
    options.IncludeAttributesWithPrefix("genai.session.customer_");
    options.AddPatternRule("account", @"acct_[0-9]{12}");
    options.EnableDiagnostics = true;
});
```
