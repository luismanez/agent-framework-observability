// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Sessions.Tests.Helpers;
using Xunit;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Melic.AgentFramework.Observability.Sessions.Tests;

/// <summary>
/// Tests for US1: Session Identity Enrichment.
/// Covers FR-001 (unique SessionId), FR-002 (stable across invocations),
/// FR-003 (propagated as span tag), FR-004 (invocation index increments).
/// </summary>
public sealed class SessionIdentityTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AIAgent BuildAgent(Action<SessionTelemetryOptions>? configure = null)
    {
        var stub = new StubAIAgent();
        return new AIAgentBuilder(stub)
            .UseSessionTelemetry(configure)
            .Build();
    }

    // ── FR-001 + FR-002: unique, stable SessionId ────────────────────────────

    [Fact]
    public async Task SessionId_Is_Assigned_On_First_Invocation()
    {
        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session);

        var sessionId = session.GetSessionId();
        Assert.NotNull(sessionId);
        Assert.NotEmpty(sessionId);
    }

    [Fact]
    public async Task SessionId_Is_Stable_Across_Multiple_Invocations()
    {
        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        await agent.RunAsync([new ChatMessage(ChatRole.User, "first")], session);
        var idAfterFirst = session.GetSessionId();

        await agent.RunAsync([new ChatMessage(ChatRole.User, "second")], session);
        var idAfterSecond = session.GetSessionId();

        Assert.NotNull(idAfterFirst);
        Assert.Equal(idAfterFirst, idAfterSecond);
    }

    [Fact]
    public async Task Different_Sessions_Get_Different_SessionIds()
    {
        var agent = BuildAgent();

        var sessionA = await agent.CreateSessionAsync();
        var sessionB = await agent.CreateSessionAsync();

        await agent.RunAsync([new ChatMessage(ChatRole.User, "a")], sessionA);
        await agent.RunAsync([new ChatMessage(ChatRole.User, "b")], sessionB);

        var idA = sessionA.GetSessionId();
        var idB = sessionB.GetSessionId();

        Assert.NotNull(idA);
        Assert.NotNull(idB);
        Assert.NotEqual(idA, idB);
    }

    // ── FR-003: SessionId propagated to Activity span tag ───────────────────

    [Fact]
    public async Task SessionId_Is_Set_As_Span_Tag_On_Current_Activity()
    {
        using var recorder = new ActivityRecorder("test-source");
        using var source = new ActivitySource("test-source");

        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        using (var activity = source.StartActivity("test-span"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session);
        }

        var span = recorder.Completed.FirstOrDefault();
        Assert.NotNull(span);

        var tag = span.GetTagItem(Abstractions.SessionAttributeNames.SessionId);
        Assert.NotNull(tag);
        Assert.Equal(session.GetSessionId(), tag as string);
    }

    // ── FR-004: Invocation index increments on each call ────────────────────

    [Fact]
    public async Task InvocationIndex_Increments_On_Each_Run()
    {
        using var recorder = new ActivityRecorder("test-source");
        using var source = new ActivitySource("test-source");

        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        int? index1 = null, index2 = null;

        using (source.StartActivity("span1"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "first")], session);
        }

        using (source.StartActivity("span2"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "second")], session);
        }

        var spans = recorder.Completed.ToList();
        Assert.Equal(2, spans.Count);

        index1 = spans[0].GetTagItem(Abstractions.SessionAttributeNames.InvocationIndex) as int?;
        index2 = spans[1].GetTagItem(Abstractions.SessionAttributeNames.InvocationIndex) as int?;

        Assert.Equal(1, index1);
        Assert.Equal(2, index2);
    }

    // ── FR-005: AssignSessionId sets caller-provided correlation ID ──────────

    [Fact]
    public async Task AssignSessionId_Propagates_Caller_Provided_Id()
    {
        const string callerSessionId = "caller-correlation-xyz";

        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        // Assign before the first invocation.
        session.AssignSessionId(callerSessionId);

        await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session);

        var sessionId = session.GetSessionId();
        Assert.Equal(callerSessionId, sessionId);
    }

    [Fact]
    public async Task AssignSessionId_Persists_Across_Multiple_Invocations()
    {
        const string callerSessionId = "caller-stable";

        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        session.AssignSessionId(callerSessionId);

        await agent.RunAsync([new ChatMessage(ChatRole.User, "first")], session);
        await agent.RunAsync([new ChatMessage(ChatRole.User, "second")], session);

        Assert.Equal(callerSessionId, session.GetSessionId());
    }

    // ── Without session: no-op (FR-017 best-effort) ─────────────────────────

    [Fact]
    public async Task No_Session_Does_Not_Throw()
    {
        var agent = BuildAgent();

        // Pass null session — must not throw.
        var response = await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session: null);
        Assert.NotNull(response);
    }
}
