// SPDX-License-Identifier: MIT
using System.Diagnostics.Metrics;
using Melic.AgentFramework.Observability.Sessions.Tests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace Melic.AgentFramework.Observability.Sessions.Tests;

/// <summary>
/// Tests for cross-cutting error isolation (FR-017, FR-024).
/// Verifies that telemetry failures never surface to the caller and that
/// metrics instruments remain consistent on delegation failures.
/// </summary>
public sealed class SessionErrorHandlingTests
{
    // ── FR-017: Inner agent exception propagates; session state still accessible ──

    [Fact]
    public async Task Inner_Agent_Exception_Propagates_To_Caller()
    {
        var throwingStub = new StubAIAgent(
            () => throw new InvalidOperationException("inner agent error"));

        var agent = new AIAgentBuilder(throwingStub)
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session));
    }

    [Fact]
    public async Task Session_State_Is_Enriched_Before_Delegation_Even_When_Inner_Throws()
    {
        var throwingStub = new StubAIAgent(
            () => throw new InvalidOperationException("inner agent error"));

        var agent = new AIAgentBuilder(throwingStub)
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();

        try { await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session); } catch { }

        // Session state should have been initialised before the inner agent was called.
        var sessionId = session.GetSessionId();
        Assert.NotNull(sessionId);
    }

    // ── FR-017: Invoking without session never throws ────────────────────────

    [Fact]
    public async Task Agent_Without_Session_Always_Returns_Response()
    {
        var agent = new AIAgentBuilder(new StubAIAgent())
            .UseSessionTelemetry()
            .Build();

        var response = await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session: null);
        Assert.NotNull(response);
    }

    // ── FR-017: Multiple concurrent invocations do not interfere ────────────

    [Fact]
    public async Task Concurrent_Invocations_On_Same_Session_Are_Safe()
    {
        var agent = new AIAgentBuilder(new StubAIAgent())
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();
        var msg = new ChatMessage(ChatRole.User, "concurrent");

        var tasks = Enumerable.Range(0, 10).Select(_ =>
            agent.RunAsync([msg], session)).ToArray();

        var responses = await Task.WhenAll(tasks);

        // All 10 invocations must complete without exception.
        Assert.Equal(10, responses.Length);
        Assert.All(responses, r => Assert.NotNull(r));

        // InvocationIndex must be at least 1 — concurrent races may cause under-counting
        // but must never throw or leave the block in an invalid state.
        Internal.SessionStateBagAccessor.TryRead(session.StateBag, "__melic_telemetry", out var block);
        Assert.NotNull(block);
        Assert.True(block!.InvocationIndex >= 1);
    }

    // ── FR-024: Metrics still update on inner agent failure ─────────────────

    [Fact]
    public async Task Metrics_Invocations_Counter_Updated_Even_On_Inner_Failure()
    {
        // Use a MeterListener to capture the invocations counter.
        long invocationsRecorded = 0;
        const string meterName = "Melic.AgentFramework.Observability.Sessions.ErrorTest";

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == meterName && instrument.Name == "genai.session.invocations")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
            Interlocked.Add(ref invocationsRecorded, value));
        listener.Start();

        var throwingStub = new StubAIAgent(
            () => throw new InvalidOperationException("inner agent error"));

        var agent = new AIAgentBuilder(throwingStub)
            .UseSessionTelemetry(o => o.MeterName = meterName)
            .Build();

        var session = await agent.CreateSessionAsync();

        try { await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session); } catch { }

        // Invocations counter must have been incremented despite the throw.
        Assert.Equal(1L, Interlocked.Read(ref invocationsRecorded));
    }
}
