using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using CleanAimTracker.Models;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Derives aim-quality metrics from the raw input buffer collected during a drill.
    /// All methods are pure static so they can be called from any context.
    /// </summary>
    public static class TelemetryCalculator
    {
        // ------------------------------------------------------------------ //
        //  1. Path Efficiency (0.0 – 1.0)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Ratio of straight-line displacement to actual path length travelled.
        /// 1.0 = perfectly direct movement; lower values indicate wobble / overshoot.
        /// </summary>
        /// <param name="samples">Raw hardware mouse deltas captured during the move.</param>
        /// <param name="startPosition">Canvas point where the cursor started (unused in
        /// calculation but kept for API symmetry with the click-offset helper).</param>
        /// <param name="endPosition">Canvas point where the cursor ended.</param>
        public static double CalculatePathEfficiency(
            List<RawInputSample> samples,
            Point startPosition,
            Point endPosition)
        {
            // Guard: too few samples → assume clean movement
            if (samples == null || samples.Count < 20)
                return 1.0;

            double totalDx = 0, totalDy = 0, actualPath = 0;

            foreach (var s in samples)
            {
                totalDx += s.Dx;
                totalDy += s.Dy;
                actualPath += Math.Sqrt((double)s.Dx * s.Dx + (double)s.Dy * s.Dy);
            }

            double straight = Math.Sqrt(totalDx * totalDx + totalDy * totalDy);

            if (straight < 0.01 || actualPath < 0.01)
                return 1.0;

            return Math.Clamp(straight / actualPath, 0.0, 1.0);
        }

        // ------------------------------------------------------------------ //
        //  2. Click Offset (pixels)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Euclidean distance between where the player clicked and the target centre.
        /// 0 = bullseye.
        /// </summary>
        public static double CalculateClickOffset(Point clickPosition, Point targetCenter)
        {
            double dx = clickPosition.X - targetCenter.X;
            double dy = clickPosition.Y - targetCenter.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Derives per-session click-offset metrics from a list of hit samples.
        /// <list type="bullet">
        ///   <item><c>AvgOffset</c> — mean Euclidean distance from click to target centre</item>
        ///   <item><c>OvershootPct</c> — % of hits where click landed &gt;8 px from centre</item>
        ///   <item><c>UndershootPct</c> — % of hits where click landed 3–8 px from centre</item>
        /// </list>
        /// </summary>
        public static (double AvgOffset, double OvershootPct, double UndershootPct)
            CalculateClickOffsets(List<ClickOffsetSample> samples)
        {
            if (samples == null || samples.Count == 0) return (0, 0, 0);

            double totalOffset = 0;
            int overshoot  = 0;
            int undershoot = 0;

            foreach (var s in samples)
            {
                double dx   = s.ClickPoint.X - s.TargetCenter.X;
                double dy   = s.ClickPoint.Y - s.TargetCenter.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                totalOffset += dist;
                if (dist > 8.0)
                    overshoot++;
                else if (dist >= 3.0)
                    undershoot++;
            }

            double count = samples.Count;
            return (
                totalOffset / count,
                overshoot  / count * 100.0,
                undershoot / count * 100.0);
        }

        // ------------------------------------------------------------------ //
        //  3. Direction-Change Lag (ms)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Average milliseconds between the moment a target changes direction and
        /// the moment the player's cursor matches that direction change.
        /// Each pair is (targetChangeTimestamp, playerChangeTimestamp) in Stopwatch ticks.
        /// </summary>
        /// <param name="directionChangePairs">
        /// List of (targetTs, playerTs) Stopwatch-tick pairs collected by the scenario.
        /// </param>
        public static double CalculateDirectionChangeLag(
            List<(long TargetChangeTs, long PlayerChangeTs)> directionChangePairs)
        {
            if (directionChangePairs == null || directionChangePairs.Count < 3)
                return 0;

            double ticksPerMs = (double)Stopwatch.Frequency / 1000.0;
            double totalLagMs = 0;

            foreach (var (targetTs, playerTs) in directionChangePairs)
            {
                double lagMs = (playerTs - targetTs) / ticksPerMs;
                // Clamp negative lag to 0 (player led the change — counts as 0 lag)
                totalLagMs += Math.Max(0, lagMs);
            }

            return totalLagMs / directionChangePairs.Count;
        }

        /// <summary>
        /// Overload for Reactive-style scenarios where the "direction change" event is
        /// a new target spawning at a random location.
        /// For each spawn timestamp the method scans the raw-input buffer to find the
        /// first sample after that timestamp where mouse movement is significant
        /// (magnitude ≥ 8 raw counts).  The elapsed time is the direction-change lag.
        /// Requires at least 3 valid lag measurements; returns 0 if insufficient data.
        /// </summary>
        /// <param name="targetChangeTimes">Stopwatch timestamps of target spawn events.</param>
        /// <param name="rawBuffer">Raw hardware mouse-delta samples from the session.</param>
        public static double CalculateDirectionChangeLag(
            List<long> targetChangeTimes,
            List<RawInputSample> rawBuffer)
        {
            if (targetChangeTimes == null || targetChangeTimes.Count < 3) return 0;
            if (rawBuffer         == null || rawBuffer.Count         < 5) return 0;

            double ticksPerMs = (double)Stopwatch.Frequency / 1000.0;
            var lags = new List<double>();

            foreach (long changeTs in targetChangeTimes)
            {
                foreach (var s in rawBuffer)
                {
                    if (s.Timestamp <= changeTs) continue;

                    double mag = Math.Sqrt((double)s.Dx * s.Dx + (double)s.Dy * s.Dy);
                    if (mag < 8.0) continue;  // ignore micro-movement / sensor jitter

                    double lagMs = (s.Timestamp - changeTs) / ticksPerMs;
                    if (lagMs > 0 && lagMs < 2000)
                        lags.Add(lagMs);
                    break;   // only the FIRST significant movement after the spawn counts
                }
            }

            if (lags.Count < 3) return 0;
            return lags.Average();
        }

        // ------------------------------------------------------------------ //
        //  4. Axis Split (horizontal / vertical tracking accuracy %)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Percentage of frames where the cursor was within <paramref name="tolerance"/>
        /// pixels of the target on each axis independently.
        /// Returns (horizontalAccuracy%, verticalAccuracy%).
        /// </summary>
        /// <param name="frames">List of (cursorPosition, targetPosition) canvas-space pairs.</param>
        /// <param name="tolerance">Pixel tolerance band (default should match target size / 2).</param>
        public static (double HorizAcc, double VertAcc) CalculateAxisSplit(
            List<(Point CursorPos, Point TargetPos)> frames,
            double tolerance)
        {
            if (frames == null || frames.Count < 100)
                return (0, 0);

            int horizHits = 0, vertHits = 0;

            foreach (var (cursor, target) in frames)
            {
                if (Math.Abs(cursor.X - target.X) <= tolerance) horizHits++;
                if (Math.Abs(cursor.Y - target.Y) <= tolerance) vertHits++;
            }

            double total = frames.Count;
            return (horizHits / total * 100.0, vertHits / total * 100.0);
        }

        // ------------------------------------------------------------------ //
        //  5. Peek Timing (early / late click %)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Percentage of peek-shots that were fired early (before the target was
        /// fully exposed) or late (after the optimal window).
        /// <paramref name="peekTimingOffsets"/> values are signed milliseconds relative
        /// to the optimal fire window: negative = early, positive = late.
        /// Returns (earlyPct%, latePct%).
        /// </summary>
        public static (double EarlyPct, double LatePct) CalculatePeekTiming(
            List<double> peekTimingOffsets)
        {
            if (peekTimingOffsets == null || peekTimingOffsets.Count == 0)
                return (0, 0);

            const double EarlyThresholdMs = -50.0;
            const double LateThresholdMs  =  50.0;

            int earlyCount = 0, lateCount = 0;

            foreach (double offset in peekTimingOffsets)
            {
                if (offset < EarlyThresholdMs) earlyCount++;
                else if (offset > LateThresholdMs) lateCount++;
            }

            double total = peekTimingOffsets.Count;
            return (earlyCount / total * 100.0, lateCount / total * 100.0);
        }
    }
}
