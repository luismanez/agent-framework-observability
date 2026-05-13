// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Sessions.Tests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace Melic.AgentFramework.Observability.Sessions.Tests;

/// <summary>
/// Tests for US4: Session Span (Mode B).
/// Covers FR-014 (session span created), FR-015 (final aggregate tags),
/// FR-016 (disabled by default / no-op when disabled).
/// </summary>
public sealed class SessionSpanTests
{
    // ── FR-016: Mode B disabled by default — BeginSessionTrace is a no-op ────

    [Fact]
    public async Task BeginSessionTrace_Is_NullDisposable_When_EnableSessionSpan_False()
    {
        var agent = new AIAgentBuilder(new StubAIAgent())
            .UseSessionTelemetry(o => o.EnableSessionSpan = false)
            .Build();

        var session = await agent.CreateSessionAsync();

        // Must not throw and must return a valid (no-op) disposable.
        using var handle = agent.BeginSessionTrace(session, new SessionTelemetryOptions { EnableSessionSpan = false });
        Assert.NotNull(handle);
    }

    [Fact]
    public async Task BeginSessionTrace_Default_Options_Does_Not_Create_Span()
    {
        using var recorder = new ActivityRecorder("span-default-source");
        var agent = new AIAgentBuilder(new StubAIAgent())
            .UseSessionTelemetry() // EnableSessionSpan = false by default
            .Build();

        var session = await agent.CreateSessionAsync();
        using (agent.BeginSessionTrace(session, new SessionTelemetryOptions()))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session);
        }

        // No session span should be in the recorder since EnableSessionSpan=false.
        var sessionSpans = recorder.Completed
            .Where(a => a.DisplayName.StartsWith("agent_session"))
            .ToList();
        Assert.Empty(sessionSpans);
    }

    // ── FR-014: Session span created with correct name ────────────────────────

    [Fact]
    public async Task BeginSessionTrace_Creates_Root_Span_With_Correct_Name()
    {
        const string sourceName = "span-mode-b-source";
        using var recorder = new ActivityRecorder(sourceName);

        var opts = new SessionTelemetryOptions
        {
            EnableSessionSpan = true,
            ActivitySourceName = sourceName,
        };

        var agent = new AIAgentBuilder(new StubAIAgent("stub", "my-agent"))
            .UseSessionTelemetry(o => { o.EnableSessionSpan = true; o.ActivitySourceName = sourceName; })
            .Build();

        var session = await agent.CreateSessionAsync();

        using (agent.BeginSessionTrace(session, opts))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session);
        }

        var sessionSpan = recorder.Completed.FirstOrDefault(a => a.DisplayName.StartsWith("agent_session"));
        Assert.NotNull(sessionSpan);
        Assert.Contains("my-agent", sessionSpan!.DisplayName);
    }

    // ── FR-015: Final aggregate tags on session span ──────────────────────────

    [Fact]
    public async Task Session_Span_Carries_Aggregate_Tags_After_Dispose()
    {
        const string sourceName = "span-aggregate-source";
        using var recorder = new ActivityRecorder(sourceName);

        var responses = new Queue<AgentResponse>(
        [
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "r1")) { Usage = new() { InputTokenCount = 10, OutputTokenCount = 5 } },
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "r2")) { Usage = new() { InputTokenCount = 20, OutputTokenCount = 8 } },
        ]);

        var opts = new SessionTelemetryOptions
        {
            EnableSessionSpan = true,
            ActivitySourceName = sourceName,
        };

        var agent = new AIAgentBuilder(new StubAIAgent(() => responses.Dequeue()))
            .UseSessionTelemetry(o => { o.EnableSessionSpan = true; o.ActivitySourceName = sourceName; })
            .Build();

        var session = await agent.CreateSessionAsync();

        using (agent.BeginSessionTrace(session, opts))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "first")], session);
            await agent.RunAsync([new ChatMessage(ChatRole.User, "second")], session);
        }

        var sessionSpan = recorder.Completed.FirstOrDefault(a => a.DisplayName.StartsWith("agent_session"));
        Assert.NotNull(sessionSpan);

        Assert.Equal(2L, sessionSpan!.GetTagItem(Abstractions.SessionAttributeNames.TotalInvocations) as int? ?? (long?)sessionSpan.GetTagItem(Abstractions.SessionAttributeNames.TotalInvocations));
        Assert.Equal(30L, sessionSpan.GetTagItem(Abstractions.SessionAttributeNames.TotalInputTokens));
        Assert.Equal(13L, sessionSpan.GetTagItem(Abstractions.SessionAttributeNames.TotalOutputTokens));
    }

    // ── FR-015: session.ended event ──────────────────────────────────────────

    [Fact]
    public async Task Session_Span_Emits_SessionEnded_Event_On_Dispose()
    {
        const string sourceName = "span-event-source";
        using var recorder = new ActivityRecorder(sourceName);

        var opts = new SessionTelemetryOptions
        {
            EnableSessionSpan = true,
            ActivitySourceName = sourceName,
        };

        var agent = new AIAgentBuilder(new StubAIAgent())
            .UseSessionTelemetry(o => { o.EnableSessionSpan = true; o.ActivitySourceName = sourceName; })
            .Build();

        var session = await agent.CreateSessionAsync();

        using (agent.BeginSessionTrace(session, opts))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session);
        }

        var sessionSpan = recorder.Completed.FirstOrDefault(a => a.DisplayName.StartsWith("agent_session"));
        Assert.NotNull(sessionSpan);

        var endedEvent = sessionSpan!.Events.FirstOrDefault(e => e.Name == "session.ended");
        Assert.NotNull(endedEvent.Name); // ActivityEvent is a struct; check by name
    }
}
