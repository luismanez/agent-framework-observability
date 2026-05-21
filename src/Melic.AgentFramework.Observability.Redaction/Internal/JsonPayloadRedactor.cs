// SPDX-License-Identifier: MIT
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Melic.AgentFramework.Observability.Redaction.Internal;

internal sealed class JsonPayloadRedactor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    internal bool TryRedact(string value, RedactionPolicy policy, out RedactionResult result)
    {
        result = default;
        if (!LooksLikeJson(value))
        {
            return false;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(value);
            if (node is null)
            {
                return false;
            }

            int matchCount = RedactNode(node, policy);
            string redacted = node.ToJsonString(SerializerOptions);
            result = new RedactionResult(redacted, matchCount > 0, matchCount, FailureCount: 0, WasTooLong: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeJson(string value)
    {
        string trimmed = value.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static int RedactNode(JsonNode? node, RedactionPolicy policy)
    {
        if (node is null)
        {
            return 0;
        }

        if (node is JsonObject jsonObject)
        {
            return RedactObject(jsonObject, policy);
        }

        if (node is JsonArray jsonArray)
        {
            int arrayMatches = 0;
            for (int index = 0; index < jsonArray.Count; index++)
            {
                JsonNode? item = jsonArray[index];
                if (item is JsonValue value && value.TryGetValue<string>(out string? text) && text is not null)
                {
                    string redacted = ApplyRules(text, policy, out int stringMatches);
                    if (stringMatches > 0)
                    {
                        jsonArray[index] = redacted;
                        arrayMatches += stringMatches;
                    }

                    continue;
                }

                arrayMatches += RedactNode(item, policy);
            }

            return arrayMatches;
        }

        return 0;
    }

    private static int RedactObject(JsonObject jsonObject, RedactionPolicy policy)
    {
        int matchCount = 0;
        foreach (KeyValuePair<string, JsonNode?> property in jsonObject.ToArray())
        {
            if (policy.SensitiveFieldNames.Contains(property.Key))
            {
                if (property.Value is not null && !IsReplacementValue(property.Value, policy.ReplacementText))
                {
                    jsonObject[property.Key] = policy.ReplacementText;
                    matchCount++;
                }

                continue;
            }

            if (property.Value is JsonValue value && value.TryGetValue<string>(out string? text) && text is not null)
            {
                string redacted = ApplyRules(text, policy, out int stringMatches);
                if (stringMatches > 0)
                {
                    jsonObject[property.Key] = redacted;
                    matchCount += stringMatches;
                }

                continue;
            }

            matchCount += RedactNode(property.Value, policy);
        }

        return matchCount;
    }

    private static string ApplyRules(string value, RedactionPolicy policy, out int matchCount)
    {
        string current = value;
        matchCount = 0;
        foreach (RedactionRule rule in policy.Rules)
        {
            current = rule.Apply(current, policy.ReplacementText, out int ruleMatches);
            matchCount += ruleMatches;
        }

        return current;
    }

    private static bool IsReplacementValue(JsonNode value, string replacementText)
        => value is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out string? text)
            && string.Equals(text, replacementText, StringComparison.Ordinal);
}
