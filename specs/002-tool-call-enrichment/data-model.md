# Data Model: Tool Call Span Enrichment

**Feature Branch**: `002-tool-call-enrichment`
**Created**: 2026-05-19

---

## Primary Entity: `ToolInvocationData`

**Namespace**: `Melic.AgentFramework.Observability.Abstractions`
**Assembly**: `Melic.AgentFramework.Observability.Abstractions`
**Lifetime**: Per tool invocation, created by an adapter from the public MAF `FunctionInvocationContext`

```csharp
// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Abstractions;

public sealed record ToolInvocationData
{
    public required string ToolName { get; init; }
    public string? CallId { get; init; }
    public required object? InputPayload { get; init; }
    public required Activity? ParentActivity { get; init; }
    public required DateTimeOffset StartedUtc { get; init; }
}
```

### Fields

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `ToolName` | `string` | Yes | Source for fallback `genai.tool.name`. Never null or whitespace after mapping. MAF `execute_tool` spans use MAF's standard `gen_ai.tool.name`. |
| `CallId` | `string?` | No | Source for fallback `genai.tool.call_id`, `genai.tool.is_retry`, and `genai.tool.attempt_index`. MAF `execute_tool` spans use MAF's standard `gen_ai.tool.call.id`. |
| `InputPayload` | `object?` | Yes | Raw argument graph to serialize as valid JSON. Empty input becomes `{}`. |
| `ParentActivity` | `Activity?` | Yes | Current activity at tool start; used to find MAF `execute_tool`, fallback parenting, and retry-state scoping. |
| `StartedUtc` | `DateTimeOffset` | Yes | Diagnostic timestamp for internal bookkeeping only. |

### Options Validation Rules

- `ToolName` falls back from `context.Function.Name` to `context.CallContent.Name`; if both are missing, the mapper substitutes a non-empty sentinel like `"unknown_tool"`.
- `CallId` is treated as absent when null, empty, or whitespace.
- `ParentActivity` may be null; this disables retry tracking for that invocation and allows a fallback tool span to float as a root span.

---

## Supporting Entity: `ToolTelemetryOptions`

**Namespace**: `Melic.AgentFramework.Observability.Tools` (public)

```csharp
public sealed class ToolTelemetryOptions
{
    public bool CaptureInput { get; set; } = true;
    public bool CaptureOutput { get; set; } = true;
    public int MaxInputLength { get; set; } = 2048;
    public int MaxOutputLength { get; set; } = 2048;
    public string ActivitySourceName { get; set; } = "Melic.AgentFramework.Observability.Tools";
}
```

| Property | Default | Notes |
| --- | --- | --- |
| `CaptureInput` | `true` | Controls whether `genai.tool.input` is emitted. |
| `CaptureOutput` | `true` | Controls whether `genai.tool.output` is emitted. |
| `MaxInputLength` | `2048` | Maximum final character length for `genai.tool.input`. Must remain valid JSON. |
| `MaxOutputLength` | `2048` | Maximum final character length for `genai.tool.output`. Must remain valid JSON. |
| `ActivitySourceName` | `"Melic.AgentFramework.Observability.Tools"` | Source name for fallback `agent_tool_call` spans when no MAF `execute_tool` span is current. |

### Validation Rules

- `MaxInputLength` and `MaxOutputLength` must be positive integers.
- `ActivitySourceName` must not be null, empty, or whitespace.
- Invalid option values should fail fast during builder configuration rather than at tool-call time.

---

## Supporting Entity: `InvocationAttemptRegistry`

**Namespace**: `Melic.AgentFramework.Observability.Tools.Internal`
**Lifetime**: Process-local, package-internal singleton per built agent

```csharp
internal sealed class InvocationAttemptRegistry
{
    private readonly ConditionalWeakTable<Activity, ConcurrentDictionary<string, int>> _attempts;
}
```

### Responsibilities

