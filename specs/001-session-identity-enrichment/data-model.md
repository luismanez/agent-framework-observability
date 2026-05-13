# Data Model: Session Identity and Enrichment

**Feature Branch**: `001-session-identity-enrichment`
**Created**: 2026-05-13

---

## Primary Entity: `SessionStateBlock`

**Namespace**: `Melic.AgentFramework.Observability.Sessions` (internal)
**Assembly**: `Melic.AgentFramework.Observability.Sessions`
**Persistence**: JSON in `AgentSession.StateBag` under key `"__melic_telemetry"` (configurable)

```csharp
// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Sessions;

internal sealed record SessionStateBlock
{
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    [JsonPropertyName("invocationIndex")]
    public int InvocationIndex { get; init; }

    [JsonPropertyName("totalInputTokens")]
    public long TotalInputTokens { get; init; }

    [JsonPropertyName("totalOutputTokens")]
    public long TotalOutputTokens { get; init; }

    [JsonPropertyName("firstSeenUtc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTimeOffset FirstSeenUtc { get; init; }

    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>Preserves fields written by newer library versions on restore by older ones.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

### Fields

| Field | Type | Default | Notes |
|---|---|---|---|
| `SessionId` | `string?` | `null` | GUID string. Assigned on first invocation (auto) or by developer. Immutable after first write. |
| `InvocationIndex` | `int` | `0` | 1-based on spans (emitted as `InvocationIndex + 1` before increment — incremented then persisted). |
| `TotalInputTokens` | `long` | `0` | Running sum of `AgentResponse.Usage.InputTokenCount`. |
| `TotalOutputTokens` | `long` | `0` | Running sum of `AgentResponse.Usage.OutputTokenCount`. |
| `FirstSeenUtc` | `DateTimeOffset` | `default` | Set once on first invocation. `default` is treated as "not yet seen". |
| `Tags` | `IReadOnlyDictionary<string, string>?` | `null` | Custom developer tags. Max 50 entries; key ≤128 chars; value ≤512 chars. |
| `ExtensionData` | `Dictionary<string, JsonElement>?` | `null` | Forward-compat passthrough for unknown fields from newer versions. |

### State Transitions

```
[no block in StateBag]
        │
        │  GetOrInitialise()
        ▼
[SessionStateBlock { SessionId=null, InvocationIndex=0, ... }]
        │
        │  First RunCoreAsync()
        ▼
[SessionStateBlock { SessionId=<GUID>, InvocationIndex=1, FirstSeenUtc=now, ... }]
        │
        │  Subsequent RunCoreAsync()
        ▼
[SessionStateBlock { ..., InvocationIndex=N, TotalInputTokens=T_in, TotalOutputTokens=T_out }]
```

### Validation Rules

- `SessionId` once non-null, **never overwritten** except by explicit `AssignSessionId()` call.
- `InvocationIndex` increment is atomic with the StateBag write (read → mutate → write within lock).
- `Tags` entries that violate limits are **silently rejected**. No partial writes.
- `FirstSeenUtc` is set once and treated as immutable thereafter.

---

## Supporting Entity: `SessionTelemetryOptions`

**Namespace**: `Melic.AgentFramework.Observability.Sessions` (public)

```csharp
public sealed class SessionTelemetryOptions
{
    public bool TrackTokenAggregates { get; set; } = true;
    public bool EnableSessionSpan { get; set; } = false;
    public string StateBagKey { get; set; } = "__melic_telemetry";
    public string ActivitySourceName { get; set; } = "Melic.AgentFramework.Observability.Sessions";
    public string MeterName { get; set; } = "Melic.AgentFramework.Observability.Sessions";
}
```

| Property | Default | Notes |
|---|---|---|
| `TrackTokenAggregates` | `true` | Controls `genai.session.total_input_tokens` / `total_output_tokens` tags |
| `EnableSessionSpan` | `false` | Mode B. When false, `BeginSessionTrace()` is a no-op |
| `StateBagKey` | `"__melic_telemetry"` | Reserved key. Must not be written by application code |
| `ActivitySourceName` | `"Melic.AgentFramework.Observability.Sessions"` | Used when starting the optional session span |
| `MeterName` | `"Melic.AgentFramework.Observability.Sessions"` | Meter name for all metrics in this package |

---

## Supporting Entity: `SessionTag`

Not a first-class class — represented as `KeyValuePair<string, string>` entries in
`SessionStateBlock.Tags`. Enforced limits:

| Constraint | Value | Enforcement |
|---|---|---|
| Max tags per session | 50 | Silent reject on `SetSessionTag()` if count ≥ 50 |
| Max key length | 128 chars | Silent reject before write |
| Max value length | 512 chars | Silent reject before write |
| Reserved key prefix | `"genai.session."` | `SetSessionTag()` rejects keys matching prefix (FR-013) |
| Empty / whitespace key | — | Silent reject |

---

## Attribute Constants (in Abstractions)

**Class**: `SessionAttributeNames` in `Melic.AgentFramework.Observability.Abstractions`

| Constant | Value | Used in |
|---|---|---|
| `SessionId` | `"genai.session.id"` | FR-003 |
| `InvocationIndex` | `"genai.session.invocation_index"` | FR-004 |
| `FirstSeen` | `"genai.session.first_seen"` | FR-005 |
| `AgeSeconds` | `"genai.session.age_seconds"` | FR-005 |
| `TotalInputTokens` | `"genai.session.total_input_tokens"` | FR-006 |
| `TotalOutputTokens` | `"genai.session.total_output_tokens"` | FR-006 |
| `MessageCount` | `"genai.session.message_count"` | TDD §5.3 |
| `TotalInvocations` | `"genai.session.total_invocations"` | FR-015 (Mode B) |
| `DurationSeconds` | `"genai.session.duration_seconds"` | FR-015 (Mode B) |
| `Errors` | `"genai.session.errors"` | FR-015 (Mode B) |
| `StartTime` | `"genai.session.start_time"` | Mode B span initial tag |

---

## Metrics Schema

**Meter**: `Melic.AgentFramework.Observability.Sessions`

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `genai.session.invocations` | `Counter<long>` | events | `gen_ai.agent.name` |
| `genai.session.duration` | `Histogram<long>` | ms | `gen_ai.agent.name` |
| `genai.session.active` | `UpDownCounter<long>` | sessions | `gen_ai.agent.name` |
| `session.statebag.write.failures` | `Counter<long>` | events | `gen_ai.agent.name` |

> `genai.session.id` MUST NOT appear in metric tags (high-cardinality).
