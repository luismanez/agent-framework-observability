// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

namespace Melic.AgentFramework.Observability.Redaction.Tests;

internal sealed class RedactionTestExporter : BaseExporter<Activity>
{
    private readonly ConcurrentQueue<CapturedActivity> _activities = new();

    internal IReadOnlyList<CapturedActivity> ExportedActivities => _activities.ToArray();

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (Activity activity in batch)
        {
            _activities.Enqueue(new CapturedActivity(
                activity.DisplayName,
                activity.TagObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                activity.Status));
        }

        return ExportResult.Success;
    }
}

internal sealed record CapturedActivity(
    string DisplayName,
    IReadOnlyDictionary<string, object?> Tags,
    ActivityStatusCode Status);
