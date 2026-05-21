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
                _           => 82,
            };

            public static double AccuracyGood(string s) => s switch
            {
                "Tracking"  => 65,
                "Precision" => 72,
                "Flicking"  => 68,
                "Switching" => 65,
                "Adaptive"  => 65,
                _           => 68,
            };

            public static double AccuracyAvg(string s) => s switch
            {
                "Tracking"  => 50,
                "Precision" => 55,
                "Flicking"  => 52,
                "Switching" => 50,
                "Adaptive"  => 50,
                _           => 52,
            };

            // Reaction — scenario-specific thresholds
            // Flicking rewards fast reactions (tightest). Precision is a patience test (loosest).
            public static double ReactionElite(string s) => s switch
            {
                "Flicking"  => 200,
                "Switching" => 220,
                "Tracking"  => 280,
                "Precision" => 350,
                "Adaptive"  => 250,
                _           => 220,
            };

            public static double ReactionGood(string s) => s switch
            {
                "Flicking"  => 300,
                "Switching" => 350,
                "Tracking"  => 420,
                "Precision" => 500,
                "Adaptive"  => 380,
                _           => 350,
            };

            public static double ReactionAverage(string s) => s switch
            {
                "Flicking"  => 450,
                "Switching" => 500,
                "Tracking"  => 580,
                "Precision" => 650,
                "Adaptive"  => 520,
                _           => 500,
            };

            public const int StreakElite   = 12;
            public const int StreakGood    = 6;
            public const int StreakAverage = 3;
        }

        // ── Public entry point ────────────────────────────────────────
        public static AiCoachReport Analyze(
            AimTrainerResult result,
            List<AimTrainerResult> history,
            SessionSummary? recentTrackerSession = null)
        {
            var context = BuildContext(result, history, recentTrackerSession);
            var report  = GenerateReport(result, context);
            var prescriptions = DrillPrescriptionEngine.Prescribe(result, context, recentTrackerSession);
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

        private static CoachContext BuildContext(
            AimTrainerResult r,
            List<AimTrainerResult> history,
            SessionSummary? tracker = null)
        {
            // Extract tracker data — only use if session was long enough to be reliable
            double? trackerCmPer360            = null;
            double? trackerSmoothness          = null;
            double? trackerCorrectionSharpness = null;

            if (tracker != null && tracker.SessionSeconds >= 45)
            {
                trackerCmPer360            = tracker.CmPer360 > 0 ? tracker.CmPer360 : (double?)null;
                trackerSmoothness          = tracker.SmoothnessScore;
                trackerCorrectionSharpness = tracker.CorrectionSharpness;
            }

            var same = history
                .Where(h => h.Scenario == r.Scenario && h.Timestamp != r.Timestamp)
                .OrderByDescending(h => h.Timestamp)
                .ToList();

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
                ["reaction"]  = 100 - Math.Min(100, r.AvgReactionMs / 6.0),
                ["streak"]    = Math.Min(100, r.MaxStreak * 7.0),
                ["endurance"] = r.Misses == 0 ? 100 : 100.0 * r.Hits / (r.Hits + r.Misses)
            };

            string weak   = areas.OrderBy(a => a.Value).First().Key;
            string strong = areas.OrderByDescending(a => a.Value).First().Key;

            // Cross-scenario context from ALL history (last 10 sessions)
            var allRecent = history
                .Where(h => h.Timestamp != r.Timestamp)
                .OrderByDescending(h => h.Timestamp)
                .Take(10)
                .ToList();

            double overallAvgAccuracy = allRecent.Count > 0 ? allRecent.Average(h => h.Accuracy) : r.Accuracy;
            double overallAvgReaction = allRecent.Count > 0 ? allRecent.Average(h => h.AvgReactionMs) : r.AvgReactionMs;
            int    totalSessionsAll   = history.Count;

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
        /// Rotates through variants by session count so the same line never
        /// repeats in consecutive sessions.
        /// </summary>
        private static string Pick(int sessionIndex, params string[] variants)
            => variants[sessionIndex % variants.Length];

        // ── Report generator ──────────────────────────────────────────
        private static AiCoachReport GenerateReport(AimTrainerResult r, CoachContext c) => new()
        {
            OverallRating       = GetRating(r, c),
            Headline            = GetHeadline(r, c),
            Strengths           = GetStrengths(r, c),
            Weaknesses          = GetWeaknesses(r, c),
            Advice              = GetAdvice(r, c),
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

        private static string GetHeadline(AimTrainerResult r, CoachContext c)
        {
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

        private static List<string> GetStrengths(AimTrainerResult r, CoachContext c)
        {
            var list = new List<string>();

            if (!c.IsFirstSession && c.TotalSessionsAll >= 5 && r.Accuracy > c.OverallAvgAccuracy + 5)
                list.Add($"This session's {r.Accuracy:F0}% accuracy is {(r.Accuracy - c.OverallAvgAccuracy):F0}% above your overall average of {c.OverallAvgAccuracy:F0}% — your best relative performance recently.");

            if (c.AccuracyGrade == "elite")
                list.Add(Pick(c.TotalSessionsAll,
                    $"Your {r.Accuracy:F0}% accuracy is elite level — most competitive players hover around {Bench.AccuracyGood(r.Scenario):F0}%. You're well above that.",
                    $"{r.Accuracy:F0}% accuracy puts you in the top tier. That's not luck — that's reps paying off.",
                    $"Elite accuracy at {r.Accuracy:F0}%. The consistency is the impressive part — anyone can have a good session, fewer can repeat it.",
                    $"Your {r.Accuracy:F0}% accuracy is the kind of number that shows up in ranked lobbies. Keep building on it."
                ));
            else if (c.AccuracyGrade == "good")
                list.Add(Pick(c.TotalSessionsAll,
                    $"{r.Accuracy:F0}% accuracy puts you in the good range for {r.Scenario}. You're making more right decisions than wrong ones.",
                    $"Solid {r.Accuracy:F0}% accuracy in {r.Scenario}. The fundamentals are there — now build consistency.",
                    $"{r.Accuracy:F0}% is a good number for {r.Scenario}. You're above average and trending in the right direction.",
                    $"Good accuracy at {r.Accuracy:F0}%. You're clicking with intent — that's the foundation everything else builds on."
                ));
            else if (c.IsImproving && c.AccuracyDelta > 3)
                list.Add(Pick(c.TotalSessionsAll,
                    $"Accuracy improved {c.AccuracyDelta:+0.0}% from your last session — that's real, measurable progress.",
                    $"Up {c.AccuracyDelta:F0}% accuracy since last session. The trajectory is pointing up.",
                    $"Accuracy trend: +{c.AccuracyDelta:F0}% from last time. Every improvement stacks.",
                    $"Your accuracy is climbing — {c.AccuracyDelta:F0}% better than last session. That's the definition of improving."
                ));

            if (c.ReactionGrade == "elite")
                list.Add(Pick(c.TotalSessionsAll,
                    $"Your {r.AvgReactionMs:F0}ms average reaction is elite level — most players are 150-200ms slower than this.",
                    $"{r.AvgReactionMs:F0}ms reaction time is genuinely fast. You're in the range where raw speed becomes an advantage.",
                    $"Elite reaction at {r.AvgReactionMs:F0}ms average. Your reads are translating to clicks — that's the hard part.",
                    $"{r.AvgReactionMs:F0}ms average. At this speed, target acquisition is a real strength, not just a stat."
                ));
            else if (c.ReactionGrade == "good")
                list.Add(Pick(c.TotalSessionsAll,
                    $"{r.AvgReactionMs:F0}ms average reaction is competitive. You're in the range where pros operate.",
                    $"Good reaction time at {r.AvgReactionMs:F0}ms average. Speed isn't the bottleneck — build on this.",
                    $"{r.AvgReactionMs:F0}ms puts your reactions in a solid range. Consistent performance at this speed is what separates good from great.",
                    $"Reaction time: {r.AvgReactionMs:F0}ms average — that's genuinely competitive. Use it."
                ));
            else if (!c.IsFirstSession && c.ReactionDelta < -20)
            {
                if (c.ReactionGrade == "slow")
                    list.Add($"Reaction time improved {Math.Abs(c.ReactionDelta):F0}ms from last session — real progress. Your average is still {r.AvgReactionMs:F0}ms, so there's more room to go, but the direction is right.");
                else
                    list.Add($"Reaction time improved {Math.Abs(c.ReactionDelta):F0}ms faster than last session — your reads are getting sharper.");
            }

            if (c.StreakGrade == "elite")
                list.Add($"A streak of {r.MaxStreak} is exceptional — it shows you can maintain focus and rhythm under pressure.");
            else if (c.StreakGrade == "good")
                list.Add($"Your {r.MaxStreak}-hit streak shows you have the ability to lock in when it counts.");

            if (c.IsConsistent)
                list.Add("Your scores are consistent across your last few sessions — consistency is the foundation of improvement.");

            if (c.NewAccuracyRecord)
                list.Add($"This is your best accuracy ever in {r.Scenario} — {r.Accuracy:F0}%. That benchmark is now yours to beat.");

            if (list.Count == 0)
            {
                list.Add(r.Hits > r.Misses
                    ? $"You hit more than you missed — {r.Hits} hits vs {r.Misses} misses. That's the foundation to build on."
                    : $"You completed a full {r.DurationSeconds}-second drill. Every session builds muscle memory, even the tough ones.");
            }

            return list.Take(2).ToList();
        }

        private static List<string> GetWeaknesses(AimTrainerResult r, CoachContext c)
        {
            var list = new List<string>();

            // ── Multi-signal combinations — checked first, return early if matched ──

            // Combination A: High correction sharpness + slow reaction
            // Diagnosis: overshoot-correct cycle artificially inflating reaction time
            if (c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value > 60
                && c.ReactionGrade == "slow"
                && (r.Scenario == "Flicking" || r.Scenario == "Switching"))
            {
                list.Add(Pick(c.TotalSessionsAll,
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
                list.Add(Pick(c.TotalSessionsAll,
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
                list.Add(Pick(c.TotalSessionsAll,
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
                            reactionMsg = $"The gap between your best reaction ({r.BestReactionMs:F0}ms) and average ({r.AvgReactionMs:F0}ms) is {r.AvgReactionMs - r.BestReactionMs:F0}ms — your ceiling is higher than your average suggests.";
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

        private static List<string> GetAdvice(AimTrainerResult r, CoachContext c)
        {
            var tips = new List<string>();

            switch (r.Scenario)
            {
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
                    if (c.TrackerCmPer360.HasValue && c.TrackerCmPer360.Value < 20)
                    {
                        tips.Add("Your primary fix is to lower your in-game sensitivity — micro-adjustments on small targets are " +
                                 "physically very hard at high sensitivity. Use the Recommend screen to find your target range, " +
                                 "then retest Precision at the new setting before drawing conclusions about your skill level.");
                    }
                    else if (c.TrackerCmPer360.HasValue && c.TrackerCmPer360.Value > 55)
                    {
                        tips.Add("Lower your in-game sensitivity first — at very low sensitivity small targets " +
                                 "will always feel unpredictable. Bring it into a more controlled range and your Precision accuracy will improve immediately.");
                    }
                    else
                    {
                        tips.Add(r.Accuracy < 75
                            ? "Treat Precision like a patience drill. Hover over the target for a half-second before clicking — the slowdown is worth the accuracy gain."
                            : "Maintain this accuracy while trying to reduce your reaction time. Precise and fast is the goal — you already have precise.");
                    }
                    break;
                case "Switching":
                    tips.Add(r.MaxStreak < 5
                        ? "Pre-aim the location where the next target will likely appear. In Switching, positioning your mouse early is faster than reacting late."
                        : "Add the Tracking scenario to your warmup — smooth movement between targets is what separates good Switching players from great ones.");
                    break;
                default:
                    tips.Add(c.AccuracyGrade is "developing" or "average"
                        ? "Accuracy over speed — every time. A slow hit is worth more than a fast miss. Trust your cursor."
                        : "You're performing well. The next level of improvement comes from reducing variance — aim for your best score, not just a good score.");
                    break;
            }

            if (c.AccuracyGrade == "elite")
                tips.Add(Pick(c.TotalSessionsAll,
                    $"Try Hard or Nightmare difficulty — challenging yourself at {r.Accuracy:F0}% accuracy on {r.Difficulty} means you're ready for the next level.",
                    $"{r.Accuracy:F0}% accuracy on {r.Difficulty} is a strong signal — step up the difficulty and find your real ceiling.",
                    $"You've outgrown {r.Difficulty} for now. Harder targets will expose gaps that {r.Difficulty} hides.",
                    $"At {r.Accuracy:F0}% you're coasting on {r.Difficulty}. The growth is in the next difficulty tier — go find it."
                ));
            else if (c.ReactionGrade == "slow")
                tips.Add(Pick(c.TotalSessionsAll,
                    "Instead of trying to react faster, work on predicting — watch the target's pattern and move your cursor before it arrives, not after.",
                    "Anticipation beats reaction every time. Before a target appears, think about where it is likely to go. Pre-aim that spot.",
                    "Reaction time training works best when you stop trying to be fast. Relax, read the pattern, and let the click happen naturally.",
                    "Your best reaction times happen when you stop thinking about reacting. Focus on the target's movement, not on clicking fast."
                ));
            else if (c.IsImproving)
                tips.Add(Pick(c.TotalSessionsAll,
                    $"You've improved {Math.Abs(c.AccuracyDelta):F0}% in accuracy recently — keep the same routine. When something is working, don't change it.",
                    "Improvement is happening — that means the current routine is working. Protect it from disruption.",
                    "The trend is up. The best thing you can do right now is show up consistently and let the progression continue.",
                    "Don't overthink it when you're improving. Same routine, same schedule, let the gains compound."
                ));
            else if (!c.IsFirstSession && c.AccuracyDelta < -5)
                tips.Add(Pick(c.TotalSessionsAll,
                    "A dip in performance is normal. Check if your sensitivity feels right today — sometimes a slight DPI or sens change explains a sudden dip.",
                    "Off sessions happen. Before your next session, check: same surface, same grip, same warmup? Small changes compound into big variance.",
                    "Performance variance is data. If this keeps happening, look at the conditions — time of day, warmup, sleep. Aim reacts to everything.",
                    "One bad session does not define the trend. Come back tomorrow with fresh hands and compare."
                ));
            else
                tips.Add(Pick(c.TotalSessionsAll,
                    "Do 3 warm-up drills at Easy before your main session. Cold muscles make cold aim — warming up moves your average up more than any other single habit.",
                    "Warmup before drilling. Easy mode for 3 minutes primes the neuromuscular connection. Cold hands produce cold aim.",
                    "If you're not warming up before drills, you're leaving 10-15% accuracy on the table. Easy mode for a few minutes first.",
                    "The first drill of a session is always your worst. Accept it, use it as warmup, and save your real performance for the second and third sessions."
                ));

            if (c.SessionCount < 5)
                tips.Add(Pick(c.TotalSessionsAll,
                    "Track 3 sessions per week consistently. At 10 sessions in the same scenario you'll start seeing clear trend lines in your history.",
                    "The first 10 sessions in a scenario are baseline building. Don't judge the trend until you have 10 data points.",
                    "Consistency matters more than volume right now. Show up 3 times per week and let the data build.",
                    "You need about 10 sessions before the trend line becomes meaningful. Keep logging."
                ));
            else if (c.IsConsistent)
                tips.Add(Pick(c.TotalSessionsAll,
                    "Your consistency is excellent. The next step is deliberately pushing your ceiling — one session per week at a difficulty that feels uncomfortable.",
                    "Consistent performance is valuable but can become a plateau. Introduce one uncomfortable session per week to keep the ceiling moving up.",
                    "You've built a solid floor. Now it's time to work on the ceiling — harder difficulty, shorter duration, higher pressure.",
                    "Consistency achieved. The next level requires deliberately breaking consistency to find and fix the gaps."
                ));
            else
                tips.Add(Pick(c.TotalSessionsAll,
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
                    2 => "The data shows you're getting better. Trust the process.",
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
