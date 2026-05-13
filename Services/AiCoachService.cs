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
            public static double AccuracyElite(string s) => s == "Precision" ? 88 : s == "Tracking" ? 75 : 85;
            public static double AccuracyGood(string s)  => s == "Precision" ? 72 : s == "Tracking" ? 60 : 70;
            public static double AccuracyAvg(string s)   => s == "Precision" ? 55 : s == "Tracking" ? 45 : 55;

            public const double ReactionElite   = 220;
            public const double ReactionGood    = 350;
            public const double ReactionAverage = 500;

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
            return GenerateReport(result, context);
        }

        // ── Context builder ───────────────────────────────────────────
        private record CoachContext(
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

            string reactGrade = r.AvgReactionMs <= Bench.ReactionElite   ? "elite"
                              : r.AvgReactionMs <= Bench.ReactionGood     ? "good"
                              : r.AvgReactionMs <= Bench.ReactionAverage  ? "average"
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
                list.Add($"Your {r.Accuracy:F0}% accuracy is elite level — most competitive players hover around {Bench.AccuracyGood(r.Scenario):F0}%. You're well above that.");
            else if (c.AccuracyGrade == "good")
                list.Add($"{r.Accuracy:F0}% accuracy puts you in the good range for {r.Scenario}. You're making more right decisions than wrong ones.");
            else if (c.IsImproving && c.AccuracyDelta > 3)
                list.Add($"Accuracy improved {c.AccuracyDelta:+0.0}% from your last session — that's real, measurable progress.");

            if (c.ReactionGrade == "elite")
                list.Add($"Your {r.AvgReactionMs:F0}ms average reaction is elite level — most players are 150-200ms slower than this.");
            else if (c.ReactionGrade == "good")
                list.Add($"{r.AvgReactionMs:F0}ms average reaction is competitive. You're in the range where pros operate.");
            else if (!c.IsFirstSession && c.ReactionDelta < -20)
                list.Add($"Reaction time improved {Math.Abs(c.ReactionDelta):F0}ms faster than last session — your reads are getting sharper.");

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

            switch (c.WeakArea)
            {
                case "accuracy":
                    // Check for sensitivity root cause first — most valuable diagnosis
                    if (c.TrackerCmPer360.HasValue && r.Scenario == "Precision")
                    {
                        if (c.TrackerCmPer360.Value < 20)
                        {
                            list.Add($"Your sensitivity is {c.TrackerCmPer360.Value:F1}cm/360 — that's too low for Precision. " +
                                     "At that range you physically cannot make the micro-adjustments small targets require. " +
                                     "This is a settings problem, not a skill problem. Use the Recommend screen to find your optimal range.");
                            break;
                        }
                        else if (c.TrackerCmPer360.Value > 55)
                        {
                            list.Add($"Your sensitivity is {c.TrackerCmPer360.Value:F1}cm/360 — that's too high for Precision. " +
                                     "You are overshooting small targets because the cursor moves too far per hand movement. " +
                                     "Lower your sensitivity and retest.");
                            break;
                        }
                    }
                    if (c.TrackerCmPer360.HasValue && r.Scenario == "Tracking")
                    {
                        if (c.TrackerCmPer360.Value < 15)
                        {
                            list.Add($"Your sensitivity is {c.TrackerCmPer360.Value:F1}cm/360 — too low to smoothly follow moving targets. " +
                                     "Tracking rewards fluid wrist movement, which is nearly impossible at this sensitivity range.");
                            break;
                        }
                        else if (c.TrackerCmPer360.Value > 65)
                        {
                            list.Add($"Your sensitivity is {c.TrackerCmPer360.Value:F1}cm/360 — too high for consistent Tracking. " +
                                     "Small hand movements translate to large cursor jumps, making smooth tracking very difficult.");
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
                        list.Add($"Your reaction time looks slow at {r.AvgReactionMs:F0}ms but your correction sharpness score " +
                                 $"({c.TrackerCorrectionSharpness.Value:F0}) suggests you are overshooting and correcting — " +
                                 "that adds 80-150ms artificially. The fix is smoother initial movement, not faster reactions. " +
                                 "Focus on landing on the target in one motion instead of correcting after overshoot.");
                    }
                    else
                    {
                        list.Add(c.ReactionGrade == "slow"
                            ? $"Your {r.AvgReactionMs:F0}ms average reaction is on the slower side. Focus less on speed and more on predicting target movement — anticipation is faster than reaction."
                            : $"The gap between your best reaction ({r.BestReactionMs:F0}ms) and average ({r.AvgReactionMs:F0}ms) is {r.AvgReactionMs - r.BestReactionMs:F0}ms — your ceiling is higher than your average suggests.");
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
                list.Add($"Your smoothness score from your last tracker session was {c.TrackerSmoothness.Value:F0}/100. " +
                         "That level of jitter makes consistent tracking physically harder — " +
                         "check your grip, surface friction, and make sure your mousepad is clean and flat.");
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
                        tips.Add($"Your primary fix is sensitivity — at {c.TrackerCmPer360.Value:F1}cm/360 you need more cm/360 " +
                                 "to make precise micro-adjustments. Use the Recommend screen to find your target range, " +
                                 "then retest Precision at the new setting before drawing conclusions about your skill level.");
                    }
                    else if (c.TrackerCmPer360.HasValue && c.TrackerCmPer360.Value > 55)
                    {
                        tips.Add($"Lower your sensitivity first — at {c.TrackerCmPer360.Value:F1}cm/360 small targets will always feel " +
                                 "unpredictable. Get into the 25-50cm range and your Precision accuracy will improve immediately.");
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
                tips.Add($"Try Hard or Nightmare difficulty — challenging yourself at {r.Accuracy:F0}% accuracy on {r.Difficulty} means you're ready for the next level.");
            else if (c.ReactionGrade == "slow")
                tips.Add("Instead of trying to react faster, work on predicting — watch the target's pattern and move your cursor before it arrives, not after.");
            else if (c.IsImproving)
                tips.Add($"You've improved {Math.Abs(c.AccuracyDelta):F0}% in accuracy recently — keep the same routine. When something is working, don't change it.");
            else if (!c.IsFirstSession && c.AccuracyDelta < -5)
                tips.Add("A dip in performance is normal. Check if your sensitivity feels right today — sometimes a slight DPI or sens change explains a sudden dip.");
            else
                tips.Add("Do 3 warm-up drills at Easy before your main session. Cold muscles make cold aim — warming up moves your average up more than any other single habit.");

            if (c.SessionCount < 5)
                tips.Add("Track 3 sessions per week consistently. At 10 sessions in the same scenario you'll start seeing clear trend lines in your history.");
            else if (c.IsConsistent)
                tips.Add("Your consistency is excellent. The next step is deliberately pushing your ceiling — one session per week at a difficulty that feels uncomfortable.");
            else
                tips.Add("Aim training works best alongside your actual game time, not instead of it. 10 minutes of drills before a game session produces better results than long standalone sessions.");

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
