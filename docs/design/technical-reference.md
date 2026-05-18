# Melic.AgentFramework.Observability — Technical Reference

> An open-source .NET library suite that extends the Microsoft Agent Framework (MAF)
> with production-grade telemetry utilities on top of OpenTelemetry.

| | |
|---|---|
| **Status** | Active |
| **Author** | Luis Mañez |
| **Last updated** | 2025-05 |
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

For tools, MAF itself adds nothing — the `execute_tool {tool_name}` spans (with `gen_ai.tool.name`, `gen_ai.tool.call.id`) are emitted by `Microsoft.Extensions.AI`'s `OpenTelemetryChatClient` when wrapping a `FunctionInvokingChatClient`.

A single `EnableSensitiveData` boolean controls whether prompts, completions, function arguments, and function results are written to telemetry.

### 1.2 Gaps this library fills

1. **No TTFT (time-to-first-token)** measurement for streaming responses.
2. **No detection of streaming stalls** (gaps between tokens).
3. **HTTP retries (429/503/timeouts) are not correlated** with the originating agent invocation.
4. **No token usage limits / circuit breaker** at agent or session level.
5. **Tool telemetry is per-call only** — no aggregation, no loop detection, no slowest-tool indicator on the parent span.
6. **No persistent session identity** — `AgentSession` has no stable ID exposed by MAF; correlating multi-turn conversations across invocations requires manual scaffolding.
7. **Sensitive-data control is a single boolean** — no redaction pipeline, no PII masking.

### 1.3 Non-goals

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
│  - TTFT / stalls / retries       │    │  - Tool aggregates             │
│  - Token-limit circuit breaker   │    │  - Loop detection              │
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

- Aggregate per-tool telemetry onto the parent `invoke_agent` span.
- Emit per-tool metrics for dashboards.
- Detect tool-call loops (same tool, same arguments, repeated within one invocation).

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
    /// <summary>Same tool + same args repeated this many times triggers a loop event. Default: 3.</summary>
    public int LoopDetectionThreshold { get; set; } = 3;

    /// <summary>If true (default), arguments participate in the loop hash.</summary>
    public bool IncludeArgumentsInHash { get; set; } = true;

    /// <summary>Optional callback invoked when a loop is detected.</summary>
    public Action<ToolLoopContext>? OnLoopDetected { get; set; }

    public string MeterName { get; set; } = "Melic.AgentFramework.Observability.Tools";
}

public sealed record ToolLoopContext(
    string ToolName,
    int RepeatCount,
    AIAgent Agent,
    AgentSession? Session);
```

### 4.3 Tags emitted on `invoke_agent` span

| Tag | Type | Notes |
|---|---|---|
| `genai.tools.calls` | int | total count |
| `genai.tools.failed` | int | count with status Error |
| `genai.tools.distinct` | int | distinct tool names invoked |
| `genai.tools.repeated_max` | int | max count of identical (tool+argsHash) within this invocation |
| `genai.tools.slowest_name` | string | name of the slowest tool |
| `genai.tools.slowest_ms` | long | duration of the slowest tool |
| `genai.tools.total_ms` | long | sum of all tool durations |

### 4.4 Metrics

Meter: `Melic.AgentFramework.Observability.Tools`.

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `genai.tool.duration` | Histogram\<long\> | ms | `gen_ai.tool.name`, `gen_ai.agent.name`, `status` (`ok` \| `error`) |
| `genai.tool.invocations` | Counter\<long\> | events | `gen_ai.tool.name`, `gen_ai.agent.name`, `status` |
| `genai.tool.errors` | Counter\<long\> | events | `gen_ai.tool.name`, `gen_ai.agent.name`, `error.type` |
| `genai.tool.loop_detected` | Counter\<long\> | events | `gen_ai.tool.name`, `gen_ai.agent.name` |

### 4.5 Implementation notes

- `ToolTelemetryAgent : DelegatingAIAgent` wraps the run. Inside it:
  - Captures `Activity.Current` (the `invoke_agent` span) after delegating to the inner agent; aggregation happens just before the span ends.
  - Subscribes a scoped `ActivityListener` filtered to operation name `execute_tool` for the duration of this run, scoped to children of the captured trace/span.
- `argsHash`:
  - Stable JSON serialization of arguments (sorted property names) → SHA-256 → hex prefix (16 chars).
  - If `IncludeArgumentsInHash == false`, hash is just the tool name.
  - Arguments are read from the `execute_tool` span's `gen_ai.tool.call.arguments` tag (only present if MAF's `EnableSensitiveData` is true). If absent, hash = tool name only.
- Loop detection runs at end-of-invocation:
  - Build `Dictionary<(toolName, argsHash), int>`.
  - `repeated_max` = max value.
  - If `repeated_max >= LoopDetectionThreshold` → emit `genai.tool.loop_detected` metric (tag = the offending tool), invoke `OnLoopDetected`.

### 4.6 Known limitations

- Loop detection without arguments (when `EnableSensitiveData=false` or `IncludeArgumentsInHash=false`) degrades to "same tool name N times", which has more false positives. This is documented in the XML doc of `IncludeArgumentsInHash`.
- Concurrent tool calls within the same invocation are correlated via the active trace; if the agent emits `execute_tool` spans on a detached `Activity` context, they will be missed. (Edge case; acceptable for v1.)

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

- Library-defined attributes use the `genai.` prefix (no underscore in second segment), e.g., `genai.session.id`, `genai.tools.calls`. This separates them from the official `gen_ai.*` semantic conventions while keeping them grouped lexicographically in dashboards.
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
- **Tools**: synthetic `execute_tool` activities created in tests verify aggregation and loop detection.
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
        o.LoopDetectionThreshold = 3;
        o.OnLoopDetected = ctx => Console.WriteLine($"Loop on {ctx.ToolName}");
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
| D5 | Loop detection includes arguments by default. Threshold: `3`. |
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
