// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Melic.AgentFramework.Observability.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Melic.AgentFramework.Observability.Sessions.Internal;

/// <summary>
/// A <see cref="DelegatingAIAgent"/> decorator that enriches every agent invocation span
/// with stable session identity, aggregate token counts, custom tags, and optional session metrics.
/// All telemetry logic is best-effort: exceptions never propagate to the caller.
/// </summary>
internal sealed class SessionTelemetryAgent : DelegatingAIAgent
{
    private readonly SessionTelemetryOptions _options;
    private readonly Meter _meter;
    private readonly SessionInstruments _instruments;

    internal SessionTelemetryAgent(AIAgent innerAgent, SessionTelemetryOptions options)
        : base(innerAgent)
    {
        _options = options;
        _meter = new Meter(options.MeterName);
        _instruments = options.CreateInstruments(_meter);
    }

    /// <inheritdoc />
    protected override async Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await EnrichAndRunAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            throw;
        }
    }

    private async Task<AgentResponse> EnrichAndRunAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        CancellationToken cancellationToken)
    {
        SessionStateBlock? block = null;

        if (session is not null)
        {
            try
            {
                block = SessionStateBagAccessor.GetOrInitialise(session.StateBag, _options.StateBagKey);
                if (block.SessionId is null)
                {
                    block = block with { SessionId = Guid.NewGuid().ToString() };
                }
                var now = DateTimeOffset.UtcNow;
                var firstSeen = block.FirstSeenUtc == default ? now : block.FirstSeenUtc;
                block = block with { InvocationIndex = block.InvocationIndex + 1, FirstSeenUtc = firstSeen };
                bool persisted = SessionStateBagAccessor.TryPersist(session.StateBag, _options.StateBagKey, block);
                if (!persisted) { try { _instruments.StateBagWriteFailures.Add(1); } catch { } block = null; }
            }
            catch { block = null; }
        }

        var sw = Stopwatch.StartNew();
        try { _instruments.Active.Add(1); } catch { }

        AgentResponse response;
        bool delegationFailed = false;
        try
        {
            response = await InnerAgent.RunAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            delegationFailed = true;
            throw;
        }
        finally
        {
            sw.Stop();
            try { _instruments.Active.Add(-1); _instruments.Invocations.Add(1); _instruments.Duration.Record(sw.ElapsedMilliseconds); } catch { }

            // T028: Persist error count when delegation throws (FR-015).
            if (delegationFailed && block is not null && session is not null)
            {
                try
                {
                    var errBlock = block with { ErrorCount = block.ErrorCount + 1 };
                    SessionStateBagAccessor.TryPersist(session.StateBag, _options.StateBagKey, errBlock);
                }
                catch { /* best-effort */ }
            }
        }

        if (block is not null && session is not null)
        {
            try
            {
                if (_options.TrackTokenAggregates && response.Usage is { } usage)
                {
                    long inputDelta = usage.InputTokenCount ?? 0L;
                    long outputDelta = usage.OutputTokenCount ?? 0L;
                    block = block with { TotalInputTokens = block.TotalInputTokens + inputDelta, TotalOutputTokens = block.TotalOutputTokens + outputDelta };
                    SessionStateBagAccessor.TryPersist(session.StateBag, _options.StateBagKey, block);
                }

                var activity = Activity.Current;
                if (activity is not null)
                {
                    activity.SetTag(SessionAttributeNames.SessionId, block.SessionId);
                    activity.SetTag(SessionAttributeNames.InvocationIndex, block.InvocationIndex);
                    if (block.FirstSeenUtc != default)
                    {
                        activity.SetTag(SessionAttributeNames.FirstSeen, block.FirstSeenUtc.ToString("O"));
                        activity.SetTag(SessionAttributeNames.AgeSeconds, (DateTimeOffset.UtcNow - block.FirstSeenUtc).TotalSeconds);
                    }
                    if (_options.TrackTokenAggregates)
                    {
                        activity.SetTag(SessionAttributeNames.TotalInputTokens, block.TotalInputTokens);
                        activity.SetTag(SessionAttributeNames.TotalOutputTokens, block.TotalOutputTokens);
                    }
                    if (block.Tags is { } tags)
                    {
                        foreach (var (key, value) in tags) { activity.SetTag(key, value); }
                    }
                }
            }
            catch { }
        }

        return response;
    }
}
