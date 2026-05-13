// SPDX-License-Identifier: MIT
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Melic.AgentFramework.Observability.Sessions.Tests.Helpers;

/// <summary>
/// A simple <see cref="AgentSession"/> subclass for use in unit and integration tests.
/// Serialises and deserialises correctly using the base-class <see cref="AgentSession.StateBag"/>.
/// </summary>
internal sealed class StubAgentSession : AgentSession
{
    internal StubAgentSession() { }

    internal StubAgentSession(AgentSessionStateBag stateBag) : base(stateBag) { }
}

/// <summary>
/// Configurable <see cref="AIAgent"/> for use in unit and integration tests.
/// Returns a pre-configured <see cref="AgentResponse"/> and supports session serialization.
/// </summary>
internal sealed class StubAIAgent : AIAgent
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Func<AgentResponse>? _responseFactory;

    /// <summary>Initialises a <see cref="StubAIAgent"/> that always returns a plain-text response.</summary>
    /// <param name="response">Fixed text response. Defaults to <c>"stub response"</c>.</param>
    /// <param name="name">Optional agent name.</param>
    internal StubAIAgent(string response = "stub response", string? name = null)
        : this(() => new AgentResponse(new ChatMessage(ChatRole.Assistant, response)), name)
    {
    }

    /// <summary>Initialises a <see cref="StubAIAgent"/> with a factory that produces each response.</summary>
    /// <param name="responseFactory">Factory invoked on each <see cref="RunCoreAsync"/> call.</param>
    /// <param name="name">Optional agent name.</param>
    internal StubAIAgent(Func<AgentResponse> responseFactory, string? name = null)
    {
        _responseFactory = responseFactory;
        Name = name ?? "StubAgent";
    }

    /// <inheritdoc />
    public override string? Name { get; }

    /// <inheritdoc />
    protected override string? IdCore => "stub-agent-id";

    /// <inheritdoc />
    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default)
        => new(new StubAgentSession());

    /// <inheritdoc />
    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(
        AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (session is not StubAgentSession stubSession)
        {
            throw new InvalidOperationException($"Expected {nameof(StubAgentSession)}, got {session.GetType().Name}.");
        }

        var dto = new SessionDto(stubSession.StateBag);
        return new(JsonSerializer.SerializeToElement(dto, jsonSerializerOptions ?? _serializerOptions));
    }

    /// <inheritdoc />
    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(
        JsonElement serializedState,
        JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default)
    {
        var jso = jsonSerializerOptions ?? _serializerOptions;
        var dto = serializedState.Deserialize<SessionDto>(jso) ?? new SessionDto(null);
        var stateBag = dto.StateBag ?? new AgentSessionStateBag();
        return new(new StubAgentSession(stateBag));
    }

    /// <inheritdoc />
    protected override Task<AgentResponse> RunCoreAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = _responseFactory?.Invoke() ?? new AgentResponse(new ChatMessage(ChatRole.Assistant, "stub"));
        return Task.FromResult(response);
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session = null,
        AgentRunOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await RunCoreAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
        foreach (var msg in response.Messages)
        {
            yield return new AgentResponseUpdate(msg.Role, msg.Text);
        }
    }

    private sealed class SessionDto
    {
        public SessionDto(AgentSessionStateBag? stateBag)
        {
            StateBag = stateBag;
        }

        [JsonPropertyName("stateBag")]
        public AgentSessionStateBag? StateBag { get; init; }
    }
}
