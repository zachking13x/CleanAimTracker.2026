using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Builds the CoachMemory context object from storage before every coaching call.
    /// All math is guarded against NaN, null, and empty-collection exceptions.
    /// Never throws — returns safe defaults on any error.
    /// </summary>
    public static class CoachMemoryBuilder
    {
        // Known scenarios — used to populate SessionsPerScenario with zeros for untried scenarios
        private static readonly string[] KnownScenarios =
            { "Tracking", "Flicking", "Precision", "Switching", "Adaptive",
              "Sniper", "Shotgun", "SmgAr" };

        /// <param name="current">The just-completed drill. Null when building for the tracker coach.</param>
        /// <param name="settings">Loaded UserSettings — read for prescription follow-up state.</param>
        public static CoachMemory Build(AimTrainerResult? current, UserSettings settings)
        {
            var memory = new CoachMemory();

            try
            {
                // ── Load raw data ─────────────────────────────────────────────
                var allDrills  = AimTrainerStorage.LoadAll(); // newest last from storage
                var allTracker = SessionStorage.LoadAll() ?? new List<SessionSummary>();

                // Order newest-first throughout
                allDrills  = allDrills.OrderByDescending(d => d.Timestamp).ToList();
                allTracker = allTracker.OrderByDescending(s => s.Timestamp).ToList();

                // Prior sessions = all drills except the current one (for delta calculations)
                var priorDrills = current != null
                    ? allDrills.Where(d => d.Timestamp != current.Timestamp).ToList()
                    : allDrills.ToList();

                // ── Core history ──────────────────────────────────────────────
                memory.AllDrills       = allDrills;
                memory.TotalDrillCount = allDrills.Count;
                memory.RecentDrills    = allDrills.Take(10).ToList();

                // ── Personal best (from PRIOR sessions only for PB detection) ─
                if (priorDrills.Count > 0)
                {
                    var pb = priorDrills.OrderByDescending(r => r.Accuracy).First();
                    memory.PersonalBestAccuracy = pb.Accuracy;
                    memory.PersonalBestScenario = pb.Scenario;
                }

                // ── Per-scenario baselines ────────────────────────────────────
                // Seed all known scenarios with 0 so RULE-NEW-4 can detect untried ones
                foreach (var s in KnownScenarios)
                    memory.SessionsPerScenario[s] = 0;

                var scenarioGroups = priorDrills.GroupBy(d => d.Scenario).ToList();
                foreach (var group in scenarioGroups)
                {
                    string scenario = group.Key;
                    var drills = group.ToList();

                    memory.SessionsPerScenario[scenario] = drills.Count;

                    if (drills.Count >= 2)
                    {
                        // TASK-19: guard all Average() calls with Count > 0
                        memory.BaselineAccuracy[scenario] = drills.Average(d => d.Accuracy);

                        var validReaction = drills
                            .Where(d => d.AvgReactionMs > 0 && d.AvgReactionMs < 2000)
                            .ToList();
                        if (validReaction.Count >= 2)
                            memory.BaselineReactionMs[scenario] = validReaction.Average(d => d.AvgReactionMs);

                        // Consistency = 100 - (accuracy std dev * 3), clamped 0-100
                        double avg = drills.Average(d => d.Accuracy);
                        double variance = drills.Count > 1
                            ? drills.Average(d => Math.Pow(d.Accuracy - avg, 2))
                            : 0;
                        double stdDev = Math.Sqrt(variance);
                        memory.BaselineConsistency[scenario] = Math.Max(0, Math.Min(100, 100.0 - stdDev * 3.0));
                    }
                }

                // ── Identity ──────────────────────────────────────────────────
                if (memory.SessionsPerScenario.Count > 0)
                    memory.MostPlayedScenario = memory.SessionsPerScenario
                        .OrderByDescending(kv => kv.Value).First().Key;

                if (memory.BaselineAccuracy.Count > 0)
                {
                    memory.WeakestScenario   = memory.BaselineAccuracy.OrderBy(kv => kv.Value).First().Key;
                    memory.StrongestScenario = memory.BaselineAccuracy.OrderByDescending(kv => kv.Value).First().Key;
                }

                // ── Accuracy trend (TASK-19 verified) ────────────────────────
                // recent5 = newest 5 sessions (includes current if in allDrills)
                // prior5  = sessions 6-10
                // Guard: AllDrills.Count >= 6
                if (allDrills.Count >= 6)
                {
                    var recent5 = allDrills.Take(5).Where(d => d.Accuracy > 0).ToList();
                    var prior5  = allDrills.Skip(5).Take(5).Where(d => d.Accuracy > 0).ToList();

                    if (recent5.Count > 0 && prior5.Count > 0)
                    {
                        double r5Avg = recent5.Average(d => d.Accuracy);
                        double p5Avg = prior5.Average(d => d.Accuracy);
                        // Guard NaN
                        if (!double.IsNaN(r5Avg) && !double.IsNaN(p5Avg) &&
                            !double.IsInfinity(r5Avg) && !double.IsInfinity(p5Avg))
                            memory.AccuracyTrend = r5Avg - p5Avg;
                    }
                }

                // ── Reaction trend (TASK-19 verified) ────────────────────────
                // Guard: only include sessions where AvgReactionMs > 0 AND < 2000
                if (allDrills.Count >= 6)
                {
                    var recent5R = allDrills.Take(5)
                        .Where(d => d.AvgReactionMs > 0 && d.AvgReactionMs < 2000).ToList();
                    var prior5R  = allDrills.Skip(5).Take(5)
                        .Where(d => d.AvgReactionMs > 0 && d.AvgReactionMs < 2000).ToList();

                    if (recent5R.Count > 0 && prior5R.Count > 0)
                    {
                        double r5 = recent5R.Average(d => d.AvgReactionMs);
                        double p5 = prior5R.Average(d => d.AvgReactionMs);
                        if (!double.IsNaN(r5) && !double.IsNaN(p5))
                            memory.ReactionTrend = r5 - p5; // negative = improving (lower is better)
                    }
                }

                // ── Plateau detection (TASK-19 verified) ─────────────────────
                // Scenario-specific, based on PRIOR sessions (excluding current)
                // plateau = (max - min) <= 3.0 AND count == 5 exactly
                if (current != null)
                {
                    var sameScenarioPrior = priorDrills
                        .Where(d => d.Scenario == current.Scenario)
                        .OrderByDescending(d => d.Timestamp)
                        .Take(5)
                        .ToList();

                    if (sameScenarioPrior.Count == 5)
                    {
                        double maxAcc = sameScenarioPrior.Max(d => d.Accuracy);
                        double minAcc = sameScenarioPrior.Min(d => d.Accuracy);
                        memory.IsAccuracyPlateaued = (maxAcc - minAcc) <= 3.0;
                        memory.PlateauAvgAccuracy  = sameScenarioPrior.Average(d => d.Accuracy);
                    }

                    // PlateauLength = consecutive sessions from newest back where
                    // accuracy is within 3% of the session before it
                    if (sameScenarioPrior.Count >= 2)
                    {
                        int len = 1;
                        for (int i = 0; i < sameScenarioPrior.Count - 1; i++)
                        {
                            if (Math.Abs(sameScenarioPrior[i].Accuracy - sameScenarioPrior[i + 1].Accuracy) <= 3.0)
                                len++;
                            else
                                break;
                        }
                        memory.PlateauLength = len;
                    }

                    // Reaction plateau — same pattern
                    var sameReaction = priorDrills
                        .Where(d => d.Scenario == current.Scenario
                                 && d.AvgReactionMs > 0 && d.AvgReactionMs < 2000)
                        .OrderByDescending(d => d.Timestamp)
                        .Take(5)
                        .ToList();

                    if (sameReaction.Count == 5)
                    {
                        double maxMs = sameReaction.Max(d => d.AvgReactionMs);
                        double minMs = sameReaction.Min(d => d.AvgReactionMs);
                        memory.IsReactionPlateaued = (maxMs - minMs) <= 15.0;
                    }
                }

                // ── Prescription follow-up ────────────────────────────────────
                memory.LastPrescribedScenario   = settings.LastPrescribedScenario  ?? "";
                memory.LastPrescribedDifficulty = settings.LastPrescribedDifficulty ?? "";

                if (!string.IsNullOrEmpty(memory.LastPrescribedScenario) && priorDrills.Count >= 1)
                {
                    var last3 = priorDrills.Take(3).ToList(); // already newest-first
                    memory.LastPrescriptionFollowed =
                        last3.Any(d => d.Scenario == memory.LastPrescribedScenario);
                }

                // ── Free session state ────────────────────────────────────────
                memory.HasUsedFreeFullSession     = settings.HasUsedFreeFullSession;
                memory.SessionsSinceLastFullCoach = settings.HasUsedFreeFullSession
                    ? Math.Max(0, priorDrills.Count - settings.LastPrescribedSessionIndex)
                    : 0;

                // ── Tracker history ───────────────────────────────────────────
                memory.RecentTrackerSessions = allTracker.Take(5).ToList();

                var validSmooth = allTracker.Where(s => s.SmoothnessScore > 0).ToList();
                if (validSmooth.Count > 0)
                    memory.TrackerSmoothnessBaseline = validSmooth.Average(s => s.SmoothnessScore);

                var validConsistency = allTracker.Where(s => s.MovementConsistency > 0).ToList();
                if (validConsistency.Count > 0)
                    memory.TrackerConsistencyBaseline = validConsistency.Average(s => s.MovementConsistency);

                // Consistency trend: last 3 tracker sessions vs prior 3
                if (allTracker.Count >= 6)
                {
                    var recentT = allTracker.Take(3).Where(s => s.MovementConsistency > 0).ToList();
                    var priorT  = allTracker.Skip(3).Take(3).Where(s => s.MovementConsistency > 0).ToList();
                    if (recentT.Count > 0 && priorT.Count > 0)
                    {
                        double rt = recentT.Average(s => s.MovementConsistency);
                        double pt = priorT.Average(s => s.MovementConsistency);
                        if (!double.IsNaN(rt) && !double.IsNaN(pt))
                            memory.ConsistencyTrend = rt - pt;
                    }
                }
            }
            catch
            {
                // Never throw — return whatever was computed before the error
            }

            return memory;
        }
    }
}
