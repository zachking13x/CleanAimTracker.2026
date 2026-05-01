using CleanAimTracker.Models;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    public static class GameProfileStorage
    {
        public static readonly List<GameProfile> Profiles = new()
        {
            new GameProfile
            {
                Name = "Valorant",
                YawPerCount = 0.07,
                ProAverageCm360 = 47,
                RecommendedCm360Min = 35,
                RecommendedCm360Max = 55,
                TypicalSensMin = 0.2,
                TypicalSensMax = 1.2
            },

            new GameProfile
            {
                Name = "Apex Legends",
                YawPerCount = 0.022,
                ProAverageCm360 = 28,
                RecommendedCm360Min = 20,
                RecommendedCm360Max = 40,
                TypicalSensMin = 0.5,
                TypicalSensMax = 3.0
            },

            new GameProfile
            {
                Name = "CS2",
                YawPerCount = 0.022,
                ProAverageCm360 = 50,
                RecommendedCm360Min = 40,
                RecommendedCm360Max = 60,
                TypicalSensMin = 0.5,
                TypicalSensMax = 3.0
            },

            new GameProfile
            {
                Name = "Rainbow Six Siege",
                YawPerCount = 0.00572957795,
                ProAverageCm360 = 12,
                RecommendedCm360Min = 8,
                RecommendedCm360Max = 20,
                TypicalSensMin = 1.0,
                TypicalSensMax = 20.0
            },

            new GameProfile
            {
                Name = "Fortnite",
                YawPerCount = 0.005555,
                ProAverageCm360 = 32,
                RecommendedCm360Min = 25,
                RecommendedCm360Max = 45,
                TypicalSensMin = 1.0,
                TypicalSensMax = 20.0
            },

            new GameProfile
            {
                Name = "Overwatch 2",
                YawPerCount = 0.0066,
                ProAverageCm360 = 33,
                RecommendedCm360Min = 25,
                RecommendedCm360Max = 45,
                TypicalSensMin = 1.0,
                TypicalSensMax = 20.0
            },

            new GameProfile
            {
                Name = "Escape From Tarkov",
                YawPerCount = 0.00572957795,
                ProAverageCm360 = 38,
                RecommendedCm360Min = 30,
                RecommendedCm360Max = 50,
                TypicalSensMin = 0.1,
                TypicalSensMax = 1.0
            },

            new GameProfile
            {
                Name = "PUBG",
                YawPerCount = 0.00572957795,
                ProAverageCm360 = 40,
                RecommendedCm360Min = 30,
                RecommendedCm360Max = 55,
                TypicalSensMin = 1.0,
                TypicalSensMax = 50.0
            },

            new GameProfile
            {
                Name = "Call of Duty (MW/WZ)",
                YawPerCount = 0.0066,
                ProAverageCm360 = 28,
                RecommendedCm360Min = 20,
                RecommendedCm360Max = 40,
                TypicalSensMin = 1.0,
                TypicalSensMax = 20.0
            },

            new GameProfile
            {
                Name = "Halo Infinite",
                YawPerCount = 0.00572957795,
                ProAverageCm360 = 40,
                RecommendedCm360Min = 30,
                RecommendedCm360Max = 55,
                TypicalSensMin = 1.0,
                TypicalSensMax = 20.0
            }
        };

        public static GameProfile LoadByName(string name)
            => Profiles.FirstOrDefault(p => p.Name == name);
    }
}
