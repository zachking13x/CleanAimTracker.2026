using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
#nullable enable

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Local AI coaching engine — no API key, no internet, no cost.
    /// Generates personalized coaching reports using the player's
    /// actual session data, benchmarks, and trend analysis.
    /// </summary>
    public static class AiCoachService
    {
        // ── Benchmarks ────────────────────────────────────────────────
        private static class Bench
        {
            // Accuracy — scenario-specific thresholds
            // Tracking is harder so thresholds are lower but not dramatically so
            // Precision is the purest accuracy test so thresholds are highest
            public static double AccuracyElite(string s) => s switch
            {
                "Tracking"  => 80,
                "Precision" => 88,
                "Flicking"  => 82,
                "Switching" => 80,
                "Adaptive"  => 80,
                "Sniper"    => 90,
                "Shotgun"   => 80,
                "SmgAr"     => 82,
                _           => 82,
            };

            public static double AccuracyGood(string s) => s switch
            {
                "Tracking"  => 65,
                "Precision" => 72,
                "Flicking"  => 68,
                "Switching" => 65,
                "Adaptive"  => 65,
                "Sniper"    => 75,
                "Shotgun"   => 65,
                "SmgAr"     => 68,
                _           => 68,
            };

            public static double AccuracyAvg(string s) => s switch
            {
                "Tracking"  => 50,
                "Precision" => 55,
                "Flicking"  => 52,
                "Switching" => 50,
                "Adaptive"  => 50,
                "Sniper"    => 55,
                "Shotgun"   => 45,
                "SmgAr"     => 50,
                _           => 52,
            };

            // Reaction — scenario-specific thresholds.
            // Sniper: reaction is NOT the primary metric — benchmarks set to 0 so reaction always
            // grades "slow", and weapon-scenario coaching explicitly suppresses reaction tips.
            public static double ReactionElite(string s) => s switch
            {
                "Flicking"  => 200,
                "Switching" => 220,
                "Tracking"  => 280,
                "Precision" => 350,
                "Adaptive"  => 250,
                "Shotgun"   => 180,
                "SmgAr"     => 220,
                "Sniper"    => 0,    // reaction coaching suppressed for Sniper
                _           => 220,
            };

            public static double ReactionGood(string s) => s switch
            {
                "Flicking"  => 300,
                "Switching" => 350,
                "Tracking"  => 420,
                "Precision" => 500,
                "Adaptive"  => 380,
                "Shotgun"   => 280,
                "SmgAr"     => 320,
                "Sniper"    => 0,    // reaction coaching suppressed for Sniper
                _           => 350,
            };

            public static double ReactionAverage(string s) => s switch
            {
                "Flicking"  => 450,
                "Switching" => 500,
                "Tracking"  => 580,
                "Precision" => 650,
                "Adaptive"  => 520,
                "Shotgun"   => 350,
                "SmgAr"     => 420,
                "Sniper"    => 0,    // reaction coaching suppressed for Sniper
                _           => 500,
            };

            public const int StreakElite   = 12;
            public const int StreakGood    = 6;
            public const int StreakAverage = 3;
        }

        // ── Public entry point ────────────────────────────────────────
        /// <summary>
        /// Primary overload — called from AimTrainerResultWindow with full CoachMemory.
        /// </summary>
        public static AiCoachReport Analyze(AimTrainerResult result, CoachMemory memory)
        {
            if (result.Hits == 0)
                return new AiCoachReport
                {
                    OverallRating       = "No Data",
                    Headline            = "No targets hit this session — run a drill to get coaching.",
                    Strengths           = new List<string> { "Click targets to generate coaching data." },
                    Weaknesses          = new List<string>(),
                    Advice              = new List<string> { "Use the Start button to begin a drill, then click the targets that appear on screen." },
                    NextDrillSuggestion = "Try Precision on Easy — it is the best starting point for new players.",
                    MotivationalClose   = "Every expert was a beginner once. Start clicking.",
                };

            var context = BuildContext(result, memory);
            var report  = GenerateReport(result, context, memory);
            var recentTracker = memory.RecentTrackerSessions.Count > 0
                ? memory.RecentTrackerSessions[0]
                : null;
            var prescriptions = DrillPrescriptionEngine.Prescribe(result, context, memory, recentTracker);
            report.Prescription = prescriptions.Count > 0 ? prescriptions[0] : null;
            return report;
        }

        // ── Context builder ───────────────────────────────────────────
        internal record CoachContext(
            string AccuracyGrade,
            string ReactionGrade,
            string StreakGrade,
            double AccuracyDelta,
            double ScoreDelta,
            double ReactionDelta,
            bool   IsFirstSession,
            bool   IsImproving,
            bool   IsConsistent,
            int    SessionCount,
            double PersonalBestAccuracy,
            double PersonalBestReaction,
            bool   NewAccuracyRecord,
            bool   NewReactionRecord,
            string WeakArea,
            string StrongArea,
            double OverallAvgAccuracy,
            double OverallAvgReaction,
            int    TotalSessionsAll,
            // New tracker fields — nullable because tracker session may not exist
            double? TrackerCmPer360,
            double? TrackerSmoothness,
            double? TrackerCorrectionSharpness
        );

        private static CoachContext BuildContext(AimTrainerResult r, CoachMemory memory)
        {
            // Derive same-scenario history from memory (excludes current session by Timestamp)
            var same = memory.AllDrills
                .Where(h => h.Scenario == r.Scenario && h.Timestamp != r.Timestamp)
                .OrderByDescending(h => h.Timestamp)
                .ToList();

            // Extract tracker data from memory — only use if session was long enough
            SessionSummary? tracker = memory.RecentTrackerSessions.Count > 0
                ? memory.RecentTrackerSessions[0]
                : null;

            double? trackerCmPer360            = null;
            double? trackerSmoothness          = null;
            double? trackerCorrectionSharpness = null;

            if (tracker != null && tracker.SessionSeconds >= 45)
            {
                trackerCmPer360            = tracker.CmPer360 > 0 ? tracker.CmPer360 : (double?)null;
                trackerSmoothness          = tracker.SmoothnessScore;
                trackerCorrectionSharpness = tracker.CorrectionSharpness;
            }

            bool isFirst = same.Count == 0;

            string accGrade = r.Accuracy >= Bench.AccuracyElite(r.Scenario) ? "elite"
                            : r.Accuracy >= Bench.AccuracyGood(r.Scenario)  ? "good"
                            : r.Accuracy >= Bench.AccuracyAvg(r.Scenario)   ? "average"
                            : "developing";

            string reactGrade = r.AvgReactionMs <= Bench.ReactionElite(r.Scenario)   ? "elite"
                              : r.AvgReactionMs <= Bench.ReactionGood(r.Scenario)    ? "good"
                              : r.AvgReactionMs <= Bench.ReactionAverage(r.Scenario) ? "average"
                              : "slow";

            string streakGrade = r.MaxStreak >= Bench.StreakElite   ? "elite"
                               : r.MaxStreak >= Bench.StreakGood    ? "good"
                               : r.MaxStreak >= Bench.StreakAverage ? "average"
                               : "low";

            double accDelta   = isFirst ? 0 : r.Accuracy      - same[0].Accuracy;
            double scoreDelta = isFirst ? 0 : r.Score         - same[0].Score;
            double reactDelta = isFirst ? 0 : r.AvgReactionMs - same[0].AvgReactionMs;

            double pbAccuracy = isFirst ? r.Accuracy      : Math.Max(r.Accuracy,      same.Max(h => h.Accuracy));
            double pbReaction = isFirst ? r.AvgReactionMs : Math.Min(r.AvgReactionMs, same.Min(h => h.AvgReactionMs));
            bool   newAccPB   = !isFirst && r.Accuracy      > same.Max(h => h.Accuracy);
            bool   newReactPB = !isFirst && r.AvgReactionMs < same.Min(h => h.AvgReactionMs);

            bool improving  = !isFirst && same.Count >= 2
                && r.Score > same.Take(3).Average(h => h.Score);
            bool consistent = !isFirst && same.Count >= 3
                && same.Take(3).Select(h => h.Accuracy).Max()
                 - same.Take(3).Select(h => h.Accuracy).Min() < 12;

            var areas = new Dictionary<string, double>
            {
                ["accuracy"]  = r.Accuracy,
                ["reaction"]  = r.AvgReactionMs > 0 ? 100 - Math.Min(100, r.AvgReactionMs / 6.0) : 50,
                ["streak"]    = Math.Min(100, r.MaxStreak * 7.0),
                ["endurance"] = r.Misses == 0 ? 100 : 100.0 * r.Hits / (r.Hits + r.Misses)
            };

            string weak   = areas.OrderBy(a => a.Value).First().Key;
            string strong = areas.OrderByDescending(a => a.Value).First().Key;

            // Cross-scenario context from ALL history (last 10 sessions, excludes current)
            var allRecent = memory.AllDrills
                .Where(h => h.Timestamp != r.Timestamp)
                .Take(10)
                .ToList();

            double overallAvgAccuracy = allRecent.Count > 0 ? allRecent.Average(h => h.Accuracy) : r.Accuracy;
            double overallAvgReaction = allRecent.Count > 0 ? allRecent.Average(h => h.AvgReactionMs) : r.AvgReactionMs;
            int    totalSessionsAll   = memory.AllDrills.Count;

            return new CoachContext(
                accGrade, reactGrade, streakGrade,
                accDelta, scoreDelta, reactDelta,
                isFirst, improving, consistent,
                same.Count + 1,
                pbAccuracy, pbReaction,
                newAccPB, newReactPB,
                weak, strong,
                overallAvgAccuracy, overallAvgReaction, totalSessionsAll,
                trackerCmPer360, trackerSmoothness, trackerCorrectionSharpness
            );
        }

        // ── Variant picker ────────────────────────────────────────────
        /// <summary>
        /// Rotates through variants by per-scenario session count so the same
        /// coaching line never repeats in consecutive same-scenario sessions.
        /// Using c.SessionCount (same-scenario history count + 1) rather than
        /// c.TotalSessionsAll means the rotation is tied to how many times the
        /// user has run THIS scenario, not their global session count — so
        /// playing a new scenario doesn't shift the rotation for existing ones.
        /// </summary>
        private static string Pick(int sessionIndex, params string[] variants)
            => variants[sessionIndex % variants.Length];

        // ── Global variant picker (rotates by total drill count + rule index) ───
        /// <summary>
        /// Used for new rules (TASK-08) — rotates by global session count + rule index
        /// so the same tip doesn't repeat across consecutive sessions of different scenarios.
        /// </summary>
        private static string PickGlobal(CoachMemory memory, int ruleIndex, params string[] variants)
            => variants[(memory.TotalDrillCount + ruleIndex) % variants.Length];

        // ── Report generator ──────────────────────────────────────────
        private static AiCoachReport GenerateReport(AimTrainerResult r, CoachContext c, CoachMemory memory) => new()
        {
            OverallRating       = GetRating(r, c),
            Headline            = GetHeadline(r, c, memory),
            Strengths           = GetStrengths(r, c, memory),
            Weaknesses          = GetWeaknesses(r, c, memory),
            Advice              = GetAdvice(r, c, memory),
            NextDrillSuggestion = GetNextDrill(r, c),
            MotivationalClose   = GetMotivation(r, c),
        };

        private static string GetRating(AimTrainerResult r, CoachContext c)
        {
            if (c.AccuracyGrade == "elite" && c.ReactionGrade is "elite" or "good") return "Excellent";
            if (c.AccuracyGrade is "elite" or "good") return "Great";
            if (c.AccuracyGrade == "average") return "Good";
            if (c.IsImproving) return "Developing";
            return "Needs Work";
        }

        private static string GetHeadline(AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            // ── WEAPON SCENARIO HEADLINE OVERRIDES ────────────────────────────
            // Fire before all generic rules so weapon-type context is always correct.
            if (r.Scenario == "Sniper")
            {
                if (r.Accuracy >= 90)
                    return $"Elite precision — {r.Accuracy:F0}% on small distant targets. That's the top tier.";
                if (r.Accuracy >= 75)
                    return $"Solid sniper session at {r.Accuracy:F0}%. The technique is there.";
                if (r.Accuracy < 55)
                    return "Tough session — sniper scenarios reward patience over speed. Slow down and let the crosshair settle before clicking.";
            }

            if (r.Scenario == "Shotgun")
            {
                if (r.AvgReactionMs > 0 && r.AvgReactionMs <= 200 && r.Accuracy >= 70)
                    return $"{r.AvgReactionMs:F0}ms average — that's elite shotgun speed. At this reaction time you're winning most fights before they start.";
                if (r.Accuracy >= 75)
                    return $"Strong shotgun session. {r.Accuracy:F0}% accuracy at close range is where it needs to be.";
                if (r.AvgReactionMs > 400)
                    return $"Reaction is the limiting factor here at {r.AvgReactionMs:F0}ms average. The accuracy is there — the hesitation before committing is what to work on.";
            }

            if (r.Scenario == "SmgAr")
            {
                double cons = 0;
                memory.BaselineConsistency.TryGetValue(r.Scenario, out cons);
                if (cons >= 75 && r.Accuracy >= 78)
                    return $"Consistent and accurate — {r.Accuracy:F0}% with {cons:F0}/100 consistency. That combination is what wins SMG/AR fights.";
                if (r.Accuracy >= 75)
                    return "Good SMG/AR session. Keep the consistency above 65 and this accuracy holds in real games.";
                if (cons > 0 && cons < 50)
                    return $"Consistency is the gap right now at {cons:F0}/100. The accuracy is workable — focus on keeping it steady rather than pushing it higher.";
            }

            // ── RULE 1: ALL-TIME PERSONAL BEST (TASK-05) ─────────────────────
            if (memory.TotalDrillCount >= 3
                && r.Accuracy > 0
                && r.Accuracy > memory.PersonalBestAccuracy)
            {
                return $"New personal best — {r.Accuracy:F0}% in {r.Scenario}. " +
                       $"That's the best you've hit in {memory.TotalDrillCount} sessions.";
            }

            // ── RULE 2: PLATEAU BREAK (TASK-05) ──────────────────────────────
            // Prior sessions were plateaued, but current session breaks through
            if (memory.IsAccuracyPlateaued
                && memory.PlateauLength >= 3
                && memory.PlateauAvgAccuracy > 0
                && r.Accuracy > memory.PlateauAvgAccuracy + 3.0)
            {
                return $"You broke through. After {memory.PlateauLength} sessions stuck around " +
                       $"{memory.PlateauAvgAccuracy:F0}%, you just hit {r.Accuracy:F0}%. " +
                       "Something clicked.";
            }

            // ── RULE 3: SIGNIFICANT IMPROVEMENT TREND (TASK-05) ──────────────
            if (memory.AccuracyTrend > 5.0 && memory.TotalDrillCount >= 6)
            {
                return $"Your {r.Scenario} accuracy is trending up — " +
                       $"{memory.AccuracyTrend:F1} points better than your last 5 sessions. " +
                       "Keep the same approach.";
            }

            // ── RULE 4: DECLINING TREND (TASK-05) ────────────────────────────
            // Never say "you're getting worse" — frame as diagnostic
            if (memory.AccuracyTrend < -5.0 && memory.TotalDrillCount >= 5)
            {
                int v = memory.TotalDrillCount % 3;
                return v switch
                {
                    0 => "Your last few sessions have been below your usual level. That's normal — here's what to check.",
                    1 => "You're running a little below your recent baseline. One of three things: sensitivity, fatigue, or warmup. Let's diagnose.",
                    _ => "The trend has dipped. That's information, not a verdict. Here's how to read it."
                };
            }

            // ── RULE 5: PLATEAU DETECTED (TASK-05) ───────────────────────────
            if (memory.IsAccuracyPlateaued
                && memory.PlateauLength >= 3
                && memory.PlateauAvgAccuracy > 0
                && r.Accuracy <= memory.PlateauAvgAccuracy + 3.0)
            {
                return $"You've been hovering around {memory.PlateauAvgAccuracy:F0}% for " +
                       $"{memory.PlateauLength} sessions. That's not a bad thing — " +
                       "it means you're ready for the next challenge.";
            }

            // ── RULES 6-7: existing logic as fallback ─────────────────────────
            if (c.NewAccuracyRecord && c.NewReactionRecord)
                return $"Personal best on both accuracy and reaction time — {r.Accuracy:F0}% accuracy, {r.AvgReactionMs:F0}ms avg. Your best session yet.";
            if (c.NewAccuracyRecord)
                return $"New personal best accuracy — {r.Accuracy:F0}% in {r.Scenario}. That's a record for you.";
            if (c.NewReactionRecord)
                return $"New personal best reaction time — {r.AvgReactionMs:F0}ms average. Fastest you've been in {r.Scenario}.";
            if (c.AccuracyGrade == "elite")
                return $"{r.Accuracy:F0}% accuracy in {r.Scenario} — that's elite level. You're consistently in the top tier.";
            if (c.IsImproving && c.AccuracyDelta > 0)
                return $"Up {c.AccuracyDelta:+0.0;+0.0}% accuracy vs your last session — clear improvement.";
            if (c.AccuracyDelta < -5 && !c.IsFirstSession)
                return $"Accuracy dipped {Math.Abs(c.AccuracyDelta):F0}% from last session — happens to everyone. Here's how to bounce back.";
            if (c.IsFirstSession)
            {
                if (c.TotalSessionsAll > 3)
                    return $"First {r.Scenario} session — {r.Accuracy:F0}% accuracy. Across your {c.TotalSessionsAll} total sessions your overall avg accuracy is {c.OverallAvgAccuracy:F0}%.";
                return $"First {r.Scenario} session logged — {r.Accuracy:F0}% accuracy gives you a solid baseline to build from.";
            }
            return $"{r.Accuracy:F0}% accuracy, {r.AvgReactionMs:F0}ms avg reaction. {(c.AccuracyGrade == "good" ? "Solid session." : "Keep building.")}";
        }

        private static List<string> GetStrengths(AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            var list = new List<string>();
            bool hasHistory = memory.TotalDrillCount >= 3;

            // ── WEAPON SCENARIO STRENGTHS ─────────────────────────────────────

            // Rule S-1 (Sniper): clean first shots
            if (r.Scenario == "Sniper"
                && r.Accuracy >= 85
                && c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value <= 20)
            {
                list.Add($"Clean first shots at {r.Accuracy:F0}% — you're committing to your aim before pulling the trigger. " +
                         "That's the hardest sniper habit to build.");
            }

            // Rule SG-1 (Shotgun): fast + accurate
            if (r.Scenario == "Shotgun"
                && r.AvgReactionMs > 0
                && r.AvgReactionMs <= 220
                && r.Accuracy >= 70)
            {
                list.Add($"{r.AvgReactionMs:F0}ms average with {r.Accuracy:F0}% accuracy — that combination wins shotgun fights. " +
                         "Fast and accurate is the hardest thing to train.");
            }

            // Rule AR-1 (SmgAr): sustained tracking
            if (r.Scenario == "SmgAr")
            {
                memory.BaselineConsistency.TryGetValue(r.Scenario, out double cons);
                if (cons >= 70 && r.Accuracy >= 75)
                {
                    list.Add($"Sustained {r.Accuracy:F0}% accuracy with {cons:F0}/100 consistency — you're staying on target through movement. " +
                             "That's the core SMG/AR skill.");
                }
            }

            // ── TASK-06: history-aware strength language ──────────────────────

            // Accuracy strength — reference trend/consistency when history exists
            if (c.AccuracyGrade == "elite")
            {
                if (hasHistory && memory.AccuracyTrend > 0)
                {
                    // Improving trend at elite level
                    list.Add(PickGlobal(memory, 0,
                        $"Your accuracy has been consistently above {Bench.AccuracyGood(r.Scenario):F0}% for your last several sessions. That's becoming a reliable strength.",
                        $"Elite accuracy at {r.Accuracy:F0}% — and the trend is pointing up. That's not luck, that's the work compounding.",
                        $"{r.Accuracy:F0}% accuracy and still improving. You're in rare territory."
                    ));
                }
                else
                {
                    list.Add(Pick(c.SessionCount,
                        $"Your {r.Accuracy:F0}% accuracy is elite level — most competitive players hover around {Bench.AccuracyGood(r.Scenario):F0}%. You're well above that.",
                        $"{r.Accuracy:F0}% accuracy puts you in the top tier. That's not luck — that's reps paying off.",
                        $"Elite accuracy at {r.Accuracy:F0}%. The consistency is the impressive part — anyone can have a good session, fewer can repeat it.",
                        $"Your {r.Accuracy:F0}% accuracy is the kind of number that shows up in ranked lobbies. Keep building on it."
                    ));
                }
            }
            else if (c.AccuracyGrade == "good")
            {
                if (hasHistory && memory.BaselineAccuracy.TryGetValue(r.Scenario, out double baseline) && r.Accuracy >= baseline)
                {
                    list.Add(PickGlobal(memory, 0,
                        $"Your {r.Scenario} accuracy has been consistently around {baseline:F0}% or better. That floor is real — build on it.",
                        $"{r.Accuracy:F0}% accuracy in {r.Scenario}, and your baseline is holding steady. Fundamentals are solid.",
                        $"Solid accuracy at {r.Accuracy:F0}% — above your recent average of {baseline:F0}%. You're clicking with intent."
                    ));
                }
                else
                {
                    list.Add(Pick(c.SessionCount,
                        $"{r.Accuracy:F0}% accuracy puts you in the good range for {r.Scenario}. You're making more right decisions than wrong ones.",
                        $"Solid {r.Accuracy:F0}% accuracy in {r.Scenario}. The fundamentals are there — now build consistency.",
                        $"{r.Accuracy:F0}% is a good number for {r.Scenario}. You're above average and trending in the right direction.",
                        $"Good accuracy at {r.Accuracy:F0}%. You're clicking with intent — that's the foundation everything else builds on."
                    ));
                }
            }
            else if (c.IsImproving && c.AccuracyDelta > 3)
            {
                list.Add(Pick(c.SessionCount,
                    $"Accuracy improved {c.AccuracyDelta:+0.0}% from your last session — that's real, measurable progress.",
                    $"Up {c.AccuracyDelta:F0}% accuracy since last session. The trajectory is pointing up.",
                    $"Accuracy trend: +{c.AccuracyDelta:F0}% from last time. Every improvement stacks.",
                    $"Your accuracy is climbing — {c.AccuracyDelta:F0}% better than last session. That's the definition of improving."
                ));
            }

            // ── Reaction trend strength (TASK-01) — fires independently ─────
            // Fires when reaction is genuinely improving across sessions, regardless
            // of accuracy strength. Both can appear. Max-2 cap applied at the end.
            if (memory.TotalDrillCount >= 3
                && memory.ReactionTrend < -10
                && r.AvgReactionMs > 0)
            {
                if (memory.ReactionTrend < -20)
                {
                    list.Add($"Your reaction time has dropped {Math.Abs(memory.ReactionTrend):F0}ms over your recent sessions. " +
                             "That kind of improvement at this accuracy level almost never happens — it means your technique " +
                             "is genuinely getting better, not just your familiarity.");
                }
                else
                {
                    list.Add($"{r.AvgReactionMs:F0}ms average, and trending lower. Speed and control improving together is rare.");
                }
            }

            // ── Reaction grade strength — fills remaining slot if trend didn't fire ──
            if (list.Count < 2)
            {
                if (c.ReactionGrade == "elite")
                {
                    list.Add(Pick(c.SessionCount,
                        $"Your {r.AvgReactionMs:F0}ms average reaction is elite level — most players are 150-200ms slower than this.",
                        $"{r.AvgReactionMs:F0}ms reaction time is genuinely fast. You're in the range where raw speed becomes an advantage.",
                        $"Elite reaction at {r.AvgReactionMs:F0}ms average. Your reads are translating to clicks — that's the hard part.",
                        $"{r.AvgReactionMs:F0}ms average. At this speed, target acquisition is a real strength, not just a stat."
                    ));
                }
                else if (c.ReactionGrade == "good")
                {
                    list.Add(Pick(c.SessionCount,
                        $"{r.AvgReactionMs:F0}ms average reaction is competitive. You're in the range where pros operate.",
                        $"Good reaction time at {r.AvgReactionMs:F0}ms average. Speed isn't the bottleneck — build on this.",
                        $"{r.AvgReactionMs:F0}ms puts your reactions in a solid range. Consistent performance at this speed is what separates good from great.",
                        $"Reaction time: {r.AvgReactionMs:F0}ms average — that's genuinely competitive. Use it."
                    ));
                }
                else if (!c.IsFirstSession && c.ReactionDelta < -20)
                {
                    if (c.ReactionGrade == "slow")
                        list.Add($"Reaction time improved {Math.Abs(c.ReactionDelta):F0}ms from last session — real progress. Your average is still {r.AvgReactionMs:F0}ms, so there's more room to go, but the direction is right.");
                    else
                        list.Add($"Reaction time improved {Math.Abs(c.ReactionDelta):F0}ms faster than last session — your reads are getting sharper.");
                }
            }

            // Streak and consistency
            if (list.Count < 2)
            {
                if (c.StreakGrade == "elite")
                    list.Add($"A streak of {r.MaxStreak} is exceptional — it shows you can maintain focus and rhythm under pressure.");
                else if (c.StreakGrade == "good")
                    list.Add($"Your {r.MaxStreak}-hit streak shows you have the ability to lock in when it counts.");
                else if (c.IsConsistent)
                    list.Add("Your accuracy has been consistent across your last few sessions — that reliability is harder to build than people think.");
            }

            if (list.Count == 0)
            {
                list.Add(r.Hits > r.Misses
                    ? $"You hit more than you missed — {r.Hits} hits vs {r.Misses} misses. That's the foundation to build on."
                    : $"You completed a full {r.DurationSeconds}-second drill. Every session builds muscle memory, even the tough ones.");
            }

            // TASK-06: maximum 2 strengths
            return list.Take(2).ToList();
        }

        private static List<string> GetWeaknesses(AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            var list = new List<string>();

            // ── WEAPON SCENARIO WEAKNESSES ────────────────────────────────────

            // Rule S-2 (Sniper Standard): high correction sharpness = not settling before clicking
            if (r.Scenario == "Sniper"
                && r.SubVariant != "Moving"   // Moving has its own variant rule below
                && c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value > 30)
            {
                list.Add("Your correction sharpness is high for a sniper scenario — you're moving and then adjusting " +
                         "rather than settling first. Try this: before each click, stop your mouse completely for " +
                         "half a second. Clean stop, then shoot.");
            }

            // Rule SN-V1 (Sniper Moving): matching target speed before clicking
            if (r.Scenario == "Sniper"
                && r.SubVariant == "Moving"
                && c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value > 25)
            {
                list.Add("Moving targets require you to match the target's speed before clicking, not chase it. " +
                         "Move with it, settle, then commit. Clicking while still catching up is what's causing your corrections.");
            }

            // Rule SN-V2 (Sniper Wind): reading the drift angle
            if (r.Scenario == "Sniper"
                && r.SubVariant == "Wind"
                && r.Accuracy < 70)
            {
                list.Add("Wind drift has a constant direction — once you learn to read the drift in the first few seconds " +
                         "you can lead your aim ahead of it rather than reacting to where it is. " +
                         "Watch the first target for 2 seconds before clicking to get the drift angle.");
            }

            // Rule SG-2 (Shotgun): high correction = overshooting
            if (r.Scenario == "Shotgun"
                && c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value > 20)
            {
                list.Add("Your correction sharpness is too high for shotgun scenarios — you're overshooting and adjusting. " +
                         "In a real shotgun fight that second motion is too slow. Aim for your first movement to land on target. " +
                         "If you miss, that's fine — commit to the next shot instead of correcting.");
            }

            // Rule SG-V1 (Shotgun Duels): decision-making
            if (r.Scenario == "Shotgun"
                && r.SubVariant == "Duels"
                && r.Accuracy < 65)
            {
                list.Add("In Duels, the decision matters more than the execution. Pick the closer target every time — " +
                         "don't evaluate both options. Closer target, instant commit. " +
                         "That one rule wins most shotgun duels.");
            }

            // Rule SG-V2 (Shotgun Peek): pre-aiming
            if (r.Scenario == "Shotgun"
                && r.SubVariant == "Peek"
                && r.AvgReactionMs > 300)
            {
                list.Add("Peek scenarios reward pre-aiming more than reaction speed. " +
                         "Position your crosshair at the edge before the target appears rather than moving to it after. " +
                         "That alone cuts your effective reaction time in half.");
            }

            // Rule SG-3 (Shotgun): slow reaction
            if (r.Scenario == "Shotgun" && r.AvgReactionMs > 350)
            {
                list.Add($"Your average reaction is {r.AvgReactionMs:F0}ms — for shotgun scenarios you want to be under 280ms. " +
                         $"Your best was {r.BestReactionMs:F0}ms which shows the speed is there. " +
                         "The gap is hesitation before committing. Trust your aim and pull the trigger.");
            }

            // Rule AR-2 (SmgAr): low consistency across sessions
            if (r.Scenario == "SmgAr")
            {
                memory.BaselineConsistency.TryGetValue(r.Scenario, out double cons);
                if (cons > 0 && cons < 55)
                {
                    list.Add($"Your consistency score is {cons:F0}/100 — your accuracy is dropping off over the session. " +
                             "SMG/AR scenarios reward players who maintain accuracy under pressure, not just in bursts. " +
                             "Try shorter sessions at the same difficulty until consistency stays above 65 throughout.");
                }
            }

            // Rule AR-V1 (SmgAr Spray): priority system
            if (r.Scenario == "SmgAr"
                && r.SubVariant == "Spray")
            {
                memory.BaselineConsistency.TryGetValue(r.Scenario, out double cons);
                if (cons > 0 && cons < 55)
                {
                    list.Add("Three targets is cognitive overload until you build a priority system. " +
                             "Always click the closest target first. Always. Don't evaluate — just closest target, click, next closest. " +
                             "That system becomes automatic after 5–6 sessions.");
                }
            }

            // Rule AR-V2 (SmgAr Strafe): direction-change moment
            if (r.Scenario == "SmgAr"
                && r.SubVariant == "Strafe")
            {
                memory.BaselineConsistency.TryGetValue(r.Scenario, out double cons);
                if (cons > 0 && cons < 60)
                {
                    list.Add("Strafe scenarios are hardest at the direction change. " +
                             "If your accuracy is inconsistent it's almost always the reversal moment causing it. " +
                             "Focus on the instant the targets change direction — that's where the session is won or lost.");
                }
            }

            // Return early for weapon scenarios once we have observations
            if ((r.Scenario is "Sniper" or "Shotgun" or "SmgAr") && list.Count > 0)
                return list.Take(2).ToList();

            // ── TASK-07 RULE 1: PRESCRIPTION FOLLOW-UP (highest priority) ────
            if (memory.LastPrescriptionFollowed
                && !string.IsNullOrEmpty(memory.LastPrescribedScenario)
                && r.Scenario == memory.LastPrescribedScenario
                && memory.TotalDrillCount >= 3)
            {
                // Check if target metric improved vs prior average for this scenario
                bool improved = false;
                string metricNote = "";
                if (memory.BaselineAccuracy.TryGetValue(r.Scenario, out double baseAcc))
                {
                    double delta = r.Accuracy - baseAcc;
                    if (delta > 1.5)
                    {
                        improved = true;
                        metricNote = $"accuracy went from {baseAcc:F0}% to {r.Accuracy:F0}%";
                    }
                    else if (delta < -1.5)
                    {
                        metricNote = $"accuracy is still at {r.Accuracy:F0}% vs your {baseAcc:F0}% average";
                    }
                }

                int v = memory.TotalDrillCount % 3;
                if (improved)
                {
                    list.Add(v switch
                    {
                        0 => $"Last time I told you to work on {memory.LastPrescribedScenario}. You did — and it shows. Your {metricNote}. Next step: push one difficulty higher.",
                        1 => $"You followed the {memory.LastPrescribedScenario} prescription and your {metricNote}. That improvement is yours to keep. Now: consolidate it at the same difficulty before moving up.",
                        _ => $"The {memory.LastPrescribedScenario} work paid off — {metricNote}. The habit is forming. Keep that scenario in your rotation."
                    });
                    if (list.Count > 0) return list;
                }
                else if (!string.IsNullOrEmpty(metricNote))
                {
                    list.Add(v switch
                    {
                        0 => $"You ran the {memory.LastPrescribedScenario} drill I suggested. The {metricNote} — that's okay, it takes repetition. Here's a more specific focus for next time: commit to center before clicking, don't rush the first motion.",
                        1 => $"You followed the {memory.LastPrescribedScenario} prescription and {metricNote}. One session isn't enough for this to show up — keep going. The mechanics take 5-8 reps to settle.",
                        _ => $"Good that you ran {memory.LastPrescribedScenario}. The {metricNote} yet. Stick with it — this kind of improvement shows up on session 4-6, not session 2."
                    });
                }
            }

            // ── TASK-07 RULE 2: PLATEAU INTERVENTION ─────────────────────────
            if (list.Count == 0 && memory.IsAccuracyPlateaued && memory.PlateauLength >= 3)
            {
                int v = memory.TotalDrillCount % 3;
                // Diagnose which metric has most room to improve
                bool reactionIsWeaker = c.ReactionGrade is "slow" or "average";
                string diagnosis = reactionIsWeaker
                    ? $"reaction time is at {r.AvgReactionMs:F0}ms — that's the next lever to pull"
                    : "sensitivity might be slightly off for this scenario";

                list.Add(v switch
                {
                    0 => $"You've plateaued at {memory.PlateauAvgAccuracy:F0}% for {memory.PlateauLength} sessions. " +
                         $"The usual causes are sensitivity being slightly off, or sessions getting too long and losing focus. " +
                         $"Here's how to tell: run one 30-second drill at max focus. If your score jumps, it's focus. If it doesn't, check your sensitivity.",
                    1 => $"Your {r.Scenario} accuracy hasn't moved in {memory.PlateauLength} sessions. That means the current approach has a ceiling. " +
                         $"Two things to try: step up the difficulty for one session to expose gaps, or cut session length to 30 seconds to force concentration.",
                    _ => $"Stuck around {memory.PlateauAvgAccuracy:F0}% in {r.Scenario}. The plateau is real — your {diagnosis}. " +
                         $"Change one variable: difficulty, duration, or sensitivity. See which one moves the needle."
                });
            }

            // ── TASK-07 RULE 3: CROSS-COACH INSIGHT ──────────────────────────
            if (list.Count < 2 && memory.RecentTrackerSessions.Count >= 2)
            {
                int v = memory.TotalDrillCount % 3;
                double trackerAvgQuality = memory.RecentTrackerSessions.Average(s => s.OverallQualityScore);
                double drillsConsistency = memory.TotalDrillCount >= 3
                    ? memory.AllDrills.Take(5).Average(d => d.Accuracy)
                    : 0;

                bool drillsImproving  = memory.AccuracyTrend > 2;
                bool ingameImproving  = memory.ConsistencyTrend > 2;
                bool ingameDeclining  = memory.ConsistencyTrend < -5;
                bool bothDeclining    = memory.AccuracyTrend < -3 && ingameDeclining;

                if (bothDeclining)
                {
                    list.Add(v switch
                    {
                        0 => "Your consistency is dropping in drills and in-game. That usually means fatigue or too many long sessions. Try shorter sessions for a week — 20 minutes max, then stop.",
                        1 => "Both your drill performance and in-game movement are trending down. That pattern points to overtraining or general fatigue. One rest day makes a bigger difference than one extra session.",
                        _ => "Drills and in-game quality both dipping. Before diagnosing mechanics, check the basics: sleep, session length, warmup. Fix those first."
                    });
                }
                else if (drillsImproving && ingameDeclining)
                {
                    list.Add(v switch
                    {
                        0 => "Your drills are getting better but your in-game performance hasn't caught up yet. That gap usually closes within 2-3 weeks — keep drilling.",
                        1 => "Drill improvement isn't showing up in-game yet. That's normal — muscle memory transfers after 15-20 sessions of the same pattern, not after 5. Stay patient.",
                        _ => "Strong drill numbers but in-game is lagging. The translation takes time. Make sure you're drilling immediately before you play — the transfer is strongest when they're close together."
                    });
                }
                else if (ingameImproving && list.Count < 2)
                {
                    list.Add(v switch
                    {
                        0 => "Your in-game smoothness is trending up. The drilling is transferring.",
                        1 => "In-game movement is improving alongside your drill accuracy. That's the outcome you're training for — keep the same routine.",
                        _ => "In-game quality is climbing. The habits you're building in drills are showing up where it counts."
                    });
                }
            }

            // ── Multi-signal combinations — checked first, return early if matched ──

            // Combination A: High correction sharpness + slow reaction
            // Diagnosis: overshoot-correct cycle artificially inflating reaction time
            if (c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value > 60
                && c.ReactionGrade == "slow"
                && (r.Scenario == "Flicking" || r.Scenario == "Switching"))
            {
                list.Add(Pick(c.SessionCount,
                    $"Your reaction time looks slow but your movement data tells a different story — you are overshooting " +
                    "and yanking back to correct. That correction cycle adds a significant delay artificially. " +
                    "The fix is smoother first motion, not faster reflexes.",

                    $"The gap between your best and average reaction time is wider than expected. " +
                    "Your movement data points to overshoot-correction cycles — you can react fast, " +
                    "you just need to trust that first motion more instead of second-guessing it.",

                    "High correction activity with slow average reaction is a specific pattern — " +
                    "your instinct is right but your follow-through overshoots. " +
                    $"Your best reaction of {r.BestReactionMs:F0}ms proves the speed is there. Train the first motion, not the reaction."
                ));
                return list;
            }

            // Combination B: Low smoothness + low cm/360 in Tracking
            // Diagnosis: sensitivity fighting the movement — not a mechanics issue
            if (c.TrackerSmoothness.HasValue
                && c.TrackerSmoothness.Value < 55
                && c.TrackerCmPer360.HasValue
                && c.TrackerCmPer360.Value < 22
                && r.Scenario == "Tracking")
            {
                list.Add(Pick(c.SessionCount,
                    "Your mouse movement was inconsistent this session — and your sensitivity may be making it worse. " +
                    "When sensitivity is very high, small hand movements cause large cursor jumps which makes smooth tracking much harder. " +
                    "Try lowering your in-game sensitivity slightly before your next session and see if your movement feels more controlled.",

                    "Your movement data and your sensitivity are working against each other. " +
                    "High sensitivity amplifies every small imperfection in your hand movement, making fluid tracking harder to achieve. " +
                    "Try the sensitivity recommendation before concluding your tracking mechanics need work.",

                    "Your movement inconsistency this session looks like a settings problem, not a skill problem. " +
                    "When sensitivity is on the higher end, your hand has less room to work with and small tremors get magnified into large cursor swings. " +
                    "Lower your in-game sensitivity slightly and retest — the difference is usually noticeable immediately."
                ));
                return list;
            }

            // Combination C: Consistent but low accuracy in Precision
            // Diagnosis: mechanically consistent but crosshair placement is off
            if (c.IsConsistent
                && (c.AccuracyGrade == "average" || c.AccuracyGrade == "developing")
                && r.Scenario == "Precision")
            {
                list.Add(Pick(c.SessionCount,
                    $"Your accuracy is {r.Accuracy:F0}% but your consistency across sessions is actually good — " +
                    "you are consistently missing, which means the mechanics are repeatable. " +
                    "This usually points to crosshair placement — you are probably stopping just short of the target center. " +
                    "Aim for the center pixel, not the edge.",

                    $"Interesting pattern: consistent performance with below-average accuracy in Precision. " +
                    "Consistent means the movement is repeatable. Below average means it is landing in the wrong spot consistently. " +
                    "The most common cause is stopping the cursor at the edge of targets instead of center.",

                    $"Your consistency score is good but accuracy is {r.Accuracy:F0}%. " +
                    "In Precision that combination usually means your stopping point is slightly off, not your speed. " +
                    "Focus on committing to center — overshoot slightly until the centering becomes natural."
                ));
                return list;
            }

            switch (c.WeakArea)
            {
                case "accuracy":
                    // Check for sensitivity root cause first — most valuable diagnosis
                    if (c.TrackerCmPer360.HasValue && r.Scenario == "Precision")
                    {
                        if (c.TrackerCmPer360.Value < 20)
                        {
                            list.Add("Your sensitivity is too high for Precision — micro-adjustments on small targets become " +
                                     "physically very difficult when sensitivity is this high. This is a settings problem, not a skill problem. " +
                                     "Lower your in-game sensitivity and retest.");
                            break;
                        }
                        else if (c.TrackerCmPer360.Value > 55)
                        {
                            list.Add("Your sensitivity is too low for Precision — your cursor moves too little per hand movement, " +
                                     "making it hard to snap onto small targets quickly. " +
                                     "Raise your in-game sensitivity and retest.");
                            break;
                        }
                    }
                    if (c.TrackerCmPer360.HasValue && r.Scenario == "Tracking")
                    {
                        if (c.TrackerCmPer360.Value < 15)
                        {
                            list.Add("Your sensitivity is too high for Tracking — fluid wrist movement is nearly impossible " +
                                     "at this sensitivity level. Lower your in-game sensitivity so your mouse " +
                                     "travels further across the pad per turn.");
                            break;
                        }
                        else if (c.TrackerCmPer360.Value > 65)
                        {
                            list.Add("Your sensitivity is too low for Tracking — small hand movements are causing large cursor jumps, " +
                                     "making smooth follow-through very difficult. " +
                                     "Raise your in-game sensitivity slightly.");
                            break;
                        }
                    }
                    // Default accuracy coaching when no sensitivity diagnosis applies
                    list.Add(r.Accuracy < 50
                        ? $"At {r.Accuracy:F0}% accuracy you are missing more than half your shots — slow down and prioritize clicking when your cursor is actually on the target."
                        : $"{r.Accuracy:F0}% accuracy has room to grow. You are rushing some clicks — wait for the moment of confidence before clicking.");
                    break;
                case "reaction":
                    // Rule S-3: suppress ALL reaction coaching for Sniper —
                    // patience wins sniper fights; slow reaction here is correct behavior.
                    if (r.Scenario == "Sniper") break;

                    // Check if slow reaction is actually a movement problem, not a cognitive one
                    if (c.TrackerCorrectionSharpness.HasValue
                        && c.TrackerCorrectionSharpness.Value > 60
                        && c.ReactionGrade == "slow")
                    {
                        list.Add($"Your reaction time looks slow at {r.AvgReactionMs:F0}ms but your movement data " +
                                 "suggests you are overshooting and correcting — " +
                                 "that correction cycle adds a significant delay artificially. The fix is smoother initial movement, not faster reactions. " +
                                 "Focus on landing on the target in one motion instead of correcting after overshoot.");
                    }
                    else
                    {
                        string reactionMsg;
                        if (c.ReactionGrade == "slow")
                        {
                            if (!c.IsFirstSession && c.ReactionDelta < -20)
                                reactionMsg = $"Your {r.AvgReactionMs:F0}ms reaction is still on the slower side, but you're heading in the right direction. Keep focusing on anticipation over raw speed.";
                            else
                                reactionMsg = $"Your {r.AvgReactionMs:F0}ms average reaction is on the slower side. Focus less on speed and more on predicting target movement — anticipation is faster than reaction.";
                        }
                        else
                        {
                            double gap = r.AvgReactionMs - r.BestReactionMs;
                            // Only surface the gap observation when it's meaningful and we have real data
                            if (gap > 15 && r.Hits >= 5 && r.BestReactionMs > 0 && r.AvgReactionMs > 0)
                            {
                                string gapAction;
                                if (gap > 50)
                                    gapAction = " To close that gap, try this: at the start of your next session, " +
                                                "take 10 seconds before each target instead of reacting immediately. " +
                                                "Force yourself to wait, then commit fast. " +
                                                "That trains the deliberate trigger pull that turns your best reactions into your average ones.";
                                else if (gap > 20)
                                    gapAction = " To close it, focus on the first 10 seconds of each drill — " +
                                                "that's usually where your best reactions happen before fatigue sets in. " +
                                                "Shorter sessions with full focus beat longer sessions where you drift.";
                                else
                                    gapAction = " That gap is small — you're already close to your ceiling. " +
                                                "One more difficulty step will test whether that holds under pressure.";

                                reactionMsg = $"The gap between your best reaction ({r.BestReactionMs:F0}ms) and average ({r.AvgReactionMs:F0}ms) is {gap:F0}ms — your ceiling is higher than your average suggests.{gapAction}";
                            }
                            else
                            {
                                reactionMsg = $"Reaction time: {r.AvgReactionMs:F0}ms average, {r.BestReactionMs:F0}ms best. Keep building consistency.";
                            }
                        }

                        // Scenario context — slow reaction means different things in different modes
                        if (r.Scenario == "Adaptive" && c.ReactionGrade == "slow")
                            reactionMsg += " Note: Adaptive targets your weakest areas specifically — slower reactions here are expected until your overall mechanics improve.";
                        else if (r.Scenario == "Precision" && c.ReactionGrade == "slow")
                            reactionMsg += " In Precision, patience matters more than speed — a measured click on a small target beats a fast miss every time.";

                        list.Add(reactionMsg);
                    }
                    break;
                case "streak":
                    list.Add($"Your best streak was {r.MaxStreak} — breaking streaks early usually means rushing after a miss or losing focus mid-drill. Take a breath after each miss and reset.");
                    break;
                case "endurance":
                    list.Add(!c.IsFirstSession && c.AccuracyDelta < -8
                        ? $"Accuracy dropped {Math.Abs(c.AccuracyDelta):F0}% from last session. This can be fatigue, distraction, or sensitivity. Note the conditions — time of day and warmup matter more than people realize."
                        : "Your later targets seem harder than early ones — this is a focus endurance issue, not a skill issue. Short drills help train consistent focus.");
                    break;
            }

            // Smoothness diagnosis — hardware root-cause, higher priority than generic scenario tips.
            // Runs before scenario tips so it claims a slot first when both conditions fire.
            if (c.TrackerSmoothness.HasValue
                && c.TrackerSmoothness.Value < 60
                && r.Scenario == "Tracking"
                && r.Accuracy < 65)
            {
                list.Add("Your movement was noticeably choppy this session — that level of jitter makes consistent tracking physically harder. " +
                         "Check your grip, surface friction, and make sure your mousepad is clean and flat.");
            }

            // Scenario-specific tips — only add if there is still room; smoothness diagnosis above takes priority.
            if (list.Count < 2)
            {
                if (r.Scenario == "Tracking" && r.Accuracy < 65)
                    list.Add("In Tracking, many players chase the center of the target — aim slightly ahead of where it's moving instead.");
                else if (r.Scenario == "Flicking" && r.AvgReactionMs > 450)
                    list.Add("For Flicking, your eyes should land on the target before your mouse moves. The eyes lead, the hand follows.");
                else if (r.Scenario == "Precision" && r.Accuracy < 75)
                    list.Add("Precision requires slowing down intentionally — if you feel rushed, you'll overshoot small targets every time.");
                else if (r.Scenario == "Switching" && r.MaxStreak < 4)
                    list.Add("In Switching, scan for the next target while clicking the current one — don't wait until after you've clicked to look for what's next.");
            }

            if (list.Count == 0)
                list.Add($"Your {c.WeakArea} is the area with the most room to grow — even small improvements there will lift your overall score significantly.");

            return list.Take(2).ToList();
        }

        private static List<string> GetAdvice(AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            var tips = new List<string>();
            bool hasHistory = memory.TotalDrillCount >= 3;

            switch (r.Scenario)
            {
                // ── WEAPON SCENARIO TIPS ──────────────────────────────────────
                case "Sniper":
                    if (r.Accuracy < 75)
                        tips.Add("Sniper accuracy is about removing movement before you click, not clicking faster. " +
                                 "Slow your mouse to about 30% of your normal speed as you approach the target, let it settle " +
                                 "completely, then fire. Speed comes from clean technique, not rushing.");
                    else
                        tips.Add(Pick(c.SessionCount,
                            "Sniper fights are won in the pause before the shot. Pre-aim likely positions, slow your approach, then fire.",
                            "Clean sniper technique: track → slow → stop → shoot. Any motion still in your hand when you click shows up as a miss.",
                            "You've earned the next difficulty step for Sniper. The targets are the same — the margin for sloppiness is just smaller."
                        ));
                    break;

                case "Shotgun":
                    tips.Add(Pick(c.SessionCount,
                        "Shotgun fights are won before the fight starts — pre-aim where opponents appear, not where they are when you see them.",
                        "Your crosshair placement before the fight matters more than your reaction during it. Practice pre-positioning at head height.",
                        "One clean shot beats two sloppy ones in shotgun scenarios. Slow down slightly and watch your accuracy improve more than your reaction speed."
                    ));
                    break;

                case "SmgAr":
                    tips.Add(Pick(c.SessionCount,
                        "SMG/AR accuracy is about smooth mouse movement, not clicking speed. If your arm tenses up during the session " +
                        "you'll lose consistency. Relax your grip and let the mouse float.",
                        "Two targets means you have to prioritize constantly. Train yourself to always be moving toward the next target " +
                        "before you've finished clicking the current one.",
                        "Spray control in a real game comes from the same muscle memory you're building here — smooth tracking through movement, not chasing the target after it moves."
                    ));
                    break;

                case "Tracking":
                    tips.Add(r.Accuracy < 65
                        ? "Lower your sensitivity slightly and focus on smooth movement — Tracking rewards control more than speed."
                        : "Try increasing the difficulty one level. Tracking at higher speeds builds the smoothest aim muscle memory.");
                    break;
                case "Flicking":
                    tips.Add(r.AvgReactionMs > 400
                        ? "Don't click as soon as you start moving — wait for your cursor to settle on the target. One confident click beats two rushed misses."
                        : $"Your reaction is already strong at {r.AvgReactionMs:F0}ms. Focus on reducing your miss rate to convert that speed into real accuracy.");
                    break;
                case "Precision":
                    // TASK-01: sensitivity tip only fires when ALL three conditions are verified:
                    // 1. cm/360 is a real reading (> 0 and not above 200 — guards uninitialized/corrupted values)
                    // 2. cm/360 < 25 — genuinely high sensitivity territory
                    // 3. accuracy is actually being affected (< 75%)
                    // When conditions are NOT met: contribute nothing here; the variance tip below covers it.
                    if (c.TrackerCmPer360.HasValue
                        && c.TrackerCmPer360.Value > 0
                        && c.TrackerCmPer360.Value < 25
                        && c.TrackerCmPer360.Value <= 200
                        && r.Accuracy < 75)
                    {
                        tips.Add($"Your sensitivity is on the higher end at {c.TrackerCmPer360.Value:F1} cm/360. " +
                                 "On small Precision targets that can work against you — micro-corrections get amplified. " +
                                 "The Recommend screen can show you a target range based on your movement patterns.");
                    }
                    break;
                case "Switching":
                    tips.Add(r.MaxStreak < 5
                        ? "Pre-aim the location where the next target will likely appear. In Switching, positioning your mouse early is faster than reacting late."
                        : "Add the Tracking scenario to your warmup — smooth movement between targets is what separates good Switching players from great ones.");
                    break;
                default:
                    if (c.AccuracyGrade is "developing" or "average")
                    {
                        tips.Add("Accuracy over speed — every time. A slow hit is worth more than a fast miss. Trust your cursor.");
                    }
                    else
                    {
                        // TASK-02: data-driven variance tip using actual scenario history
                        var scenarioSessions = memory.RecentDrills
                            .Where(d => d.Scenario == r.Scenario)
                            .Take(5)
                            .ToList();

                        if (scenarioSessions.Count >= 3)
                        {
                            double minAcc = scenarioSessions.Min(d => d.Accuracy);
                            double maxAcc = scenarioSessions.Max(d => d.Accuracy);
                            double range  = maxAcc - minAcc;

                            if (range > 8)
                                tips.Add($"Your {r.Scenario} scores have ranged from {minAcc:F0}% to {maxAcc:F0}% recently — " +
                                         $"a {range:F0}-point spread. Closing that gap is the next challenge. " +
                                         "Consistency at this level is harder than getting here.");
                            else
                                tips.Add($"Your {r.Scenario} scores are tightly grouped around {scenarioSessions.Average(d => d.Accuracy):F0}%. " +
                                         "That consistency is the foundation — now push the ceiling by moving up a difficulty.");
                        }
                        else
                        {
                            tips.Add($"Run a few more {r.Scenario} sessions and the coach can give you a specific breakdown of your score range.");
                        }
                    }
                    break;
            }

            if (c.AccuracyGrade == "elite")
            {
                // TASK-08: reference actual session count and accuracy from memory
                if (hasHistory && memory.BaselineAccuracy.TryGetValue(r.Scenario, out double scBase))
                {
                    int aboveBaseCount = memory.AllDrills
                        .Where(d => d.Scenario == r.Scenario && d.Accuracy >= Bench.AccuracyElite(r.Scenario))
                        .Count();
                    tips.Add(PickGlobal(memory, 2,
                        $"You've hit above {Bench.AccuracyElite(r.Scenario):F0}% in {aboveBaseCount} of your {r.Scenario} sessions. {r.Difficulty} is no longer challenging enough — move up.",
                        $"Your {r.Scenario} average is {scBase:F0}% and you just hit {r.Accuracy:F0}%. {r.Difficulty} difficulty is hiding your ceiling. The next level up will expose the gaps.",
                        $"{r.Accuracy:F0}% on {r.Difficulty} with a {scBase:F0}% average — {(r.Difficulty == "Easy" ? "Medium" : r.Difficulty == "Medium" ? "Hard" : "Nightmare")} is the next step.",
                        $"You've earned the next difficulty. {r.Accuracy:F0}% accuracy on {r.Difficulty} across multiple sessions is a clear signal."
                    ));
                }
                else
                {
                    tips.Add(Pick(c.SessionCount,
                        $"Try Hard or Nightmare difficulty — challenging yourself at {r.Accuracy:F0}% accuracy on {r.Difficulty} means you're ready for the next level.",
                        $"{r.Accuracy:F0}% accuracy on {r.Difficulty} is a strong signal — step up the difficulty and find your real ceiling.",
                        $"You've outgrown {r.Difficulty} for now. Harder targets will expose gaps that {r.Difficulty} hides.",
                        $"At {r.Accuracy:F0}% you're coasting on {r.Difficulty}. The growth is in the next difficulty tier — go find it."
                    ));
                }
            }
            else if (c.ReactionGrade == "slow")
                tips.Add(PickGlobal(memory, 3,
                    $"Your reaction time is {r.AvgReactionMs:F0}ms average. Instead of trying to react faster, work on predicting — move your cursor to where the target will be, not where it is.",
                    $"Anticipation beats reaction. At {r.AvgReactionMs:F0}ms you're reacting after the fact. Watch the pattern and pre-aim instead.",
                    $"Your best reaction of {r.BestReactionMs:F0}ms proves the speed is there. The gap to your average ({r.AvgReactionMs:F0}ms) is a focus and anticipation problem, not a physical limit.",
                    $"{r.AvgReactionMs:F0}ms average. Reaction time training works best when you stop trying to be fast. Focus on the target's movement, not on clicking."
                ));
            else if (c.IsImproving)
                tips.Add(PickGlobal(memory, 3,
                    $"You've improved {Math.Abs(c.AccuracyDelta):F0}% in accuracy this session — keep the same routine. When something is working, don't change it.",
                    "Improvement is happening — that means the current routine is working. Protect it from disruption.",
                    "The trend is up. The best thing you can do right now is show up consistently and let the progression continue.",
                    "Don't overthink it when you're improving. Same routine, same schedule, let the gains compound."
                ));
            else if (!c.IsFirstSession && c.AccuracyDelta < -5)
                tips.Add(PickGlobal(memory, 3,
                    "A dip in performance is normal. Check if your sensitivity feels right today — sometimes a slight DPI or sens change explains a sudden dip.",
                    "Off sessions happen. Before your next session, check: same surface, same grip, same warmup? Small changes compound into big variance.",
                    "Performance variance is data. If this keeps happening, look at the conditions — time of day, warmup, sleep. Aim reacts to everything.",
                    "One bad session does not define the trend. Come back tomorrow with fresh hands and compare."
                ));
            else
                tips.Add(PickGlobal(memory, 3,
                    "Do 3 warm-up drills at Easy before your main session. Cold muscles make cold aim — warming up moves your average up more than any other single habit.",
                    "Warmup before drilling. Easy mode for 3 minutes primes the neuromuscular connection. Cold hands produce cold aim.",
                    "If you're not warming up before drills, you're leaving 10-15% accuracy on the table. Easy mode for a few minutes first.",
                    "The first drill of a session is always your worst. Accept it, use it as warmup, and save your real performance for the second and third sessions."
                ));

            if (c.SessionCount < 5)
                tips.Add(Pick(c.SessionCount,
                    "Track 3 sessions per week consistently. At 10 sessions in the same scenario you'll start seeing clear trend lines in your history.",
                    "The first 10 sessions in a scenario are baseline building. Don't judge the trend until you have 10 data points.",
                    "Consistency matters more than volume right now. Show up 3 times per week and let your session history build.",
                    "You need about 10 sessions before the trend line becomes meaningful. Keep logging."
                ));
            else if (c.IsConsistent)
            {
                // TASK-02: variance tip — shows min/max spread across recent scenario sessions
                var scenarioSessions = memory.RecentDrills
                    .Where(d => d.Scenario == r.Scenario)
                    .Take(5)
                    .ToList();

                if (scenarioSessions.Count >= 3)
                {
                    double minAcc = scenarioSessions.Min(d => d.Accuracy);
                    double maxAcc = scenarioSessions.Max(d => d.Accuracy);
                    double range  = maxAcc - minAcc;
                    double avg    = scenarioSessions.Average(d => d.Accuracy);

                    if (range > 8)
                    {
                        tips.Add($"Your {r.Scenario} scores have ranged from {minAcc:F0}% to {maxAcc:F0}% recently — " +
                                 $"a {range:F0}-point spread. Closing that gap is the next challenge. " +
                                 "Consistency at this level is harder than getting here.");
                    }
                    else
                    {
                        tips.Add($"Your {r.Scenario} scores are tightly grouped around {avg:F0}%. " +
                                 "That consistency is the foundation — now push the ceiling by moving up a difficulty.");
                    }
                }
                else
                {
                    tips.Add($"Run a few more {r.Scenario} sessions and the coach can give you a specific variance breakdown.");
                }
            }
            else
                tips.Add(Pick(c.SessionCount,
                    "Aim training works best alongside your actual game time, not instead of it. 10 minutes of drills before a game session produces better results than long standalone sessions.",
                    "Short sessions immediately before you play are more effective than long standalone sessions. Keep it tight and purposeful.",
                    "The transfer from training to game is strongest when drilling and playing happen close together. Train, then play.",
                    "10 focused minutes beats 30 distracted minutes. Keep sessions short enough to maintain full attention."
                ));

            return tips;
        }

        private static string GetNextDrill(AimTrainerResult r, CoachContext c)
        {
            if (c.AccuracyGrade == "elite" && r.Difficulty != "Hard" && r.Difficulty != "Nightmare")
                return $"Move up to Hard {r.Scenario} — you've earned it with {r.Accuracy:F0}% accuracy. The increased pressure will expose new areas to work on.";
            if (c.AccuracyGrade is "developing" or "average" && r.Difficulty == "Hard")
                return $"Drop to Medium {r.Scenario} for your next 2-3 sessions. Build confidence at a manageable level before returning to Hard.";
            if (r.Scenario == "Flicking" && c.ReactionGrade == "slow")
                return "Try Precision next — it trains the same target acquisition skill as Flicking but removes the time pressure, helping you build a cleaner click habit.";
            if (r.Scenario == "Tracking" && r.Accuracy < 55)
                return $"Stay on {r.Difficulty} Tracking but cut the duration to 30 seconds. Shorter sessions help you stay focused for the whole drill instead of fading.";
            if (c.WeakArea == "streak")
                return $"Try Switching on {r.Difficulty} — it specifically trains target-to-target transitions which directly improves your streak consistency.";
            if (!c.IsFirstSession && c.AccuracyDelta > 5)
                return $"Repeat {r.Scenario} at {r.Difficulty} to reinforce today's improvement — back-to-back sessions on the same scenario lock in gains faster.";
            return $"Run {r.Scenario} again at {r.Difficulty} — consistency in the same scenario builds the muscle memory that transfers directly to your game.";
        }

        private static string GetMotivation(AimTrainerResult r, CoachContext c)
        {
            // Rotate variants using session count so same line never repeats consecutively
            int v = c.TotalSessionsAll % 4;

            if (c.NewAccuracyRecord || c.NewReactionRecord)
                return v switch {
                    0 => "Personal records don't happen by accident — you earned this one.",
                    1 => "That's a new benchmark. Now you know what you're capable of.",
                    2 => "Records are proof the work is paying off. Keep showing up.",
                    _ => "New personal best. That number is yours to beat now."
                };

            if (c.IsImproving && c.SessionCount >= 5)
                return v switch {
                    0 => $"Session {c.SessionCount} in the books — the trend is real and it's pointing up.",
                    1 => $"{c.SessionCount} sessions logged and the improvement is measurable. Stay consistent.",
                    2 => "The improvement is measurable and it's real. Trust the process.",
                    _ => $"Upward trend confirmed across {c.SessionCount} sessions. Don't break the streak."
                };

            if (c.AccuracyGrade == "elite")
                return v switch {
                    0 => "You're performing at a level most players never reach. Keep showing up.",
                    1 => "Elite accuracy isn't luck — it's reps. You're putting them in.",
                    2 => "Top tier performance. The gap between you and average players is widening.",
                    _ => "That accuracy puts you ahead of the vast majority. Maintain it."
                };

            if (c.IsFirstSession)
                return v switch {
                    0 => "First session is just the start. Come back tomorrow and you'll already be better.",
                    1 => "Baseline set. Everything from here is measurable improvement.",
                    2 => "Day one done. The players who improve are the ones who come back for day two.",
                    _ => "First session logged. Now you have something to beat."
                };

            if (c.AccuracyDelta < -5)
                return v switch {
                    0 => "Bad sessions are data, not failure. You showed up — that's what separates players who improve from players who don't.",
                    1 => "Off sessions happen to everyone. What matters is you tracked it and came back.",
                    2 => "The dip is logged. That's how you find the pattern — show up again tomorrow.",
                    _ => "One bad session doesn't define the trend. Your history is longer than today."
                };

            if (c.TotalSessionsAll >= 10)
                return v switch {
                    0 => $"{c.TotalSessionsAll} sessions logged across all scenarios. Your overall average accuracy is {c.OverallAvgAccuracy:F0}% — that's your real benchmark.",
                    1 => $"Double digits in. At {c.TotalSessionsAll} sessions your data is starting to tell a real story.",
                    2 => $"{c.TotalSessionsAll} sessions tracked. The improvement is in the data whether you feel it today or not.",
                    _ => $"Your {c.TotalSessionsAll}-session history is your actual performance record. Trust it over any single session."
                };

            if (c.SessionCount >= 10)
                return v switch {
                    0 => $"{c.SessionCount} sessions tracked. The players who log 10+ sessions are the ones who actually improve — you're one of them.",
                    1 => $"10+ sessions in this scenario. Muscle memory is building whether you notice it yet or not.",
                    2 => "Consistency over 10 sessions is rarer than people think. You're in the group that improves.",
                    _ => $"{c.SessionCount} sessions and still showing up. That's the whole game."
                };

            return v switch {
                0 => "Every drill is a deposit in the bank. It adds up faster than you think.",
                1 => "Showing up consistently is the actual skill. You're building it.",
                2 => "Progress in aim training is slow and then suddenly obvious. Keep going.",
                _ => "The session is logged. That's one more data point working in your favor."
            };
        }
    }
}
