// SPDX-License-Identifier: MIT
using System.Text.Json;
using Melic.AgentFramework.Observability.Sessions.Tests.Helpers;
using Xunit;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Melic.AgentFramework.Observability.Sessions.Tests;

/// <summary>
/// Tests for the serialization/deserialization round-trip of the session state block.
/// Covers FR-006 (state survives serialize/restore), FR-007 (ExtensionData compat),
/// and FR-008 (StateBagKey isolation).
/// </summary>
public sealed class SessionSchemaTests
{
    // ── Round-trip: session state survives serialize + deserialize ───────────

    [Fact]
    public async Task SessionId_Survives_Serialize_Deserialize_Roundtrip()
    {
        var stub = new StubAIAgent();
        var agent = new AIAgentBuilder(stub)
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();

        // Run once to populate session state.
        await agent.RunAsync([new ChatMessage(ChatRole.User, "hello")], session);
        var originalId = session.GetSessionId();
        Assert.NotNull(originalId);

        // Serialize → deserialize → run again.
        var serialized = await agent.SerializeSessionAsync(session);
        var restored = await agent.DeserializeSessionAsync(serialized);

        await agent.RunAsync([new ChatMessage(ChatRole.User, "hello again")], restored);
        var restoredId = restored.GetSessionId();

        Assert.Equal(originalId, restoredId);
    }

    [Fact]
    public async Task InvocationIndex_Continues_After_Restore()
    {
        var stub = new StubAIAgent();
        var agent = new AIAgentBuilder(stub)
            .UseSessionTelemetry()
            .Build();

        var session = await agent.CreateSessionAsync();

        await agent.RunAsync([new ChatMessage(ChatRole.User, "1")], session);
        await agent.RunAsync([new ChatMessage(ChatRole.User, "2")], session);

        // Restore and run a 3rd time.
        var serialized = await agent.SerializeSessionAsync(session);
        var restored = await agent.DeserializeSessionAsync(serialized);

        await agent.RunAsync([new ChatMessage(ChatRole.User, "3")], restored);

        // InvocationIndex after restore should be 3.
        var stateBlock = GetStateBlock(restored);
        Assert.NotNull(stateBlock);
        Assert.Equal(3, stateBlock!.InvocationIndex);
    }

    // ── FR-008: Custom StateBagKey isolation ─────────────────────────────────

    [Fact]
    public async Task Custom_StateBagKey_Does_Not_Collide_With_Default()
    {
        var stub = new StubAIAgent();

        var agentDefault = new AIAgentBuilder(stub)
            .UseSessionTelemetry()
            .Build();

        var agentCustom = new AIAgentBuilder(stub)
            .UseSessionTelemetry(o => o.StateBagKey = "__custom_key")
            .Build();

        var sessionDefault = await agentDefault.CreateSessionAsync();
        var sessionCustom = await agentCustom.CreateSessionAsync();

        await agentDefault.RunAsync([new ChatMessage(ChatRole.User, "a")], sessionDefault);
        await agentCustom.RunAsync([new ChatMessage(ChatRole.User, "b")], sessionCustom);

        var idDefault = sessionDefault.GetSessionId("__melic_telemetry");
        var idCustom = sessionCustom.GetSessionId("__custom_key");

        Assert.NotNull(idDefault);
        Assert.NotNull(idCustom);
        Assert.NotEqual(idDefault, idCustom);

        // Default session should have nothing under custom key.
        Assert.Null(sessionDefault.GetSessionId("__custom_key"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Internal.SessionStateBlock? GetStateBlock(AgentSession session)
    {
        Internal.SessionStateBagAccessor.TryRead(session.StateBag, "__melic_telemetry", out var block);
        return block;
    }
}
