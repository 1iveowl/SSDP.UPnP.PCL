using System.Reactive.Subjects;
using SSDP.UPnP.PCL.Internal;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class RxExTests
{
    [Fact]
    public void FinallyAsync_RunsOnCompletion_NotPerElement()
    {
        var subject = new Subject<int>();
        var finallyCount = 0;

        using var subscription = subject
            .FinallyAsync(() =>
            {
                finallyCount++;
                return Task.CompletedTask;
            })
            .Subscribe(_ => { });

        subject.OnNext(1);
        subject.OnNext(2);
        Assert.Equal(0, finallyCount);

        subject.OnCompleted();
        Assert.Equal(1, finallyCount);
    }

    [Fact]
    public void FinallyAsync_RunsOnError()
    {
        var subject = new Subject<int>();
        var finallyCount = 0;
        Exception? seen = null;

        using var subscription = subject
            .FinallyAsync(() =>
            {
                finallyCount++;
                return Task.CompletedTask;
            })
            .Subscribe(_ => { }, ex => seen = ex);

        subject.OnError(new InvalidOperationException("boom"));

        Assert.Equal(1, finallyCount);
        Assert.IsType<InvalidOperationException>(seen);
    }
}
