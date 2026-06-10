using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    public static class TrackerCoachService
    {
        public record TrackerCoachReport(
            string Headline,
            List<string> Observations,
            List<string> Suggestions,
            string NextDrillSuggestion
        );

        public static TrackerCoachReport Analyze(
            SessionSummary session,
            List<SessionSummary> history,
            CoachMemory? memory = null)
        {
            // observations = Strength (max 1) → displayed under "WHAT I NOTICED"
            // suggestions  = [Area to Improve, Suggestion 1-2] → "WHAT TO WORK ON"
            var observations = new List<string>();
            var suggestions  = new List<string>();

            int sessionIndex = history?.Count ?? 0;

            bool isShortSession = session.SessionSeconds < 120;
            string dataQualifier = isShortSession
                ? "Short session — observations are directional only. "
                : "";

            // ── TASK-06: Tracker tip rotation using "t_" prefixed keys ────────
            var recentTrackerKeys = memory?.RecentTipKeys?.Take(9).ToList() ?? new List<string>();
            bool TrackerKeyRecent(string key) => recentTrackerKeys.Contains("t_" + key);
            var selectedTrackerKeys = new List<string>();
            void AddTrackerKey(string key)
            {
                string prefixed = "t_" + key;
                if (!selectedTrackerKeys.Contains(prefixed))
                    selectedTrackerKeys.Add(prefixed);
            }

            // ── Baseline calculations ─────────────────────────────────────────
            var prior = (history ?? new List<SessionSummary>())
                .Where(h => h.Timestamp < session.Timestamp)
                .OrderByDescending(h => h.Timestamp)
                .ToList();

            double avgSmooth  = prior.Count >= 2 ? prior.Take(5).Average(h => h.SmoothnessScore) : 0;
            double avgQuality = prior.Count >= 2 ? prior.Take(5).Average(h => h.OverallQualityScore) : 0;
            bool   hasHistory = prior.Count >= 3;

            // ═════════════════════════════════════════════════════════════════
            // SECTION 2 — ONE STRENGTH (highest-priority positive data point)
            // Do not manufacture a positive if none fires. Section stays empty.
            // ═════════════════════════════════════════════════════════════════
            string? strengthObs = null;

            // Priority 1: Smoothness trend improving over last 4 sessions
            // DISABLED pending TASK-1.1 — do not re-enable without validity gate
            // Smoothness metric is unvalidated (pegged at 0 across sessions while siblings read nonzero).
            // if (strengthObs == null && prior.Count >= 4 && !TrackerKeyRecent("smoothness_improving"))
            // {
            //     double oldest4 = (prior[2].SmoothnessScore + prior[3].SmoothnessScore) / 2.0;
            //     double newest4 = (prior[0].SmoothnessScore + prior[1].SmoothnessScore) / 2.0;
            //     if (newest4 - oldest4 > 8)
            //     {
            //         int v = sessionIndex % 4;
            //         strengthObs = v switch
            //         {
            //             0 => "Your in-game movement has been getting smoother over your last four sessions. That's the kind of improvement that shows up in your accuracy in ranked.",
            //             1 => $"Smoothness trending up — {oldest4:F0} to {newest4:F0} over your last four sessions. The fundamentals are compounding.",
            //             2 => "Consistent improvement in movement quality over four sessions. That's the trajectory you want.",
            //             _ => $"Four sessions of improving smoothness: {oldest4:F0} → {newest4:F0}. The habits are building. Don't change what's working."
            //         };
            //         AddTrackerKey("smoothness_improving");
            //     }
            // }

            // Priority 2: Consistent quality above baseline for 3+ sessions
            if (strengthObs == null && prior.Count >= 3 && avgQuality >= 60
                && !TrackerKeyRecent("consistency_improving"))
            {
                bool allAbove = prior.Take(3).All(h => h.OverallQualityScore >= avgQuality * 0.9);
                if (allAbove && session.OverallQualityScore >= avgQuality * 0.9)
                {
                    int v = sessionIndex % 3;
                    strengthObs = v switch
                    {
                        0 => $"Your quality score has been consistently above {avgQuality:F0} for your last 3+ sessions. That consistency is harder to build than a single peak score.",
                        1 => $"Multiple sessions of quality around {avgQuality:F0}. Consistency at this level means the habits are becoming automatic.",
                        _ => $"Your session quality has been steady at {avgQuality:F0}+ for several sessions. That's a floor you've built — now push the ceiling."
                    };
                    AddTrackerKey("consistency_improving");
                }
            }

            // Priority 3: Cross-coach transfer confirmed
            if (strengthObs == null && memory != null && memory.RecentDrills.Count >= 3
                && prior.Count >= 2 && memory.ConsistencyTrend > 2
                && !TrackerKeyRecent("drill_transfer"))
            {
                int v = sessionIndex % 3;
                strengthObs = v switch
                {
                    0 => "Your in-game movement matches your drill performance closely. The training is transferring well.",
                    1 => "Good alignment between your drill performance and in-game mechanics. That's the outcome you're training for.",
                    _ => "Your in-game quality is running parallel to your drill baseline. The practice is landing."
                };
                AddTrackerKey("drill_transfer");
            }

            // Priority 4: Correction sharpness low (clean first motions)
            if (strengthObs == null && session.CorrectionSharpness < 30 && session.CorrectionSharpness > 0
                && !TrackerKeyRecent("correction_low"))
            {
                strengthObs = $"Very low correction sharpness ({session.CorrectionSharpness:F0}) — you were committing to first motions cleanly. That is good mechanics.";
                AddTrackerKey("correction_low");
            }

            if (strengthObs != null)
                observations.Add(strengthObs);

            // ═════════════════════════════════════════════════════════════════
            // SECTION 3 — ONE AREA TO IMPROVE (highest-priority problem)
            // Goes into suggestions[0] so it leads in "WHAT TO WORK ON."
            // Must end with one specific next action.
            // ═════════════════════════════════════════════════════════════════
            string? areaObs = null;
            string? areaKey = null;

            // Priority 1: Smoothness significantly below baseline (> 15 pts, requires history)
            // DISABLED pending TASK-1.1 — do not re-enable without validity gate
            // Smoothness metric is unvalidated; the grip-tension causal narrative is unverified.
            // if (hasHistory && avgSmooth > 0 && session.SmoothnessScore < avgSmooth - 15
            //     && !TrackerKeyRecent("smoothness_drop"))
            // {
            //     double drop = avgSmooth - session.SmoothnessScore;
            //     areaObs = $"Your smoothness dropped to {session.SmoothnessScore:F0}/100 this session — {drop:F0} points below your {avgSmooth:F0} average. " +
            //               "That size of drop in one session has two common causes: grip tension or a surface/sensitivity change. " +
            //               "If nothing physical changed, it's tension. Try consciously relaxing your grip at the start of the next session.";
            //     areaKey = "smoothness_drop";
            // }

            // Priority 2: Consistency decline (3+ sessions dropping)
            // DISABLED pending TASK-1.1 — do not re-enable without validity gate
            // Trend is computed entirely from SmoothnessScore, which is unvalidated.
            // if (areaObs == null && prior.Count >= 4 && !TrackerKeyRecent("consistency_decline"))
            // {
            //     double oldest4c = (prior[2].SmoothnessScore + prior[3].SmoothnessScore) / 2.0;
            //     double newest4c = (prior[0].SmoothnessScore + prior[1].SmoothnessScore) / 2.0;
            //     int decliningCount = 0;
            //     for (int i = 0; i < Math.Min(prior.Count - 1, 3); i++)
            //     {
            //         if (prior[i].SmoothnessScore < prior[i + 1].SmoothnessScore) decliningCount++;
            //         else break;
            //     }
            //     if (newest4c - oldest4c < -8 && decliningCount >= 2)
            //     {
            //         int v = sessionIndex % 4;
            //         areaObs = v switch
            //         {
            //             0 => "Your in-game consistency has dipped across your last few sessions. Before your next session, run a quick Tracking drill to reset your baseline.",
            //             1 => $"Smoothness has dropped from {oldest4c:F0} to {newest4c:F0} over four sessions. That pattern points to accumulated tension or overtraining. One rest day, then drill before you play.",
            //             2 => "Movement quality has been dropping session over session. Take a reset day — one easy drill, no pressure, just feel.",
            //             _ => $"Declining smoothness trend: {oldest4c:F0} → {newest4c:F0}. Before diagnosing mechanics, check session length and fatigue level. Shorter sessions often fix this faster than more drilling."
            //         };
            //         areaKey = "consistency_decline";
            //     }
            // }

            // Priority 3: Large flick ratio > 60%
            if (areaObs == null && session.FlickCount > 5 && !TrackerKeyRecent("large_flick"))
            {
                double largeRatio = session.LargeFlickCount / (double)session.FlickCount;
                if (largeRatio > 0.6)
                {
                    int v = sessionIndex % 4;
                    areaObs = v switch
                    {
                        0 => $"More than 60% of your flicks were large movements — that's a crosshair placement problem as much as an aim problem. " +
                             "Before your next game, consciously hold your crosshair at head-height where enemies typically appear.",
                        1 => $"Your ratio of large to small flicks is high ({session.LargeFlickCount} large vs {session.SmallFlickCount} small). " +
                             "Large flicks mean getting caught out of position. Focus on one common angle per map and hold head-height there.",
                        2 => "High large-flick count means you're reacting to targets rather than anticipating them. " +
                             "Practice holding crosshair at common head-height positions — it converts large flicks to small corrections.",
                        _ => $"{session.LargeFlickCount} large flicks this session. Pre-aim one common angle per map and you'll start converting those into small corrections."
                    };
                    areaKey = "large_flick";
                }
            }

            // Priority 4: In-game vs drill gap (drills improving, in-game not transferring)
            // DISABLED pending TASK-1.1 — do not re-enable without validity gate
            // Compares SmoothnessScore (unvalidated, 0-100 movement metric) against drill
            // accuracy percentage — cross-unit comparison on top of a dead metric.
            // if (areaObs == null && memory != null && memory.RecentDrills.Count >= 3
            //     && prior.Count >= 2 && memory.MostPlayedScenario.Length > 0
            //     && memory.BaselineAccuracy.TryGetValue(memory.MostPlayedScenario, out double drillBaseline4)
            //     && !TrackerKeyRecent("drill_gap"))
            // {
            //     double smoothRatio = drillBaseline4 > 0 ? session.SmoothnessScore / drillBaseline4 : 1.0;
            //     if (smoothRatio < 0.7)
            //     {
            //         int v = sessionIndex % 4;
            //         areaObs = v switch
            //         {
            //             0 => "Your in-game movement is rougher than your drill performance suggests it should be. " +
            //                  "Drill immediately before you play — the transfer is strongest when they're close together.",
            //             1 => $"Your drills average {drillBaseline4:F0}% accuracy but in-game smoothness is lower than expected. " +
            //                  "Even 5 minutes of Tracking before a session accelerates the transfer.",
            //             2 => "Drill performance is stronger than in-game movement right now. " +
            //                  "Drill before you play this week — close the gap.",
            //             _ => "Strong drill numbers but in-game is lagging. Drill before you play to narrow the gap."
            //         };
            //         areaKey = "drill_gap";
            //     }
            // }

            // Priority 5: Sensitivity flag
            // DISABLED per TASK-0.3 — session coach must not opine on sensitivity.
            // Contradicts RecommendationEngine (sole sensitivity authority per TASK-3.1).
            // if (areaObs == null && session.CmPer360 > 0)
            // {
            //     if (session.CmPer360 < 20 && session.SmoothnessScore < 60
            //         && !TrackerKeyRecent("sensitivity_low"))
            //     {
            //         areaObs = "Your sensitivity is on the low end and your in-game movement is rough. " +
            //                   "Those two things together usually mean your sensitivity is actually too low for how you naturally move. " +
            //                   "Try bumping it up slightly and retest — if the roughness decreases, that's the answer.";
            //         areaKey = "sensitivity_low";
            //     }
            //     else if (session.CmPer360 > 55 && session.CorrectionSharpness > 70
            //              && !TrackerKeyRecent("sensitivity_high"))
            //     {
            //         areaObs = "Your sensitivity is high and you're making a lot of corrections. " +
            //                   "High sensitivity amplifies small hand movements — lower your sensitivity slightly and compare.";
            //         areaKey = "sensitivity_high";
            //     }
            // }

            if (areaObs != null)
            {
                suggestions.Add(areaObs);   // Area to Improve leads suggestions list
                AddTrackerKey(areaKey!);
            }

            // ═════════════════════════════════════════════════════════════════
            // SECTION 4 — TWO SUGGESTIONS MAX
            // Suggestion 1: paired with Area to Improve
            // Suggestion 2: forward-looking drill prescription (omit if none fire)
            // ═════════════════════════════════════════════════════════════════

            // Suggestion 1: mechanically paired with the identified area
            if (suggestions.Count < 2)
            {
                bool addedPaired = false;

                // DISABLED pending TASK-1.1 — do not re-enable without validity gate
                // Grip-tension narrative built on the unvalidated smoothness metric.
                // if ((areaKey == "smoothness_drop" || areaKey == "consistency_decline"
                //      || (session.CorrectionSharpness > 65 && session.SmoothnessScore < 60))
                //     && !TrackerKeyRecent("suggestion_tension"))
                // {
                //     int v = sessionIndex % 4;
                //     suggestions.Add(v switch
                //     {
                //         0 => "Before your next session try 2 minutes of slow Tracking Easy — it primes fluid movement before you need precision.",
                //         1 => $"Correction sharpness at {session.CorrectionSharpness:F0} with smoothness at {session.SmoothnessScore:F0} — try consciously loosening your grip mid-session and see if smoothness rises.",
                //         2 => "Take 10 deep breaths before your next game and keep your grip lighter than feels natural.",
                //         _ => "Run one Tracking Easy drill before your next session: it breaks the tension pattern in under 2 minutes."
                //     });
                //     AddTrackerKey("suggestion_tension");
                //     addedPaired = true;
                // }
                if (areaKey == "large_flick" && !TrackerKeyRecent("suggestion_flick"))
                {
                    int v = sessionIndex % 4;
                    suggestions.Add(v switch
                    {
                        0 => "Running Flicking in your next drill session builds faster target initiation and reduces the need for large reactive movements.",
                        1 => "Practice holding crosshair at common head-height angles — it converts large flicks to small corrections.",
                        2 => "Flicking drills — Medium difficulty — specifically trains the reactive initiation that reduces large flick frequency.",
                        _ => "Pre-aim one common angle per map you play. That single habit converts large reactive flicks into small corrections."
                    });
                    AddTrackerKey("suggestion_flick");
                    addedPaired = true;
                }
                else if (areaKey == "drill_gap" && !TrackerKeyRecent("suggestion_transfer"))
                {
                    int v = sessionIndex % 3;
                    suggestions.Add(v switch
                    {
                        0 => "Run a 5-minute Tracking session immediately before your next game. The transfer is strongest when drill and play are close together.",
                        1 => "Your drills are building the habit — drill right before you play this week. Even one 3-minute Tracking drill is enough to prime the mechanics.",
                        _ => "Drill before you play this week. The closer in time to your game, the faster the mechanics transfer."
                    });
                    AddTrackerKey("suggestion_transfer");
                    addedPaired = true;
                }

                // If area fired but no specific pairing matched, add a targeted smoothness fix
                // DISABLED pending TASK-1.1 — do not re-enable without validity gate
                // "Low smoothness has a physical cause" asserts an unverified causal claim on an unvalidated metric.
                // if (!addedPaired && areaKey != null && session.SmoothnessScore < 55
                //     && !TrackerKeyRecent("suggestion_smoothness"))
                // {
                //     int v = sessionIndex % 4;
                //     suggestions.Add(v switch
                //     {
                //         0 => "Low smoothness has a physical cause: tight grip, arm tension, or cold hands. Address those before diagnosing mechanics.",
                //         1 => $"Smoothness at {session.SmoothnessScore:F0}/100. Before your next session, run one 60-second Tracking drill to reset choppy movement.",
                //         2 => "Set a physical reminder before your next game to relax your grip — it's the fastest fix for low smoothness.",
                //         _ => "Smoothness is telling you something. Check grip tension and surface friction — those fix choppy movement faster than drilling more."
                //     });
                //     AddTrackerKey("suggestion_smoothness");
                // }
                _ = addedPaired; // retained for the disabled blocks above
            }

            // Suggestion 2 (forward-looking drill prescription — omit if none fire)
            if (suggestions.Count < 2)
            {
                if (session.CorrectionSharpness > 65 && !TrackerKeyRecent("suggestion_flick_drill"))
                {
                    suggestions.Add("Flicking Single Target — Medium — focus on clean first motion, no corrections");
                    AddTrackerKey("suggestion_flick_drill");
                }
                else if (session.LargeFlickCount > session.SmallFlickCount && session.FlickCount > 5
                         && !TrackerKeyRecent("suggestion_flick_drill"))
                {
                    suggestions.Add("Flicking Timed Pressure — Medium — builds faster target initiation");
                    AddTrackerKey("suggestion_flick_drill");
                }
                // DISABLED pending TASK-1.1 — do not re-enable without validity gate
                // Tracking Smooth Arc prescription justified solely by the unvalidated smoothness metric.
                // else if (session.SmoothnessScore < 55 && areaKey != "smoothness_drop"
                //          && areaKey != "consistency_decline" && !TrackerKeyRecent("suggestion_track_drill"))
                // {
                //     suggestions.Add("Tracking Smooth Arc — Easy — slow deliberate movement to reset muscle memory");
                //     AddTrackerKey("suggestion_track_drill");
                // }
                // No forward-looking drill fires → no Suggestion 2. Empty is better than generic filler.
            }

            // ── TASK-06: persist tracker tip keys back into memory ─────────────
            if (memory != null && selectedTrackerKeys.Count > 0)
            {
                memory.RecentTipKeys.InsertRange(0, selectedTrackerKeys);
                while (memory.RecentTipKeys.Count > 20)
                    memory.RecentTipKeys.RemoveAt(memory.RecentTipKeys.Count - 1);
            }

            // ── Next drill suggestion ──────────────────────────────────────────
            string nextDrill;
            if (session.CorrectionSharpness > 65)
                nextDrill = "Flicking Single Target — Medium — focus on clean first motion, no corrections";
            // DISABLED pending TASK-1.1 — do not re-enable without validity gate
            // Smoothness-justified remedial prescription (fired on Elite sessions via dead metric).
            // else if (session.SmoothnessScore < 55)
            //     nextDrill = "Tracking Smooth Arc — Easy — slow deliberate movement to reset muscle memory";
            else if (session.LargeFlickCount > session.SmallFlickCount)
                nextDrill = "Flicking Timed Pressure — Medium — builds faster target initiation";
            else
                nextDrill = "Tracking Standard — Medium — maintain the consistency you showed this session";

            // ── TASK-10: Headline — quality tier relative to recent baseline ────
            static string QualityTier(double score) => score switch
            {
                >= 80 => "Elite session",
                >= 70 => "Strong session",
                >= 60 => "Solid session",
                >= 50 => "Below average session",
                _     => "Rough session"
            };

            double qualityScore = session.OverallQualityScore;
            string tier = QualityTier(qualityScore);
            string headline;

            if (avgQuality > 0 && prior.Count >= 3)
            {
                double delta = qualityScore - avgQuality;
                string comparison = delta >= 0
                    ? $"{delta:F0} points above your recent average"
                    : $"{Math.Abs(delta):F0} points below your recent average";
                headline = $"{tier} — quality {qualityScore:F0}/100 ({comparison})";
            }
            else
            {
                headline = $"{tier} — quality {qualityScore:F0}/100";
            }

            return new TrackerCoachReport(
                Headline:            dataQualifier + headline,
                Observations:        observations,
                Suggestions:         suggestions,
                NextDrillSuggestion: nextDrill
            );
        }
    }
}
