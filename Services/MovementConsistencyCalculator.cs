using System;
using System.Collections.Generic;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// T0.1: canonical session-consistency formula, extracted so it can be unit
    /// tested (the live computation is inline in MainWindow's raw-input handler,
    /// which is not testable). The handler mirrors this exact step math
    /// incrementally for live display; this is the single source of truth for the
    /// formula. Gate 3 (T3.1) folds the handler onto this shared service.
    /// </summary>
    public static class MovementConsistencyCalculator
    {
        /// <summary>
        /// Bump when the formula changes so stored readings are never compared across
        /// versions — the same mistake ClickMetricVersion exists to prevent.
        ///
        /// 0 = legacy running-mean (see <see cref="SessionConsistencyV0"/>)
        /// 1 = fixed session mean (current)
        /// </summary>
        public const int FormulaVersion = 1;

        /// <summary>
        /// Movement-distance consistency over per-event mouse-travel distances: how
        /// closely each event's travel sits to the session's own average travel.
        /// For each event, deviation from the SESSION mean → instant consistency
        /// clamp(100 − deviation*10) → averaged over the session.
        ///
        /// AUDIT 2026-08-19 — what changed and why:
        /// The previous version divided by a RUNNING mean that included the current
        /// event. Two consequences, both inflating the score:
        ///   • the first event was compared against itself, so its deviation was always
        ///     0 and it always scored a perfect 100;
        ///   • an expanding mean chases whatever it has seen so far, so deviations came
        ///     out systematically smaller than against a fixed centre.
        /// A fixed session mean removes both. Expect readings to be slightly LOWER than
        /// before — most on short sessions, where the always-perfect first event carried
        /// the most weight. That is the artefact going away, not performance dropping.
        ///
        /// Worked example, distances [5, 15, 10] (session mean = 10):
        ///   e=5:  dev=5, inst=50
        ///   e=15: dev=5, inst=50
        ///   e=10: dev=0, inst=100   → (50+50+100)/3 = 66.67
        /// Under the old running mean the same input scored 83.33.
        /// Constant-velocity buffer [10,10,…] → every dev=0 → 100, unchanged.
        ///
        /// ── KNOWN LIMITATION, DELIBERATELY NOT FIXED HERE ───────────────────────────
        /// Distances are RAW MOUSE COUNTS, so identical hand movement scores differently
        /// at different DPI/sensitivity. This is a valid PERSONAL, within-setup metric;
        /// it is not comparable across users, and it shifts if the player changes their
        /// sensitivity. Normalising (counts-per-degree, or per target distance) is a
        /// real fix but changes the scale again and would need the authored thresholds
        /// recalibrated against captured data. Until then, do not present this as a
        /// cross-player comparison, and label it movement-DISTANCE consistency.
        /// </summary>
        public static double SessionConsistency(IReadOnlyList<double> eventDistances)
        {
            if (eventDistances == null || eventDistances.Count == 0) return 0;

            double total = 0;
            for (int i = 0; i < eventDistances.Count; i++) total += eventDistances[i];
            double mean = total / eventDistances.Count;

            double consistencySum = 0;
            for (int i = 0; i < eventDistances.Count; i++)
            {
                double dev = Math.Abs(eventDistances[i] - mean);
                consistencySum += Math.Clamp(100 - dev * 10, 0, 100);
            }
            return consistencySum / eventDistances.Count;
        }

        /// <summary>
        /// The legacy running-mean formula, kept ONLY so tests can demonstrate the
        /// difference and so the version gate has something concrete to describe.
        /// Never call this for a live reading.
        /// </summary>
        public static double SessionConsistencyV0(IReadOnlyList<double> eventDistances)
        {
            if (eventDistances == null || eventDistances.Count == 0) return 0;

            double distanceTotal  = 0;
            double consistencySum = 0;
            for (int i = 0; i < eventDistances.Count; i++)
            {
                distanceTotal += eventDistances[i];
                double mean = distanceTotal / (i + 1);
                double dev  = Math.Abs(eventDistances[i] - mean);
                consistencySum += Math.Clamp(100 - dev * 10, 0, 100);
            }
            return consistencySum / eventDistances.Count;
        }
    }
}
