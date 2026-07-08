namespace PostCodeSerialMonitor.Utils;

// Decides whether a log view should keep auto-scrolling to the bottom as new
// entries arrive, versus staying put because the user scrolled away.
public class AutoScrollTracker
{
    // Distance from the bottom (in pixels) still considered "at the bottom", to absorb layout jitter
    // from item virtualization/resizing so autoscroll doesn't flicker on/off during normal updates.
    private const double BottomThreshold = 4;

    // Set right before we call ScrollToEnd() ourselves, so the ScrollChanged it triggers isn't
    // mistaken for the user scrolling away (which happens if content keeps growing between our
    // call and that event, leaving the offset behind a moving extent).
    private bool _isProgrammaticScroll;

    public bool AutoScroll { get; private set; } = true;

    public bool ShouldScrollToEnd()
    {
        if (!AutoScroll)
            return false;

        _isProgrammaticScroll = true;
        return true;
    }

    public void OnScrollChanged(double offsetY, double extentHeight, double viewportHeight)
    {
        if (_isProgrammaticScroll)
        {
            _isProgrammaticScroll = false;
            return;
        }

        AutoScroll = offsetY >= extentHeight - viewportHeight - BottomThreshold;
    }

    public void Reset()
    {
        AutoScroll = true;
    }
}
