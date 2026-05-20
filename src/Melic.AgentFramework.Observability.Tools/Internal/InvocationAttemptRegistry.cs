// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Melic.AgentFramework.Observability.Tools.Internal;

internal sealed class InvocationAttemptRegistry
{
    private readonly ConditionalWeakTable<Activity, ConcurrentDictionary<string, int>> _attempts = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _mafInvocationAttempts = new();

    internal AttemptInfo? Record(Activity? parentActivity, string? callId)
    {
        if (parentActivity is null || string.IsNullOrWhiteSpace(callId))
        {
            return null;
        }

        string? mafInvocationKey = GetMafInvocationKey(parentActivity);
        if (mafInvocationKey is not null)
        {
            var perMafInvocation = _mafInvocationAttempts.GetOrAdd(mafInvocationKey, static _ => new ConcurrentDictionary<string, int>());
            int mafAttemptIndex = perMafInvocation.AddOrUpdate(callId, 1, static (_, current) => current + 1);
            return new AttemptInfo(mafAttemptIndex > 1, mafAttemptIndex);
        }

        var perInvocation = _attempts.GetOrCreateValue(parentActivity);
        int attemptIndex = perInvocation.AddOrUpdate(callId, 1, static (_, current) => current + 1);
        return new AttemptInfo(attemptIndex > 1, attemptIndex);
    }

    private static string? GetMafInvocationKey(Activity activity)
    {
        if (!string.Equals(
            activity.GetTagItem(ToolTelemetryAgent.MafOperationNameAttribute) as string,
            ToolTelemetryAgent.MafExecuteToolOperationName,
            StringComparison.Ordinal))
        {
            return null;
        }

        ActivitySpanId parentSpanId = activity.ParentSpanId;
        return parentSpanId == default ? null : $"{activity.TraceId}:{parentSpanId}";
    }
}

internal readonly record struct AttemptInfo(bool IsRetry, int AttemptIndex);