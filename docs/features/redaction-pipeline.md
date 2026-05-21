# Redaction Pipeline

`Melic.AgentFramework.Observability.Redaction` redacts selected trace attributes before telemetry is exported. It is OpenTelemetry-native, independent from Sessions and Tools, and intended to protect high-risk payload attributes while preserving enough structure for diagnostics.

## Installation

```xml
<PackageReference Include="Melic.AgentFramework.Observability.Redaction" Version="1.*" />
```

The package targets `net8.0`, `net9.0`, and `net10.0`.

## Minimal Setup

Register the processor before exporters on your `TracerProviderBuilder`:

```csharp
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Tools")
    .AddTelemetryRedaction()
    .AddConsoleExporter()
    .Build();
```

By default, Redaction targets `genai.tool.input` and `genai.tool.output`. Built-in session correlation attributes such as `genai.session.id` are not default targets.

## Configuration

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddTelemetryRedaction(options =>
    {
        options.IncludeAttribute("app.customer.email");
        options.IncludeAttributesWithPrefix("genai.session.customer_");
        options.ExcludeAttribute("genai.tool.output");
        options.IncludeStandardAttribute("gen_ai.prompt");
        options.AddSensitiveFieldName("customerEmail");
        options.AddPatternRule("account-number", @"acct_[0-9]{12}");
        options.EnableDiagnostics = true;
    });
```

Official `gen_ai.*` attributes are opt-in only through `IncludeStandardAttribute` or `IncludeStandardAttributesWithPrefix`. Package-owned custom attributes use the `genai.*` prefix.

## MAF Sensitive Data Telemetry

Many applications enable MAF sensitive-data capture for audit, analytics, or troubleshooting:

```csharp
AIAgent agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry(configure: options =>
    {
        options.EnableSensitiveData = true;
    })
    .Build();
```

When this is enabled, MAF and `Microsoft.Extensions.AI` may emit prompt, response, tool argument, and tool result data under official `gen_ai.*` attributes. Redaction supports that surface through an explicit preset:

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddTelemetryRedaction(options =>
    {
        options.IncludeMafSensitiveDataAttributes();
        options.RedactExceptionMessages = true;
    })
    .AddConsoleExporter()
    .Build();
```

This is equivalent to opting in the `gen_ai.` standard-attribute prefix. The explicit method is preferred because it documents intent at the call site: MAF is allowed to capture sensitive telemetry for observability, and Redaction is responsible for masking sensitive substrings before export.

The current processor redacts string-valued `Activity` tags. If a future MAF version emits sensitive content as activity events rather than span attributes, event redaction should be added as a separate processor enhancement.

## Defaults

Default rules redact:

- Email addresses
- Phone-like numbers
- Bearer tokens
- API-token-like fragments
- Connection-string secrets
- Password-like fragments
- Sensitive JSON field names such as `password`, `secret`, `token`, `apiKey`, `accessToken`, and `connectionString`

JSON-looking payloads are parsed first. When parsing succeeds, Redaction preserves valid JSON and replaces sensitive string values. Malformed JSON falls back to string redaction.

## Failure Behavior

The default `RedactionFailureMode.ReplaceValue` fails closed by replacing over-limit or failed targeted values with the replacement text. `PreserveOriginal` is available when a consumer explicitly prefers telemetry fidelity over fail-closed behavior.

`MaxValueLength` bounds processing. Values above the limit use the configured failure behavior.

## Diagnostics

Diagnostics are disabled by default. When `EnableDiagnostics = true`, Redaction emits only aggregate attributes:

| Attribute | Type |
| --- | --- |
| `genai.redaction.applied` | bool |
| `genai.redaction.match_count` | int |
| `genai.redaction.failure_count` | int |

Diagnostics do not include rule names, matched values, original fragments, field values, or payload snippets.

## See Also

- [Spec quickstart](../../specs/003-redaction-pipeline/quickstart.md)
- [Feature specification](../../specs/003-redaction-pipeline/spec.md)
- [Technical reference](../design/technical-reference.md)
