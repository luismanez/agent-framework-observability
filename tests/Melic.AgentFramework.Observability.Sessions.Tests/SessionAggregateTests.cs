// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Sessions.Tests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace Melic.AgentFramework.Observability.Sessions.Tests;

/// <summary>
/// Tests for US2: Session Aggregate Enrichment.
/// Covers FR-004 (InvocationIndex), FR-005 (FirstSeen/Age), FR-006 (TokenAggregates),
/// FR-007 (totals survive restore), FR-022 (token source priority).
/// </summary>
public sealed class SessionAggregateTests
{
    // ── FR-004: InvocationIndex on span tags ─────────────────────────────────

    [Fact]
    public async Task InvocationIndex_Tag_Matches_Invocation_Count()
    {
        using var recorder = new ActivityRecorder("agg-test-source");
        using var source = new ActivitySource("agg-test-source");

        var agent = new AIAgentBuilder(new StubAIAgent())
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();

        for (int i = 1; i <= 3; i++)
        {
            using (source.StartActivity($"span-{i}"))
            {
                await agent.RunAsync([new ChatMessage(ChatRole.User, $"msg {i}")], session);
            }
        }

        var spans = recorder.Completed.ToList();
        Assert.Equal(3, spans.Count);

        for (int i = 0; i < 3; i++)
        {
            var tag = spans[i].GetTagItem(Abstractions.SessionAttributeNames.InvocationIndex);
            Assert.Equal(i + 1, tag);
        }
    }

    // ── FR-005: FirstSeen set once; AgeSeconds grows ─────────────────────────

    [Fact]
    public async Task FirstSeen_Is_Set_On_First_Invocation_And_Stable()
    {
        using var recorder = new ActivityRecorder("agg-test-source2");
        using var source = new ActivitySource("agg-test-source2");

        var agent = new AIAgentBuilder(new StubAIAgent())
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();

        using (source.StartActivity("span1"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "first")], session);
        }

        await Task.Delay(10); // Small delay so age > 0

        using (source.StartActivity("span2"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "second")], session);
        }

        var spans = recorder.Completed.ToList();
        Assert.Equal(2, spans.Count);

        var firstSeen1 = spans[0].GetTagItem(Abstractions.SessionAttributeNames.FirstSeen) as string;
        var firstSeen2 = spans[1].GetTagItem(Abstractions.SessionAttributeNames.FirstSeen) as string;

        Assert.NotNull(firstSeen1);
        Assert.Equal(firstSeen1, firstSeen2); // Stable across invocations

        // AgeSeconds should be non-negative.
        var ageTag = spans[1].GetTagItem(Abstractions.SessionAttributeNames.AgeSeconds);
        Assert.NotNull(ageTag);
        Assert.True((double)ageTag! >= 0);
    }

    // ── FR-006: Token aggregates accumulate ──────────────────────────────────

    [Fact]
    public async Task Token_Totals_Accumulate_Across_Invocations()
    {
        using var recorder = new ActivityRecorder("agg-token-source");
        using var source = new ActivitySource("agg-token-source");

        var responses = new Queue<AgentResponse>(
        [
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "r1")) { Usage = new() { InputTokenCount = 10, OutputTokenCount = 5 } },
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "r2")) { Usage = new() { InputTokenCount = 20, OutputTokenCount = 8 } },
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "r3")) { Usage = new() { InputTokenCount = 15, OutputTokenCount = 3 } },
        ]);

        var stub = new StubAIAgent(() => responses.Dequeue());
        var agent = new AIAgentBuilder(stub)
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();

        for (int i = 1; i <= 3; i++)
        {
            using (source.StartActivity($"tok-span-{i}"))
            {
                await agent.RunAsync([new ChatMessage(ChatRole.User, $"q{i}")], session);
            }
        }

        var spans = recorder.Completed.ToList();
        var lastSpan = spans[^1];

        var totalIn = lastSpan.GetTagItem(Abstractions.SessionAttributeNames.TotalInputTokens);
        var totalOut = lastSpan.GetTagItem(Abstractions.SessionAttributeNames.TotalOutputTokens);

        Assert.Equal(45L, totalIn); // 10+20+15
        Assert.Equal(16L, totalOut); // 5+8+3
    }

    [Fact]
    public async Task Token_Tags_Absent_When_TrackTokenAggregates_False()
    {
        using var recorder = new ActivityRecorder("agg-notoken-source");
        using var source = new ActivitySource("agg-notoken-source");

        var stub = new StubAIAgent(
            () => new AgentResponse(new ChatMessage(ChatRole.Assistant, "r"))
                  { Usage = new() { InputTokenCount = 50, OutputTokenCount = 25 } });

        var agent = new AIAgentBuilder(stub)
            .UseSessionTelemetry(o => o.TrackTokenAggregates = false)
            .Build();

        var session = await agent.CreateSessionAsync();

        using (source.StartActivity("span1"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "q")], session);
        }

        var span = recorder.Completed.Single();
        Assert.Null(span.GetTagItem(Abstractions.SessionAttributeNames.TotalInputTokens));
        Assert.Null(span.GetTagItem(Abstractions.SessionAttributeNames.TotalOutputTokens));
    }

    // ── FR-007: Token totals survive serialize + deserialize ─────────────────

    [Fact]
    public async Task Token_Totals_Survive_Serialize_Deserialize_Roundtrip()
    {
        var responses = new Queue<AgentResponse>(
        [
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "r1")) { Usage = new() { InputTokenCount = 100, OutputTokenCount = 50 } },
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "r2")) { Usage = new() { InputTokenCount = 200, OutputTokenCount = 75 } },
        ]);

        var stub = new StubAIAgent(() => responses.Dequeue());
        var agent = new AIAgentBuilder(stub)
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();

        // First invocation.
        await agent.RunAsync([new ChatMessage(ChatRole.User, "first")], session);

        // Serialize + deserialize.
        var serialized = await agent.SerializeSessionAsync(session);
        var restored = await agent.DeserializeSessionAsync(serialized);

        // Second invocation on restored session.
        await agent.RunAsync([new ChatMessage(ChatRole.User, "second")], restored);

        var block = GetStateBlock(restored);
        Assert.NotNull(block);
        Assert.Equal(300L, block!.TotalInputTokens);  // 100+200
        Assert.Equal(125L, block.TotalOutputTokens);  // 50+75
    }

    // ── FR-022: Zero-usage turn leaves totals unchanged ─────────────────────

    [Fact]
    public async Task Zero_Usage_Turn_Does_Not_Change_Token_Totals()
    {
        var responses = new Queue<AgentResponse>(
        [
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "r1")) { Usage = new() { InputTokenCount = 10, OutputTokenCount = 5 } },
            new AgentResponse(new ChatMessage(ChatRole.Assistant, "r2")), // no usage
        ]);

        var stub = new StubAIAgent(() => responses.Dequeue());
        var agent = new AIAgentBuilder(stub)
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();

        await agent.RunAsync([new ChatMessage(ChatRole.User, "first")], session);
        await agent.RunAsync([new ChatMessage(ChatRole.User, "second")], session);

        var block = GetStateBlock(session);
        Assert.NotNull(block);
        Assert.Equal(10L, block!.TotalInputTokens);
        Assert.Equal(5L, block.TotalOutputTokens);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Internal.SessionStateBlock? GetStateBlock(AgentSession session)
    {
        Internal.SessionStateBagAccessor.TryRead(session.StateBag, "__melic_telemetry", out var block);
        return block;
    }
}
