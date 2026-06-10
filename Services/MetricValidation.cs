using CleanAimTracker.Models;
using System;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-1.2: pure evaluation functions producing MetricValidity for each
    /// movement-derived session metric. Pure (no UI, no storage) so they are
    /// unit-testable.
    /// </summary>
    public static class MetricValidation
    {
        // ── Thresholds — justified from TASK-1.1 acceptance session ─────────
        // The 2026-06-10 17:33 acceptance session logged 21,402 raw-input events
        // over 62s of deliberate continuous movement ≈ 345 events/sec.
        // 500 samples ≈ 1.5s of sustained movement. Below that, angle-derived
        // statistics (smoothness) are dominated by a handful of direction
        // changes and deviation-derived statistics (consistency) by startup
        // transients — neither is a meaningful session statistic.
        public const int MinMovementSamples = 500;

        /// <summary>Smoothness: session mean of per-event angle-change scores.</summary>
        public static MetricValidity ForSmoothness(int sampleCount, double sum)
        {
            if (sampleCount <= 0)
                return MetricValidity.Invalid(MetricInvalidReason.NotComputed, 0);
            if (double.IsNaN(sum) || double.IsInfinity(sum))
                return MetricValidity.Invalid(MetricInvalidReason.DegenerateInput, sampleCount);
            if (sampleCount < MinMovementSamples)
                return MetricValidity.Invalid(MetricInvalidReason.InsufficientSamples, sampleCount);
            return MetricValidity.Valid(sampleCount);
        }

        /// <summary>Consistency / correction sharpness: per-event deviation statistics.</summary>
        public static MetricValidity ForMovementMetric(int sampleCount, double value)
        {
            if (sampleCount <= 0)
                return MetricValidity.Invalid(MetricInvalidReason.NotComputed, 0);
            if (double.IsNaN(value) || double.IsInfinity(value))
                return MetricValidity.Invalid(MetricInvalidReason.DegenerateInput, sampleCount);
            if (sampleCount < MinMovementSamples)
                return MetricValidity.Invalid(MetricInvalidReason.InsufficientSamples, sampleCount);
            return MetricValidity.Valid(sampleCount);
        }

        /// <summary>
        /// Overall quality is a weighted blend of smoothness, consistency, and
        /// correction sharpness — it is only as valid as its inputs.
        /// </summary>
        public static MetricValidity ForOverallQuality(
            MetricValidity smoothness, MetricValidity consistency, MetricValidity correction)
        {
            int samples = Math.Min(smoothness.SampleCount,
                          Math.Min(consistency.SampleCount, correction.SampleCount));

            if (!smoothness.IsValid || !consistency.IsValid || !correction.IsValid)
            {
                // Propagate the most informative reason: degenerate > insufficient > not computed
                MetricInvalidReason reason =
                      smoothness.InvalidReason  == MetricInvalidReason.DegenerateInput
                   || consistency.InvalidReason == MetricInvalidReason.DegenerateInput
                   || correction.InvalidReason  == MetricInvalidReason.DegenerateInput
                        ? MetricInvalidReason.DegenerateInput
                   : smoothness.InvalidReason  == MetricInvalidReason.InsufficientSamples
                   || consistency.InvalidReason == MetricInvalidReason.InsufficientSamples
                   || correction.InvalidReason  == MetricInvalidReason.InsufficientSamples
                        ? MetricInvalidReason.InsufficientSamples
                        : MetricInvalidReason.NotComputed;
                return MetricValidity.Invalid(reason, samples);
            }
            return MetricValidity.Valid(samples);
        }
    }
}
