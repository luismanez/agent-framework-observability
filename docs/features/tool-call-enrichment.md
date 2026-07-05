# Tool Call Span Enrichment

`Melic.AgentFramework.Observability.Tools` enriches Microsoft Agent Framework (MAF) tool telemetry without replacing it. MAF already emits `execute_tool` spans with standard `gen_ai.tool.*` identity attributes such as tool name and call id; this package adds bounded payload capture, retry markers, and fallback tool spans only when no MAF tool span is current.

## Installation

```xml
<PackageReference Include="Melic.AgentFramework.Observability.Tools" Version="0.1.0" />
```

The package targets `net8.0`, `net9.0`, and `net10.0`.

## Usage

### Minimal setup

Add `.UseToolTelemetry()` to the agent builder after `.UseOpenTelemetry()`:

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Tools;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseOpenTelemetry()
    .UseToolTelemetry()
    .Build();

AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync("Check order 42", session);
```

For each executed tool, the MAF `execute_tool` span keeps MAF's standard `gen_ai.tool.name` and `gen_ai.tool.call.id` attributes and receives package-owned `genai.tool.input`, `genai.tool.output`, `genai.tool.is_retry`, and `genai.tool.attempt_index` attributes when applicable.

### Disable payload capture

If you only want MAF's built-in timing, tool identity, call id, and status, disable input and output capture independently:

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

### Keep payloads but bound their size

Payloads are serialized as JSON and shrunk before final serialization so the emitted value remains valid JSON within the configured limit.

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

### Combine with Sessions telemetry

Tools and Sessions are independent packages. When both are present, Sessions enriches the enclosing `invoke_agent` span and Tools enriches the MAF `execute_tool` spans below it.

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Sessions;
using Melic.AgentFramework.Observability.Tools;

AIAgent agent = new AIAgentBuilder(myInnerAgent)
    .UseOpenTelemetry()
    .UseSessionTelemetry()
    .UseToolTelemetry()
    .Build();
```

### Register trace sources

Register MAF's activity source to see the parent invocation and MAF tool-call spans. Register the Tools source only if you want fallback `agent_tool_call` spans when no MAF `execute_tool` span is current.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Tools") // fallback spans only
    .AddConsoleExporter()
    .Build();
```

If you customize `ToolTelemetryOptions.ActivitySourceName`, register that custom source for fallback spans.

## Public API Reference

### `UseToolTelemetry`

```csharp
namespace Melic.AgentFramework.Observability.Tools;

public static class ToolTelemetryAgentBuilderExtensions
{
    public static AIAgentBuilder UseToolTelemetry(
        this AIAgentBuilder builder,
        Action<ToolTelemetryOptions>? configure = null);
}
```

`UseToolTelemetry()` is optional and opt-in. When it is not configured, agent and tool behavior remain unchanged.

### `ToolTelemetryOptions`

```csharp
namespace Melic.AgentFramework.Observability.Tools;

public sealed class ToolTelemetryOptions
{
    public bool CaptureInput { get; set; } = true;
    public bool CaptureOutput { get; set; } = true;
    public int MaxInputLength { get; set; } = 2048;
    public int MaxOutputLength { get; set; } = 2048;
    public string ActivitySourceName { get; set; } = "Melic.AgentFramework.Observability.Tools";
}
```

## Configuration Options

| Option | Default | Description |
| --- | --- | --- |
| `CaptureInput` | `true` | Emits JSON-serialized tool input as `genai.tool.input` when serialization succeeds. |
| `CaptureOutput` | `true` | Emits JSON-serialized tool output, or structured error output, as `genai.tool.output` when serialization succeeds. |
| `MaxInputLength` | `2048` | Maximum final character length for `genai.tool.input`. The emitted value remains valid JSON. |
| `MaxOutputLength` | `2048` | Maximum final character length for `genai.tool.output`. The emitted value remains valid JSON. |
| `ActivitySourceName` | `Melic.AgentFramework.Observability.Tools` | ActivitySource name for fallback `agent_tool_call` spans. This does not affect MAF `execute_tool` spans. |

`MaxInputLength` and `MaxOutputLength` must be positive. `ActivitySourceName` must not be null, empty, or whitespace.

## Telemetry Schema

### Target spans

| Field | Value |
| --- | --- |
| Primary target | Current MAF `execute_tool` span |
| Fallback span | `agent_tool_call` from the configured Tools activity source |
| Fallback kind | `Internal` |
| Fallback parent | Current `Activity` at tool-call start |
| Status | `OK` on success, `ERROR` with exception message on failure |

### Attributes

| Attribute | Type | Notes |
| --- | --- | --- |
| `gen_ai.tool.name` | string | MAF standard attribute on `execute_tool` spans. Use this for tool identity on MAF spans. |
| `gen_ai.tool.call.id` | string | MAF standard attribute on `execute_tool` spans when a call id is available. |
| `genai.tool.name` | string | Fallback `agent_tool_call` spans only. MAF `execute_tool` spans already carry `gen_ai.tool.name`. |
| `genai.tool.call_id` | string | Fallback `agent_tool_call` spans only. Omitted when absent. |
| `genai.tool.input` | string | Valid JSON input payload. Omitted when input capture is disabled or serialization fails. |
| `genai.tool.output` | string | Valid JSON output payload, or structured error JSON with at least `type` and `message`. Omitted when output capture is disabled or serialization fails. |
| `genai.tool.is_retry` | bool | `false` for the first observed call id in an invocation, `true` for repeated call ids. Omitted when no call id is available. |
| `genai.tool.attempt_index` | int | 1-based occurrence count for the call id in the current invocation. Omitted when no call id is available. |

All package-owned attribute keys are available as constants on `Melic.AgentFramework.Observability.Abstractions.ToolAttributeNames`.

## Reliability And Privacy

Telemetry is best-effort. Span enrichment, fallback span creation, retry bookkeeping, and serialization failures never alter tool results or exceptions.

The Tools package does not redact payloads. If tool inputs or outputs may contain sensitive data, disable capture or apply redaction before exporting telemetry.

## See Also

- [Spec quickstart](../../specs/002-tool-call-enrichment/quickstart.md)
- [Feature specification](../../specs/002-tool-call-enrichment/spec.md)
- [Technical reference](../design/technical-reference.md)
