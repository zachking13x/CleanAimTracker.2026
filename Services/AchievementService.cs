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
                File.WriteAllText(FilePath, JsonSerializer.Serialize(unlocked,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static List<Achievement> EvaluateAfterSession(
            AimTrainerResult result,
            List<AimTrainerResult> allResults,
            int currentStreakDays,
            int challengesCompleted = 0)
        {
            var unlocked      = LoadUnlocked();
            var unlockedIds   = unlocked.Select(a => a.Id).ToHashSet();
            var definitions   = GetAllDefinitions();
            var newlyUnlocked = new List<Achievement>();

            void TryUnlock(string id)
            {
                if (unlockedIds.Contains(id)) return;
                var def = definitions.FirstOrDefault(d => d.Id == id);
                if (def == null) return;
                def.IsUnlocked = true;
                def.UnlockedAt = DateTime.UtcNow;
                unlocked.Add(def);
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
            if (result.Accuracy >= 95)  TryUnlock("elite_grade");  // "Excellent" AI coach threshold
            if (result.Accuracy >= 100) TryUnlock("accuracy_100");

            if (result.AvgReactionMs > 0 && result.AvgReactionMs < 300) TryUnlock("reaction_300");
            if (result.AvgReactionMs > 0 && result.AvgReactionMs < 220) TryUnlock("reaction_220");

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
            if (scenariosTried.Contains("Tracking") && scenariosTried.Contains("Flicking") &&
                scenariosTried.Contains("Precision") && scenariosTried.Contains("Switching"))
                TryUnlock("all_scenarios");

            if (currentStreakDays >= 3)  TryUnlock("streak_3");
            if (currentStreakDays >= 7)  TryUnlock("streak_7");
            if (currentStreakDays >= 14) TryUnlock("streak_14");
            if (currentStreakDays >= 30) TryUnlock("streak_30");

            var tier = ProgressionService.GetTier(SessionStorage.LoadAll() ?? new List<SessionSummary>());
            if (tier.Name is "Bronze" or "Silver" or "Gold" or "Elite") TryUnlock("reach_bronze");
            if (tier.Name is "Silver" or "Gold" or "Elite")              TryUnlock("reach_silver");
            if (tier.Name is "Gold" or "Elite")                          TryUnlock("reach_gold");
            if (tier.Name == "Elite")                                     TryUnlock("reach_elite");

            if (challengesCompleted >= 1)  TryUnlock("first_challenge");
            if (challengesCompleted >= 7)  TryUnlock("challenge_7");
            if (challengesCompleted >= 30) TryUnlock("challenge_30");

            if (newlyUnlocked.Count > 0)
                SaveUnlocked(unlocked);

            return newlyUnlocked;
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
    }
}
