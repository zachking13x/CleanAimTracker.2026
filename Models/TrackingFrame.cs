using System.Windows;

namespace CleanAimTracker.Models
{
    /// <summary>
    /// A single per-frame cursor/target position pair captured during a tracking drill.
    /// Used to compute HorizontalTrackingAcc and VerticalTrackingAcc after the session.
    /// Stored as a struct to keep the per-frame buffer allocation lightweight.
    /// </summary>
    public readonly struct TrackingFrame
    {
        public readonly Point CursorPos;
        public readonly Point TargetPos;

        public TrackingFrame(Point cursorPos, Point targetPos)
        {
            CursorPos = cursorPos;
            TargetPos = targetPos;
        }
    }
}
