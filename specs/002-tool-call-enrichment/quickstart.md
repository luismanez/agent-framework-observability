# Quickstart: Tool Call Span Enrichment

**Package**: `Melic.AgentFramework.Observability.Tools`
**Feature Branch**: `002-tool-call-enrichment`
**Created**: 2026-05-19

---

## Installation

```xml
<PackageReference Include="Melic.AgentFramework.Observability.Tools" Version="1.*" />
```

---

## Scenario 1 — Minimal setup

One line in the agent builder enables child spans for every executed tool.

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Tools;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseOpenTelemetry()   // creates the parent invoke_agent span
    .UseToolTelemetry()   // emits agent_tool_call child spans
    .Build();

AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync("Check order 42", session);

// For each executed tool, the trace now includes:
// - span name: agent_tool_call
// - genai.tool.name
// - genai.tool.input
// - genai.tool.output
// - otel.status_code
```

---

## Scenario 2 — Disable payload capture

If you only want timing, name, and status, disable input/output capture independently.

```csharp
AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseOpenTelemetry()
    .UseToolTelemetry(options =>
    {
        options.CaptureInput = false;
        options.CaptureOutput = false;
    })
    .Build();
```

---

## Scenario 3 — Keep payloads but bound their size

The payload serializer preserves valid JSON while shrinking large values to fit the configured limits.

```csharp
AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseOpenTelemetry()
    .UseToolTelemetry(options =>
    {
        options.MaxInputLength = 1024;
        options.MaxOutputLength = 1024;
    })
    .Build();
```

---

## Scenario 4 — Combine with Sessions telemetry

The Tools and Sessions packages are independent. When both are present, tool spans remain children
of `invoke_agent`, while the parent invocation span carries `genai.session.*` tags.

```csharp
using Melic.AgentFramework.Observability.Sessions;
using Melic.AgentFramework.Observability.Tools;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseOpenTelemetry()      // outermost: creates invoke_agent span
    .UseSessionTelemetry()   // enriches invoke_agent with genai.session.*
    .UseToolTelemetry()      // emits agent_tool_call spans below invoke_agent
    .Build();
```

---

## Scenario 5 — Register trace sources

To see both the parent invocation span and the tool child spans, register both `ActivitySource`s.

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Tools")
    .Build();
```

If you customize `ToolTelemetryOptions.ActivitySourceName`, register that custom source instead.

---

## Telemetry emitted

### Span

| Field | Example value |
|---|---|
| Name | `agent_tool_call` |
| Kind | `Internal` |
| Parent | current `invoke_agent` span when present |

### Span attributes

| Attribute | Example value |
|---|---|
| `genai.tool.name` | `"get_order"` |
| `genai.tool.call_id` | `"call_123"` |
| `genai.tool.input` | `{"orderId":"42"}` |
| `genai.tool.output` | `{"status":"shipped"}` |
| `genai.tool.is_retry` | `true` |
| `genai.tool.attempt_index` | `2` |
| `otel.status_code` | `"OK"` or `"ERROR"` |
| `otel.status_description` | `"Missing order id"` |

---

## Troubleshooting

**No `agent_tool_call` spans appear**

- Confirm the agent was built with `.UseToolTelemetry()` before `.Build()`.
- Confirm an `ActivityListener` or OTel tracer provider subscribes to the configured tool source name.
- If the agent stack does not use `FunctionInvokingChatClient`, the middleware cannot intercept tool execution.

**Tool spans are roots instead of children**

- Ensure `.UseOpenTelemetry()` is registered so an `invoke_agent` span is current when the tool starts.
- If no parent `Activity` is active, the tool span is still emitted best-effort as a root span.

**`genai.tool.input` or `genai.tool.output` is missing**

- Capture may be disabled in `ToolTelemetryOptions`.
- Serialization may have failed; the package omits the attribute rather than throwing.
- The configured max length may be too small for a meaningful payload.
