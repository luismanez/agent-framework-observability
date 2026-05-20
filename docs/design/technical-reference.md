# Melic.AgentFramework.Observability — Technical Reference

> An open-source .NET library suite that extends the Microsoft Agent Framework (MAF)
> with production-grade telemetry utilities on top of OpenTelemetry.

| | |
|---|---|
| **Status** | Active |
| **Author** | Luis Mañez |
| **Last updated** | 2026-05 |
| **Targets** | `net8.0`, `net9.0`, `net10.0` |
| **License** | MIT |

---

## Table of Contents

1. [Background](#1-background)
2. [Architecture Overview](#2-architecture-overview)
3. [Package: Performance](#3-package-melicagentframeworkobservabilityperformance)
4. [Package: Tools](#4-package-melicagentframeworkobservabilitytools)
5. [Package: Sessions](#5-package-melicagentframeworkobservabilitysessions)
6. [Package: Redaction](#6-package-melicagentframeworkobservabilityredaction)
7. [Cross-cutting Concerns](#7-cross-cutting-concerns)
8. [End-to-End Example](#8-end-to-end-example)
9. [Future Packages](#9-future-packages)
10. [Key Decisions](#10-key-decisions)

---

## 1. Background

### 1.1 What MAF emits today

The Microsoft Agent Framework already integrates with OpenTelemetry through:

- **`OpenTelemetryAgent`** (`dotnet/src/Microsoft.Agents.AI/OpenTelemetryAgent.cs`) — a delegating `AIAgent` that wraps `OpenTelemetryChatClient` from `Microsoft.Extensions.AI`. It follows OpenTelemetry **Semantic Conventions for Generative AI v1.37** (experimental).
- **`UseOpenTelemetry(sourceName, configure)`** extension method on `AIAgentBuilder`.
- Default `ActivitySource` name: `Experimental.Microsoft.Agents.AI`.

It produces, per agent invocation, a span named `invoke_agent <name>` with tags:

- `gen_ai.agent.id`
- `gen_ai.agent.name`
- `gen_ai.agent.description`
- `gen_ai.operation.name = "invoke_agent"`
- `gen_ai.provider.name`

Plus the chat-client tags inherited from `OpenTelemetryChatClient`:
`gen_ai.request.model`, `gen_ai.response.model`, `gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, etc.

For tools, the MAF OpenTelemetry stack emits `execute_tool {tool_name}` spans through `Microsoft.Extensions.AI`'s `OpenTelemetryChatClient` when wrapping a `FunctionInvokingChatClient`. These spans use the official `gen_ai.*` semantic-convention namespace, including attributes such as `gen_ai.operation.name`, `gen_ai.tool.name`, `gen_ai.tool.call.id`, and, when sensitive data is enabled, tool arguments and results.

A single `EnableSensitiveData` boolean controls whether prompts, completions, function arguments, and function results are written to telemetry.

### 1.2 Gaps this library fills

1. **No TTFT (time-to-first-token)** measurement for streaming responses.
2. **No detection of streaming stalls** (gaps between tokens).
3. **HTTP retries (429/503/timeouts) are not correlated** with the originating agent invocation.
4. **No token usage limits / circuit breaker** at agent or session level.
5. **Tool telemetry is per-call only** — no bounded payload capture, retry markers, or package-owned tool attributes for cross-package dashboards.
6. **No persistent session identity** — `AgentSession` has no stable ID exposed by MAF; correlating multi-turn conversations across invocations requires manual scaffolding.
7. **Sensitive-data control is a single boolean** — no redaction pipeline, no PII masking.

### 1.3 Package value over MAF out of the box

The goal of this library is not to replace MAF telemetry. Each package should make the telemetry MAF already emits easier to operate in production. The tables below are the package-level positioning contract: if a package does not add one of these practical improvements, it should not exist as a separate package.

#### Sessions

| Area | MAF out of the box | `Melic.AgentFramework.Observability.Sessions` improvement | Use the package when |
|---|---|---|---|
| Invocation span | Emits `invoke_agent` spans with `gen_ai.agent.*`, model, provider, and usage attributes when `UseOpenTelemetry()` is enabled. | Enriches each `invoke_agent` span with stable `genai.session.*` attributes. | You need to correlate multiple invocations as one logical conversation. |
| Session identity | Provides `AgentSession`, but no stable telemetry session id contract across requests/processes. | Assigns or accepts a stable `genai.session.id` and persists it in session state. | Your app has conversation IDs, tickets, users, or workflow IDs that must be queryable in telemetry. |
| Multi-turn counters | Token usage is per invocation. Cross-turn aggregates are left to the application. | Adds invocation index, session age, total input/output tokens, and total invocation count. | You need running totals per conversation, not just per model call. |
| Business context | MAF does not manage custom conversation tags. | Adds session tags once and propagates them to every invocation span. | You want tags such as tenant, plan, region, or workflow on every span without repeating code. |
| Visual trace grouping | Trace grouping follows the current request/activity context. Cross-request conversations are separate traces. | Optional Mode B creates an explicit `agent_session` parent span for bounded in-process sessions. | You want one visual trace tree for a short-lived chat, demo, batch unit, or workflow. |

#### Tools

| Area | MAF out of the box | `Melic.AgentFramework.Observability.Tools` improvement | Use the package when |
|---|---|---|---|
| Baseline tool visibility | Emits `execute_tool` spans with standard attributes such as `gen_ai.operation.name`, `gen_ai.tool.name`, `gen_ai.tool.call.id`, `gen_ai.tool.type`, and tool description. | No added value by itself; the package deliberately reuses this span and does not duplicate MAF's standard tool identity attributes on it. | Do not install this package only to get tool spans, tool names, or call ids; MAF already provides them. |
| Arguments and results | Controlled by MAF's broad `EnableSensitiveData` switch. Payloads may be absent or large. | Captures input/output independently with `CaptureInput`, `CaptureOutput`, `MaxInputLength`, and `MaxOutputLength`, preserving valid JSON after truncation. | You need bounded payload diagnostics without enabling every MAF sensitive-data field globally. |
| Retry/loop signals | Shows individual tool executions, but does not mark repeated call ids as retries. | Adds `genai.tool.is_retry` and `genai.tool.attempt_index` per invocation scope. | You need to spot repeated tool calls, retries, or model/tool loops quickly. |
| Fallback instrumentation | Requires the MAF `execute_tool` span to be current for tool-span telemetry. | Emits `agent_tool_call` with `genai.tool.name` and optional `genai.tool.call_id` only when there is no MAF tool span to enrich. | You run a nonstandard pipeline where the function middleware executes without a current MAF `execute_tool` activity. |
| Future safety controls | MAF has no package-specific redaction hook for tool payloads. | Provides a stable payload surface for future Redaction integration. | You want tool payload observability today with a path to policy-based masking later. |

#### Redaction

| Area | MAF out of the box | `Melic.AgentFramework.Observability.Redaction` improvement | Use the package when |
|---|---|---|---|
| Sensitive data switch | `EnableSensitiveData` is an all-or-nothing telemetry detail switch. | Planned processor pipeline can inspect and redact selected fields before export. | You need to keep useful telemetry while masking secrets, PII, or regulated data. |
| Scope | MAF writes spans/logs; export-time policy is left to the application/backend. | Planned Activity and log processors apply the same redaction policy consistently before telemetry leaves the process. | You want one application-side policy for prompts, completions, tool args/results, and logs. |
| Detectors | No built-in PII detector pipeline in MAF telemetry. | Planned standard redactors for email, credit cards, IBAN, JWTs, private keys, password-like fields, and optional high-risk detectors. | You need common detectors without hand-writing processors for each app. |
| Failure behavior | Application-defined. | Planned fail-safe modes ensure redactor failures do not leak raw data by default. | You require predictable privacy behavior under redactor exceptions/timeouts. |
| Extensibility | Custom processors are possible but app-specific. | Planned `IRedactor` extensibility, additional tag patterns, placeholders, and metrics. | You want reusable redaction components shared across agent apps. |

### 1.4 Non-goals

- **No model price catalog.** Pricing is too volatile and provider-fragmented to be maintained in this library. A "Bring Your Own Pricing Resolver" satellite package (`Melic.AgentFramework.Observability.Cost.Contracts`) is planned post-MVP.
- **No pre-call token estimation.** No tokenizer dependency (TikToken, Sharpen, etc.). All token-based limits are post-hoc circuit breakers.
- **No Microsoft Foundry Control Plane integration** — would require hosted infrastructure, contrary to the OSS-only goal.
- **No NER / semantic PII detection** in core. Names, addresses, etc. require a Presidio-style satellite package.
- **No country-specific PII detectors in core.** The library is globally neutral; locale-specific detectors live in optional satellite packages.
- **No quality / LLM-as-judge metrics.**

---

## 2. Architecture Overview

The library is split into independent packages. Each package can be installed and used standalone. Packages compose via the existing `AIAgentBuilder` and `TracerProvider`/`MeterProvider` builder pipelines.

```
┌──────────────────────────────────┐    ┌────────────────────────────────┐
│  Observability.Performance       │    │  Observability.Tools           │
│  - TTFT / stalls / retries       │    │  - execute_tool enrichment     │
│  - Token-limit circuit breaker   │    │  - Bounded payloads / retries  │
└──────────────┬───────────────────┘    └──────────────┬─────────────────┘
               │                                       │
               ▼                                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│           Microsoft Agent Framework (existing OpenTelemetryAgent)    │
└──────────────────────────────────────────────────────────────────────┘
               ▲                                       ▲
               │                                       │
┌──────────────┴───────────────────┐    ┌──────────────┴─────────────────┐
│  Observability.Sessions          │    │  Observability.Redaction       │
│  - Persistent SessionId          │    │  - Activity/Log processors     │
│  - Aggregate enrichment          │    │  - Pluggable redactor pipeline │
│  - Optional session span         │    │                                │
└──────────────────────────────────┘    └────────────────────────────────┘
                          ▲
                          │
              ┌───────────┴───────────┐
              │  Observability        │
              │  .Abstractions        │
              │  (shared interfaces,  │
              │   enums, constants)   │
              └───────────────────────┘
```

All four MVP packages share these design principles:

- **Decorator-based** for agent-pipeline concerns (`Performance`, `Tools`, `Sessions`).
- **OTel processor-based** for cross-cutting concerns (`Redaction`).
- **Provider-agnostic.** Works with any `AIAgent` implementation.
- **Fail-safe.** A misbehaving redactor or telemetry decorator must never break the agent run.
- **Cardinality-aware.** Per-session/per-agent IDs go on spans; metrics use bounded labels only.

### 2.1 Compatibility & registration order

This library extends, never replaces, MAF's `OpenTelemetryAgent`. Required ordering on the builder pipeline:

```csharp
agent.AsBuilder()
     .UseOpenTelemetry(sourceName: "MyApp")               // MUST come first
     .UsePerformanceTelemetry(...)                        // any order from here
     .UseToolTelemetry(...)
     .UseSessionTelemetry(...)
     .Build();
```

**Rationale:** `OpenTelemetryAgent` creates the `invoke_agent` `Activity`. The decorators above enrich that activity by reading `Activity.Current` after delegating downstream — they require the activity to already exist when their `RunCoreAsync` body runs.

`Melic.AgentFramework.Observability.Redaction` is registered separately on the `TracerProvider` / `LoggerProvider` builders, not on the agent pipeline.

---

## 3. Package: `Melic.AgentFramework.Observability.Performance`

### 3.1 Responsibilities

- Measure metrics not derivable from existing MAF spans:
  - Time-to-first-token (TTFT) for streaming.
  - Streaming stall events (inter-token gaps above a threshold).
  - HTTP retries (429 / 503 / transient timeouts) correlated to the agent invocation.
- Enforce **token-usage circuit breakers** per request and per session.

### 3.2 Public API

```csharp
namespace Melic.AgentFramework.Observability.Performance;

public static class PerformanceTelemetryAgentBuilderExtensions
{
    public static AIAgentBuilder UsePerformanceTelemetry(
        this AIAgentBuilder builder,
        Action<PerformanceTelemetryOptions>? configure = null);
}

public sealed class PerformanceTelemetryOptions
{
    /// <summary>Threshold above which an inter-token gap is counted as a stall. Default: 1s.</summary>
    public TimeSpan StreamingStallThreshold { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>If null, retry tracking is disabled. Default: enabled.</summary>
    public RetryTrackingOptions? RetryTracking { get; set; } = new();

    /// <summary>If null, token limits are disabled. Default: null.</summary>
    public TokenLimitPolicy? TokenLimits { get; set; }

    /// <summary>Meter name. Default: "Melic.AgentFramework.Observability.Performance".</summary>
    public string MeterName { get; set; } = "Melic.AgentFramework.Observability.Performance";
}

public sealed class RetryTrackingOptions
{
    /// <summary>HTTP status codes treated as transient retries.</summary>
    public HashSet<int> TransientStatusCodes { get; } = new() { 408, 425, 429, 500, 502, 503, 504 };
}

public sealed class TokenLimitPolicy
{
    public long? MaxTotalTokensPerRequest { get; set; }
    public long? MaxTotalTokensPerSession { get; set; }
    public long? MaxOutputTokensPerSession { get; set; }

    /// <summary>Default: Warn (does not throw on the next call).</summary>
    public TokenLimitAction OnExceeded { get; set; } = TokenLimitAction.Warn;

    /// <summary>Required when OnExceeded == Callback.</summary>
    public Func<TokenLimitContext, ValueTask<TokenLimitDecision>>? Callback { get; set; }
}

public enum TokenLimitAction { Warn, Throw, Callback }

public enum TokenLimitDecision { Continue, Abort, ResetCounters }

public sealed record TokenLimitContext(
    AIAgent Agent,
    AgentSession? Session,
    TokenLimitKind Kind,           // PerRequest | SessionTotal | SessionOutput
    long ObservedValue,
    long Limit);

public sealed class TokenBudgetExceededException : Exception { /* ... */ }
```

### 3.3 Tags emitted on `invoke_agent` span

| Tag | Type | Emitted when |
|---|---|---|
| `genai.latency.ttft_ms` | long | Streaming response with at least one content update |
| `genai.streaming.stalls` | int | Streaming response (always; may be 0) |
| `genai.streaming.stall_threshold_ms` | int | When `stalls > 0` |
| `genai.retries.count` | int | When `> 0` |
| `genai.retries.last_status` | int | When `count > 0` |
| `genai.tokens.limit_exceeded` | bool | True when this call tripped any limit |
| `genai.tokens.limit_kind` | string | `per_request` \| `session_total` \| `session_output` |

### 3.4 Metrics

Meter: `Melic.AgentFramework.Observability.Performance` (configurable).

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `genai.latency.ttft` | Histogram\<long\> | ms | `gen_ai.request.model`, `gen_ai.provider.name`, `gen_ai.agent.name` |
| `genai.streaming.stalls_observed` | Counter\<long\> | events | same as above |
| `genai.retries.observed` | Counter\<long\> | events | same + `status_code` |
| `genai.tokens.session_total` | UpDownCounter\<long\> | tokens | `gen_ai.agent.name` |
| `genai.tokens.limit_exceeded_total` | Counter\<long\> | events | `gen_ai.agent.name`, `limit_kind` |

> **Cardinality rule:** `gen_ai.session.id` and `gen_ai.agent.id` MUST NOT appear in metric tags.

### 3.5 Implementation notes

- `PerformanceTelemetryAgent : DelegatingAIAgent` instruments `RunCoreAsync` (no TTFT/stalls there) and `RunCoreStreamingAsync` (full instrumentation).
- TTFT = elapsed time from method entry to the first `AgentResponseUpdate` containing text content. "Content" is detected by `update.Contents` having a non-empty `TextContent` or any non-empty function-call/result content.
- Stalls = count of inter-token gaps where `(now - lastTokenAt) >= StreamingStallThreshold`. Computed against monotonic timestamps from a single `Stopwatch`.
- Retry tracking uses an `ActivityListener` registered for `System.Net.Http` and the agent source. On `OnEnd`, child activities of the active `invoke_agent` whose status code matches `TransientStatusCodes` are counted into a `ConditionalWeakTable<Activity, RetryState>` keyed by the parent.
- Token limits:
  - State is stored on the `AgentSession.StateBag` under key `__telemetry.tokens`.
  - On each call, after delegating, the resulting `Usage` is read from the response (`AgentResponse.Usage` if available, otherwise from `Activity.Current` tags).
  - Counters are updated; if any limit is exceeded, `genai.tokens.limit_exceeded=true` tag is set and `OnExceeded` is queued for the **next** call.
  - On the next `RunCoreAsync` of the same session, before delegating, the policy is enforced: `Throw` raises `TokenBudgetExceededException`; `Warn` logs and tags the new span with `genai.tokens.limit_already_exceeded=true`; `Callback` invokes the user delegate and acts on its `TokenLimitDecision`.
- Per-request limit is enforced after the response (next call blocked); same semantics as session limits.

### 3.6 Known limitations

- TTFT and stalls require streaming. In `RunAsync`, those tags/metrics are not emitted.
- Token limits are **post-hoc circuit breakers**, not hard limits — the call that tripped the limit completes and returns normally; only the **next** call in the same session is blocked.
- Retry tracking only sees retries that produce child `Activity` instances (i.e., `HttpClient` instrumentation must be enabled).

---

## 4. Package: `Melic.AgentFramework.Observability.Tools`

### 4.1 Responsibilities

- Enrich MAF's existing `execute_tool` span without duplicating MAF's standard `gen_ai.tool.*` identity attributes.
- Capture tool inputs and outputs independently from MAF's global `EnableSensitiveData` switch.
- Bound captured payload size while preserving valid JSON.
- Mark repeated tool call identifiers with retry metadata within one agent invocation.
- Emit a fallback `agent_tool_call` span only when no MAF `execute_tool` span is current.

### 4.2 Public API

```csharp
namespace Melic.AgentFramework.Observability.Tools;

public static class ToolTelemetryAgentBuilderExtensions
{
    public static AIAgentBuilder UseToolTelemetry(
        this AIAgentBuilder builder,
        Action<ToolTelemetryOptions>? configure = null);
}

public sealed class ToolTelemetryOptions
{
    /// <summary>Emit JSON-serialized tool input as genai.tool.input. Default: true.</summary>
    public bool CaptureInput { get; set; } = true;

    /// <summary>Emit JSON-serialized tool output or error as genai.tool.output. Default: true.</summary>
    public bool CaptureOutput { get; set; } = true;

    /// <summary>Maximum final character length for genai.tool.input. Default: 2048.</summary>
    public int MaxInputLength { get; set; } = 2048;

    /// <summary>Maximum final character length for genai.tool.output. Default: 2048.</summary>
    public int MaxOutputLength { get; set; } = 2048;

    /// <summary>ActivitySource name for fallback agent_tool_call spans only.</summary>
    public string ActivitySourceName { get; set; } = "Melic.AgentFramework.Observability.Tools";
}
```

### 4.3 Attributes added to the tool span

The primary target is MAF's current `execute_tool` span. If that span is not current, the package creates a fallback `agent_tool_call` span from `ToolTelemetryOptions.ActivitySourceName` and writes the same attributes there.

| Attribute | Type | Notes |
|---|---|---|
| `genai.tool.name` | string | Fallback `agent_tool_call` spans only. MAF `execute_tool` spans already carry `gen_ai.tool.name`. |
| `genai.tool.call_id` | string | Fallback `agent_tool_call` spans only. MAF `execute_tool` spans already carry `gen_ai.tool.call.id`. Omitted when absent. |
| `genai.tool.input` | string | Valid JSON input payload. Omitted when capture is disabled or serialization fails. |
| `genai.tool.output` | string | Valid JSON output or structured error payload. Omitted when capture is disabled or serialization fails. |
| `genai.tool.is_retry` | bool | `false` for first observed call id, `true` for repeated call ids within the invocation. |
| `genai.tool.attempt_index` | int | 1-based occurrence count for the call id within the invocation. |

### 4.4 Status and payload behavior

- On success, the target span status is set to `OK`.
- On exception, the target span status is set to `ERROR` with the exception message as status description, and `genai.tool.output` is a JSON object with at least `type` and `message` when output capture is enabled.
- Empty input serializes as `{}`.
- Null output serializes as `null`.
- Oversized payloads are shrunk before final serialization so the final attribute remains valid JSON and fits the configured length.
- Serialization failures omit the corresponding attribute and never affect tool execution.

### 4.5 Implementation notes

- `UseToolTelemetry()` uses MAF's public function-invocation middleware so it runs immediately around the actual tool execution.
- `ToolInvocationMapper` converts `FunctionInvocationContext` into the Abstractions-owned `ToolInvocationData` shape.
- `ToolTelemetryAgent` resolves the target span at tool-call start:
    - If `Activity.Current` has `gen_ai.operation.name = execute_tool`, enrich that activity with payload and retry attributes only.
    - Otherwise start fallback `agent_tool_call` from the configured `ActivitySource` and add fallback identity, payload, and retry attributes.
- Retry tracking uses the current invocation scope. In the normal MAF shape, repeated `execute_tool` spans are grouped by their trace id and parent span id; fallback paths use the current parent `Activity` instance.
- `ToolPayloadSerializer` uses `System.Text.Json` and a JSON DOM shrink pass to preserve valid JSON under configured limits.

### 4.6 Known limitations

- Bounded payload capture is not redaction. Disable capture or use the future Redaction package when payloads may contain sensitive data.
- Retry detection requires a non-empty tool call id. Without one, retry attributes are omitted.
- `ActivitySourceName` affects fallback spans only; it does not change MAF's `execute_tool` source.
- If MAF changes the marker used to identify `execute_tool` spans, the enrichment target detection may need to be updated.

---

## 5. Package: `Melic.AgentFramework.Observability.Sessions`

### 5.1 Responsibilities

- Provide a **stable, persistent `SessionId`** that survives `AgentSession` serialization/deserialization.
- Enrich each `invoke_agent` span with session-scoped tags and aggregates.
- Optionally open an explicit "session span" parent for short-lived in-memory scenarios (Mode B).

### 5.2 Public API

```csharp
namespace Melic.AgentFramework.Observability.Sessions;

public static class SessionTelemetryAgentBuilderExtensions
{
    public static AIAgentBuilder UseSessionTelemetry(
        this AIAgentBuilder builder,
        Action<SessionTelemetryOptions>? configure = null);
}

public sealed class SessionTelemetryOptions
{
    /// <summary>Track input/output token aggregates per session. Default: true.</summary>
    public bool TrackTokenAggregates { get; set; } = true;

    /// <summary>Enable Mode B (BeginSessionTrace). Default: false.</summary>
    public bool EnableSessionSpan { get; set; } = false;

    public string SourceName { get; set; } = "Melic.AgentFramework.Observability.Sessions";
    public string MeterName { get; set; } = "Melic.AgentFramework.Observability.Sessions";

    /// <summary>StateBag key used to persist telemetry state. Default: "__telemetry".</summary>
    public string StateBagKey { get; set; } = "__telemetry";
}

public static class SessionTelemetryExtensions
{
    /// <summary>Returns the stable session id, generating one if absent.</summary>
    public static string GetOrAssignSessionId(this AgentSession session);

    /// <summary>Overrides the session id. Use before the first invocation.</summary>
    public static void AssignSessionId(this AgentSession session, string sessionId);

    /// <summary>Adds a custom tag propagated to every invoke_agent span of this session.</summary>
    public static void SetSessionTag(this AgentSession session, string key, string value);

    /// <summary>Mode B: opens a parent span grouping subsequent invocations on the current async context.</summary>
    public static IDisposable BeginSessionTrace(this AIAgent agent, AgentSession session);
}
```

### 5.3 Tags emitted on `invoke_agent` span

| Tag | Type | Notes |
|---|---|---|
| `genai.session.id` | string | stable, persisted in StateBag |
| `genai.session.invocation_index` | int | 1-based, increments per call |
| `genai.session.message_count` | int | only if a `ChatHistoryProvider` is reachable |
| `genai.session.total_input_tokens` | long | running total (when `TrackTokenAggregates` true) |
| `genai.session.total_output_tokens` | long | running total |
| `genai.session.age_seconds` | long | since first observed invocation |
| `genai.session.first_seen` | string (ISO 8601) | timestamp |
| user-defined session tags | string | from `SetSessionTag` |

### 5.4 Mode B (session span)

When `EnableSessionSpan = true` and the user calls `agent.BeginSessionTrace(session)`:

- Span name: `agent_session <agent.name>`.
- Source: `Melic.AgentFramework.Observability.Sessions` (configurable).
- Initial tags: `genai.session.id`, `gen_ai.agent.name`, `gen_ai.agent.id`, `genai.session.start_time`.
- Subsequent `invoke_agent` calls become children via `Activity.Current` propagation through `AsyncLocal`.
- On dispose, final tags are added: `genai.session.total_invocations`, `genai.session.total_input_tokens`, `genai.session.total_output_tokens`, `genai.session.duration_seconds`, `genai.session.errors`.
- A span event `session.ended` is emitted before the activity stops.

### 5.5 Metrics

Meter: `Melic.AgentFramework.Observability.Sessions`.

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `genai.session.invocations` | Counter\<long\> | events | `gen_ai.agent.name` |
| `genai.session.duration` | Histogram\<long\> | ms | `gen_ai.agent.name` |
| `genai.session.active` | UpDownCounter\<long\> | sessions | `gen_ai.agent.name` |

> `genai.session.id` MUST NOT appear in metric tags.

### 5.6 Implementation notes

- `SessionTelemetryAgent : DelegatingAIAgent`:
  1. Reads or creates the `__telemetry` block in `session.StateBag` (a JSON-serializable record with: `SessionId`, `InvocationIndex`, `TotalInputTokens`, `TotalOutputTokens`, `FirstSeenUtc`, `CustomTags`).
  2. Increments `InvocationIndex` and persists.
  3. Delegates.
  4. After delegation, reads `Activity.Current` (the `invoke_agent` span) and writes session tags.
  5. Reads usage from response, updates aggregates, persists to StateBag.
  6. Records metrics.
- `SetSessionTag` mutates the StateBag; the next invocation picks them up.
- Mode B uses an `AsyncLocal<Activity?>` slot internally to ensure invocations made within the `using` block are parented to the session span.
- `GetOrAssignSessionId` is idempotent and thread-safe via `lock` on the StateBag.

### 5.7 Known limitations

- The library cannot infer when a session "ends" without explicit user signalling (Mode B `using`).
- Mode B is only meaningful for short-lived sessions within a single process; cross-process session resumption uses Mode A enrichment only.
- Message count is only emitted when an `InMemoryChatHistoryProvider` (or compatible) is reachable via `AgentSessionExtensions.TryGetInMemoryChatHistory`.

---

## 6. Package: `Melic.AgentFramework.Observability.Redaction`

### 6.1 Responsibilities

- Redact sensitive content from telemetry **after** MAF has written it but **before** export.
- Apply a configurable pipeline of `IRedactor` instances to span tags and log records.
- Remain globally neutral — no country-specific assumptions in core.

### 6.2 Architecture

Implemented as **OpenTelemetry processors**, not as agent decorators:

- `RedactingActivityProcessor : BaseProcessor<Activity>` — invoked on `OnEnd` of each activity.
- `RedactingLogRecordProcessor : BaseProcessor<LogRecord>` — invoked on each log record.

Both processors share the same `RedactionPipeline` and the same `RedactionOptions`.

### 6.3 Public API

```csharp
namespace Melic.AgentFramework.Observability.Redaction;

public static class RedactionTracerProviderBuilderExtensions
{
    public static TracerProviderBuilder AddRedaction(
        this TracerProviderBuilder builder,
        Action<RedactionOptions> configure);
}

public static class RedactionLoggerProviderBuilderExtensions
{
    public static OpenTelemetryLoggerOptions AddRedaction(
        this OpenTelemetryLoggerOptions options,
        Action<RedactionOptions> configure);
}

public sealed class RedactionOptions
{
    public IList<IRedactor> Redactors { get; } = new List<IRedactor>();

    /// <summary>Default: All.</summary>
    public RedactionField Fields { get; set; } = RedactionField.All;

    /// <summary>Default: Token (e.g., "&lt;EMAIL&gt;").</summary>
    public RedactionPlaceholder Placeholder { get; set; } = RedactionPlaceholder.Token;

    /// <summary>Required when Placeholder == Custom.</summary>
    public Func<RedactionMatch, string>? CustomPlaceholderFactory { get; set; }

    /// <summary>Extra tag keys (regex) to include for redaction beyond the built-in allowlist.</summary>
    public IList<string> AdditionalTagPatterns { get; } = new List<string>();

    /// <summary>Default: FailSafe.</summary>
    public RedactionFailureMode FailureMode { get; set; } = RedactionFailureMode.FailSafe;

    /// <summary>Default: true.</summary>
    public bool EmitMetrics { get; set; } = true;

    /// <summary>Per-field timeout for the entire pipeline. Default: 100ms.</summary>
    public TimeSpan PerFieldTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

    public string MeterName { get; set; } = "Melic.AgentFramework.Observability.Redaction";
}

public static class RedactionOptionsExtensions
{
    /// <summary>Adds Email, CreditCard, IBAN, JWT, PrivateKey, PasswordPattern.</summary>
    public static RedactionOptions UseStandardRedactors(this RedactionOptions o);

    /// <summary>Adds every built-in redactor including ApiKey (entropy-based) and Guid.</summary>
    public static RedactionOptions UseAllBuiltInRedactors(this RedactionOptions o);

    public static RedactionOptions AddRegexRedactor(this RedactionOptions o,
        string name, string pattern, string? category = null);

    public static RedactionOptions AddRedactor(this RedactionOptions o,
        string name, Func<string, string> redact);
}

public interface IRedactor
{
    string Name { get; }
    RedactionResult Redact(string input, RedactionContext context);
}

public readonly record struct RedactionContext(
    RedactionField Field,
    string? TagName,
    string? AgentName,
    string? ToolName);

public sealed record RedactionResult(
    string Output,
    int MatchCount,
    IReadOnlyDictionary<string, int>? MatchesByCategory = null);

public sealed record RedactionMatch(
    string RedactorName,
    string Category,
    int OriginalLength,
    int StartIndex);

public enum RedactionPlaceholder { Token, Hash, Length, Stars, Custom }

[Flags]
public enum RedactionField
{
    None = 0,
    Prompt = 1,
    Completion = 2,
    ToolArguments = 4,
    ToolResult = 8,
    LogMessages = 16,
    All = Prompt | Completion | ToolArguments | ToolResult | LogMessages
}

public enum RedactionFailureMode { FailSafe, Drop, Throw }
```

### 6.4 Built-in redactors

All built-in redactors are internationally neutral.

| Redactor | Detection | Notes |
|---|---|---|
| `EmailRedactor` | Simplified RFC 5322 regex | Universal |
| `CreditCardRedactor` | Length 13–19 digits + Luhn checksum | Universal |
| `IbanRedactor` | ISO 13616 format + mod-97 checksum | Universal |
| `IpAddressRedactor` | IPv4 + IPv6 | Universal |
| `MacAddressRedactor` | Standard `xx:xx:xx:xx:xx:xx` / `xx-xx-...` | Universal |
| `UrlRedactor` | Configurable: full URL or query string only | Universal |
| `JwtRedactor` | Pattern `eyJ` + 3 base64url segments | Universal |
| `ApiKeyRedactor` | Length > 20 + Shannon entropy >= threshold (default 4.5) | High false-positive risk; opt-in via `UseAllBuiltInRedactors` |
| `PrivateKeyRedactor` | `-----BEGIN [TYPE] PRIVATE KEY-----` blocks | Universal |
| `PasswordPatternRedactor` | Regex on `password=`, `pwd:`, `secret:` etc. | Case-insensitive |
| `GuidRedactor` | UUID format | Opt-in via `UseAllBuiltInRedactors` |
| `PhoneNumberRedactor` | E.164 format only (`+CC...`) | Non-international formats not detected |

`UseStandardRedactors()` includes: **Email, CreditCard, IBAN, JWT, PrivateKey, PasswordPattern**.

### 6.5 Built-in tag allowlist

The processor only inspects tag values whose key matches:

- `gen_ai.prompt.*`
- `gen_ai.completion.*`
- `gen_ai.tool.call.*.arguments`
- `gen_ai.tool.call.*.result`
- Any user-supplied pattern in `AdditionalTagPatterns`.

Numeric tags and non-string tags are skipped.

### 6.6 Placeholder formats

| Placeholder | Example output for `[email protected]` |
|---|---|
| `Token` | `<EMAIL>` |
| `Hash` | `<EMAIL:9f3a82c4>` (SHA-256, first 8 hex chars) |
| `Length` | `<EMAIL:17chars>` |
| `Stars` | `*****************` |
| `Custom` | from `CustomPlaceholderFactory` |

Token labels come from the redactor's category (or its `Name`).

### 6.7 Metrics

Meter: `Melic.AgentFramework.Observability.Redaction`.

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `redaction.matches` | Counter\<long\> | matches | `redactor`, `field` |
| `redaction.duration` | Histogram\<long\> | ms | `redactor` |
| `redaction.failures` | Counter\<long\> | events | `redactor`, `failure_mode` |

### 6.8 Failure mode behavior

| Mode | On redactor exception |
|---|---|
| `FailSafe` (default) | Replace the entire field value with `<REDACTION_ERROR>`; emit failure metric. |
| `Drop` | Remove the tag entirely from the activity/log; emit failure metric. |
| `Throw` | Rethrow. **For tests only.** |

**Invariant:** unredacted data must never be leaked because of a redactor bug.

### 6.9 Implementation notes

- `RedactionPipeline` runs redactors in registration order. Recommended order (most specific first): PrivateKey → JWT → IBAN → CreditCard → Email → ApiKey → PasswordPattern → URL → IP → MAC → Phone → Guid.
- Each redactor receives the **already-partially-redacted** text from previous redactors (chained pipeline).
- `PerFieldTimeout` is enforced via `Stopwatch` + cancellation cooperation; redactors that exceed the budget are short-circuited under the active `FailureMode`.
- Allowlist matching is regex-cached per process.
- Redactors must be thread-safe and stateless. Built-in regexes are compiled once (`RegexOptions.Compiled`).
- A base class `RedactorBase` exposes a per-redactor `AllowList` (collection of literal strings) that bypasses detection — used to suppress known false positives. Consumers can extend `RedactorBase` for custom redactors.

### 6.10 Known limitations

- No NER / no semantic detection (no names, no addresses).
- No country-specific detectors in core.
- Phone numbers without international prefix are not detected.
- `ApiKeyRedactor` by entropy can produce false positives; not included in the default preset.
- No reversible tokenization.

---

## 7. Cross-cutting Concerns

### 7.1 Telemetry attribute naming conventions

- Library-defined attributes use the `genai.` prefix (no underscore in second segment), e.g., `genai.session.id`, `genai.tool.input`. This separates them from the official `gen_ai.*` semantic conventions while keeping them grouped lexicographically in dashboards.
- If the OTel GenAI SemConv adopts an equivalent attribute in a future release, the library will emit **both** the standard one and its `genai.*` counterpart for one minor version, then deprecate the latter with a documentation notice.

### 7.2 Cardinality discipline

- Spans may carry high-cardinality identifiers (`session.id`, `agent.id`, `tool.call.id`).
- Metrics MUST NOT include any of: `session.id`, `agent.id`, `tool.call.id`, raw user inputs, or per-request request IDs.

### 7.3 Failure isolation

- Each decorator runs telemetry work in `try`/`catch` that swallows non-cancellation exceptions and logs at `Warning`.
- A telemetry failure must never break the agent run.
- Exception: `TokenBudgetExceededException` is intentional and propagates normally.

### 7.4 Threading and async

- All decorators must be safe for concurrent invocation across sessions.
- Per-session state (token aggregates) is mutated under a `lock` taken on the `AgentSession` instance.
- Activity context propagation relies on `Activity.Current` and standard async-local flow; no custom `AsyncLocal` except the Mode B session span slot.

### 7.5 Configuration patterns

All packages follow a consistent registration pattern:

- A single `Use<Name>Telemetry(Action<Options>)` extension on `AIAgentBuilder` (or `AddRedaction` on the OTel builders).
- Options class is a plain POCO with sensible defaults; all properties are writable.
- No DI container required, but DI-friendly (options can be constructed from `IServiceProvider` if needed in a future hosted-builder extension).

### 7.6 Targeting and dependencies

- Target frameworks: `net8.0`, `net9.0`, `net10.0`.
- Dependencies kept minimal:
  - `Microsoft.Agents.AI` (latest aligned with MAF release).
  - `OpenTelemetry.Api` (and `OpenTelemetry` for processor packages).
  - `Microsoft.Extensions.Logging.Abstractions`.
- No transitive HTTP client dependencies in core packages.
- No `System.Text.Json` source generation requirement (plain `JsonSerializer` for StateBag persistence).

### 7.7 Testability

Each package exposes test seams:

- **Performance**: a fake `IAsyncEnumerable<AgentResponseUpdate>` driver lets tests assert TTFT/stalls deterministically.
- **Tools**: synthetic `execute_tool` activities created in tests verify in-place enrichment, bounded payload capture, retry detection, and fallback span behavior.
- **Sessions**: an in-memory `AgentSession` subclass exercises StateBag persistence.
- **Redaction**: each redactor tested in isolation; the pipeline tested with synthetic activities.

---

## 8. End-to-End Example

```csharp
using Microsoft.Agents.AI;
using Melic.AgentFramework.Observability.Performance;
using Melic.AgentFramework.Observability.Tools;
using Melic.AgentFramework.Observability.Sessions;
using Melic.AgentFramework.Observability.Redaction;
using OpenTelemetry;
using OpenTelemetry.Trace;

const string AppSource = "MyApp";

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(AppSource)
    .AddSource("Experimental.Microsoft.Agents.AI")
    .AddSource("Melic.AgentFramework.Observability.Sessions")
    .AddRedaction(o => o.UseStandardRedactors())
    .AddOtlpExporter()
    .Build();

var agent = chatClient
    .AsAIAgent(name: "Support", instructions: "...")
    .AsBuilder()
    .UseOpenTelemetry(sourceName: AppSource, configure: c => c.EnableSensitiveData = true)
    .UsePerformanceTelemetry(o =>
    {
        o.StreamingStallThreshold = TimeSpan.FromSeconds(1);
        o.TokenLimits = new TokenLimitPolicy
        {
            MaxTotalTokensPerSession = 200_000,
            OnExceeded = TokenLimitAction.Warn
        };
    })
    .UseToolTelemetry(o =>
    {
        o.MaxInputLength = 1024;
        o.MaxOutputLength = 2048;
        o.CaptureInput = true;
        o.CaptureOutput = true;
    })
    .UseSessionTelemetry(o => o.EnableSessionSpan = true)
    .Build();

var session = await agent.CreateSessionAsync();
session.SetSessionTag("user.tier", "premium");

await using (agent.BeginSessionTrace(session))
{
    while (true)
    {
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input)) break;

        await foreach (var update in agent.RunStreamingAsync(input, session))
            Console.Write(update.Text);
    }
}
```

---

## 9. Future Packages

| Package | Notes |
|---|---|
| `Melic.AgentFramework.Observability.Cost.Contracts` | Bring-your-own pricing resolver; metrics/tags for cost. |
| `Melic.AgentFramework.Observability.Redaction.<Country>` | Country-specific PII detectors. First one only after MVP is published and stable. |
| `Melic.AgentFramework.Observability.Redaction.Presidio` | HTTP-based integration with Microsoft Presidio. |
| `Melic.AgentFramework.Observability.Workflows` | Multi-agent / handoff telemetry. |
| `Melic.AgentFramework.Observability.Sampling` | Tail samplers (by error, by duration). |
| `Melic.AgentFramework.Observability.Testing` | `InMemoryActivityRecorder`, fluent assertions. |
| Grafana / Azure Workbook templates | Out-of-the-box dashboards. |

---

## 10. Key Decisions

| # | Decision |
|---|---|
| D1 | Cost catalog not included in MVP. |
| D2 | Token limits implemented as post-hoc circuit breakers (next-call enforcement). Default action: `Warn`. |
| D3 | Performance and Tools are separate packages. |
| D4 | Streaming stall threshold default: `1s`. |
| D5 | Tools enriches MAF `execute_tool` spans in place and creates `agent_tool_call` only as a fallback. |
| D6 | `SessionId` auto-generated and persisted in `StateBag`; overridable via `AssignSessionId`. |
| D7 | Mode B (session span) is included in MVP, opt-in. |
| D8 | Token aggregates enabled by default. |
| D9 | Redaction implemented as OTel processors (Activity + Log), not as agent decorators. |
| D10 | Standard redactor preset = Email, CreditCard, IBAN, JWT, PrivateKey, PasswordPattern. |
| D11 | Default placeholder = `Token`. |
| D12 | Allowlist is per-redactor, not global. |
| D13 | No country-specific detectors in core. First satellite only after MVP is published and stable. |
| D14 | Failure mode default = `FailSafe`. |
| D15 | All library-defined attributes use the `genai.` prefix; the official `gen_ai.` namespace is never reused except via standard OTel SemConv emissions. |

---

## Revision History

| Version | Date | Change |
|---|---|---|
| 0.1 | 2025-05 | Initial draft (internal design). Converted from working spec notes. |
