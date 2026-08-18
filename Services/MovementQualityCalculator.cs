using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>T5: movement-quality metrics computed from a raw-input buffer.</summary>
    public readonly struct MovementQuality
    {
        public readonly double Smoothness;    // 0..100, mean per-event angular steadiness
        public readonly double Consistency;   // 0..100, mean per-event travel-distance steadiness
        public readonly int    SampleCount;
        // GATE 2: Velocity (speed) consistency during ACTIVE movement.
        public readonly double VelocityStability;   // 0..100, -1 = invalid (too few active samples)
        public readonly double VelocityCv;          // raw coefficient of variation (for T2.2 calibration)
        public readonly int    ActiveVelocitySamples;
        public MovementQuality(double smoothness, double consistency, int sampleCount,
                               double velocityStability, double velocityCv, int activeVelocitySamples)
        {
            Smoothness            = smoothness;
            Consistency           = consistency;
            SampleCount           = sampleCount;
            VelocityStability     = velocityStability;
            VelocityCv            = velocityCv;
            ActiveVelocitySamples = activeVelocitySamples;
        }
    }

    /// <summary>
    /// T5: the single, shared implementation of the movement-quality math
    /// (smoothness + consistency). Previously it lived only inline in
    /// MainWindow's raw-input handler and ran only on tracker sessions; the drill
    /// path was blind to movement quality. Both paths now call this one
    /// implementation — no divergent copies.
    ///
    /// SMOOTHNESS mirrors the corrected inline math (TASK-1.1): per consecutive
    /// pair, the angular change between travel directions, wrap-normalized to
    /// ≤180°, scored clamp(100 − angleDiff*2), averaged. The first event has no
    /// predecessor, so diffs accumulate from the second event onward (the inline
    /// version's prevAngle=0 start counted one spurious first sample — corrected
    /// here; over hundreds of events the difference is immaterial).
    ///
    /// CONSISTENCY reuses MovementConsistencyCalculator (the T0.1 canonical
    /// running-mean-deviation formula) — not a second copy.
    /// </summary>
    public static class MovementQualityCalculator
    {
        // GATE 2 — Velocity Stability tuning.
        // A consecutive-event gap longer than this is a PAUSE, not continuous
        // movement — its "velocity" (distance / long gap) is an idle artifact and
        // is excluded. This is what the old AvgVel/PeakVel metric failed to do:
        // idle time diluted the average and a single sub-ms spike latched the peak.
        public const double ActiveGapCeilingSec = 0.10;
        // Below this many active-velocity samples the metric is invalid, not floored.
        public const int    MinActiveVelocitySamples = 30;
        // T2.2 — CALIBRATED from a real StaticClicking capture set (2026-06-15):
        //   clean/natural sessions cluster CV 0.63–0.77; deliberately-jerky CV 1.28;
        //   extreme-erratic (white-knuckle) CV 2.0–10.2.
        // CvCeiling 2.0 → clean #13 (CV 0.71) reads 64, jerky #11 (CV 1.28) reads 36
        //   (clear separation, both off floor/ceiling); CV ≥ 2.0 pegs at 0 (genuinely
        //   unstable). CV at/above ceiling → 0; CV 0 (perfectly even speed) → 100.
        public const double VelocityCvCeiling = 2.0;

        public static MovementQuality FromBuffer(IReadOnlyList<RawInputSample> samples)
        {
            if (samples == null || samples.Count < 2)
                return new MovementQuality(0, 0, samples?.Count ?? 0, -1, -1, 0);

            // ── Consistency: per-event travel distances → shared calculator ──
            var distances = new List<double>(samples.Count);
            foreach (var s in samples)
                distances.Add(Math.Sqrt((double)s.Dx * s.Dx + (double)s.Dy * s.Dy));
            double consistency = MovementConsistencyCalculator.SessionConsistency(distances);

            // ── Smoothness: per-pair wrap-normalized angular change ──────────
            double smoothnessSum = 0;
            int smoothnessSamples = 0;
            double prevAngle = AngleDeg(samples[0]);
            for (int i = 1; i < samples.Count; i++)
            {
                double angle = AngleDeg(samples[i]);
                double angleDiff = Math.Abs(angle - prevAngle);
                if (angleDiff > 180) angleDiff = 360 - angleDiff;   // wrap-around (TASK-1.1)
                smoothnessSum += Math.Clamp(100 - angleDiff * 2, 0, 100);
                smoothnessSamples++;
                prevAngle = angle;
            }
            double smoothness = smoothnessSamples > 0 ? smoothnessSum / smoothnessSamples : 0;

            // ── Velocity stability: CV of per-event speed during active movement ─
            // velocity_i = distance_i / dt_i (counts/sec). Samples following a pause
            // (dt > ceiling) are excluded — that idle-gap exclusion is the fix for
            // the old metric. CV = stddev/mean is unit-free; map low CV → high score.
            double freq = System.Diagnostics.Stopwatch.Frequency;
            double gapCeilingTicks = ActiveGapCeilingSec * freq;
            var activeVel = new List<double>();
            for (int i = 1; i < samples.Count; i++)
            {
                long dt = samples[i].Timestamp - samples[i - 1].Timestamp;
                if (dt <= 0 || dt > gapCeilingTicks) continue;   // non-positive or idle gap
                double dist = Math.Sqrt((double)samples[i].Dx * samples[i].Dx + (double)samples[i].Dy * samples[i].Dy);
                activeVel.Add(dist * freq / dt);                 // counts/sec
            }

            double velStability = -1, cv = -1;
            if (activeVel.Count >= MinActiveVelocitySamples)
            {
                double mean = activeVel.Average();
                if (mean > 0.0001)
                {
                    double variance = activeVel.Sum(v => (v - mean) * (v - mean)) / activeVel.Count;
                    cv = Math.Sqrt(variance) / mean;
                    velStability = Math.Clamp(100.0 * (1.0 - cv / VelocityCvCeiling), 0, 100);
                }
            }

            return new MovementQuality(smoothness, consistency, samples.Count,
                                       velStability, cv, activeVel.Count);
        }

        private static double AngleDeg(RawInputSample s) =>
            Math.Atan2(s.Dy, s.Dx) * (180.0 / Math.PI);
    }
}
