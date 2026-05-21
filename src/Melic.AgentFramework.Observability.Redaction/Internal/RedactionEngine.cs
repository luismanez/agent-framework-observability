// SPDX-License-Identifier: MIT
namespace Melic.AgentFramework.Observability.Redaction.Internal;

internal sealed class RedactionEngine
{
    private readonly JsonPayloadRedactor _jsonPayloadRedactor = new();

    internal RedactionResult Redact(string value, RedactionPolicy policy)
    {
        try
        {
            if (value.Length > policy.MaxValueLength)
            {
                return HandleFailure(value, policy, wasTooLong: true);
            }

            if (_jsonPayloadRedactor.TryRedact(value, policy, out RedactionResult jsonResult))
            {
                return jsonResult;
            }

            return RedactString(value, policy);
        }
        catch
        {
            return HandleFailure(value, policy, wasTooLong: false);
        }
    }

    private static RedactionResult RedactString(string value, RedactionPolicy policy)
    {
        string current = value;
        int matchCount = 0;
        foreach (RedactionRule rule in policy.Rules)
        {
            current = rule.Apply(current, policy.ReplacementText, out int ruleMatches);
            matchCount += ruleMatches;
        }

        return new RedactionResult(current, matchCount > 0, matchCount, FailureCount: 0, WasTooLong: false);
    }

    private static RedactionResult HandleFailure(string originalValue, RedactionPolicy policy, bool wasTooLong)
    {
        if (policy.FailureMode == RedactionFailureMode.PreserveOriginal)
        {
            return new RedactionResult(originalValue, Changed: false, MatchCount: 0, FailureCount: 1, WasTooLong: wasTooLong);
        }

        return new RedactionResult(policy.ReplacementText, Changed: !string.Equals(originalValue, policy.ReplacementText, StringComparison.Ordinal), MatchCount: 1, FailureCount: 1, WasTooLong: wasTooLong);
    }
}
