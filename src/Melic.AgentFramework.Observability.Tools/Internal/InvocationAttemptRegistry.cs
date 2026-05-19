// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Melic.AgentFramework.Observability.Tools.Internal;

internal sealed class InvocationAttemptRegistry
{
    private readonly ConditionalWeakTable<Activity, ConcurrentDictionary<string, int>> _attempts = new();

    internal AttemptInfo? Record(Activity? parentActivity, string? callId)
    {
        if (parentActivity is null || string.IsNullOrWhiteSpace(callId))
        {
            return null;
        }

        var perInvocation = _attempts.GetOrCreateValue(parentActivity);
        int attemptIndex = perInvocation.AddOrUpdate(callId, 1, static (_, current) => current + 1);
        return new AttemptInfo(attemptIndex > 1, attemptIndex);
    }
}

internal readonly record struct AttemptInfo(bool IsRetry, int AttemptIndex);