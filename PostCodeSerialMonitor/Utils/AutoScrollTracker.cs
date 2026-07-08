namespace PostCodeSerialMonitor.Utils;

// Decides whether a log view should keep auto-scrolling to the bottom as new
// entries arrive, versus staying put because the user scrolled away.
public class AutoScrollTracker
{
    // Distance from the bottom (in pixels) still considered "at the bottom", to absorb layout jitter
    // from item virtualization/resizing so autoscroll doesn't flicker on/off during normal updates.
    private const double BottomThreshold = 4;

    private double _lastOffsetY;

    public bool AutoScroll { get; private set; } = true;

    public void OnScrollChanged(double offsetY, double extentHeight, double viewportHeight)
    {
        if (offsetY < _lastOffsetY)
        {
            // The offset moved backward. Our own auto-scroll (ScrollToEnd) never does that -
            // it only ever moves forward, chasing a growing extent - so this can only be the
            // user dragging the scrollbar up.
            AutoScroll = false;
        }
        else if (offsetY >= extentHeight - viewportHeight - BottomThreshold)
        {
            AutoScroll = true;
        }

        _lastOffsetY = offsetY;
    }

    public void Reset()
    {
        AutoScroll = true;
    }
}
