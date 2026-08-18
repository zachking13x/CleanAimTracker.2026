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
        // F6: Stopwatch.GetTimestamp() ticks at click time — same clock as
        // RawInputSample.Timestamp, so the raw movement buffer can be split into
        // per-acquisition segments at click boundaries for PathEfficiency.
        public readonly long  Timestamp;

        public ClickOffsetSample(Point clickPoint, Point targetCenter, long timestamp = 0)
        {
            ClickPoint   = clickPoint;
            TargetCenter = targetCenter;
            Timestamp    = timestamp;
        }
    }
}
