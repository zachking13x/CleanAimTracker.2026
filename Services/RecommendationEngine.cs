using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    public static class RecommendationEngine
    {
        private static readonly int[] StandardDpiSteps =
            { 400, 800, 1200, 1600, 3200, 6400, 7200 };

        // ══════════════════════════════════════════════════════════
        //  MAIN ENTRY
        // ══════════════════════════════════════════════════════════
        public static SensitivityRecommendation Analyze(SessionSummary s, GameProfile p)
        {
            var rec = new SensitivityRecommendation
            {
                CurrentDPI = s.DPI,
                CurrentSensitivity = s.Sensitivity,
                CurrentCm360 = s.CmPer360,
                GameName = p.Name,
                ProAverageCm360 = p.ProAverageCm360,
                Cm360RangeMin = p.RecommendedCm360Min,
                Cm360RangeMax = p.RecommendedCm360Max
            };

            // ══════════════════════════════════════════════════════
            // 1. DIAGNOSTIC SCORING (weighted)
            // ══════════════════════════════════════════════════════

            rec.JitterScore = ScoreJitter(s);
            rec.SmoothnessScore = s.SmoothnessScore;
            rec.ConsistencyScore = s.MovementConsistency;
            rec.FlickControlScore = ScoreFlickControl(s);

            rec.CorrectionSharpnessScore = ScoreCorrectionSharpness(s);
            rec.VelocityStabilityScore = ScoreVelocityStability(s);
            rec.IdlePenaltyScore = ScoreIdlePenalty(s);

            rec.OverallDiagnostic =
                (rec.SmoothnessScore * 0.35) +
                (rec.ConsistencyScore * 0.30) +
                (rec.FlickControlScore * 0.20) +
                (rec.JitterScore * 0.15);

            // ══════════════════════════════════════════════════════
            // 2. TARGET CM/360
            // ══════════════════════════════════════════════════════

            double targetCm360 = DetermineTargetCm360(s, p, rec);
            rec.RecommendedCm360 = targetCm360;

            // ══════════════════════════════════════════════════════
            // 3. DPI SELECTION
            // ══════════════════════════════════════════════════════

            rec.RecommendedDPI = (int)Math.Round(
                PickBestDpi(s.DPI, targetCm360, s.Sensitivity, p)
            );

            // ══════════════════════════════════════════════════════
            // 4. SENSITIVITY CALCULATION
            // ══════════════════════════════════════════════════════

            double sens = CmPer360ToSensitivity(targetCm360, rec.RecommendedDPI, p.YawPerCount);

            // Clamp to profile limits
            sens = Math.Clamp(sens, p.TypicalSensMin, p.TypicalSensMax);

            // Round to 4 decimals
            sens = Math.Round(sens, 4);

            rec.RecommendedSensitivity = sens;

            // Sensitivity range (±3%)
            rec.RecommendedSensitivityMin = Math.Round(sens * 0.97, 4);
            rec.RecommendedSensitivityMax = Math.Round(sens * 1.03, 4);

            // ══════════════════════════════════════════════════════
            // 5. MUSCLE MEMORY PROTECTION
            // ══════════════════════════════════════════════════════

            double pctChange = Math.Abs(sens - s.Sensitivity) / s.Sensitivity * 100;
            rec.MinimalChangeRecommended = pctChange < 5;

            // ══════════════════════════════════════════════════════
            // 6. CONFIDENCE SCORE
            // ══════════════════════════════════════════════════════

            int confidence = 100;

            confidence -= (int)(100 - rec.OverallDiagnostic) / 2;
            if (s.SessionSeconds < 45) confidence -= 20;
            if (s.FlickCount < 3) confidence -= 10;

            rec.Confidence = Math.Clamp(confidence, 30, 100);

            // ══════════════════════════════════════════════════════
            // 7. VERDICTS
            // ══════════════════════════════════════════════════════

            rec.DpiVerdict = BuildDpiVerdict(s.DPI, rec.RecommendedDPI);
            rec.SensVerdict = BuildSensVerdict(s.Sensitivity, sens);
            rec.Cm360Verdict = BuildCm360Verdict(s.CmPer360, targetCm360, p);
            rec.OverallVerdict = BuildOverallVerdict(rec);

            // ══════════════════════════════════════════════════════
            // 8. EXPLANATION PARAGRAPH
            // ══════════════════════════════════════════════════════

            rec.Explanation = BuildExplanation(rec);

            // ══════════════════════════════════════════════════════
            // 9. TIPS
            // ══════════════════════════════════════════════════════

            rec.Tips = GenerateTips(s, p, rec);

            // ══════════════════════════════════════════════════════
            // 10. TREND ANALYSIS
            // ══════════════════════════════════════════════════════

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
        //  DIAGNOSTIC SCORING
        // ══════════════════════════════════════════════════════════

        private static double ScoreJitter(SessionSummary s)
        {
            if (s.TotalSamples == 0) return 100;
            double jitterRatio = s.JitterAmount / s.TotalSamples;
            return Math.Clamp(100 - (jitterRatio * 500), 0, 100);
        }

        private static double ScoreFlickControl(SessionSummary s)
        {
            if (s.FlickCount == 0) return 80;

            double controlledRatio = (double)s.SmallFlickCount / s.FlickCount;
            double score = controlledRatio * 100;

            if (s.LargeFlickCount > s.SmallFlickCount)
                score -= 20;

            return Math.Clamp(score, 0, 100);
        }

        private static double ScoreCorrectionSharpness(SessionSummary s)
        {
            if (s.CorrectionSharpness <= 0) return 80;
            return Math.Clamp(100 - s.CorrectionSharpness, 0, 100);
        }

        private static double ScoreVelocityStability(SessionSummary s)
        {
            if (s.PeakVelocity <= 0) return 80;
            double ratio = s.AverageVelocity / s.PeakVelocity;
            return Math.Clamp(ratio * 100, 0, 100);
        }

        private static double ScoreIdlePenalty(SessionSummary s)
        {
            return Math.Clamp(100 - (s.IdlePercentage * 1.5), 0, 100);
        }

        // ══════════════════════════════════════════════════════════
        //  TARGET CM/360
        // ══════════════════════════════════════════════════════════

        private static double DetermineTargetCm360(SessionSummary s, GameProfile p, SensitivityRecommendation rec)
        {
            double target = p.ProAverageCm360;

            if (rec.JitterScore < 50) target += 5;
            if (rec.SmoothnessScore < 40) target += 3;
            if (rec.FlickControlScore < 40 && s.LargeFlickCount > s.SmallFlickCount) target += 4;

            if (rec.ConsistencyScore > 80 && rec.SmoothnessScore > 70)
            {
                if (s.CmPer360 >= p.RecommendedCm360Min && s.CmPer360 <= p.RecommendedCm360Max)
                    target = s.CmPer360;
            }

            return Math.Clamp(Math.Round(target, 1), p.RecommendedCm360Min, p.RecommendedCm360Max);
        }

        // ══════════════════════════════════════════════════════════
        //  DPI SELECTION
        // ══════════════════════════════════════════════════════════

        private static double PickBestDpi(double currentDpi, double targetCm360, double currentSens, GameProfile p)
        {
            if (StandardDpiSteps.Contains((int)currentDpi))
            {
                double testSens = CmPer360ToSensitivity(targetCm360, currentDpi, p.YawPerCount);
                if (testSens >= p.TypicalSensMin && testSens <= p.TypicalSensMax)
                    return currentDpi;
            }

            double typicalMid = (p.TypicalSensMin + p.TypicalSensMax) / 2.0;
            double bestDpi = 800;
            double bestDiff = double.MaxValue;

            foreach (int dpi in StandardDpiSteps)
            {
                double sens = CmPer360ToSensitivity(targetCm360, dpi, p.YawPerCount);
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
        //  VERDICTS
        // ══════════════════════════════════════════════════════════

        private static string BuildDpiVerdict(double current, double recommended)
        {
            if (Math.Abs(current - recommended) < 1)
                return "Your DPI is already optimal.";

            return current < recommended
                ? $"Increase DPI from {current:F0} to {recommended:F0} for finer control."
                : $"Decrease DPI from {current:F0} to {recommended:F0} for more precision.";
        }

        private static string BuildSensVerdict(double current, double recommended)
        {
            double pct = Math.Abs(recommended - current) / current * 100;

            if (pct < 5)
                return "Your sensitivity is already close to optimal.";

            return current > recommended
                ? $"Lower sensitivity from {current:F3} to {recommended:F3} ({pct:F0}% decrease)."
                : $"Raise sensitivity from {current:F3} to {recommended:F3} ({pct:F0}% increase).";
        }

        private static string BuildCm360Verdict(double current, double target, GameProfile p)
        {
            if (current < p.RecommendedCm360Min)
                return $"Your cm/360 ({current:F1}) is below the recommended range ({p.RecommendedCm360Min:F0}–{p.RecommendedCm360Max:F0}).";

            if (current > p.RecommendedCm360Max)
                return $"Your cm/360 ({current:F1}) is above the recommended range ({p.RecommendedCm360Min:F0}–{p.RecommendedCm360Max:F0}).";

            return $"Your cm/360 ({current:F1}) is within the recommended range.";
        }

        private static string BuildOverallVerdict(SensitivityRecommendation rec)
        {
            if (rec.OverallDiagnostic >= 80)
                return "Excellent aim fundamentals. Only minor adjustments are suggested.";

            if (rec.OverallDiagnostic >= 60)
                return "Good fundamentals with room for refinement.";

            if (rec.OverallDiagnostic >= 40)
                return "Aim mechanics need improvement. Sensitivity adjustments will help.";

            return "Significant aim issues detected. A sensitivity change is strongly recommended.";
        }

        // ══════════════════════════════════════════════════════════
        //  EXPLANATION PARAGRAPH
        // ══════════════════════════════════════════════════════════

        private static string BuildExplanation(SensitivityRecommendation r)
        {
            List<string> parts = new();

            if (r.JitterScore < 50)
                parts.Add("High jitter suggests your sensitivity may be too high.");

            if (r.SmoothnessScore < 50)
                parts.Add("Low smoothness indicates difficulty maintaining stable tracking.");

            if (r.FlickControlScore < 50)
                parts.Add("Flick control issues suggest overshooting or inconsistent corrections.");

            if (r.ConsistencyScore < 50)
                parts.Add("Low consistency indicates uneven movement patterns.");

            if (parts.Count == 0)
                return "Your aim profile is well‑balanced. The recommended settings fine‑tune your control without disrupting muscle memory.";

            return string.Join(" ", parts);
        }

        // ══════════════════════════════════════════════════════════
        //  TIPS
        // ══════════════════════════════════════════════════════════

        private static List<string> GenerateTips(SessionSummary s, GameProfile p, SensitivityRecommendation rec)
        {
            var tips = new List<string>();

            if (rec.JitterScore < 50)
                tips.Add("Try lowering sensitivity slightly to reduce jitter.");

            if (rec.SmoothnessScore < 50)
                tips.Add("Focus on smooth, continuous tracking movements.");

            if (rec.FlickControlScore < 50)
                tips.Add("Practice flick‑shot drills to improve control.");

            if (rec.ConsistencyScore < 50)
                tips.Add("Work on maintaining consistent movement speed.");

            if (s.IdlePercentage > 30)
                tips.Add("High idle time suggests hesitation — try to stay active during tracking.");

            if (tips.Count == 0)
                tips.Add("Your aim fundamentals look solid. Keep practicing to reinforce muscle memory.");

            return tips;
        }

        // ══════════════════════════════════════════════════════════
        //  TREND ANALYSIS
        // ══════════════════════════════════════════════════════════

        private static string BuildTrendSummary(List<SessionSummary> history)
        {
            if (history.Count < 3)
                return "Not enough data for trends.";

            var recent = history.OrderByDescending(h => h.Timestamp).Take(5).ToList();

            double avgQuality = recent.Average(s => s.OverallQualityScore);
            double avgSmoothness = recent.Average(s => s.SmoothnessScore);

            var older = recent.Skip(recent.Count / 2).ToList();
            var newer = recent.Take(recent.Count / 2).ToList();

            double olderAvg = older.Average(s => s.OverallQualityScore);
            double newerAvg = newer.Average(s => s.OverallQualityScore);

            string direction =
                newerAvg > olderAvg + 5 ? "improving" :
                newerAvg < olderAvg - 5 ? "declining" :
                "stable";

            return $"Across your last {recent.Count} sessions: Average quality {avgQuality:F0}/100, smoothness {avgSmoothness:F0}/100. Trend: {direction}.";
        }

        // ══════════════════════════════════════════════════════════
        //  MATH HELPERS
        // ══════════════════════════════════════════════════════════

        private static double CmPer360ToSensitivity(double cm360, double dpi, double yaw)
        {
            if (cm360 <= 0 || dpi <= 0 || yaw <= 0) return 1.0;
            return 914.4 / (cm360 * dpi * yaw);
        }
    }
}
