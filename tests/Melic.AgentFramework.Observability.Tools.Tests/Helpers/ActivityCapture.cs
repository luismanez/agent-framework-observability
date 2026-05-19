// SPDX-License-Identifier: MIT
using System.Diagnostics;

namespace Melic.AgentFramework.Observability.Tools.Tests.Helpers;

internal sealed class ActivityCapture : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _completed = [];
    private bool _disposed;

    internal ActivityCapture(string sourceName)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _completed.Add(activity)
        };

        ActivitySource.AddActivityListener(_listener);
    }

    internal IReadOnlyList<Activity> Completed => _completed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _listener.Dispose();
            _disposed = true;
        }
    }
}