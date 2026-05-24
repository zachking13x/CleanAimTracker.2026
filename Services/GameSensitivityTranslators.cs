using System;
using System.Collections.Generic;

namespace CleanAimTracker.Services
{
    public static class GameSensitivityTranslators
    {
        // Yaw values for each supported game (degrees per mouse count at in-game sensitivity = 1).
        // These must stay in sync with GameProfile.YawPerCount and Windows\GameSensitivityTranslators.cs.
        //
        // Verified reference points:
        //   CS2 / Apex       0.022   → sens 1.1 @ 800 DPI = 47 cm/360  ✓
        //   Valorant         0.07    → sens 0.36 @ 800 DPI = 45 cm/360  ✓
        //   OW2 / CoD        0.0066  → sens 7 @ 800 DPI = 25 cm/360     ✓
        //   Fortnite         0.005555→ sens 7 @ 800 DPI = 29 cm/360     ✓
        //   R6S              0.000572957795 → sens 50 @ 800 DPI = 40 cm ✓
        //   PUBG             0.000572957795 → sens 50 @ 800 DPI = 40 cm ✓
        //   Halo Infinite    0.0066  → sens 4 @ 800 DPI = 43 cm/360     ✓
        //   Tarkov           0.09    → sens 0.33 @ 800 DPI = 38 cm/360  ✓
        private static readonly Dictionary<string, double> GameYaw = new()
        {
            { "Valorant",           0.07            },
            { "CS2",                0.022           },
            { "Apex Legends",       0.022           },
            { "Overwatch 2",        0.0066          },
            { "Rainbow Six Siege",  0.000572957795  },  // was 0.00572957795 — 10× too large
            { "Fortnite",           0.005555        },
            { "PUBG",               0.000572957795  },  // was 0.00565 — wrong value entirely
            { "Halo Infinite",      0.0066          },  // was 0.005 — wrong
            { "MW2/MW3/Warzone",    0.0066          },
            { "Escape From Tarkov", 0.09            },  // new — was missing
        };

        // List of supported games for dropdowns
        public static IEnumerable<string> GetSupportedGames() => GameYaw.Keys;

        // Convert in-game sensitivity → cm/360
        // Formula: cm360 = 914.4 / (sens × dpi × yaw)  where 914.4 = 360 × 2.54
        public static double ToCm360(string game, double dpi, double sens)
        {
            if (!GameYaw.TryGetValue(game, out double yaw) || yaw <= 0 || dpi <= 0 || sens <= 0)
                return 0;
            return 914.4 / (sens * dpi * yaw);
        }

        // Convert cm/360 → in-game sensitivity
        public static double FromCm360(string game, double dpi, double cm360)
        {
            if (!GameYaw.TryGetValue(game, out double yaw) || yaw <= 0 || dpi <= 0 || cm360 <= 0)
                return 0;
            return 914.4 / (cm360 * dpi * yaw);
        }

        // Recommended DPI per game
        public static int GetRecommendedDpi(string game)
        {
            return game switch
            {
                "Valorant"           => 800,
                "CS2"                => 400,
                "Apex Legends"       => 800,
                "Overwatch 2"        => 800,
                "Rainbow Six Siege"  => 800,
                "Fortnite"           => 800,
                "PUBG"               => 800,
                "Halo Infinite"      => 800,
                "MW2/MW3/Warzone"    => 800,
                "Escape From Tarkov" => 800,
                _                    => 800
            };
        }
    }
}
