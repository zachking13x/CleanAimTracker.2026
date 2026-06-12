using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CleanAimTracker.Services
{
    public static class AchievementService
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CleanAimTracker", "achievements.json");

        public static List<Achievement> GetAllDefinitions() => new()
        {
            // TRAINING MILESTONES
            new() { Id = "first_drill",     Name = "First Blood",      Emoji = "🎯", Category = "Training",   Description = "Complete your first drill" },
            new() { Id = "ten_sessions",    Name = "Ten Down",         Emoji = "🔟", Category = "Training",   Description = "Complete 10 drills" },
            new() { Id = "25_sessions",     Name = "Committed",        Emoji = "💪", Category = "Training",   Description = "Complete 25 drills" },
            new() { Id = "50_sessions",     Name = "Dedicated",        Emoji = "🏋️", Category = "Training",   Description = "Complete 50 drills" },
            new() { Id = "100_sessions",    Name = "Century",          Emoji = "💯", Category = "Training",   Description = "Complete 100 drills" },

            // ACCURACY
            new() { Id = "accuracy_80",     Name = "Sharp",            Emoji = "🎖️", Category = "Accuracy",   Description = "Hit 80% accuracy in any drill" },
            new() { Id = "accuracy_90",     Name = "Precision",        Emoji = "🏅", Category = "Accuracy",   Description = "Hit 90% accuracy in any drill" },
            new() { Id = "accuracy_100",    Name = "Flawless",         Emoji = "✨", Category = "Accuracy",   Description = "Hit 100% accuracy in any drill" },
            new() { Id = "elite_grade",     Name = "Elite",            Emoji = "💎", Category = "Accuracy",   Description = "Receive an elite accuracy grade from the AI Coach" },
            new() { Id = "reaction_300",    Name = "Quick Draw",       Emoji = "🤠", Category = "Accuracy",   Description = "Achieve average reaction time under 300ms" },
            new() { Id = "reaction_220",    Name = "Lightning",        Emoji = "⚡", Category = "Accuracy",   Description = "Achieve average reaction time under 220ms" },

            // STREAKS
            new() { Id = "streak_3",        Name = "Hat Trick",        Emoji = "🔥", Category = "Streak",     Description = "Maintain a 3-day training streak" },
            new() { Id = "streak_7",        Name = "Week Warrior",     Emoji = "📅", Category = "Streak",     Description = "Maintain a 7-day training streak" },
            new() { Id = "streak_14",       Name = "Two Week Grind",   Emoji = "⚡", Category = "Streak",     Description = "Maintain a 14-day training streak" },
            new() { Id = "streak_30",       Name = "Iron Discipline",  Emoji = "🛡️", Category = "Streak",     Description = "Maintain a 30-day training streak" },
            new() { Id = "max_streak_20",   Name = "Unstoppable",      Emoji = "🏆", Category = "Streak",     Description = "Hit a streak of 20 within a single drill" },
            new() { Id = "max_streak_50",   Name = "God Mode",         Emoji = "👑", Category = "Streak",     Description = "Hit a streak of 50 within a single drill" },

            // MASTERY
            new() { Id = "all_scenarios",   Name = "Well Rounded",     Emoji = "🌐", Category = "Mastery",    Description = "Complete a drill in every scenario" },
            new() { Id = "nightmare_clear", Name = "Nightmare Mode",   Emoji = "👹", Category = "Mastery",    Description = "Complete any drill on Nightmare difficulty" },
            new() { Id = "tracking_elite",  Name = "Tracker",          Emoji = "👁️", Category = "Mastery",    Description = "Hit elite accuracy in Tracking" },
            new() { Id = "flicking_elite",  Name = "Flickshot",        Emoji = "⚡", Category = "Mastery",    Description = "Hit elite accuracy in Flicking" },
            new() { Id = "precision_elite", Name = "Sniper",           Emoji = "🎯", Category = "Mastery",    Description = "Hit elite accuracy in Precision" },

            // PROGRESSION
            new() { Id = "reach_bronze",    Name = "Bronze",           Emoji = "🥉", Category = "Dedication", Description = "Reach Bronze tier" },
            new() { Id = "reach_silver",    Name = "Silver",           Emoji = "🥈", Category = "Dedication", Description = "Reach Silver tier" },
            new() { Id = "reach_gold",      Name = "Gold",             Emoji = "🥇", Category = "Dedication", Description = "Reach Gold tier" },
            new() { Id = "reach_elite",     Name = "Elite Tier",       Emoji = "💎", Category = "Dedication", Description = "Reach Elite tier" },

            // DAILY CHALLENGE
            new() { Id = "first_challenge", Name = "Challenger",       Emoji = "📋", Category = "Training",   Description = "Complete your first daily challenge" },
            new() { Id = "challenge_7",     Name = "Weekly Warrior",   Emoji = "🗓️", Category = "Dedication", Description = "Complete 7 daily challenges" },
            new() { Id = "challenge_30",    Name = "Monthly Grind",    Emoji = "📆", Category = "Dedication", Description = "Complete 30 daily challenges" },

            // TASK-30: New scenario mastery achievements
            new() { Id = "air_elite",        Name = "Airborne",         Emoji = "✈️", Category = "Mastery",    Description = "Hit elite accuracy in Air Tracking" },
            new() { Id = "peek_master",      Name = "Peek Master",      Emoji = "👀", Category = "Mastery",    Description = "Achieve 80%+ accuracy in Peek Training on any difficulty" },
            new() { Id = "full_assessment",  Name = "Fully Assessed",   Emoji = "📊", Category = "Mastery",    Description = "Complete all 8 assessment tests" },
            new() { Id = "reactive_sub100",  Name = "Reflex Arc",       Emoji = "⚡", Category = "Accuracy",   Description = "Achieve average reaction time under 100ms in Reactive" },
            new() { Id = "straight_shot",    Name = "Straight Shot",    Emoji = "📏", Category = "Mastery",    Description = "Achieve 90%+ path efficiency in any drill" },
            new() { Id = "all_scenarios_v2", Name = "Full Rotation",    Emoji = "🌐", Category = "Mastery",    Description = "Complete a drill in all 12 scenarios (including new pillar scenarios)" },
        };

        public static List<Achievement> LoadUnlocked()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<List<Achievement>>(json) ?? new();
            }
            catch { return new(); }
        }

        private static void SaveUnlocked(List<Achievement> unlocked)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

                // Atomic write: write to .tmp then replace so a crash mid-write
                // never corrupts the achievements file.
                string json    = JsonSerializer.Serialize(unlocked, new JsonSerializerOptions { WriteIndented = true });
                string tmpPath = FilePath + ".tmp";
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, FilePath, overwrite: true);
            }
            catch { }
        }

        // TASK-0.1: minimum real drills before a tier is evaluated — a tier is an
        // average; one drill is not an average.
        public const int MinDrillsForTier = 5;

        public static List<Achievement> EvaluateAfterSession(
            AimTrainerResult result,
            List<AimTrainerResult> allResults,
            int currentStreakDays,
            int challengesCompleted = 0)
        {
            var unlocked = LoadUnlocked();
            var newlyUnlocked = EvaluateCore(
                result, allResults, currentStreakDays, challengesCompleted,
                unlocked.Select(a => a.Id).ToHashSet());

            if (newlyUnlocked.Count > 0)
            {
                unlocked.AddRange(newlyUnlocked);
                SaveUnlocked(unlocked);
            }
            return newlyUnlocked;
        }

        /// <summary>
        /// TASK-0.1: pure evaluation core — no disk I/O, unit-testable.
        /// Calibration/assessment sessions are excluded entirely: the evaluated
        /// session never earns achievements, and stored assessment results never
        /// count toward totals, tiers, or scenario coverage. (This was the root
        /// cause of the 8-achievement cascade: the first real drill after
        /// calibration was evaluated against allResults polluted with the four
        /// calibration sessions.)
        /// </summary>
        public static List<Achievement> EvaluateCore(
            AimTrainerResult result,
            List<AimTrainerResult> allResults,
            int currentStreakDays,
            int challengesCompleted,
            HashSet<string> unlockedIds)
        {
            var definitions   = GetAllDefinitions();
            var newlyUnlocked = new List<Achievement>();

            // Calibration sessions are baseline data, not accomplishments.
            if (result.IsAssessmentSession) return newlyUnlocked;
            allResults = (allResults ?? new List<AimTrainerResult>())
                .Where(r => !r.IsAssessmentSession)
                .ToList();

            void TryUnlock(string id)
            {
                if (unlockedIds.Contains(id)) return;
                var def = definitions.FirstOrDefault(d => d.Id == id);
                if (def == null) return;
                def.IsUnlocked = true;
                def.UnlockedAt = DateTime.UtcNow;
                newlyUnlocked.Add(def);
                unlockedIds.Add(id);
            }

            int total = allResults.Count;

            if (total >= 1)   TryUnlock("first_drill");
            if (total >= 10)  TryUnlock("ten_sessions");
            if (total >= 25)  TryUnlock("25_sessions");
            if (total >= 50)  TryUnlock("50_sessions");
            if (total >= 100) TryUnlock("100_sessions");

            if (result.Accuracy >= 80)  TryUnlock("accuracy_80");
            if (result.Accuracy >= 90)  TryUnlock("accuracy_90");
            if (result.Accuracy >= 100) TryUnlock("accuracy_100");

            // Elite threshold matches AiCoachService.Bench.AccuracyElite() so the
            // achievement fires exactly when the coach would grade the session "Excellent".
            double eliteThreshold = result.Scenario switch
            {
                "Tracking"  => 80,
                "Flicking"  => 82,
                "Precision" => 88,
                "Switching" => 80,
                "Adaptive"  => 80,
                "Sniper"    => 90,
                "Shotgun"   => 80,
                "SmgAr"     => 82,
                _           => 82,
            };
            if (result.Accuracy >= eliteThreshold) TryUnlock("elite_grade");

            // TASK-0.3: reaction badges only for scenarios that measure true
            // stimulus-to-hit reaction — a 240ms time-per-target in Tracking is
            // not a 240ms reaction.
            if (ReactionMetric.IsTrueReaction(result.Scenario))
            {
                if (result.AvgReactionMs > 0 && result.AvgReactionMs < 300) TryUnlock("reaction_300");
                if (result.AvgReactionMs > 0 && result.AvgReactionMs < 220) TryUnlock("reaction_220");
            }

            if (result.MaxStreak >= 20) TryUnlock("max_streak_20");
            if (result.MaxStreak >= 50) TryUnlock("max_streak_50");

            if (result.Difficulty == "Nightmare") TryUnlock("nightmare_clear");

            if (result.Accuracy >= 85)
            {
                switch (result.Scenario)
                {
                    case "Tracking":  TryUnlock("tracking_elite");  break;
                    case "Flicking":  TryUnlock("flicking_elite");  break;
                    case "Precision": TryUnlock("precision_elite"); break;
                }
            }

            var scenariosTried = allResults.Select(r => r.Scenario).Distinct().ToHashSet();
            if (scenariosTried.Contains("Tracking")  && scenariosTried.Contains("Flicking") &&
                scenariosTried.Contains("Precision") && scenariosTried.Contains("Switching") &&
                scenariosTried.Contains("Sniper")    && scenariosTried.Contains("Shotgun") &&
                scenariosTried.Contains("SmgAr"))
                TryUnlock("all_scenarios");

            // ── TASK-30: New scenario mastery achievements ────────────────────
            // air_elite: AirTracking elite accuracy threshold
            if (result.Scenario == "AirTracking" && result.Accuracy >= 78)
                TryUnlock("air_elite");

            // peek_master: 80%+ accuracy in PeekTraining
            if (result.Scenario == "PeekTraining" && result.Accuracy >= 80)
                TryUnlock("peek_master");

            // full_assessment: all 8 tests completed (DiagnosticHistory has at least one profile)
            // Evaluated via settings rather than result — check via overload that accepts UserSettings
            // (basic version: unlock after any result if DiagnosticHistory is non-empty)

            // reactive_sub100: under 100ms reaction in Reactive scenario
            if (result.Scenario == "Reactive" && result.AvgReactionMs > 0 && result.AvgReactionMs < 100)
                TryUnlock("reactive_sub100");

            // straight_shot: 90%+ path efficiency in any drill
            if (result.PathEfficiency >= 0.90)
                TryUnlock("straight_shot");

            // all_scenarios_v2: completed all 12 (legacy 7 + new 5)
            bool triedAll12 =
                scenariosTried.Contains("Tracking")       && scenariosTried.Contains("Flicking") &&
                scenariosTried.Contains("Precision")      && scenariosTried.Contains("Switching") &&
                scenariosTried.Contains("Sniper")         && scenariosTried.Contains("Shotgun") &&
                scenariosTried.Contains("SmgAr")          &&
                scenariosTried.Contains("StaticClicking") && scenariosTried.Contains("DynamicClicking") &&
                scenariosTried.Contains("Reactive")       &&
                scenariosTried.Contains("AirTracking")    && scenariosTried.Contains("PeekTraining");
            if (triedAll12)
                TryUnlock("all_scenarios_v2");

            if (currentStreakDays >= 3)  TryUnlock("streak_3");
            if (currentStreakDays >= 7)  TryUnlock("streak_7");
            if (currentStreakDays >= 14) TryUnlock("streak_14");
            if (currentStreakDays >= 30) TryUnlock("streak_30");

            // ── TASK-0.1: tier achievements ───────────────────────────────────
            // Tiers are sequential states — the user holds exactly ONE current
            // tier. Reaching Gold awards Gold on the session that crosses the
            // threshold; it does not retroactively dump Bronze and Silver too.
            // A tier is an average, so it needs a real sample first.
            if (allResults.Count >= MinDrillsForTier)
            {
                double avgAccuracy = allResults.Average(r => r.Accuracy);
                string tierName = avgAccuracy >= 82 ? "Elite"
                                : avgAccuracy >= 70 ? "Gold"
                                : avgAccuracy >= 55 ? "Silver"
                                : avgAccuracy >= 40 ? "Bronze"
                                : "Rookie";

                string tierAchievement = tierName switch
                {
                    "Bronze" => "reach_bronze",
                    "Silver" => "reach_silver",
                    "Gold"   => "reach_gold",
                    "Elite"  => "reach_elite",
                    _        => ""
                };
                if (tierAchievement.Length > 0) TryUnlock(tierAchievement);
            }

            if (challengesCompleted >= 1)  TryUnlock("first_challenge");
            if (challengesCompleted >= 7)  TryUnlock("challenge_7");
            if (challengesCompleted >= 30) TryUnlock("challenge_30");

            return newlyUnlocked;
        }

        /// <summary>
        /// TASK-0.1: toast rate-limit — at most 2 unlock toasts per session.
        /// Previously queued toasts show first (oldest debt first); everything
        /// beyond 2 is queued for the next session. Achievements themselves are
        /// always unlocked immediately — only the celebration is paced.
        /// </summary>
        public static (List<Achievement> Show, List<string> Queue) SplitToasts(
            List<string> pendingIds,
            List<Achievement> newlyUnlocked)
        {
            const int MaxToastsPerSession = 2;

            var definitions = GetAllDefinitions();
            var all = new List<Achievement>();

            foreach (var id in pendingIds ?? new List<string>())
            {
                var def = definitions.FirstOrDefault(d => d.Id == id);
                if (def != null && all.All(a => a.Id != id))
                {
                    def.IsUnlocked = true;
                    all.Add(def);
                }
            }
            foreach (var a in newlyUnlocked ?? new List<Achievement>())
                if (all.All(x => x.Id != a.Id))
                    all.Add(a);

            return (all.Take(MaxToastsPerSession).ToList(),
                    all.Skip(MaxToastsPerSession).Select(a => a.Id).ToList());
        }

        /// <summary>
        /// Overload that also accepts UserSettings so full_assessment can be checked.
        /// TASK-30.
        /// </summary>
        public static List<Achievement> EvaluateAfterSession(
            AimTrainerResult result,
            List<AimTrainerResult> allResults,
            int currentStreakDays,
            UserSettings settings,
            int challengesCompleted = 0)
        {
            // Run the base evaluation first
            var newly = EvaluateAfterSession(result, allResults, currentStreakDays, challengesCompleted);

            // full_assessment: user has completed at least one DiagnosticProfile where all 8 scores are set
            if (settings.DiagnosticHistory.Any(p =>
                    p.CloseRangeStatic > 0 && p.LongRangeStatic > 0 &&
                    p.HorizontalTracking > 0 && p.VerticalTracking > 0 &&
                    p.DiagonalTracking > 0 && p.CloseSwitching > 0 &&
                    p.FarSwitching > 0 && p.PeekReaction > 0))
            {
                var unlocked    = LoadUnlocked();
                var unlockedIds = unlocked.Select(a => a.Id).ToHashSet();
                if (!unlockedIds.Contains("full_assessment"))
                {
                    var def = GetAllDefinitions().FirstOrDefault(d => d.Id == "full_assessment");
                    if (def != null)
                    {
                        def.IsUnlocked = true;
                        def.UnlockedAt = DateTime.UtcNow;
                        unlocked.Add(def);
                        newly.Add(def);
                        SaveUnlocked(unlocked);
                    }
                }
            }

            return newly;
        }

        public static List<Achievement> GetAllWithStatus()
        {
            var unlocked    = LoadUnlocked();
            var unlockedMap = unlocked.ToDictionary(a => a.Id);
            var definitions = GetAllDefinitions();
            foreach (var def in definitions)
            {
                if (unlockedMap.TryGetValue(def.Id, out var u))
                {
                    def.IsUnlocked = true;
                    def.UnlockedAt = u.UnlockedAt;
                }
            }
            return definitions;
        }

        public static int GetUnlockedCount() => LoadUnlocked().Count;
        public static int GetTotalCount()    => GetAllDefinitions().Count;

        /// <summary>
        /// Returns a single near-miss hint string if the result came close to unlocking
        /// an achievement the user hasn't earned yet, or null if nothing is close.
        /// Priority: accuracy > reaction > streak.
        /// </summary>
        public static string? GetNearMissHint(AimTrainerResult result, List<Achievement> unlocked)
        {
            var ids = unlocked.Select(a => a.Id).ToHashSet();

            if (!ids.Contains("accuracy_100") && result.Accuracy >= 95 && result.Accuracy < 100)
                return $"⚡  {(100 - result.Accuracy):F0}% more accuracy away from Flawless";

            if (!ids.Contains("accuracy_90") && result.Accuracy >= 85 && result.Accuracy < 90)
                return $"🎯  {(90 - result.Accuracy):F0}% away from the Precision badge";

            if (!ids.Contains("accuracy_80") && result.Accuracy >= 72 && result.Accuracy < 80)
                return $"🎖️  Almost Sharp — hit 80% accuracy once to unlock it";

            // TASK-0.3: reaction near-miss hints only where the metric IS reaction.
            if (ReactionMetric.IsTrueReaction(result.Scenario))
            {
                if (!ids.Contains("reaction_220") && result.AvgReactionMs > 0
                        && result.AvgReactionMs >= 220 && result.AvgReactionMs < 265)
                    return $"⚡  {(result.AvgReactionMs - 220):F0}ms from the Lightning badge";

                if (!ids.Contains("reaction_300") && result.AvgReactionMs > 0
                        && result.AvgReactionMs >= 300 && result.AvgReactionMs < 355)
                    return $"🤠  {(result.AvgReactionMs - 300):F0}ms from Quick Draw";
            }

            if (!ids.Contains("max_streak_50") && result.MaxStreak >= 35 && result.MaxStreak < 50)
                return $"👑  {50 - result.MaxStreak} more consecutive hits for God Mode";

            if (!ids.Contains("max_streak_20") && result.MaxStreak >= 12 && result.MaxStreak < 20)
                return $"🏆  {20 - result.MaxStreak} more consecutive hits to unlock Unstoppable";

            return null;
        }
    }
}
