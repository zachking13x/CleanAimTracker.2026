using System.Collections.Generic;
using CleanAimTracker.Services;

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
                    TypicalSensMin      = 1.0,  // was 0.5 — at 400 DPI, 0.5 = 207 cm/360 (impossibly high)
                    TypicalSensMax      = 4.0   // was 3.0
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
                    YawPerCount         = 0.00572957795,
                    RecommendedCm360Min = 25,
                    RecommendedCm360Max = 55,
                    ProAverageCm360     = 40,
                    TypicalSensMin      = 6.0,  // was 5.0 — tightened to realistic pro range
                    TypicalSensMax      = 12.0  // was 15.0
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
                    YawPerCount         = 0.005555,  // was 0.5585 — wrong by factor of 100
                    RecommendedCm360Min = 18,
                    RecommendedCm360Max = 45,
                    ProAverageCm360     = 30,
                    TypicalSensMin      = 1.0,   // was 0.01 — Fortnite typical sens is 1–20 not 0.01–0.15
                    TypicalSensMax      = 20.0
                },

                // ─── PUBG ────────────────────────────────────
                new GameProfile
                {
                    Name                = "PUBG",
                    Category            = "Battle Royale",
                    Description         = "PUBG Battle Royale profile",
                    // PUBG yaw at General Sensitivity = 1 is ~0.000573 deg/count (linear 1–200 scale)
                    // At 800 DPI / 40 cm → sens ≈ 50, which matches real-game reference data
                    YawPerCount         = 0.000572957795,
                    RecommendedCm360Min = 30,
                    RecommendedCm360Max = 55,
                    ProAverageCm360     = 40,
                    TypicalSensMin      = 20.0,
                    TypicalSensMax      = 65.0
                },

                // ─── Halo Infinite ───────────────────────────
                new GameProfile
                {
                    Name                = "Halo Infinite",
                    Category            = "Arena",
                    Description         = "Halo Infinite arena FPS profile",
                    // Halo Infinite uses 0.0066 deg/count at sensitivity 1 (1–10 scale)
                    // same engine-class constant as CoD/OW2 — at 800 DPI / 40 cm → sens ≈ 4.3
                    YawPerCount         = 0.0066,
                    RecommendedCm360Min = 30,
                    RecommendedCm360Max = 55,
                    ProAverageCm360     = 40,
                    TypicalSensMin      = 1.0,
                    TypicalSensMax      = 10.0
                },

                // ─── Escape From Tarkov ──────────────────────
                new GameProfile
                {
                    Name                = "Escape From Tarkov",
                    Category            = "Tactical",
                    Description         = "Escape From Tarkov hardcore FPS profile",
                    // EFT sensitivity slider runs 0.0–1.0; at sens=1 the yaw is ~0.09 deg/count
                    // At 800 DPI / 38 cm → sens ≈ 0.33, matching competitive EFT play
                    YawPerCount         = 0.09,
                    RecommendedCm360Min = 30,
                    RecommendedCm360Max = 50,
                    ProAverageCm360     = 38,
                    TypicalSensMin      = 0.1,
                    TypicalSensMax      = 1.0
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

        /// <summary>
        /// Sanity-check all built-in profiles at startup.
        /// Real game yaw values are all well below 0.1 — anything higher is almost certainly
        /// a data-entry error (e.g. Fortnite was accidentally set to 0.5585 instead of 0.005555).
        /// </summary>
        public static void ValidateProfiles()
        {
            foreach (var p in GetDefaults())
            {
                if (p.YawPerCount > 0.1)
                    LogService.Warn(
                        $"GameProfile validation: {p.Name} has suspicious YawPerCount {p.YawPerCount} — expected below 0.1");
            }
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
