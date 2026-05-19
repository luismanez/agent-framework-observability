# Tool Call Span Enrichment

`Melic.AgentFramework.Observability.Tools` adds OpenTelemetry spans around Microsoft Agent Framework tool invocations. It uses the public MAF function-invocation middleware and emits one `agent_tool_call` span for each intercepted tool call.

## When To Use It

Use this package when an `invoke_agent` span is too coarse and you need to see which tools ran, how long each took, whether they failed, and what inputs or outputs were involved. Tool telemetry is opt-in and does not require changes to individual tool implementations.

## Setup

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Tools;

AIAgent agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry()
    .UseToolTelemetry()
    .Build();
```

Register the Tools activity source in your OpenTelemetry pipeline:

```csharp
Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Tools")
    .Build();
```

If no parent `Activity` is current, the package still emits `agent_tool_call` as a root span when listeners are active.

## Emitted Span

| Field | Value |
| --- | --- |
| Name | `agent_tool_call` |
| Kind | `Internal` |
| Source | `Melic.AgentFramework.Observability.Tools` by default |
| Parent | Current `Activity` at tool-call start |
| Status | `OK` or `ERROR` via `Activity.SetStatus` |

## Attributes

| Attribute | Type | Notes |
| --- | --- | --- |
| `genai.tool.name` | string | Required tool name. |
| `genai.tool.call_id` | string | Omitted when MAF does not provide a call id. |
| `genai.tool.input` | string | Valid JSON input payload when input capture is enabled and serialization succeeds. |
| `genai.tool.output` | string | Valid JSON output or error payload when output capture is enabled and serialization succeeds. |
| `genai.tool.is_retry` | bool | Present only when a call id is available and retry tracking succeeds. |
| `genai.tool.attempt_index` | int | 1-based attempt count for the call id in the current parent invocation. |

All custom attribute keys are declared in `Melic.AgentFramework.Observability.Abstractions.ToolAttributeNames`.

## Payload Capture

Input and output capture are enabled by default. Payloads are serialized with `System.Text.Json`, bounded by `ToolTelemetryOptions.MaxInputLength` and `ToolTelemetryOptions.MaxOutputLength`, and kept as valid JSON after truncation.

```csharp
AIAgent agent = new AIAgentBuilder(innerAgent)
    .UseOpenTelemetry()
    .UseToolTelemetry(options =>
    {
        options.CaptureInput = false;
        options.MaxOutputLength = 1024;
    })
    .Build();
```

When a tool throws, `genai.tool.output` is a JSON object with at least `type` and `message`, and the original exception still propagates to MAF unchanged.

## Retry Detection

Retry tracking is scoped to the current parent `Activity`. If the same tool call id appears more than once under that parent, subsequent spans include:

```text
genai.tool.is_retry = true
genai.tool.attempt_index = 2, 3, ...
```

When no call id or parent activity is available, retry attributes are omitted. Tool execution and span creation continue normally.

## Reliability And Privacy

Telemetry is best-effort. Span creation, tag writing, retry bookkeeping, and serialization failures are swallowed and never alter tool results.

The Tools package does not redact payloads. If tool inputs or outputs may contain sensitive data, disable capture or use the future Redaction package when it becomes available.