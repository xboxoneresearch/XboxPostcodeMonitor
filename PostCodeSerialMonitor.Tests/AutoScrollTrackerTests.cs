using PostCodeSerialMonitor.Utils;
using Xunit;

namespace PostCodeSerialMonitor.Tests;

public class AutoScrollTrackerTests
{
    [Fact]
    public void OnScrollChanged_ContentGrowsFasterThanScrollToEndCanCatchUp_StaysAutoScrolled()
    {
        var tracker = new AutoScrollTracker();

        // Simulates a burst of fast-arriving log lines: ScrollToEnd() was called while the
        // extent was still 104 (viewport 50), setting the offset to 54. Before that resulting
        // ScrollChanged is processed, more lines already grew the extent further to 130. The
        // offset only ever moved forward, so this is not the user scrolling away.
        tracker.OnScrollChanged(offsetY: 54, extentHeight: 130, viewportHeight: 50);

        Assert.True(tracker.AutoScroll);
    }

    [Fact]
    public void OnScrollChanged_UserDragsScrollbarUp_Detaches()
    {
        var tracker = new AutoScrollTracker();
        tracker.OnScrollChanged(offsetY: 76, extentHeight: 130, viewportHeight: 50); // starts at bottom

        // The offset moves backward - only a manual scrollbar drag does that.
        tracker.OnScrollChanged(offsetY: 20, extentHeight: 130, viewportHeight: 50);

        Assert.False(tracker.AutoScroll);
    }

    [Fact]
    public void OnScrollChanged_UserDragsAwayAfterSustainedAutoScrolling_StillDetaches()
    {
        var tracker = new AutoScrollTracker();

        // Content keeps growing, offset keeps chasing it forward - auto-scroll stays engaged.
        tracker.OnScrollChanged(offsetY: 50, extentHeight: 104, viewportHeight: 50);
        tracker.OnScrollChanged(offsetY: 76, extentHeight: 130, viewportHeight: 50);
        Assert.True(tracker.AutoScroll);

        // The user then grabs the scrollbar and drags it up.
        tracker.OnScrollChanged(offsetY: 20, extentHeight: 130, viewportHeight: 50);

        Assert.False(tracker.AutoScroll);
    }
}
