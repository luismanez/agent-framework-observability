// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using Melic.AgentFramework.Observability.Sessions.Internal;
using Microsoft.Agents.AI;

namespace Melic.AgentFramework.Observability.Sessions;

/// <summary>
/// Extension methods for working with session identity in agent sessions.
/// </summary>
public static class SessionTelemetryExtensions
{
    /// <summary>
    /// Returns the stable session ID that the <see cref="SessionTelemetryAgent"/> has assigned to this session,
    /// or <see langword="null"/> if the session has not yet been enriched by at least one invocation.
    /// </summary>
    /// <param name="session">The <see cref="AgentSession"/> to query.</param>
    /// <param name="stateBagKey">
    /// The <see cref="SessionTelemetryOptions.StateBagKey"/> used when the agent was built.
    /// Defaults to <c>"__melic_telemetry"</c>.
    /// </param>
    /// <returns>The session ID string, or <see langword="null"/> if not yet set.</returns>
    public static string? GetSessionId(
        this AgentSession session,
        string stateBagKey = "__melic_telemetry")
    {
        ArgumentNullException.ThrowIfNull(session);

        if (SessionStateBagAccessor.TryRead(session.StateBag, stateBagKey, out var block))
        {
            return block?.SessionId;
        }

        return null;
    }

    /// <summary>
    /// Assigns an explicit session ID to this session, replacing any previously assigned value.
    /// Use this overload when you want to propagate a caller-provided correlation ID.
    /// </summary>
    /// <param name="session">The <see cref="AgentSession"/> to update.</param>
    /// <param name="sessionId">The session ID value to store.</param>
    /// <param name="stateBagKey">The <see cref="SessionTelemetryOptions.StateBagKey"/> used when the agent was built.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sessionId"/> is null or whitespace.</exception>
    public static void AssignSessionId(
        this AgentSession session,
        string sessionId,
        string stateBagKey = "__melic_telemetry")
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var block = SessionStateBagAccessor.GetOrInitialise(session.StateBag, stateBagKey);
        var updated = block with { SessionId = sessionId };
        SessionStateBagAccessor.TryPersist(session.StateBag, stateBagKey, updated);
    }

    /// <summary>
    /// Attaches a custom string tag to the session that will be emitted on every subsequent invocation span.
    /// Tags survive serialization/deserialization round-trips and appear as span attributes.
    /// Tag values are written as-is without any redaction — the caller is responsible for omitting sensitive data.
    /// </summary>
    /// <param name="session">The <see cref="AgentSession"/> to tag.</param>
    /// <param name="key">
    /// Tag key. Silently rejected when: null/whitespace, longer than 128 chars,
    /// or starts with the reserved prefix <c>"genai.session."</c>.
    /// </param>
    /// <param name="value">Tag value. Silently rejected when longer than 512 chars.</param>
    /// <param name="stateBagKey">The <see cref="SessionTelemetryOptions.StateBagKey"/> used when the agent was built.</param>
    public static void SetSessionTag(
        this AgentSession session,
        string key,
        string value,
        string stateBagKey = "__melic_telemetry")
    {
        ArgumentNullException.ThrowIfNull(session);

        // FR-020: Validate key — silently reject on violation.
        if (string.IsNullOrWhiteSpace(key)) return;
        if (key.Length > 128) return;
        if (key.StartsWith("genai.session.", StringComparison.Ordinal)) return;

        // FR-020: Validate value.
        if (value is null) return;
        if (value.Length > 512) return;

        var block = SessionStateBagAccessor.GetOrInitialise(session.StateBag, stateBagKey);

        // FR-020: Enforce 50-tag limit.
        var existingTags = block.Tags;
        bool isNewKey = existingTags is null || !existingTags.ContainsKey(key);
        if (isNewKey && (existingTags?.Count ?? 0) >= 50) return;

        // Copy-on-write upsert.
        var newTags = existingTags is null
            ? new Dictionary<string, string>(StringComparer.Ordinal) { [key] = value }
            : new Dictionary<string, string>(existingTags, StringComparer.Ordinal) { [key] = value };

        var updated = block with { Tags = newTags };
        SessionStateBagAccessor.TryPersist(session.StateBag, stateBagKey, updated);
    }

    /// <summary>
    /// Opens an explicit session span that becomes the trace parent for all invocations in scope (Mode B).
    /// </summary>
    /// <remarks>
    /// When <see cref="SessionTelemetryOptions.EnableSessionSpan"/> is <see langword="false"/>,
    /// this method returns a no-op disposable. Mode B must be explicitly enabled via
    /// <see cref="SessionTelemetryAgentBuilderExtensions.UseSessionTelemetry"/>.
    /// On <see cref="IDisposable.Dispose"/>, the session span emits final aggregate tags
    /// and a <c>session.ended</c> span event.
    /// </remarks>
    /// <param name="agent">The <see cref="AIAgent"/> wrapped with session telemetry.</param>
    /// <param name="session">The <see cref="AgentSession"/> to trace.</param>
    /// <param name="options">The <see cref="SessionTelemetryOptions"/> configured on the agent.</param>
    /// <returns>
    /// An <see cref="IDisposable"/> that closes the session span on disposal.
    /// </returns>
    public static IDisposable BeginSessionTrace(
        this AIAgent agent,
        AgentSession session,
        SessionTelemetryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(session);

        var opts = options ?? new SessionTelemetryOptions();
        if (!opts.EnableSessionSpan)
        {
            return NullDisposable.Instance;
        }

        var source = new ActivitySource(opts.ActivitySourceName);
        var activity = source.StartActivity($"agent_session {agent.Name ?? "unknown"}");
        if (activity is null)
        {
            source.Dispose();
            return NullDisposable.Instance;
        }

        // Read initial session state.
        SessionStateBagAccessor.TryRead(session.StateBag, opts.StateBagKey, out var initBlock);
        activity.SetTag(SessionAttributeNames.SessionId, initBlock?.SessionId);
        activity.SetTag("gen_ai.agent.name", agent.Name);
        activity.SetTag(SessionAttributeNames.StartTime, DateTimeOffset.UtcNow.ToString("O"));

        return new SessionSpanHandle(activity, source, session, opts);
    }

    private sealed class SessionSpanHandle : IDisposable
    {
        private readonly Activity _activity;
        private readonly ActivitySource _source;
        private readonly AgentSession _session;
        private readonly SessionTelemetryOptions _opts;

        internal SessionSpanHandle(Activity activity, ActivitySource source, AgentSession session, SessionTelemetryOptions opts)
        {
            _activity = activity;
            _source = source;
            _session = session;
            _opts = opts;
        }

        public void Dispose()
        {
            try
            {
                SessionStateBagAccessor.TryRead(_session.StateBag, _opts.StateBagKey, out var block);
                if (block is not null)
                {
                    _activity.SetTag(SessionAttributeNames.TotalInvocations, block.InvocationIndex);
                    _activity.SetTag(SessionAttributeNames.TotalInputTokens, block.TotalInputTokens);
                    _activity.SetTag(SessionAttributeNames.TotalOutputTokens, block.TotalOutputTokens);
                    _activity.SetTag(SessionAttributeNames.Errors, block.ErrorCount);

                    if (block.FirstSeenUtc != default)
                    {
                        _activity.SetTag(SessionAttributeNames.DurationSeconds,
                            (DateTimeOffset.UtcNow - block.FirstSeenUtc).TotalSeconds);
                    }
                }

                _activity.AddEvent(new ActivityEvent("session.ended"));
            }
            catch { /* best-effort */ }
            finally
            {
                _activity.Stop();
                _source.Dispose();
            }
        }
    }

    private sealed class NullDisposable : IDisposable
    {
        internal static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
