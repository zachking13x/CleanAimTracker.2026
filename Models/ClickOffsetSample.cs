using System.Windows;

namespace CleanAimTracker.Models
{
    /// <summary>
    /// Records a single click position alongside the center of the target that was hit.
    /// Used to compute AvgClickOffset, OvershootPct, and UndershootPct after the session.
    /// Stored as a struct to avoid GC pressure from per-click allocations.
    /// </summary>
    public readonly struct ClickOffsetSample
    {
        public readonly Point ClickPoint;
        public readonly Point TargetCenter;

        public ClickOffsetSample(Point clickPoint, Point targetCenter)
        {
            ClickPoint   = clickPoint;
            TargetCenter = targetCenter;
        }
    }
}
