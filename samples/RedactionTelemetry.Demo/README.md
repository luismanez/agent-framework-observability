# Redaction Telemetry Demo

This sample emits local `Activity` telemetry, runs it through `Melic.AgentFramework.Observability.Redaction`, and exports the resulting spans to either Console or Azure Application Insights.

It intentionally does not require an Azure OpenAI endpoint or an `AIAgent`. The goal is to isolate the export-boundary Redaction processor and show how it behaves with the same span attributes that Tools, Sessions, application code, and MAF sensitive-data telemetry emit in real agent applications.

## Configuration

By default, the sample exports to Console.

To export to Application Insights instead, set:

```powershell
$env:APPLICATIONINSIGHTS_CONNECTION_STRING = "InstrumentationKey=...;IngestionEndpoint=..."
```

Then run:

```powershell
dotnet run --project samples/RedactionTelemetry.Demo/RedactionTelemetry.Demo.csproj
```

## Scenarios

### Scenario 1 - Default Tool Payload Redaction

Configuration:

```csharp
.AddTelemetryRedaction(options => options.EnableDiagnostics = true)
```

Expected behavior:

- `genai.tool.input` and `genai.tool.output` are redacted by default.
- `app.note` is unchanged because application attributes are not targeted by default.
- `gen_ai.prompt` is unchanged because official MAF/OpenTelemetry attributes are explicit opt-in.

### Scenario 2 - MAF Sensitive-Data Preset

Configuration:

```csharp
.AddTelemetryRedaction(options =>
{
    options.IncludeMafSensitiveDataAttributes();
    options.RedactExceptionMessages = true;
    options.EnableDiagnostics = true;
})
```

This is the recommended Redaction configuration when an application enables MAF sensitive-data capture:

```csharp
.UseOpenTelemetry(configure: options =>
{
    options.EnableSensitiveData = true;
})
```

Expected behavior:

- `gen_ai.prompt` is redacted.
- `gen_ai.response.text` is redacted.
- `gen_ai.tool.call.arguments` is redacted.
- `gen_ai.tool.call.result` is redacted.
- `exception.message` is redacted because `RedactExceptionMessages` is enabled.

### Scenario 3 - Custom Scope And Rules

Configuration opts in a session custom prefix, an application attribute, a JSON field name, and a custom account-number pattern.

Expected behavior:

- `genai.session.id` is unchanged.
- `genai.session.customer_email` is redacted because the `genai.session.customer_` prefix is opted in.
- `app.customer.profile` is redacted because the exact attribute is opted in.
- `app.unrelated` is unchanged even if it contains a matching account pattern.

### Scenario 4 - Bounded Fail-Closed Behavior

Configuration sets a short `MaxValueLength`.

Expected behavior:

- Over-limit targeted values are replaced entirely with `[REDACTED]`.
- Shorter targeted values are redacted normally.
- Aggregate diagnostics include failure counts without exposing payload fragments.

## What To Inspect

Console output should show spans named `redaction.*`.

In Application Insights, use Transaction Search and filter by span name beginning with `redaction`. Inspect custom dimensions for:

- `genai.tool.input`
- `genai.tool.output`
- `gen_ai.prompt`
- `gen_ai.response.text`
- `genai.redaction.applied`
- `genai.redaction.match_count`
- `genai.redaction.failure_count`

Diagnostics never include rule names, matched values, original fragments, field values, or payload snippets.

## Notes

The Redaction package itself has no Azure Monitor or Application Insights dependency. This sample owns the exporter dependency, matching the intended application-level integration pattern.

Redaction is MAF-aware through telemetry attributes, not through direct `AIAgent` integration. It does not alter prompts before model invocation, model responses, tool arguments, tool outputs, or application state. It only transforms selected string-valued span attributes before export.
