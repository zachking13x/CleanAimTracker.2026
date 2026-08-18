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
        /// Session-mean movement consistency over per-event mouse-travel distances.
        /// For each event: deviation from the CURRENT running mean of all event
        /// distances so far (not a stale once-per-second average) → instant
        /// consistency clamp(100 − deviation*10) → averaged over the session.
        ///
        /// Worked example, distances [5, 15, 10]:
        ///   n=1: mean=5,  dev=0,  inst=100
        ///   n=2: mean=10, dev=5,  inst=50
        ///   n=3: mean=10, dev=0,  inst=100   → (100+50+100)/3 = 83.33
        /// Constant-velocity buffer [10,10,…] → every dev=0 → 100.
        /// </summary>
        public static double SessionConsistency(IReadOnlyList<double> eventDistances)
        {
            if (eventDistances == null || eventDistances.Count == 0) return 0;

            double distanceTotal  = 0;
            double consistencySum = 0;
            for (int i = 0; i < eventDistances.Count; i++)
            {
                distanceTotal += eventDistances[i];
                double mean = distanceTotal / (i + 1);                 // running mean, current
                double dev  = Math.Abs(eventDistances[i] - mean);
                consistencySum += Math.Clamp(100 - dev * 10, 0, 100);
            }
            return consistencySum / eventDistances.Count;
        }
    }
}
