using PostCodeSerialMonitor.Utils;
using Xunit;

namespace PostCodeSerialMonitor.Tests;

public class AutoScrollTrackerTests
{
    [Fact]
    public void OnScrollChanged_ContentGrowsFasterThanScrollToEndCanCatchUp_StaysAutoScrolled()
    {
        var tracker = new AutoScrollTracker();

        // Simulates a burst of fast-arriving log lines: new content arrives, so the caller asks
        // whether it should scroll to the end (as MainWindow does on every ItemsRepeater layout
        // update) and does so, which is what will trigger the ScrollChanged below.
        Assert.True(tracker.ShouldScrollToEnd());

        // Before that resulting ScrollChanged is processed, more lines already grew the extent
        // further (104 -> 130) while the offset still reflects the smaller extent it was set
        // against (54). The user never touched the scrollbar, so auto-scroll should not detach.
        tracker.OnScrollChanged(offsetY: 54, extentHeight: 130, viewportHeight: 50);

        Assert.True(tracker.AutoScroll);
    }

    [Fact]
    public void OnScrollChanged_UserScrollsAwayWithoutScrollToEnd_Detaches()
    {
        var tracker = new AutoScrollTracker();

        // No ShouldScrollToEnd() call precedes this - the user dragged the scrollbar up themselves.
        tracker.OnScrollChanged(offsetY: 0, extentHeight: 130, viewportHeight: 50);

        Assert.False(tracker.AutoScroll);
    }
}
