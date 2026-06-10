using System.Collections.Generic;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Voltaic Framework community benchmark lookup tables.
    /// Reaction: lower is better — percentile 90 if at or below thresholds[0].
    /// Accuracy: higher is better — percentile 90 if at or above thresholds[0].
    /// Returns -1 when no benchmark is defined for the scenario/difficulty.
    /// </summary>
    public static class PercentileBenchmarks
    {
        // [top10, top25, top50, top75] — reaction in ms
        private static readonly Dictionary<string, Dictionary<string, int[]>> ReactionThresholds = new()
        {
            ["Flicking"] = new()
            {
                ["Easy"]      = new[] { 350, 500, 700, 900 },
                ["Medium"]    = new[] { 280, 400, 580, 780 },
                ["Hard"]      = new[] { 220, 320, 460, 640 },
                ["Nightmare"] = new[] { 170, 250, 370, 520 },
            },
            ["Reactive"] = new()
            {
                ["Easy"]      = new[] { 400, 560, 750, 950 },
                ["Medium"]    = new[] { 300, 430, 600, 800 },
                ["Hard"]      = new[] { 220, 330, 480, 660 },
                ["Nightmare"] = new[] { 160, 250, 380, 540 },
            },
            ["Shotgun"] = new()
            {
                ["Easy"]      = new[] { 250, 360, 500, 680 },
                ["Medium"]    = new[] { 190, 280, 400, 560 },
                ["Hard"]      = new[] { 150, 220, 320, 450 },
                ["Nightmare"] = new[] { 120, 175, 260, 380 },
            },
            ["SpeedSwitching"] = new()
            {
                ["Easy"]      = new[] { 380, 520, 700, 900 },
                ["Medium"]    = new[] { 290, 410, 580, 780 },
                ["Hard"]      = new[] { 220, 320, 460, 640 },
                ["Nightmare"] = new[] { 170, 250, 370, 530 },
            },
            ["PeekTraining"] = new()
            {
                ["Easy"]      = new[] { 300, 440, 620, 820 },
                ["Medium"]    = new[] { 230, 340, 490, 660 },
                ["Hard"]      = new[] { 175, 260, 380, 530 },
                ["Nightmare"] = new[] { 140, 210, 310, 440 },
            },
            ["StaticClicking"] = new()
            {
                ["Easy"]      = new[] { 320, 460, 640, 840 },
                ["Medium"]    = new[] { 250, 360, 510, 700 },
                ["Hard"]      = new[] { 190, 280, 400, 560 },
                ["Nightmare"] = new[] { 150, 220, 320, 460 },
            },
            ["DynamicClicking"] = new()
            {
                ["Easy"]      = new[] { 360, 500, 690, 890 },
                ["Medium"]    = new[] { 280, 400, 560, 760 },
                ["Hard"]      = new[] { 210, 310, 450, 630 },
                ["Nightmare"] = new[] { 165, 245, 360, 510 },
            },
            ["Evasive"] = new()
            {
                ["Easy"]      = new[] { 400, 560, 760, 960 },
                ["Medium"]    = new[] { 310, 440, 620, 820 },
                ["Hard"]      = new[] { 240, 350, 500, 700 },
                ["Nightmare"] = new[] { 185, 275, 400, 570 },
            },
        };

        // [top10, top25, top50, top75] — accuracy in percent
        private static readonly Dictionary<string, Dictionary<string, int[]>> AccuracyThresholds = new()
        {
            ["Flicking"] = new()
            {
                ["Easy"]      = new[] { 95, 88, 78, 65 },
                ["Medium"]    = new[] { 92, 84, 73, 60 },
                ["Hard"]      = new[] { 88, 79, 68, 54 },
                ["Nightmare"] = new[] { 82, 72, 60, 47 },
            },
            ["Tracking"] = new()
            {
                ["Easy"]      = new[] { 90, 80, 68, 54 },
                ["Medium"]    = new[] { 86, 75, 63, 49 },
                ["Hard"]      = new[] { 81, 70, 57, 44 },
                ["Nightmare"] = new[] { 74, 63, 50, 37 },
            },
            ["Reactive"] = new()
            {
                ["Easy"]      = new[] { 85, 74, 61, 47 },
                ["Medium"]    = new[] { 79, 68, 55, 41 },
                ["Hard"]      = new[] { 72, 61, 48, 35 },
                ["Nightmare"] = new[] { 64, 53, 41, 29 },
            },
            ["Precision"] = new()
            {
                ["Easy"]      = new[] { 92, 83, 71, 57 },
                ["Medium"]    = new[] { 88, 78, 66, 52 },
                ["Hard"]      = new[] { 83, 72, 60, 46 },
                ["Nightmare"] = new[] { 76, 65, 53, 39 },
            },
            ["Adaptive"] = new()
            {
                ["Easy"]      = new[] { 88, 78, 66, 52 },
                ["Medium"]    = new[] { 83, 72, 60, 46 },
                ["Hard"]      = new[] { 77, 66, 53, 40 },
                ["Nightmare"] = new[] { 70, 59, 46, 33 },
            },
        };

        // Reaction: lower is better. Returns approximate percentile, or -1 if no data.
        // Sniper always returns -1 (reaction not meaningful).
        public static int GetReactionPercentile(string scenario, string difficulty, double reactionMs)
        {
            if (scenario == "Sniper") return -1;
            if (!ReactionThresholds.TryGetValue(scenario, out var byDiff)) return -1;
            if (!byDiff.TryGetValue(difficulty, out var t)) return -1;

            if (reactionMs <= t[0]) return 90;
            if (reactionMs <= t[1]) return 75;
            if (reactionMs <= t[2]) return 50;
            if (reactionMs <= t[3]) return 25;
            return 10;
        }

        // Accuracy: higher is better. Returns approximate percentile, or -1 if no data.
        public static int GetAccuracyPercentile(string scenario, string difficulty, double accuracy)
        {
            // Fall back to Flicking thresholds for unmapped scenarios
            if (!AccuracyThresholds.TryGetValue(scenario, out var byDiff))
            {
                if (!AccuracyThresholds.TryGetValue("Flicking", out byDiff)) return -1;
            }
            if (!byDiff.TryGetValue(difficulty, out var t)) return -1;

            if (accuracy >= t[0]) return 90;
            if (accuracy >= t[1]) return 75;
            if (accuracy >= t[2]) return 50;
            if (accuracy >= t[3]) return 25;
            return 10;
        }
    }
}
