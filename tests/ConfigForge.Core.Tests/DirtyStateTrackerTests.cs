using ConfigForge.Abstractions;
using ConfigForge.Core.Documents;
using Xunit;

namespace ConfigForge.Core.Tests;

public sealed class DirtyStateTrackerTests
{
    [Fact]
    public void Snapshot_LeavesTrackerClean()
    {
        var tracker = new DirtyStateTracker();
        var document = new ConfigDocument { ["a"] = "1" };

        tracker.Snapshot(document);

        Assert.False(tracker.IsDirty);
        Assert.Empty(tracker.DirtyKeys);
    }

    [Fact]
    public void Update_AfterMutation_MarksChangedKeyDirty()
    {
        var tracker = new DirtyStateTracker();
        var document = new ConfigDocument { ["a"] = "1", ["b"] = "2" };
        tracker.Snapshot(document);

        document["a"] = "changed";
        tracker.Update(document);

        Assert.True(tracker.IsDirty);
        Assert.Contains("a", tracker.DirtyKeys);
        Assert.DoesNotContain("b", tracker.DirtyKeys);
    }

    [Fact]
    public void Snapshot_AfterMutation_ResetsToClean()
    {
        var tracker = new DirtyStateTracker();
        var document = new ConfigDocument { ["a"] = "1" };
        tracker.Snapshot(document);
        document["a"] = "changed";
        tracker.Update(document);
        Assert.True(tracker.IsDirty);

        tracker.Snapshot(document);

        Assert.False(tracker.IsDirty);
        Assert.Empty(tracker.DirtyKeys);
    }

    [Fact]
    public void Update_RaisesDirtyStateChanged_WhenStateTransitions()
    {
        var tracker = new DirtyStateTracker();
        var document = new ConfigDocument { ["a"] = "1" };
        tracker.Snapshot(document);

        int fired = 0;
        tracker.DirtyStateChanged += (_, _) => fired++;

        document["a"] = "changed";
        tracker.Update(document);

        Assert.True(fired > 0);
    }
}
