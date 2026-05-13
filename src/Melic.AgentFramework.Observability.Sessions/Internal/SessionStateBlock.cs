// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Melic.AgentFramework.Observability.Sessions.Internal;

/// <summary>
/// Persistent session telemetry state stored in <see cref="Microsoft.Agents.AI.AgentSessionStateBag"/>.
/// Survives session serialization and deserialization. Forward-compatible: unknown fields from newer
/// library versions are preserved in <see cref="ExtensionData"/> and round-tripped correctly.
/// </summary>
internal sealed record SessionStateBlock
{
    /// <summary>Stable session identifier. Null until first invocation or explicit assignment.</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    /// <summary>Number of invocations recorded in this session.</summary>
    [JsonPropertyName("invocationIndex")]
    public int InvocationIndex { get; init; }

    /// <summary>Running total of input tokens across all invocations.</summary>
    [JsonPropertyName("totalInputTokens")]
    public long TotalInputTokens { get; init; }

    /// <summary>Running total of output tokens across all invocations.</summary>
    [JsonPropertyName("totalOutputTokens")]
    public long TotalOutputTokens { get; init; }

    /// <summary>UTC timestamp of the first invocation. Default value means not yet seen.</summary>
    [JsonPropertyName("firstSeenUtc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DateTimeOffset FirstSeenUtc { get; init; }

    /// <summary>Number of errors recorded during this session (used by Mode B session span).</summary>
    [JsonPropertyName("errorCount")]
    public int ErrorCount { get; init; }

    /// <summary>
    /// Developer-supplied custom tags. Max 50 entries; key ≤ 128 chars; value ≤ 512 chars.
    /// Keys matching the <c>genai.session.</c> prefix are rejected at write time.
    /// </summary>
    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Tags { get; init; }

    /// <summary>
    /// Preserves fields written by newer library versions when deserialized by older ones.
    /// Round-tripped transparently without data loss.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
