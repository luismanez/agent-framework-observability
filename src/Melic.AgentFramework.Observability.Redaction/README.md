# Melic.AgentFramework.Observability.Redaction

MAF-aware OpenTelemetry trace-attribute redaction for Melic Agent Framework Observability.

This package redacts sensitive values from selected trace attributes before export. It is an OpenTelemetry processor, not an `AIAgent` middleware. It composes with MAF, Sessions, Tools, and application telemetry by processing their span attributes at the export boundary.

Exporter packages belong in applications and samples. The Redaction package itself does not depend on Sessions, Tools, Azure Monitor, Application Insights, or exporter-specific packages.

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

Default targets are `genai.tool.input` and `genai.tool.output`. Official `gen_ai.*` attributes and custom session payload attributes are opt-in. Redaction does not mutate prompts, responses, tool arguments, tool results, or application state; it only changes exported telemetry attributes.

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
