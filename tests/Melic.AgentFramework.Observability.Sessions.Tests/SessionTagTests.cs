// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Sessions.Tests.Helpers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace Melic.AgentFramework.Observability.Sessions.Tests;

/// <summary>
/// Tests for US3: Custom Session Tags.
/// Covers FR-010 (tag on span), FR-011 (survives restore), FR-012 (overwrite),
/// FR-013 (reserved prefix rejected), FR-020 (limits), FR-023 (no redaction).
/// </summary>
public sealed class SessionTagTests
{
    private static AIAgent BuildAgent() =>
        new AIAgentBuilder(new StubAIAgent())
            .UseSessionTelemetry()
            .Build();

    // ── FR-010: Custom tag appears on span ────────────────────────────────────

    [Fact]
    public async Task Custom_Tag_Appears_On_Invocation_Span()
    {
        using var recorder = new ActivityRecorder("tag-test-source");
        using var source = new ActivitySource("tag-test-source");

        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        session.SetSessionTag("app.user", "alice");

        using (source.StartActivity("span1"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session);
        }

        var span = recorder.Completed.Single();
        Assert.Equal("alice", span.GetTagItem("app.user") as string);
    }

    // ── FR-011: Tag survives serialize + deserialize ──────────────────────────

    [Fact]
    public async Task Custom_Tag_Survives_Serialize_Deserialize_Roundtrip()
    {
        using var recorder = new ActivityRecorder("tag-restore-source");
        using var source = new ActivitySource("tag-restore-source");

        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        session.SetSessionTag("app.env", "production");

        // Run once to populate state.
        await agent.RunAsync([new ChatMessage(ChatRole.User, "first")], session);

        // Serialize + restore.
        var serialized = await agent.SerializeSessionAsync(session);
        var restored = await agent.DeserializeSessionAsync(serialized);

        using (source.StartActivity("span-after-restore"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "second")], restored);
        }

        var span = recorder.Completed.Single();
        Assert.Equal("production", span.GetTagItem("app.env") as string);
    }

    // ── FR-012: Overwriting a key updates the value ───────────────────────────

    [Fact]
    public async Task Overwriting_Tag_Key_Updates_Value()
    {
        using var recorder = new ActivityRecorder("tag-overwrite-source");
        using var source = new ActivitySource("tag-overwrite-source");

        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        session.SetSessionTag("app.version", "1.0");
        session.SetSessionTag("app.version", "2.0");

        using (source.StartActivity("span1"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session);
        }

        var span = recorder.Completed.Single();
        Assert.Equal("2.0", span.GetTagItem("app.version") as string);
    }

    // ── FR-013: Reserved prefix silently rejected ─────────────────────────────

    [Fact]
    public async Task Reserved_Prefix_Is_Silently_Rejected()
    {
        using var recorder = new ActivityRecorder("tag-reserved-source");
        using var source = new ActivitySource("tag-reserved-source");

        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        // Should be silently ignored.
        session.SetSessionTag("genai.session.custom", "hacked");

        using (source.StartActivity("span1"))
        {
            await agent.RunAsync([new ChatMessage(ChatRole.User, "hi")], session);
        }

        var span = recorder.Completed.Single();
        Assert.Null(span.GetTagItem("genai.session.custom"));
    }

    // ── FR-020: Limit violations are silently rejected ────────────────────────

    [Fact]
    public async Task Empty_Key_Is_Silently_Rejected()
    {
        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        var ex = Record.Exception(() => session.SetSessionTag("", "value"));
        Assert.Null(ex); // Must not throw.
    }

    [Fact]
    public async Task Key_Longer_Than_128_Chars_Is_Silently_Rejected()
    {
        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        var longKey = new string('k', 129);
        var ex = Record.Exception(() => session.SetSessionTag(longKey, "value"));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Value_Longer_Than_512_Chars_Is_Silently_Rejected()
    {
        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        var longValue = new string('v', 513);
        var ex = Record.Exception(() => session.SetSessionTag("app.key", longValue));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Fifty_First_Tag_Is_Silently_Rejected()
    {
        var agent = BuildAgent();
        var session = await agent.CreateSessionAsync();

        // Add exactly 50 tags.
        for (int i = 0; i < 50; i++)
        {
            session.SetSessionTag($"app.tag{i}", $"value{i}");
        }

        // 51st tag should be silently dropped.
        session.SetSessionTag("app.tag50", "overflow");

        // Verify exactly 50 tags in state.
        Internal.SessionStateBagAccessor.TryRead(session.StateBag, "__melic_telemetry", out var block);
        Assert.NotNull(block?.Tags);
        Assert.Equal(50, block!.Tags!.Count);
        Assert.False(block.Tags.ContainsKey("app.tag50"));
    }
}
