// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Abstractions;
using OpenTelemetry;

namespace Melic.AgentFramework.Observability.Redaction.Internal;

internal sealed class RedactionProcessor : BaseProcessor<Activity>
{
    private readonly RedactionPolicy _policy;
    private readonly RedactionEngine _engine = new();

    internal RedactionProcessor(RedactionPolicy policy)
    {
        _policy = policy;
    }

    public override void OnEnd(Activity data)
    {
        try
        {
            ApplyRedaction(data);
        }
        catch
        {
        }
    }

    private void ApplyRedaction(Activity activity)
    {
        int totalMatches = 0;
        int totalFailures = 0;
        bool applied = false;

        foreach (KeyValuePair<string, object?> tag in activity.TagObjects.ToArray())
        {
            if (tag.Value is not string value || !_policy.TargetMatcher.IsTargeted(tag.Key))
            {
                continue;
            }

            RedactionResult result = _engine.Redact(value, _policy);
            totalMatches += result.MatchCount;
            totalFailures += result.FailureCount;
            if (result.Changed)
            {
                applied = true;
                activity.SetTag(tag.Key, result.Value);
            }
        }

        if (_policy.EnableDiagnostics)
        {
            activity.SetTag(RedactionAttributeNames.Applied, applied);
            activity.SetTag(RedactionAttributeNames.MatchCount, totalMatches);
            activity.SetTag(RedactionAttributeNames.FailureCount, totalFailures);
        }
    }
}
