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
        // A1 — MovementOvershoot tuning constants.
        public const int    OvershootMinSegmentSamples   = 5;    // same meaningful-segment floor as F6
        public const double OvershootMinTravelCounts     = 10.0; // |clickPos| below this = no real acquisition flick
        public const double OvershootNoiseFloor          = 0.05; // segment ratio above this counts as "an overshoot"
        public const int    OvershootMinQualifyingSegments = 5;  // fewer scorable segments → metric invalid

        // ------------------------------------------------------------------ //
        //  0. Movement Overshoot (in-flight, axial excursion)  — A1
        // ------------------------------------------------------------------ //

        /// <summary>
        /// A1 — Mean per-acquisition in-flight overshoot: within each click segment
        /// (reusing F6's raw-buffer click segmentation), how far PAST the click
        /// position, ALONG THE TRAVEL DIRECTION, the cursor path reached before
        /// returning — normalized by the flick distance.
        ///   clean flick     → excursion ≈ 0  (path stops at/short of the endpoint)
        ///   fly-past-return → excursion &gt; 0  (path overshot, then came back to click)
        ///
        /// Projecting onto the start→click axis is essential: it isolates ALONG-TRAVEL
        /// overshoot and ignores orthogonal settle-wobble — the exact contamination that
        /// made PathEfficiency non-discriminating on real captures (0.42 vs 0.41).
        ///
        /// Worked example, one segment of x-deltas +10×6 then −10×2:
        ///   cumulative proj 10,20,30,40,50,60,50,40 ; clickPos = net 40, |clickPos| = 40
        ///   maxProj = 60 ; excursion = max(0, 60−40) = 20 ; ratio = 20/40 = 0.50
        /// A straight +10×4 → cumulative 10,20,30,40 ; maxProj = 40 = projClick → excursion 0.
        /// </summary>
        /// <returns>(MeanOvershoot ratio, OvershootSegmentPct, QualifyingSegments).
        /// MeanOvershoot/Pct are -1 when fewer than the minimum qualifying segments
        /// exist — the caller marks the metric invalid, never a false 0.</returns>
        public static (double MeanOvershoot, double SegmentPct, int QualifyingSegments)
            CalculateMovementOvershoot(List<RawInputSample> samples, IReadOnlyList<long> clickTimestamps)
        {
            if (samples == null || samples.Count < 20
                || clickTimestamps == null || clickTimestamps.Count < 2)
                return (-1, -1, 0);

            double ratioSum = 0;
            int qualifying = 0;
            int overshootSegments = 0;

            for (int c = 1; c < clickTimestamps.Count; c++)
            {
                long lo = clickTimestamps[c - 1];
                long hi = clickTimestamps[c];
                if (hi <= lo) continue;

                // Pass 1 — net displacement (clickPos) and sample count for this segment.
                double clickX = 0, clickY = 0;
                int segSamples = 0;
                foreach (var s in samples)
                {
                    if (s.Timestamp <= lo || s.Timestamp > hi) continue; // (lo, hi]
                    clickX += s.Dx; clickY += s.Dy; segSamples++;
                }
                if (segSamples < OvershootMinSegmentSamples) continue;

                double clickDist = Math.Sqrt(clickX * clickX + clickY * clickY);
                if (clickDist < OvershootMinTravelCounts) continue; // no meaningful travel axis

                double axisX = clickX / clickDist, axisY = clickY / clickDist;
                double projClick = clickDist; // dot(clickPos, axis) == |clickPos|

                // Pass 2 — walk the cumulative path, track max projection onto the axis.
                double posX = 0, posY = 0, maxProj = double.NegativeInfinity;
                foreach (var s in samples)
                {
                    if (s.Timestamp <= lo || s.Timestamp > hi) continue;
                    posX += s.Dx; posY += s.Dy;
                    double proj = posX * axisX + posY * axisY;
                    if (proj > maxProj) maxProj = proj;
                }

                double excursion = Math.Max(0, maxProj - projClick);
                double ratio = excursion / clickDist;
                ratioSum += ratio;
                qualifying++;
                if (ratio > OvershootNoiseFloor) overshootSegments++;
            }

            if (qualifying < OvershootMinQualifyingSegments)
                return (-1, -1, qualifying);

            return (ratioSum / qualifying, 100.0 * overshootSegments / qualifying, qualifying);
        }

        // ------------------------------------------------------------------ //
        //  1. Path Efficiency (0.0 – 1.0)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// F6 — Mean per-acquisition path efficiency: straight-line displacement ÷
        /// actual path travelled, computed SEPARATELY for each target-acquisition
        /// segment (the raw samples between two consecutive clicks), then averaged.
        /// 1.0 = each move went directly to its target; lower = overshoot / orbiting.
        ///
        /// Why segmentation matters (the bug this replaces): the old version summed
        /// every delta across the WHOLE drill into one net-displacement vector. In a
        /// multi-target scenario the moves crisscross the screen and cancel — net
        /// displacement ≈ 0 while total path is huge — so the ratio pinned near 0.05
        /// regardless of how cleanly the player moved. Real captures: clean 4.87%,
        /// overshoot 5.18% (no discrimination, backwards). Segmenting per acquisition
        /// makes each segment a single A→B move, where overshoot-and-correct shows up
        /// as a long path against a short straight line.
        ///
        /// Worked example, one segment of deltas:
        ///   straight A→B (10,0),(10,0),(10,0)           → net (30,0), path 30 → 1.00
        ///   overshoot (10,0),(10,0),(10,0),(-10,0)      → net (20,0), path 40 → 0.50
        /// </summary>
        /// <param name="samples">Raw mouse deltas for the whole drill (with timestamps).</param>
        /// <param name="clickTimestamps">Stopwatch-tick timestamps of each hit, ascending.
        /// Consecutive timestamps bound one acquisition segment.</param>
        public static double CalculatePathEfficiency(
            List<RawInputSample> samples,
            IReadOnlyList<long> clickTimestamps)
        {
            // Need at least two click boundaries to form one bounded segment.
            if (samples == null || samples.Count < 20
                || clickTimestamps == null || clickTimestamps.Count < 2)
                return 0.0;   // not computable → caller's validity gate treats as "not measured"

            double effSum = 0;
            int    segments = 0;

            for (int c = 1; c < clickTimestamps.Count; c++)
            {
                long lo = clickTimestamps[c - 1];
                long hi = clickTimestamps[c];
                if (hi <= lo) continue;   // out-of-order / duplicate click stamps

                double segDx = 0, segDy = 0, segPath = 0;
                int segSamples = 0;
                foreach (var s in samples)
                {
                    if (s.Timestamp <= lo || s.Timestamp > hi) continue; // (lo, hi]
                    segDx += s.Dx;
                    segDy += s.Dy;
                    segPath += Math.Sqrt((double)s.Dx * s.Dx + (double)s.Dy * s.Dy);
                    segSamples++;
                }

                // A meaningful acquisition has several movement events; tiny segments
                // (a target clicked almost on top of the last) are skipped, not scored.
                if (segSamples < 5 || segPath < 0.01) continue;

                double straight = Math.Sqrt(segDx * segDx + segDy * segDy);
                effSum += Math.Clamp(straight / segPath, 0.0, 1.0);
                segments++;
            }

            if (segments == 0) return 0.0;   // nothing scorable → "not measured"
            return Math.Clamp(effSum / segments, 0.0, 1.0);
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

        // V2 — directional click-point classification tuning.
        // AxialNoiseFloorPx: |along-axis offset| at or below this is NEUTRAL — placement
        //   wobble, not a directional habit. Calibrated against the centered auto-clicker
        //   captures (2026-07-06): its detection noise is ±2–4 px with random sign.
        // MinApproachDistPx: an approach shorter than this (target essentially on top of
        //   the previous click) has no meaningful travel axis — the sample is skipped.
        // MinScoredSamples: fewer scored samples than this → percentages invalid (-1).
        public const double AxialNoiseFloorPx  = 4.0;
        public const double MinApproachDistPx  = 20.0;
        public const int    MinScoredSamples   = 3;

        /// <summary>
        /// V2 — Derives per-session click-offset metrics from a list of hit samples.
        ///
        /// OVERSHOOT/UNDERSHOOT ARE DIRECTIONAL: for each hit the approach axis is
        /// the vector from the PREVIOUS click point to the current target centre.
        /// The click's offset from centre is projected onto that axis —
        ///   positive beyond the noise floor = landed PAST the target (overshoot),
        ///   negative beyond the noise floor = landed SHORT of it   (undershoot),
        ///   inside the floor, or orthogonal wobble = neutral (neither).
        ///
        /// The V1 implementation this replaces bucketed by RAW DISTANCE from centre
        /// (&gt;8 px = "overshoot", 3–8 px = "undershoot", any direction). On a 36 px
        /// Medium target the &gt;8 px ring is 80% of the legal hit area, so V1 read
        /// 57–77% "overshoot" for every player — including a bot clicking exact
        /// target centres (76%). It measured target geometry, not direction.
        ///
        /// Worked examples (axis → +X, floor 4 px):
        ///   prev click (0,0), centre (100,0), click (107,3): axial +7 &gt; 4  → overshoot
        ///   prev click (0,0), centre (100,0), click (94,-2): axial −6 &lt; −4 → undershoot
        ///   prev click (0,0), centre (100,0), click (101,9): axial +1        → neutral
        ///     (9 px off-centre — V1 called this an "overshoot"; it is sideways wobble)
        ///
        /// <list type="bullet">
        ///   <item><c>AvgOffset</c> — mean Euclidean click-to-centre distance (unchanged, all samples)</item>
        ///   <item><c>OvershootPct</c> — % of scored hits past the centre along the approach; -1 if &lt;3 scorable</item>
        ///   <item><c>UndershootPct</c> — % of scored hits short of the centre; -1 if &lt;3 scorable</item>
        /// </list>
        /// Samples must be in chronological order (they are appended per hit).
        /// </summary>
        public static (double AvgOffset, double OvershootPct, double UndershootPct)
            CalculateClickOffsets(List<ClickOffsetSample> samples)
        {
            if (samples == null || samples.Count == 0) return (0, -1, -1);

            double totalOffset = 0;
            foreach (var s in samples)
            {
                double dx = s.ClickPoint.X - s.TargetCenter.X;
                double dy = s.ClickPoint.Y - s.TargetCenter.Y;
                totalOffset += Math.Sqrt(dx * dx + dy * dy);
            }
            double avgOffset = totalOffset / samples.Count;

            int overshoot = 0, undershoot = 0, scored = 0;
            for (int i = 1; i < samples.Count; i++)
            {
                // Approach axis: previous click → this target's centre.
                double axisX = samples[i].TargetCenter.X - samples[i - 1].ClickPoint.X;
                double axisY = samples[i].TargetCenter.Y - samples[i - 1].ClickPoint.Y;
                double axisLen = Math.Sqrt(axisX * axisX + axisY * axisY);
                if (axisLen < MinApproachDistPx) continue;   // no meaningful travel axis

                double offX = samples[i].ClickPoint.X - samples[i].TargetCenter.X;
                double offY = samples[i].ClickPoint.Y - samples[i].TargetCenter.Y;
                double axial = (offX * axisX + offY * axisY) / axisLen;

                scored++;
                if (axial >  AxialNoiseFloorPx) overshoot++;
                else if (axial < -AxialNoiseFloorPx) undershoot++;
                // else neutral — counted in the denominator, no directional habit
            }

            if (scored < MinScoredSamples)
                return (avgOffset, -1, -1);

            return (
                avgOffset,
                overshoot  * 100.0 / scored,
                undershoot * 100.0 / scored);
        }

        // ------------------------------------------------------------------ //
        //  3. Direction-Change Lag (ms)
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Average milliseconds between a target direction change and the moment the
        /// player's movement STARTS — measured as cumulative raw displacement crossing a
        /// threshold, NOT as the cursor's direction matching the target's.
        ///
        /// AUDIT 2026-08-19: this doc-comment used to say "the moment the player's cursor
        /// matches that direction change", which the implementation has never done. It
        /// integrates the ABSOLUTE magnitude of movement, so motion in the wrong direction
        /// satisfies the threshold just as well as motion in the right one. That wording
        /// is how coaching text drifted into "you're chasing the trail" and "your eyes
        /// haven't caught the turn" — claims the maths cannot support.
        ///
        /// Read it as RESPONSE-ONSET timing: how quickly the hand starts, not how well it
        /// aims. The user-facing label is "Your movement-onset delay" for that reason. A
        /// true direction-match version would need target-velocity direction, cursor
        /// direction over a short window, and a sustained positive dot product — a
        /// worthwhile upgrade, but a different metric, and it should get a different key.
        ///
        /// The stored key stays AvgDirectionChangeLagMs: it is persisted in
        /// PrescriptionState.VerifyMetric, so renaming it strands active prescriptions.
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

        // Reaction-onset detection tuning (raw mouse counts / milliseconds).
        // OnsetDistanceCounts: cumulative displacement from the stimulus that marks
        //   "the hand has clearly begun moving toward the new target." Integrating
        //   distance (not a per-sample spike) catches smooth aim, not just hard flicks.
        // ReactionWindowMs: onset must occur inside this window or the event is
        //   DISCARDED — never latched onto a later cycle's motion.
        // MinReactionMs: floor below which apparent onset is carry-over/settle from the
        //   previous click, not a visual reaction to the new target → discard the event.
        private const double OnsetDistanceCounts = 20.0;
        private const double ReactionWindowMs    = 600.0;
        private const double MinReactionMs       = 90.0;

        /// <summary>
        /// Overload for discrete-stimulus scenarios (Reactive spawn, Switching target
        /// switch) where each stimulus forces a fresh movement toward a new location.
        ///
        /// For each stimulus timestamp the method integrates raw mouse displacement
        /// forward in time and marks REACTION ONSET at the first sample where the
        /// cumulative distance since the stimulus crosses <see cref="OnsetDistanceCounts"/>.
        /// The lag is (onset − stimulus). An event is COUNTED only when that onset lands
        /// inside the human reaction window [<see cref="MinReactionMs"/>,
        /// <see cref="ReactionWindowMs"/>]; otherwise the event is discarded (no onset
        /// in-window, or onset too fast to be a genuine reaction = settle carry-over).
        /// This structurally bounds every counted lag to ≤600 ms, so the metric can no
        /// longer latch a spawn/switch interval the way per-sample flick detection did.
        /// Requires ≥3 clean onsets; returns 0 (→ invalid) otherwise.
        ///
        /// Worked example (Stopwatch.Frequency = 10 MHz ⇒ ticksPerMs = 10 000):
        ///   stimulus tick T = 1 000 000. Raw samples after T:
        ///     T+500 000  ( 50 ms): (dx,dy)=(2,1)  mag 2.24  cum 2.24   — below 20, continue
        ///     T+1 000 000(100 ms): (1,0)          mag 1.00  cum 3.24   — below 20, continue
        ///     T+1 800 000(180 ms): (10,8)         mag 12.8  cum 16.04  — below 20, continue
        ///     T+2 000 000(200 ms): (9,6)          mag 10.8  cum 26.84  — crosses 20 ⇒ onset
        ///   lag = (T+2 000 000 − T)/10 000 = 200 ms; 200 ∈ [90,600] ⇒ counted as 200 ms.
        /// </summary>
        /// <param name="targetChangeTimes">Stopwatch timestamps of stimulus events.</param>
        /// <param name="rawBuffer">Raw hardware mouse-delta samples from the session.</param>
        public static double CalculateDirectionChangeLag(
            IReadOnlyList<long> targetChangeTimes,
            List<RawInputSample> rawBuffer)
        {
            if (targetChangeTimes == null || targetChangeTimes.Count < 3) return 0;
            if (rawBuffer         == null || rawBuffer.Count         < 5) return 0;

            double ticksPerMs = (double)Stopwatch.Frequency / 1000.0;
            var lags = new List<double>();

            foreach (long changeTs in targetChangeTimes)
            {
                double cumulative = 0;
                foreach (var s in rawBuffer)
                {
                    if (s.Timestamp <= changeTs) continue;

                    double lagMs = (s.Timestamp - changeTs) / ticksPerMs;
                    if (lagMs > ReactionWindowMs) break;  // no onset in-window → discard event

                    cumulative += Math.Sqrt((double)s.Dx * s.Dx + (double)s.Dy * s.Dy);
                    if (cumulative < OnsetDistanceCounts) continue;

                    // Cumulative motion has crossed the onset threshold. Count it only if
                    // it lands after the reaction floor (else it's settle carry-over).
                    if (lagMs >= MinReactionMs)
                        lags.Add(lagMs);
                    break;   // first onset per stimulus, counted or discarded
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
        /// <summary>
        /// Per-axis time-on-target for a tracking drill. Returns -1 on an axis that
        /// cannot be honestly scored.
        ///
        /// CAT_AXIS_MOTION_GATE (2026-08-12): this used to return (0, 0) on failure and
        /// score both axes unconditionally. On HeadTrack — where bots strafe sideways
        /// and barely move vertically — the vertical axis scored 5/100 purely because
        /// there was no vertical motion to track, and the coach turned that into
        /// "your vertical control is underbuilt, you're aiming from the wrist."
        /// That is a diagnosis manufactured from an absent stimulus.
        ///
        /// An axis is only scored when the target actually MOVED along it. -1 means
        /// "not measurable", which consumers must render as "—" and never as a zero.
        /// </summary>
        public const double AxisMinTravelPx = 40.0;

        /// <summary>
        /// Scenarios whose targets genuinely travel on BOTH axes, so a horizontal-vs-
        /// vertical comparison is meaningful.
        ///
        /// This must be checked by CONSUMERS too, not just at capture time. Sessions
        /// recorded before the capture-side gate existed still carry axis numbers that
        /// were never valid — Zach's own history has HeadTrack rows reading H53/V5 — and
        /// the coach reads from storage. Gating only the write would have left every
        /// historical session still producing "your vertical control is underbuilt".
        /// </summary>
        public static bool HasTwoAxisTracking(string scenario) =>
            scenario is "AirTracking" or "Tracking" or "Evasive";

        public static (double HorizAcc, double VertAcc) CalculateAxisSplit(
            List<(Point CursorPos, Point TargetPos)> frames,
            double tolerance)
        {
            if (frames == null || frames.Count < 100)
                return (-1, -1);

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            int horizHits = 0, vertHits = 0;

            foreach (var (cursor, target) in frames)
            {
                if (Math.Abs(cursor.X - target.X) <= tolerance) horizHits++;
                if (Math.Abs(cursor.Y - target.Y) <= tolerance) vertHits++;

                if (target.X < minX) minX = target.X;
                if (target.X > maxX) maxX = target.X;
                if (target.Y < minY) minY = target.Y;
                if (target.Y > maxY) maxY = target.Y;
            }

            double total = frames.Count;
            bool horizMoved = (maxX - minX) >= AxisMinTravelPx;
            bool vertMoved  = (maxY - minY) >= AxisMinTravelPx;

            return (horizMoved ? horizHits / total * 100.0 : -1,
                    vertMoved  ? vertHits  / total * 100.0 : -1);
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
