using System;
using System.Collections.Generic;

namespace CleanAimTracker.Services
{
    public static class GameSensitivityTranslators
    {
        // Yaw values for each supported game
        private static readonly Dictionary<string, double> GameYaw = new()
        {
            { "Valorant", 0.07 },
            { "CS2", 0.022 },
            { "Apex Legends", 0.022 },
            { "Overwatch 2", 0.0066 },
            { "Rainbow Six Siege", 0.00572957795 },
            { "Fortnite", 0.005555 },
            { "PUBG", 0.00565 },
            { "Halo Infinite", 0.005 },
            { "MW2/MW3/Warzone", 0.0066 }
        };

        // List of supported games for dropdowns
        public static IEnumerable<string> GetSupportedGames() => GameYaw.Keys;

        // Convert sensitivity → cm/360
        public static double ToCm360(string game, double dpi, double sens)
        {
            double yaw = GameYaw[game];
            return 360 / (sens * dpi * yaw) * 2.54;
        }

        // Convert cm/360 → sensitivity
        public static double FromCm360(string game, double dpi, double cm360)
        {
            double yaw = GameYaw[game];
            return 360 / (cm360 / 2.54 * dpi * yaw);
        }

        // Recommended DPI per game
        public static int GetRecommendedDpi(string game)
        {
            return game switch
            {
                "Valorant" => 800,
                "CS2" => 400,
                "Apex Legends" => 800,
                "Overwatch 2" => 800,
                "Rainbow Six Siege" => 800,
                "Fortnite" => 800,
                "PUBG" => 800,
                "Halo Infinite" => 800,
                "MW2/MW3/Warzone" => 1600,
                _ => 800
            };
        }
    }
}
