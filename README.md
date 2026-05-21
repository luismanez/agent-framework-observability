# Agent Framework Observability

Observability library for the [Microsoft Agent Framework](https://github.com/microsoft/agent-framework) (MAF).

Enriches AI agent sessions and tool calls with stable identity, bounded payload telemetry, and OpenTelemetry-native export-boundary redaction.

## Packages

| Package | NuGet | Description |
| --- | --- | --- |
| `Melic.AgentFramework.Observability.Abstractions` | `0.1.0-preview.2` | Shared attribute-name constants |
| `Melic.AgentFramework.Observability.Sessions` | `0.1.0-preview.2` | Session identity & telemetry enrichment |
| `Melic.AgentFramework.Observability.Tools` | `0.1.0-preview.2` | Bounded tool-call payload and retry enrichment |
| `Melic.AgentFramework.Observability.Redaction` | *(unreleased)* | MAF-aware OpenTelemetry trace-attribute redaction before export |

## Installation

```xml
<PackageReference Include="Melic.AgentFramework.Observability.Sessions" Version="1.*" />
<PackageReference Include="Melic.AgentFramework.Observability.Tools" Version="1.*" />
<PackageReference Include="Melic.AgentFramework.Observability.Redaction" Version="1.*" />
```

## Quick start (Scenario 1 — Mode A enrichment)

One line in your agent builder chain. Every `invoke_agent` span automatically gets
`genai.session.id`, `genai.session.invocation_index`, `genai.session.age_seconds`,
and running token totals.

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Sessions;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseSessionTelemetry()
    .Build();

AgentSession session = await agent.CreateSessionAsync();

// First invocation — session id is auto-assigned
AgentResponse r1 = await agent.RunAsync("Hello", session);
// span tags: genai.session.id=<guid>, genai.session.invocation_index=1, ...

// Second invocation — same id, accumulated totals
AgentResponse r2 = await agent.RunAsync("How are you?", session);
// span tags: genai.session.id=<same guid>, genai.session.invocation_index=2, ...
```

### Tool telemetry quick start

Add tool telemetry after MAF OpenTelemetry to enrich MAF's existing `execute_tool`
spans with bounded payload and retry attributes. MAF continues to provide standard
tool identity fields such as `gen_ai.tool.name` and `gen_ai.tool.call.id`.

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Tools;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseOpenTelemetry()
    .UseToolTelemetry()
    .Build();

AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync("Check order 42", session);

// MAF execute_tool span:
// - gen_ai.tool.name / gen_ai.tool.call.id from MAF
// - genai.tool.input / genai.tool.output from this package
// - genai.tool.is_retry / genai.tool.attempt_index when a call id repeats
```

### Redaction quick start

Register Redaction on the OpenTelemetry trace pipeline before exporters. It is not an `AIAgent` middleware; it is an export-boundary telemetry processor that understands the MAF/GenAI attribute surface. By default it redacts package-owned tool payload attributes `genai.tool.input` and `genai.tool.output`.

```csharp
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Tools")
    .AddTelemetryRedaction(options =>
    {
        // Recommended when MAF UseOpenTelemetry has EnableSensitiveData=true.
        options.IncludeMafSensitiveDataAttributes();
        options.IncludeAttributesWithPrefix("genai.session.customer_");
    })
    .AddConsoleExporter()
    .Build();
```

Built-in `genai.session.*` correlation attributes are not redacted by default. Official `gen_ai.*` attributes are processed only when explicitly opted in, either individually or with `IncludeMafSensitiveDataAttributes()` for MAF prompt/response/tool sensitive-data telemetry. Redaction changes exported span attributes only; it does not modify agent messages, model inputs, model outputs, tool arguments, or application state.

### ⚠️ Pipeline order matters when combining with `UseOpenTelemetry()`

`UseSessionTelemetry()` enriches the **currently active** `Activity` (the `invoke_agent`
span emitted by MAF's `UseOpenTelemetry()`). For the enrichment to land on the right span,
`UseOpenTelemetry()` must be registered **before** `UseSessionTelemetry()` so it ends up
as the outermost wrapper and its span is still open when the session decorator runs.

`AIAgentBuilder` rule: **first `.Use()` registered = outermost wrapper**.

```csharp
// ✅ Correct — OpenTelemetry outermost, span open during enrichment
AIAgent agent = new AIAgentBuilder(inner)
    .UseOpenTelemetry()        // 1) outermost: opens `invoke_agent` activity
    .UseSessionTelemetry()     // 2) innermost: SetTag on Activity.Current works
    .Build();

// ❌ Wrong — session enrichment runs AFTER the span is closed; tags are lost
AIAgent agent = new AIAgentBuilder(inner)
    .UseSessionTelemetry()     // outermost
    .UseOpenTelemetry()        // innermost — closes span before enrichment runs
    .Build();
```

If you only use `UseSessionTelemetry()` without `UseOpenTelemetry()`, no `invoke_agent`
span is emitted at all and only Mode B (`BeginSessionTrace`) will produce traces.

Don't forget to subscribe both `ActivitySource`s in your `TracerProvider`:

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")          // MAF invoke_agent spans
    .AddSource("Melic.AgentFramework.Observability.Sessions") // Mode B session span
    .AddSource("Melic.AgentFramework.Observability.Tools")    // fallback tool-call spans
    .AddConsoleExporter()
    .Build();
```

See the [full quickstart](specs/001-session-identity-enrichment/quickstart.md) for more scenarios:

- Scenario 2 — Assign your own session identifier
- Scenario 3 — Custom business context tags
- Scenario 4 — Session persistence across serialization/restore
- Scenario 5 — Mode B session-level span (`BeginSessionTrace`)

## Features

- [Session Identity & Enrichment](docs/features/session-identity-enrichment.md) — stable session ID, aggregate token counts, custom tags, optional session span (Mode B)
- [Tool Call Span Enrichment](docs/features/tool-call-enrichment.md) — enriches MAF `execute_tool` spans with bounded payload and retry attributes
- [Redaction Pipeline](docs/features/redaction-pipeline.md) — redacts selected trace attributes before export with JSON-preserving rules and aggregate diagnostics

## Requirements

- .NET 8, 9, or 10
- `Microsoft.Agents.AI` 1.5+
- `OpenTelemetry.Api` 1.9+

## License

MIT

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
