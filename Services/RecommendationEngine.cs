using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Analyses session metrics against a game profile and produces
    /// DPI, sensitivity, and cm/360 recommendations with diagnostic verdicts.
    /// </summary>
    public static class RecommendationEngine
    {
        // ── Standard DPI steps the engine can recommend ───────────
        private static readonly int[] StandardDpiSteps =
            { 400, 800, 1200, 1600, 3200 };

        // ══════════════════════════════════════════════════════════
        //  Main entry point
        // ══════════════════════════════════════════════════════════
        public static SensitivityRecommendation Analyze(
            SessionSummary session,
            GameProfile profile)
        {
            var rec = new SensitivityRecommendation
            {
                CurrentDPI = session.DPI,
                CurrentSensitivity = session.Sensitivity,
                CurrentCm360 = session.CmPer360,
                GameName = profile.Name,
                ProAverageCm360 = profile.ProAverageCm360,
                Cm360RangeMin = profile.RecommendedCm360Min,
                Cm360RangeMax = profile.RecommendedCm360Max
            };

            // ── 1. Diagnostic scores (0–100, higher = better) ─────
            rec.JitterScore = ScoreJitter(session);
            rec.SmoothnessScore = session.SmoothnessScore;
            rec.ConsistencyScore = session.MovementConsistency;
            rec.FlickControlScore = ScoreFlickControl(session);
            rec.OverallDiagnostic = (rec.JitterScore
                                   + rec.SmoothnessScore
                                   + rec.ConsistencyScore
                                   + rec.FlickControlScore) / 4.0;

            // ── 2. Determine ideal cm/360 from diagnostics ────────
            double targetCm360 = DetermineTargetCm360(session, profile, rec);

            // ── 3. Pick recommended DPI ───────────────────────────
            rec.RecommendedDPI = (int)Math.Round(
                PickBestDpi(session.DPI, targetCm360, session.Sensitivity, profile)
            );


            // ── 4. Compute matching sensitivity ───────────────────
            rec.RecommendedSensitivity = CmPer360ToSensitivity(
                targetCm360, rec.RecommendedDPI, profile.YawPerCount);

            rec.RecommendedCm360 = targetCm360;

            // ── 5. Generate verdicts ──────────────────────────────
            rec.DpiVerdict = BuildDpiVerdict(session.DPI, rec.RecommendedDPI);
            rec.SensVerdict = BuildSensVerdict(session.Sensitivity, rec.RecommendedSensitivity);
            rec.Cm360Verdict = BuildCm360Verdict(session.CmPer360, targetCm360, profile);
            rec.OverallVerdict = BuildOverallVerdict(rec);

            // ── 6. Generate tips ──────────────────────────────────
            rec.Tips = GenerateTips(session, profile, rec);

            // ── 7. Trend analysis ─────────────────────────────────
            var history = SessionStorage.LoadAll();
            if (history.Count >= 3)
            {
                rec.HasTrendData = true;
                rec.TrendSummary = BuildTrendSummary(history);
            }
            else
            {
                rec.HasTrendData = false;
                rec.TrendSummary = "Not enough session data for trend analysis (need at least 3 sessions).";
            }

            return rec;
        }

        // ══════════════════════════════════════════════════════════
        //  Diagnostic scoring
        // ══════════════════════════════════════════════════════════

        private static double ScoreJitter(SessionSummary s)
        {
            if (s.TotalSamples == 0) return 100;
            double jitterRatio = s.JitterAmount / s.TotalSamples;
            double score = 100 - (jitterRatio * 500);
            return Math.Max(0, Math.Min(100, score));
        }

        private static double ScoreFlickControl(SessionSummary s)
        {
            if (s.FlickCount == 0) return 80; // no flicks = decent but untested

            // Ratio of small (controlled) flicks to total
            double controlledRatio = (double)s.SmallFlickCount / s.FlickCount;
            double score = controlledRatio * 100;

            // Penalize excessive large flicks
            if (s.LargeFlickCount > s.SmallFlickCount)
                score -= 20;

            return Math.Max(0, Math.Min(100, score));
        }

        // ══════════════════════════════════════════════════════════
        //  Target cm/360 determination
        // ══════════════════════════════════════════════════════════

        private static double DetermineTargetCm360(
            SessionSummary s, GameProfile p, SensitivityRecommendation rec)
        {
            double current = s.CmPer360;
            double proAvg = p.ProAverageCm360;
            double rangeMin = p.RecommendedCm360Min;
            double rangeMax = p.RecommendedCm360Max;

            // Start from pro average
            double target = proAvg;

            // Adjust based on diagnostics
            if (rec.JitterScore < 50)
            {
                // High jitter → sensitivity too high → increase cm/360
                target = Math.Min(rangeMax, target + 5);
            }

            if (rec.SmoothnessScore < 40)
            {
                // Low smoothness → may need higher cm/360 (lower sens)
                target = Math.Min(rangeMax, target + 3);
            }

            if (rec.FlickControlScore < 40 && s.LargeFlickCount > s.SmallFlickCount)
            {
                // Poor flick control with many large flicks → raise cm/360
                target = Math.Min(rangeMax, target + 4);
            }

            if (rec.ConsistencyScore > 80 && rec.SmoothnessScore > 70)
            {
                // Good consistency and smoothness → stay near current if in range
                if (current >= rangeMin && current <= rangeMax)
                    target = current;
            }

            // Clamp to recommended range
            target = Math.Max(rangeMin, Math.Min(rangeMax, target));

            return Math.Round(target, 1);
        }

        // ══════════════════════════════════════════════════════════
        //  DPI selection
        // ══════════════════════════════════════════════════════════

        private static double PickBestDpi(double currentDpi, double targetCm360,
                                           double currentSens, GameProfile profile)
        {
            // If current DPI is a standard step and produces reasonable sens, keep it
            if (StandardDpiSteps.Contains((int)currentDpi))
            {
                double testSens = CmPer360ToSensitivity(targetCm360, currentDpi, profile.YawPerCount);
                if (testSens >= profile.TypicalSensMin && testSens <= profile.TypicalSensMax)
                    return currentDpi;
            }

            // Otherwise find the DPI step that yields sensitivity closest to typical range midpoint
            double typicalMid = (profile.TypicalSensMin + profile.TypicalSensMax) / 2.0;
            double bestDpi = 800;
            double bestDiff = double.MaxValue;

            foreach (int dpi in StandardDpiSteps)
            {
                double sens = CmPer360ToSensitivity(targetCm360, dpi, profile.YawPerCount);
                double diff = Math.Abs(sens - typicalMid);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestDpi = dpi;
                }
            }

            return bestDpi;
        }

        // ══════════════════════════════════════════════════════════
        //  Verdict builders
        // ══════════════════════════════════════════════════════════

        private static string BuildDpiVerdict(double current, double recommended)
        {
            if (Math.Abs(current - recommended) < 1)
                return "Your DPI is already at the recommended value.";

            return current < recommended
                ? $"Consider increasing DPI from {current:F0} to {recommended:F0} for finer control."
                : $"Consider decreasing DPI from {current:F0} to {recommended:F0} for more precision.";
        }

        private static string BuildSensVerdict(double current, double recommended)
        {
            double pctChange = Math.Abs(recommended - current) / current * 100;
            if (pctChange < 5)
                return "Your sensitivity is close to the recommended value.";

            return current > recommended
                ? $"Lower your sensitivity from {current:F3} to {recommended:F3} ({pctChange:F0}% decrease)."
                : $"Raise your sensitivity from {current:F3} to {recommended:F3} ({pctChange:F0}% increase).";
        }

        private static string BuildCm360Verdict(double current, double target, GameProfile profile)
        {
            if (current < profile.RecommendedCm360Min)
                return $"Your cm/360 ({current:F1}) is BELOW the recommended range ({profile.RecommendedCm360Min:F0}–{profile.RecommendedCm360Max:F0}). You may be overshooting targets.";

            if (current > profile.RecommendedCm360Max)
                return $"Your cm/360 ({current:F1}) is ABOVE the recommended range ({profile.RecommendedCm360Min:F0}–{profile.RecommendedCm360Max:F0}). You may lack speed for fast reactions.";

            return $"Your cm/360 ({current:F1}) is within the recommended range ({profile.RecommendedCm360Min:F0}–{profile.RecommendedCm360Max:F0}).";
        }

        private static string BuildOverallVerdict(SensitivityRecommendation rec)
        {
            if (rec.OverallDiagnostic >= 80)
                return "Excellent aim mechanics. Your settings are well-tuned. Minor adjustments may help optimization.";
            if (rec.OverallDiagnostic >= 60)
                return "Good fundamentals with room for improvement. Review the recommendations below to fine-tune your setup.";
            if (rec.OverallDiagnostic >= 40)
                return "Your aim mechanics need attention. The sensitivity adjustments below should help improve consistency.";
            return "Significant aim issues detected. A sensitivity change is strongly recommended to improve your control.";
        }

        // ══════════════════════════════════════════════════════════
        //  Tips generation
        // ══════════════════════════════════════════════════════════

        private static List<string> GenerateTips(
            SessionSummary s, GameProfile p, SensitivityRecommendation rec)
        {
            var tips = new List<string>();

            if (rec.JitterScore < 50)
                tips.Add("High jitter detected. Try lowering your sensitivity or DPI to reduce micro-corrections.");

            if (rec.SmoothnessScore < 50)
                tips.Add("Low smoothness score. Focus on making fluid, deliberate mouse movements rather than quick corrections.");

            if (rec.FlickControlScore < 50)
                tips.Add("Flick control needs work. Practice flick-shot drills at your current sensitivity before changing settings.");

            if (rec.ConsistencyScore < 50)
                tips.Add("Movement consistency is low. Try to maintain even speed during tracking — avoid sudden stops and starts.");

            if (s.IdlePercentage > 30)
                tips.Add("High idle time detected. If tracking during gameplay, this may indicate hesitation or over-aiming.");

            if (s.CmPer360 < p.RecommendedCm360Min)
                tips.Add($"Your cm/360 ({s.CmPer360:F1}) is below the recommended minimum ({p.RecommendedCm360Min:F0}). You may be overshooting targets.");

            if (s.CmPer360 > p.RecommendedCm360Max)
                tips.Add($"Your cm/360 ({s.CmPer360:F1}) is above the recommended maximum ({p.RecommendedCm360Max:F0}). Consider lowering cm/360 for faster reactions.");

            if (s.LargeFlickCount > s.SmallFlickCount && s.FlickCount > 5)
                tips.Add("You have more large flicks than small ones. This suggests over-correction — try reducing sensitivity slightly.");

            if (tips.Count == 0)
                tips.Add("Your aim profile looks solid. Keep practicing with your current setup to build muscle memory.");

            return tips;
        }

        // ══════════════════════════════════════════════════════════
        //  Trend analysis
        // ══════════════════════════════════════════════════════════

        private static string BuildTrendSummary(List<SessionSummary> history)
        {
            if (history.Count < 3)
                return "Not enough data for trends.";

            // Take last 5 sessions
            var recent = history.OrderByDescending(h => h.Timestamp).Take(5).ToList();

            double avgQuality = recent.Average(s => s.OverallQualityScore);
            double avgSmoothness = recent.Average(s => s.SmoothnessScore);

            // Compare first half vs second half for trend direction
            var older = recent.Skip(recent.Count / 2).ToList();
            var newer = recent.Take(recent.Count / 2).ToList();

            double olderAvg = older.Average(s => s.OverallQualityScore);
            double newerAvg = newer.Average(s => s.OverallQualityScore);

            string direction;
            if (newerAvg > olderAvg + 5)
                direction = "improving";
            else if (newerAvg < olderAvg - 5)
                direction = "declining";
            else
                direction = "stable";

            return $"Across your last {recent.Count} sessions: Average quality {avgQuality:F0}/100, " +
                   $"smoothness {avgSmoothness:F0}/100. Trend: {direction}.";
        }

        // ══════════════════════════════════════════════════════════
        //  Math helpers
        // ══════════════════════════════════════════════════════════

        /// <summary>cm/360 → in-game sensitivity</summary>
        private static double CmPer360ToSensitivity(double cm360, double dpi, double yaw)
        {
            if (cm360 <= 0 || dpi <= 0 || yaw <= 0) return 1.0;
            return 914.4 / (cm360 * dpi * yaw);
        }
    }
}
