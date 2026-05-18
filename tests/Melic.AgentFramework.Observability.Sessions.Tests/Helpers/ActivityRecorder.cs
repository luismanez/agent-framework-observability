// SPDX-License-Identifier: MIT
using System.Collections.Generic;
using System.Diagnostics;

namespace Melic.AgentFramework.Observability.Sessions.Tests.Helpers;

/// <summary>
/// Records all <see cref="Activity"/> objects started by a given <see cref="ActivitySource"/> name.
/// Implements <see cref="IDisposable"/> — dispose to unregister the listener.
/// </summary>
internal sealed class ActivityRecorder : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _completed = [];
    private bool _disposed;

    internal ActivityRecorder(string sourceName)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _completed.Add(activity)
        };

        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>All activities that have been stopped since this recorder was created.</summary>
    internal IReadOnlyList<Activity> Completed => _completed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _listener.Dispose();
            _disposed = true;
        }
    }
}
