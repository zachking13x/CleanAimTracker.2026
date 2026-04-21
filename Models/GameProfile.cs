using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    /// <summary>
    /// Defines a game-type sensitivity profile for the recommendation engine.
    /// Uses genre-based descriptors instead of trademarked game names.
    /// Each profile stores the yaw-per-count value and recommended cm/360 ranges.
    /// Users can create their own custom profiles and name them whatever they want.
    /// </summary>
    public class GameProfile
    {
        // ── Identity ──────────────────────────────────────────────
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";   // Tactical, Balanced, Arena
        public string Description { get; set; } = "";   // Human-readable description

        // ── Sensitivity math ──────────────────────────────────────
        /// <summary>Degrees of in-game rotation per raw mouse count at sensitivity 1.0.</summary>
        public double YawPerCount { get; set; }

        // ── Recommended ranges (cm/360) ───────────────────────────
        public double RecommendedCm360Min { get; set; }
        public double RecommendedCm360Max { get; set; }
        public double ProAverageCm360 { get; set; }

        // ── Typical sensitivity values ────────────────────────────
        public double TypicalSensMin { get; set; }
        public double TypicalSensMax { get; set; }

        // ── Custom profile flag ───────────────────────────────────
        public bool IsCustom { get; set; } = false;

        // ── Display helper ────────────────────────────────────────
        /// <summary>Returns the name shown in combo boxes (includes yaw for clarity).</summary>
        public string DisplayName =>
            IsCustom ? $"{Name} (Custom)" : $"{Name}  [Yaw {YawPerCount}]";

        // ── Static library of built-in genre-based presets ────────
        /// <summary>
        /// Returns built-in presets using genre-based descriptors and
        /// publicly measured yaw constants. No trademarked game names.
        /// Users who want game-specific profiles can create custom ones.
        /// </summary>
        public static List<GameProfile> GetDefaults()
        {
            return new List<GameProfile>
            {
                // ─── Tactical Shooter A ───────────────────────
                // Yaw 0.022 — low-sens tactical FPS preset
                new GameProfile
                {
                    Name                = "Tactical Shooter A",
                    Category            = "Tactical",
                    Description         = "Yaw 0.022 | Low-sens tactical FPS preset",
                    YawPerCount         = 0.022,
                    RecommendedCm360Min = 30,
                    RecommendedCm360Max = 65,
                    ProAverageCm360     = 47,
                    TypicalSensMin      = 0.5,
                    TypicalSensMax      = 3.0
                },

                // ─── Tactical Shooter B ───────────────────────
                // Yaw 0.07 — tactical FPS with higher yaw constant
                new GameProfile
                {
                    Name                = "Tactical Shooter B",
                    Category            = "Tactical",
                    Description         = "Yaw 0.07 | Tactical FPS, higher yaw constant",
                    YawPerCount         = 0.07,
                    RecommendedCm360Min = 30,
                    RecommendedCm360Max = 60,
                    ProAverageCm360     = 45,
                    TypicalSensMin      = 0.15,
                    TypicalSensMax      = 0.8
                },

                // ─── Battle Royale A ──────────────────────────
                // Yaw 0.022 — balanced BR with same yaw as Tactical A
                new GameProfile
                {
                    Name                = "Battle Royale A",
                    Category            = "Balanced",
                    Description         = "Yaw 0.022 | Balanced BR, same yaw as Tactical A",
                    YawPerCount         = 0.022,
                    RecommendedCm360Min = 18,
                    RecommendedCm360Max = 40,
                    ProAverageCm360     = 28,
                    TypicalSensMin      = 1.0,
                    TypicalSensMax      = 5.0
                },

                // ─── Arena Shooter ────────────────────────────
                // Yaw 0.0066 — fast-paced arena FPS preset
                new GameProfile
                {
                    Name                = "Arena Shooter",
                    Category            = "Arena",
                    Description         = "Yaw 0.0066 | Fast-paced arena FPS preset",
                    YawPerCount         = 0.0066,
                    RecommendedCm360Min = 15,
                    RecommendedCm360Max = 40,
                    ProAverageCm360     = 25,
                    TypicalSensMin      = 3.0,
                    TypicalSensMax      = 12.0
                },

                // ─── Tactical CQB ─────────────────────────────
                // Yaw 0.005729 — close-quarters tactical FPS preset
                new GameProfile
                {
                    Name                = "Tactical CQB",
                    Category            = "Tactical",
                    Description         = "Yaw 0.005729 | Close-quarters tactical preset",
                    YawPerCount         = 0.005729,
                    RecommendedCm360Min = 25,
                    RecommendedCm360Max = 55,
                    ProAverageCm360     = 40,
                    TypicalSensMin      = 5.0,
                    TypicalSensMax      = 15.0
                },

                // ─── Balanced FPS ─────────────────────────────
                // Yaw 0.0066 — standard balanced FPS preset
                new GameProfile
                {
                    Name                = "Balanced FPS",
                    Category            = "Balanced",
                    Description         = "Yaw 0.0066 | Standard balanced FPS preset",
                    YawPerCount         = 0.0066,
                    RecommendedCm360Min = 18,
                    RecommendedCm360Max = 40,
                    ProAverageCm360     = 27,
                    TypicalSensMin      = 3.0,
                    TypicalSensMax      = 12.0
                },

                // ─── Battle Royale B ──────────────────────────
                // Yaw 0.5585 — BR with high yaw constant (low sens values)
                new GameProfile
                {
                    Name                = "Battle Royale B",
                    Category            = "Balanced",
                    Description         = "Yaw 0.5585 | BR with high yaw (low sens values)",
                    YawPerCount         = 0.5585,
                    RecommendedCm360Min = 18,
                    RecommendedCm360Max = 45,
                    ProAverageCm360     = 30,
                    TypicalSensMin      = 0.01,
                    TypicalSensMax      = 0.15
                },

                // ─── Generic / Custom ─────────────────────────
                // Yaw 0.022 — general-purpose default
                new GameProfile
                {
                    Name                = "Generic Profile",
                    Category            = "General",
                    Description         = "Yaw 0.022 | General-purpose default preset",
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
        /// Merges built-in defaults with user-created custom profiles.
        /// Custom profiles appear at the end of the list.
        /// </summary>
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
                        YawPerCount = p.CmPer360 > 0 ? 360.0 / (p.CmPer360 * 2.54) : 0.022, // fallback
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


