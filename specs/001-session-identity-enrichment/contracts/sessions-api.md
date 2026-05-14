# Public API Contract: Sessions Package

**Package**: `Melic.AgentFramework.Observability.Sessions`
**Version**: 1.0.0 (first release)
**Feature Branch**: `001-session-identity-enrichment`
**Created**: 2026-05-13

---

## Overview

This contract defines the complete public API surface for the Sessions package.
All types listed here are `public`. Internal types (`SessionTelemetryAgent`,
`SessionStateBlock`, `SessionStateBagAccessor`) are excluded — they are implementation
details and subject to change without notice.

---

## Namespace: `Melic.AgentFramework.Observability.Sessions`

### `SessionTelemetryAgentBuilderExtensions` (static class)

```csharp
/// <summary>
/// Extension methods for configuring session telemetry on an <see cref="AIAgentBuilder"/> pipeline.
/// </summary>
public static class SessionTelemetryAgentBuilderExtensions
{
    /// <summary>
    /// Adds session telemetry to the agent pipeline. Session identity, aggregate tags,
    /// and optional custom tags are enriched on every <c>invoke_agent</c> span.
    /// </summary>
    /// <param name="builder">The <see cref="AIAgentBuilder"/> to configure.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="SessionTelemetryOptions"/>. When
    /// <see langword="null"/>, defaults are applied.
    /// </param>
    /// <returns>The same <see cref="AIAgentBuilder"/> instance for chaining.</returns>
    public static AIAgentBuilder UseSessionTelemetry(
        this AIAgentBuilder builder,
        Action<SessionTelemetryOptions>? configure = null);
}
```

### `SessionTelemetryOptions` (sealed class)

```csharp
/// <summary>
/// Configuration options for the session telemetry decorator.
/// Resolved once at agent build time and applied to all subsequent invocations.
/// </summary>
public sealed class SessionTelemetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether input and output token totals are
    /// accumulated and emitted as session span tags. Default: <see langword="true"/>.
    /// </summary>
    public bool TrackTokenAggregates { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether Mode B (explicit session span) is
    /// available via <see cref="SessionTelemetryExtensions.BeginSessionTrace"/>. Default: <see langword="false"/>.
    /// </summary>
    public bool EnableSessionSpan { get; set; } = false;

    /// <summary>
    /// Gets or sets the key used to store the session telemetry state block in the
    /// session's <c>StateBag</c>. Must not be written to by application code.
    /// Default: <c>"__melic_telemetry"</c>.
    /// </summary>
    public string StateBagKey { get; set; } = "__melic_telemetry";

    /// <summary>
    /// Gets or sets the <see cref="ActivitySource"/> name for session spans (Mode B).
    /// Default: <c>"Melic.AgentFramework.Observability.Sessions"</c>.
    /// </summary>
    public string ActivitySourceName { get; set; } = "Melic.AgentFramework.Observability.Sessions";

    /// <summary>
    /// Gets or sets the meter name for session metrics.
    /// Default: <c>"Melic.AgentFramework.Observability.Sessions"</c>.
    /// </summary>
    public string MeterName { get; set; } = "Melic.AgentFramework.Observability.Sessions";
}
```

### `SessionTelemetryExtensions` (static class)

