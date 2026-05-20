// SPDX-License-Identifier: MIT
using System.Diagnostics;
using Melic.AgentFramework.Observability.Tools.Internal;
using Xunit;

namespace Melic.AgentFramework.Observability.Tools.Tests;

public sealed class InvocationAttemptRegistryTests
{
    [Fact]
    public void Records_First_Second_And_Third_Attempts()
    {
        var registry = new InvocationAttemptRegistry();
        using var source = new ActivitySource("registry-test");
        using Activity activity = source.StartActivity("parent") ?? new Activity("parent").Start();

        Assert.Equal(new AttemptInfo(false, 1), registry.Record(activity, "call-1"));
        Assert.Equal(new AttemptInfo(true, 2), registry.Record(activity, "call-1"));
        Assert.Equal(new AttemptInfo(true, 3), registry.Record(activity, "call-1"));
    }

    [Fact]
    public void Tracks_Different_Ids_And_Parents_Independently()
    {
        var registry = new InvocationAttemptRegistry();
        using var first = new Activity("first").Start();
        using var second = new Activity("second").Start();

        Assert.Equal(new AttemptInfo(false, 1), registry.Record(first, "a"));
        Assert.Equal(new AttemptInfo(false, 1), registry.Record(first, "b"));
        Assert.Equal(new AttemptInfo(false, 1), registry.Record(second, "a"));
    }

    [Fact]
    public void Omits_Attempts_Without_Parent_Or_CallId()
    {
        var registry = new InvocationAttemptRegistry();
        using var parent = new Activity("parent").Start();

        Assert.Null(registry.Record(null, "call"));
        Assert.Null(registry.Record(parent, null));
        Assert.Null(registry.Record(parent, " "));
    }

    [Fact]
    public void Records_Concurrent_Attempts_Without_Lost_Updates()
    {
        var registry = new InvocationAttemptRegistry();
        using var parent = new Activity("parent").Start();

        Parallel.For(0, 100, _ => registry.Record(parent, "call"));

        Assert.Equal(new AttemptInfo(true, 101), registry.Record(parent, "call"));
    }
}