// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Melic.AgentFramework.Observability.Tools.Internal;

internal sealed class ToolPayloadSerializer
{
    private const string TruncatedSentinel = "[truncated]";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal string? SerializeInput(object? inputPayload, int maxLength)
        => SerializeBounded(inputPayload ?? new Dictionary<string, object?>(), maxLength);

    internal string? SerializeOutput(object? outputPayload, int maxLength)
        => SerializeBounded(outputPayload, maxLength);

    internal string? SerializeException(Exception exception, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return SerializeBounded(new Dictionary<string, object?>
        {
            ["type"] = exception.GetType().Name,
            ["message"] = exception.Message
        }, maxLength);
    }

    private static string? SerializeBounded(object? payload, int maxLength)
    {
        try
        {
            if (payload is null)
            {
                return maxLength >= 4 ? "null" : SmallestValidJson(maxLength);
            }

            JsonNode node = JsonSerializer.SerializeToNode(payload, SerializerOptions) ?? JsonNode.Parse("null")!;

            string serialized = node.ToJsonString(SerializerOptions);
            if (serialized.Length <= maxLength)
            {
                return serialized;
            }

            JsonNode? shrunk = Shrink(node, maxLength);
            serialized = (shrunk ?? JsonNode.Parse("null")!).ToJsonString(SerializerOptions);
            if (serialized.Length <= maxLength)
            {
                return serialized;
            }

            return SmallestValidJson(maxLength);
        }
        catch
        {
            return null;
        }
    }

    private static JsonNode? Shrink(JsonNode? node, int maxLength)
    {
        if (node is null)
        {
            return JsonNode.Parse("null");
        }

        if (node is JsonValue value)
        {
            return ShrinkValue(value, maxLength);
        }

        if (node is JsonArray array)
        {
            var copy = new JsonArray();
            foreach (JsonNode? item in array)
            {
                copy.Add(Shrink(item?.DeepClone(), maxLength));
                if (copy.ToJsonString(SerializerOptions).Length > maxLength)
                {
                    copy.RemoveAt(copy.Count - 1);
                    copy.Add(TruncatedSentinel);
                    break;
                }
            }

            return copy.ToJsonString(SerializerOptions).Length <= maxLength ? copy : JsonValue.Create(TruncatedSentinel);
        }

        if (node is JsonObject obj)
        {
            var copy = new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> property in obj)
            {
                copy[property.Key] = Shrink(property.Value?.DeepClone(), maxLength);
                if (copy.ToJsonString(SerializerOptions).Length > maxLength)
                {
                    copy[property.Key] = TruncatedSentinel;
                }
            }

            return copy.ToJsonString(SerializerOptions).Length <= maxLength ? copy : JsonValue.Create(TruncatedSentinel);
        }

        return JsonValue.Create(TruncatedSentinel);
    }

    private static JsonNode? ShrinkValue(JsonValue value, int maxLength)
    {
        if (value.TryGetValue<string>(out string? text) && text is not null)
        {
            int quoteOverhead = 2;
            int usable = Math.Max(0, maxLength - quoteOverhead);
            string truncated = text.Length <= usable ? text : text[..usable];
            return JsonValue.Create(truncated);
        }

        return value.DeepClone();
    }

    private static string? SmallestValidJson(int maxLength)
    {
        if (maxLength >= 2)
        {
            return "\"\"";
        }

        return maxLength >= 1 ? "0" : null;
    }
}