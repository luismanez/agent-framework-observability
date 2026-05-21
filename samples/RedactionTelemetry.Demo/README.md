# Redaction Telemetry Demo

This sample exports local `Activity` telemetry through the Redaction processor and the console exporter. It demonstrates how Redaction composes with package-owned attributes from Tools and Sessions without depending on those packages at runtime.

It also demonstrates the recommended preset for applications that enable MAF sensitive-data telemetry with `UseOpenTelemetry(configure: o => o.EnableSensitiveData = true)`: call `IncludeMafSensitiveDataAttributes()` so prompt, response, tool argument, and tool result attributes emitted under the official `gen_ai.*` namespace are inspected before export.

## Run

```powershell
dotnet run --project samples/RedactionTelemetry.Demo/RedactionTelemetry.Demo.csproj
```

## What To Inspect

The console exporter should show:

- `genai.tool.input` and `genai.tool.output` with sensitive values replaced by `[REDACTED]`.
- `gen_ai.prompt` and `gen_ai.response.text` redacted because the sample enables `IncludeMafSensitiveDataAttributes()`.
- `genai.session.id` unchanged by default, while `genai.session.customer_email` is redacted because the sample opts in the `genai.session.customer_` prefix.
- Aggregate diagnostic attributes: `genai.redaction.applied`, `genai.redaction.match_count`, and `genai.redaction.failure_count`.

The diagnostics never include rule names, matched values, original fragments, field values, or payload snippets.

## App Insights Notes

The Redaction package has no Azure Monitor or Application Insights dependency. In a real app, register `.AddTelemetryRedaction()` before your exporter in the same `TracerProviderBuilder`, then add the Azure Monitor exporter in the application project.
