# Quickstart: Tool Call Span Enrichment

**Package**: `Melic.AgentFramework.Observability.Tools`
**Feature Branch**: `002-tool-call-enrichment`
**Created**: 2026-05-19

---

## Installation

```xml
<PackageReference Include="Melic.AgentFramework.Observability.Tools" Version="0.1.0" />
```

---

## Scenario 1 — Minimal setup

One line in the agent builder enriches MAF tool telemetry for every executed tool.

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Tools;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseOpenTelemetry()   // creates invoke_agent and execute_tool spans
    .UseToolTelemetry()   // enriches execute_tool spans with bounded payload and retry attributes
    .Build();

AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync("Check order 42", session);

// For each executed tool, the MAF execute_tool span now includes:
// - MAF standard gen_ai.tool.name and gen_ai.tool.call.id
// - genai.tool.input
// - genai.tool.output
// - OpenTelemetry span status
```

---

## Scenario 2 — Disable payload capture

If you only want MAF's built-in timing, name, call id, and status, disable input/output capture independently.

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

The Tools and Sessions packages are independent. When both are present, the `invoke_agent` span
carries `genai.session.*` tags and MAF `execute_tool` spans carry the `genai.tool.*` enrichment.
carries `genai.session.*` tags and MAF `execute_tool` spans carry package-owned payload/retry enrichment.

```csharp
using Melic.AgentFramework.Observability.Sessions;
using Melic.AgentFramework.Observability.Tools;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseOpenTelemetry()      // outermost: creates invoke_agent span
    .UseSessionTelemetry()   // enriches invoke_agent with genai.session.*
    .UseToolTelemetry()      // enriches execute_tool spans below invoke_agent
    .Build();
```

---

## Scenario 5 — Register trace sources

To see both the parent invocation span and MAF tool-call spans, register MAF's `ActivitySource`.
Register the Tools source only if you also want fallback `agent_tool_call` spans when no MAF
`execute_tool` span is current.

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Tools") // fallback spans only
    .Build();
```

If you customize `ToolTelemetryOptions.ActivitySourceName`, register that custom source for fallback spans.

---

## Telemetry emitted

### Target Span

| Field | Example value |
| --- | --- |
| Primary name | `execute_tool` |
| Fallback name | `agent_tool_call` |
| Fallback kind | `Internal` |
| Parent | current `Activity` when fallback is needed |
| Status | `OK` or `ERROR` |
| Status description | Exception message on failure |

### Span attributes

| Attribute | Example value |
| --- | --- |
| `gen_ai.tool.name` | `"get_order"` |
| `gen_ai.tool.call.id` | `"call_123"` |
| `genai.tool.input` | `{"orderId":"42"}` |
| `genai.tool.output` | `{"status":"shipped"}` |
| `genai.tool.is_retry` | `true` |
| `genai.tool.attempt_index` | `2` |

---

## Troubleshooting

### `genai.tool.*` attributes do not appear

- Confirm the agent was built with `.UseToolTelemetry()` before `.Build()`.
- If the agent stack does not use `FunctionInvokingChatClient`, the middleware cannot intercept tool execution.

### No fallback `agent_tool_call` spans appear

- This is expected when MAF's `execute_tool` span is current; the package enriches that span instead.
- Confirm an `ActivityListener` or OTel tracer provider subscribes to the configured fallback source name if you intentionally run without MAF `execute_tool` spans.

### Tool spans are roots instead of children

- Ensure `.UseOpenTelemetry()` is registered so MAF emits `execute_tool` spans.
- If no MAF `execute_tool` span is active, the package emits a best-effort fallback `agent_tool_call` span.

### `genai.tool.input` or `genai.tool.output` is missing

- Capture may be disabled in `ToolTelemetryOptions`.
- Serialization may have failed; the package omits the attribute rather than throwing.
- The configured max length may be too small for a meaningful payload.