| Responsibility | Notes |
| --- | --- |
| Scope retry state to one `invoke_agent` span | Keyed by parent `Activity` instance, or by MAF `execute_tool` trace/parent span identity when the invocation Activity object is not directly current. |
| Count attempts per `call_id` | First occurrence = 1, subsequent occurrences increment atomically. |
| Support concurrency safely | Backed by `ConcurrentDictionary<string, int>`. |
| Avoid memory leaks | `ConditionalWeakTable` releases state when the parent `Activity` is no longer referenced. |

### State Transitions

```text
[parent Activity not seen]
        │
        │ Record(callId = "abc")
        ▼
[parent Activity -> { "abc": 1 }]
        │
        │ Record(callId = "abc")
        ▼
[parent Activity -> { "abc": 2 }]
        │
        │ parent activity completes and becomes unreachable
        ▼
[state collected automatically]
```

---

## Supporting Entity: `ToolPayloadSerializer`

**Namespace**: `Melic.AgentFramework.Observability.Tools.Internal`
**Purpose**: Convert inputs, outputs, and errors into valid JSON strings bounded by configured length.

### Inputs

| Input kind | Source |
| --- | --- |
| Success input | `ToolInvocationData.InputPayload` |
| Success output | Tool result object returned from `next(...)` |
| Error output | Exception object converted to `{ type, message }` |

### Output Rules

| Rule | Effect |
| --- | --- |
| Empty input arguments | Serialize as `{}` |
| Null result | Serialize as `null` |
| Oversized string values | Truncate string leaves first |
| Oversized nested arrays/objects | Replace oversized subtrees with compact sentinel strings if needed |
| Serialization failure | Omit the corresponding attribute entirely |

---

## Attribute Constants (in Abstractions)

**Class**: `ToolAttributeNames` in `Melic.AgentFramework.Observability.Abstractions`

| Constant | Value | Used in |
| --- | --- | --- |
| `ToolName` | `"genai.tool.name"` | Fallback `agent_tool_call` spans only |
| `ToolCallId` | `"genai.tool.call_id"` | Fallback `agent_tool_call` spans only; retry correlation uses the internal mapped call id |
| `ToolInput` | `"genai.tool.input"` | Optional JSON input payload |
| `ToolOutput` | `"genai.tool.output"` | Optional JSON output or error payload |
| `IsRetry` | `"genai.tool.is_retry"` | Retry marker when `call_id` repeats within one invocation |
| `AttemptIndex` | `"genai.tool.attempt_index"` | 1-based occurrence counter per `call_id` and parent invocation |

---

## Span Schema

**Primary target span**: MAF `execute_tool`
**Fallback span name**: `agent_tool_call`
**Fallback ActivityKind**: `Internal`
**Fallback ActivitySource**: Configurable, default `Melic.AgentFramework.Observability.Tools`

| Attribute | Type | Required | Notes |
| --- | --- | --- | --- |
| `genai.tool.name` | `string` | Fallback only | Tool/function name on fallback `agent_tool_call` spans; MAF `execute_tool` spans use `gen_ai.tool.name` |
| `genai.tool.call_id` | `string` | Fallback only | Omitted when absent; MAF `execute_tool` spans use `gen_ai.tool.call.id` |
| `genai.tool.input` | `string` | No | Valid JSON string; omitted when capture disabled or serialization fails |
| `genai.tool.output` | `string` | No | Valid JSON string; omitted when capture disabled or serialization fails |
| `genai.tool.is_retry` | `bool` | No | Omitted when `call_id` absent or retry tracking unavailable |
| `genai.tool.attempt_index` | `int` | No | Omitted when `call_id` absent or retry tracking unavailable |
| OpenTelemetry span status code | status | Yes | `OK` or `ERROR` via `Activity.SetStatus` |
| OpenTelemetry span status description | status | No | Exception message on failure via `Activity.SetStatus` |

---

## Public API Surface

This feature introduces exactly two public types in the Tools package:

- `ToolTelemetryAgentBuilderExtensions`
- `ToolTelemetryOptions`

Everything else (`ToolTelemetryAgent`, `ToolInvocationMapper`, `InvocationAttemptRegistry`, `ToolPayloadSerializer`) remains internal and may evolve without notice.
