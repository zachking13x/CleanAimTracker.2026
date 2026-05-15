using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    public class GameProfile
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";

        public double YawPerCount { get; set; }

        public double RecommendedCm360Min { get; set; }
        public double RecommendedCm360Max { get; set; }
        public double ProAverageCm360 { get; set; }

        public double TypicalSensMin { get; set; }
        public double TypicalSensMax { get; set; }

        public bool IsCustom { get; set; } = false;

        public string DisplayName =>
            IsCustom ? $"{Name} (Custom)" : Name;

        public static List<GameProfile> GetDefaults()
        {
            return new List<GameProfile>
            {
                // ─── Counter-Strike 2 ─────────────────────────
                new GameProfile
                {
                    Name                = "Counter-Strike 2",
                    Category            = "Tactical",
                    Description         = "CS2-style low-sens tactical FPS",
                    YawPerCount         = 0.022,
                    RecommendedCm360Min = 30,
                    RecommendedCm360Max = 65,
                    ProAverageCm360     = 47,
                    TypicalSensMin      = 0.5,
                    TypicalSensMax      = 3.0
                },

                // ─── Valorant ────────────────────────────────
                new GameProfile
                {
                    Name                = "Valorant",
                    Category            = "Tactical",
                    Description         = "Valorant-style tactical FPS",
                    YawPerCount         = 0.07,
                    RecommendedCm360Min = 30,
                    RecommendedCm360Max = 60,
                    ProAverageCm360     = 45,
                    TypicalSensMin      = 0.15,
                    TypicalSensMax      = 0.8
                },

                // ─── Apex Legends ─────────────────────────────
                new GameProfile
                {
                    Name                = "Apex Legends",
                    Category            = "Battle Royale",
                    Description         = "Apex Legends BR profile",
                    YawPerCount         = 0.022,
                    RecommendedCm360Min = 18,
                    RecommendedCm360Max = 40,
                    ProAverageCm360     = 28,
                    TypicalSensMin      = 1.0,
                    TypicalSensMax      = 5.0
                },

                // ─── Overwatch 2 ─────────────────────────────
                new GameProfile
                {
                    Name                = "Overwatch 2",
                    Category            = "Arena",
                    Description         = "Fast-paced arena FPS",
                    YawPerCount         = 0.0066,
                    RecommendedCm360Min = 15,
                    RecommendedCm360Max = 40,
                    ProAverageCm360     = 25,
                    TypicalSensMin      = 3.0,
                    TypicalSensMax      = 12.0
                },

                // ─── Rainbow Six Siege ───────────────────────
                new GameProfile
                {
                    Name                = "Rainbow Six Siege",
                    Category            = "Tactical",
                    Description         = "Close-quarters tactical FPS",
                    YawPerCount         = 0.005729,
                    RecommendedCm360Min = 25,
                    RecommendedCm360Max = 55,
                    ProAverageCm360     = 40,
                    TypicalSensMin      = 5.0,
                    TypicalSensMax      = 15.0
                },

                // ─── Call of Duty (MW/WZ) ─────────────────────
                new GameProfile
                {
                    Name                = "Call of Duty: MW / Warzone",
                    Category            = "Balanced",
                    Description         = "Standard CoD-style FPS",
                    YawPerCount         = 0.0066,
                    RecommendedCm360Min = 18,
                    RecommendedCm360Max = 40,
                    ProAverageCm360     = 27,
                    TypicalSensMin      = 3.0,
                    TypicalSensMax      = 12.0
                },

                // ─── Fortnite ────────────────────────────────
                new GameProfile
                {
                    Name                = "Fortnite",
                    Category            = "Battle Royale",
                    Description         = "Fortnite BR profile",
                    YawPerCount         = 0.5585,
                    RecommendedCm360Min = 18,
                    RecommendedCm360Max = 45,
                    ProAverageCm360     = 30,
                    TypicalSensMin      = 0.01,
                    TypicalSensMax      = 0.15
                },

                // ─── Generic FPS Profile ─────────────────────
                new GameProfile
                {
                    Name                = "Generic FPS Profile",
                    Category            = "General",
                    Description         = "General-purpose FPS preset",
                    YawPerCount         = 0.022,
                    RecommendedCm360Min = 20,
                    RecommendedCm360Max = 55,
                    ProAverageCm360     = 35,
                    TypicalSensMin      = 0.5,
                    TypicalSensMax      = 5.0
                }
            };
        }

        public static List<GameProfile> GetAllProfiles(List<AimProfile>? customProfiles)
        {
            var all = GetDefaults();

            if (customProfiles != null)
            {
                foreach (var p in customProfiles)
                {
                    all.Add(new GameProfile
                    {
                        Name = p.Name,
                        Category = "Custom",
                        Description = "User-created profile",
                        YawPerCount = p.CmPer360 > 0 ? 360.0 / (p.CmPer360 * 2.54) : 0.022,
                        RecommendedCm360Min = p.CmPer360,
                        RecommendedCm360Max = p.CmPer360,
                        ProAverageCm360 = p.CmPer360,
                        TypicalSensMin = p.Sensitivity,
                        TypicalSensMax = p.Sensitivity,
                        IsCustom = true
                    });
                }
            }

            return all;
        }
    }
}
