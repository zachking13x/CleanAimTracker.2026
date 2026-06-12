using CleanAimTracker.Models;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-2.4: the single authority for the drill→in-game transfer narrative.
    /// One observation, FactKey="transfer", computed in one place from CoachMemory
    /// cross-coach data. All other transfer phrasings are deleted — three
    /// contradictory positions shipped across two days because every engine had
    /// its own copy.
    ///
    /// Signal: CoachMemory.ConsistencyTrend — in-game movement consistency, last 3
    /// tracker sessions vs the prior 3 (low-activity excluded by CoachMemoryBuilder),
    /// read alongside active drilling (3+ recent drills). It compares like units
    /// (consistency points vs consistency points) — unlike the deleted variant that
    /// divided a 0–100 smoothness score by a drill accuracy percentage.
    /// </summary>
    public static class TransferObservationSource
    {
        public const string FactKey = "transfer";

        /// <summary>Trend threshold (consistency points) before transfer is claimed either way.</summary>
        public const double TrendThreshold = 2.0;

        public static CoachObservation? Compute(CoachMemory? memory)
        {
            if (memory == null) return null;
            if (memory.RecentDrills == null || memory.RecentDrills.Count < 3) return null;
            if (memory.RecentTrackerSessions == null || memory.RecentTrackerSessions.Count < 2) return null;
            // ConsistencyTrend == 0 when CoachMemoryBuilder lacked 6 tracker sessions — no claim.
            if (memory.ConsistencyTrend == 0) return null;

            if (memory.ConsistencyTrend > TrendThreshold)
            {
                return new CoachObservation
                {
                    FactKey       = FactKey,
                    SourceEngine  = nameof(TransferObservationSource),
                    Section       = CoachSection.Strength,
                    Polarity      = ObservationPolarity.Strength,
                    Severity      = 2,
                    Message       = $"Your in-game movement consistency is up {memory.ConsistencyTrend:F0} points across your recent sessions while you've been drilling — the training is transferring.",
                    RequiredMetrics = { "MovementConsistency" }
                };
            }

            if (memory.ConsistencyTrend < -TrendThreshold)
            {
                return new CoachObservation
                {
                    FactKey       = FactKey,
                    SourceEngine  = nameof(TransferObservationSource),
                    Section       = CoachSection.Tip,
                    Polarity      = ObservationPolarity.Neutral,
                    Severity      = 1,
                    RequiresBehaviorChange = true,
                    Message       = "Your drill results aren't showing up in-game yet. Transfer lags practice — drilling for a few minutes right before you play speeds it up.",
                    RequiredMetrics = { "MovementConsistency" }
                };
            }

            return null;
        }
    }
}