```csharp
/// <summary>
/// Extension methods for managing session identity and custom tags on an <see cref="AgentSession"/>.
/// </summary>
public static class SessionTelemetryExtensions
{
    /// <summary>
    /// Returns the stable session ID that the telemetry decorator has assigned to this
    /// session, or <see langword="null"/> if the session has not yet been enriched by
    /// at least one invocation. Auto-generation occurs inside the agent pipeline, not
    /// via this method.
    /// </summary>
    /// <param name="session">The agent session.</param>
    /// <param name="stateBagKey">
    /// The <see cref="SessionTelemetryOptions.StateBagKey"/> used when the agent was
    /// built. Defaults to <c>"__melic_telemetry"</c>.
    /// </param>
    /// <returns>The session ID string, or <see langword="null"/> if not yet set.</returns>
    public static string? GetSessionId(
        this AgentSession session,
        string stateBagKey = "__melic_telemetry");

    /// <summary>
    /// Assigns an explicit session identifier to this session. Must be called before
    /// the first invocation; subsequent calls overwrite any previously assigned value.
    /// Use this overload to propagate a caller-provided correlation ID.
    /// </summary>
    /// <param name="session">The agent session.</param>
    /// <param name="sessionId">The session ID value to store.</param>
    /// <param name="stateBagKey">
    /// The <see cref="SessionTelemetryOptions.StateBagKey"/> used when the agent was
    /// built. Defaults to <c>"__melic_telemetry"</c>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="sessionId"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public static void AssignSessionId(
        this AgentSession session,
        string sessionId,
        string stateBagKey = "__melic_telemetry");

    /// <summary>
    /// Attaches a custom string tag to the session. The tag is propagated to every
    /// subsequent <c>invoke_agent</c> span and survives session serialization.
    /// </summary>
    /// <param name="session">The agent session.</param>
    /// <param name="key">
    /// The tag key. Must not be <see langword="null"/>, empty, whitespace, exceed
    /// 128 characters, or start with <c>"genai.session."</c>. Tags violating these
    /// rules are silently rejected.
    /// </param>
    /// <param name="value">
    /// The tag value. Must not exceed 512 characters. Tags violating this rule are
    /// silently rejected.
    /// </param>
    /// <remarks>
    /// Custom tags are written to span attributes without any filtering. Developers
    /// are responsible for ensuring tag values do not contain sensitive data.
    /// </remarks>
    public static void SetSessionTag(this AgentSession session, string key, string value);

    /// <summary>
    /// Opens an explicit session span that becomes the trace parent for all
    /// <c>invoke_agent</c> spans executed within the returned scope.
    /// </summary>
    /// <param name="agent">The agent configured with session telemetry.</param>
    /// <param name="session">The agent session whose identifier labels the span.</param>
    /// <param name="options">
    /// Optional <see cref="SessionTelemetryOptions"/> override. When <see langword="null"/>,
    /// a default instance is used (<see cref="SessionTelemetryOptions.EnableSessionSpan"/>
    /// defaults to <see langword="false"/>, so the no-op disposable is returned unless
    /// options with <c>EnableSessionSpan = true</c> are provided).
    /// </param>
    /// <returns>
    /// An <see cref="IDisposable"/> that closes the session span and records final
    /// aggregates when disposed. Returns a no-op disposable if
    /// <see cref="SessionTelemetryOptions.EnableSessionSpan"/> is <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Mode B is only meaningful within a single process and async call chain.
    /// For cross-process or long-lived session correlation, use Mode A enrichment
    /// (filter by <c>genai.session.id</c> tag).
    /// </remarks>
    public static IDisposable BeginSessionTrace(
        this AIAgent agent,
        AgentSession session,
        SessionTelemetryOptions? options = null);
}
```

---

## Namespace: `Melic.AgentFramework.Observability.Abstractions`

### `SessionAttributeNames` (static class) — new constants added by this feature

```csharp
/// <summary>
/// Constants for telemetry attribute names emitted by the Sessions package.
/// </summary>
public static class SessionAttributeNames
{
    public static readonly string SessionId              = "genai.session.id";
    public static readonly string InvocationIndex        = "genai.session.invocation_index";
    public static readonly string FirstSeen              = "genai.session.first_seen";
    public static readonly string AgeSeconds             = "genai.session.age_seconds";
    public static readonly string TotalInputTokens       = "genai.session.total_input_tokens";
    public static readonly string TotalOutputTokens      = "genai.session.total_output_tokens";
    public static readonly string MessageCount           = "genai.session.message_count";
    public static readonly string TotalInvocations       = "genai.session.total_invocations";
    public static readonly string DurationSeconds        = "genai.session.duration_seconds";
    public static readonly string Errors                 = "genai.session.errors";
    public static readonly string StartTime              = "genai.session.start_time";
}
```

---

## Breaking Change Policy

- No public type, method, or property may be removed or have its signature changed in a
  patch or minor release.
- `SessionStateBlock` (internal) schema changes must be forward-compatible (R-002 in
  research.md): new fields default-initialised on restore; unknown fields preserved via
  `[JsonExtensionData]`.
- The `StateBagKey` default (`"__melic_telemetry"`) is stable and must not change once
  published.
