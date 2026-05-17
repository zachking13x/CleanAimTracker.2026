using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    public static class RecommendationEngine
    {
        // Standard DPI steps commonly available on gaming mice
        private static readonly int[] StandardDpiSteps =
            { 400, 800, 1200, 1600, 3200, 6400, 7200 };

        // ══════════════════════════════════════════════════════════
        //  MAIN ENTRY
        // ══════════════════════════════════════════════════════════
        public static SensitivityRecommendation Analyze(SessionSummary s, GameProfile p)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (p == null) throw new ArgumentNullException(nameof(p));

            // Basic sanity clamps to avoid garbage values
            double safeDpi = s.DPI <= 0 ? 800 : s.DPI;
            double safeSens = s.Sensitivity <= 0 ? 1.0 : s.Sensitivity;
            double safeCm360 = s.CmPer360 <= 0 ? 30.0 : s.CmPer360;

            var rec = new SensitivityRecommendation
            {
                CurrentDPI = (int)Math.Round(safeDpi),
                CurrentSensitivity = safeSens,
                CurrentCm360 = safeCm360,
                GameName = p.Name ?? "Unknown Game",
                ProAverageCm360 = p.ProAverageCm360,
                Cm360RangeMin = p.RecommendedCm360Min,
                Cm360RangeMax = p.RecommendedCm360Max
            };

            // ══════════════════════════════════════════════════════
            // 1. DIAGNOSTIC SCORING (weighted)
            // ══════════════════════════════════════════════════════

            rec.JitterScore = ScoreJitter(s);
            rec.SmoothnessScore = ClampScore(s.SmoothnessScore);
            rec.ConsistencyScore = ClampScore(s.MovementConsistency);
            rec.FlickControlScore = ScoreFlickControl(s);
            rec.CorrectionSharpnessScore = ScoreCorrectionSharpness(s);
            rec.VelocityStabilityScore = ScoreVelocityStability(s);
            rec.IdlePenaltyScore = ScoreIdlePenalty(s);

            rec.OverallDiagnostic =
                (rec.SmoothnessScore * 0.35) +
                (rec.ConsistencyScore * 0.30) +
                (rec.FlickControlScore * 0.20) +
                (rec.JitterScore * 0.15);

            rec.OverallDiagnostic = ClampScore(rec.OverallDiagnostic);

            // ══════════════════════════════════════════════════════
            // 2. TARGET CM/360
            // ══════════════════════════════════════════════════════

            double targetCm360 = DetermineTargetCm360(s, p, rec);
            rec.RecommendedCm360 = targetCm360;

            // ══════════════════════════════════════════════════════
            // 3. DPI SELECTION
            // ══════════════════════════════════════════════════════

            rec.RecommendedDPI = (int)Math.Round(
                PickBestDpi(safeDpi, targetCm360, safeSens, p)
            );

            if (rec.RecommendedDPI <= 0)
                rec.RecommendedDPI = 800;

            // ══════════════════════════════════════════════════════
            // 4. SENSITIVITY CALCULATION
            // ══════════════════════════════════════════════════════

            double sens = CmPer360ToSensitivity(
                targetCm360,
                rec.RecommendedDPI,
                p.YawPerCount <= 0 ? 0.022 : p.YawPerCount // fallback yaw if bad
            );

            // Clamp to profile limits
            double minSens = p.TypicalSensMin > 0 ? p.TypicalSensMin : 0.01;
            double maxSens = p.TypicalSensMax > minSens ? p.TypicalSensMax : minSens * 4;

            sens = Math.Clamp(sens, minSens, maxSens);
            sens = Math.Round(sens, 4);

            rec.RecommendedSensitivity = sens;

            // Sensitivity range (±3%)
            rec.RecommendedSensitivityMin = Math.Round(sens * 0.97, 4);
            rec.RecommendedSensitivityMax = Math.Round(sens * 1.03, 4);

            // ══════════════════════════════════════════════════════
            // 5. MUSCLE MEMORY PROTECTION
            // ══════════════════════════════════════════════════════

            double pctChange = 0;

            if (safeSens > 0)
            {
                pctChange = Math.Abs(sens - safeSens) / safeSens * 100.0;
            }
            else
            {
                pctChange = 100.0;
            }

            if (double.IsNaN(pctChange) || double.IsInfinity(pctChange))
                pctChange = 100.0;

            rec.MinimalChangeRecommended = pctChange < 5.0;

            // ══════════════════════════════════════════════════════
            // 6. CONFIDENCE SCORE
            // ══════════════════════════════════════════════════════

            int confidence = 100;

            confidence -= (int)((100 - rec.OverallDiagnostic) / 2.0);
            if (s.SessionSeconds < 45) confidence -= 20;
            if (s.FlickCount < 3) confidence -= 10;

            rec.Confidence = Math.Clamp(confidence, 30, 100);

            // ══════════════════════════════════════════════════════
            // 7. VERDICTS
            // ══════════════════════════════════════════════════════

            rec.DpiVerdict = BuildDpiVerdict(safeDpi, rec.RecommendedDPI);
            rec.SensVerdict = BuildSensVerdict(safeSens, sens);
            rec.Cm360Verdict = BuildCm360Verdict(safeCm360, targetCm360, p);
            rec.OverallVerdict = BuildOverallVerdict(rec);

            // ══════════════════════════════════════════════════════
            // 8. EXPLANATION PARAGRAPH
            // ══════════════════════════════════════════════════════

            rec.Explanation = BuildExplanation(rec) ??
                              "Your aim profile is well‑balanced. The recommended settings fine‑tune your control without disrupting muscle memory.";

            // ══════════════════════════════════════════════════════
            // 9. TIPS
            // ══════════════════════════════════════════════════════

            rec.Tips = GenerateTips(s, p, rec) ?? new List<string>
            {
                "Your aim fundamentals look solid. Keep practicing to reinforce muscle memory."
            };

            // ══════════════════════════════════════════════════════
            // 10. TREND ANALYSIS
            // ══════════════════════════════════════════════════════

            var history = SessionStorage.LoadAll() ?? new List<SessionSummary>();
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

            if (string.IsNullOrWhiteSpace(rec.TrendSummary))
            {
                rec.HasTrendData = false;
                rec.TrendSummary = "Trend data unavailable.";
            }

            return rec;
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTIC SCORING
        // ══════════════════════════════════════════════════════════

        private static double ClampScore(double value) =>
            Math.Clamp(double.IsNaN(value) ? 0 : value, 0, 100);

        private static double ScoreJitter(SessionSummary s)
        {
            if (s.TotalSamples <= 0) return 100;

            double jitterRatio = s.JitterAmount / Math.Max(1.0, s.TotalSamples);
            double score = 100 - (jitterRatio * 500.0);

            return ClampScore(score);
        }

        private static double ScoreFlickControl(SessionSummary s)
        {
            if (s.FlickCount <= 0) return 80;

            double controlledRatio = (double)s.SmallFlickCount / Math.Max(1, s.FlickCount);
            double score = controlledRatio * 100.0;

            if (s.LargeFlickCount > s.SmallFlickCount)
                score -= 20.0;

            return ClampScore(score);
        }

        private static double ScoreCorrectionSharpness(SessionSummary s)
        {
            if (s.CorrectionSharpness <= 0) return 80;
            double score = 100 - s.CorrectionSharpness;
            return ClampScore(score);
        }

        private static double ScoreVelocityStability(SessionSummary s)
        {
            if (s.PeakVelocity <= 0) return 80;

            double ratio = s.AverageVelocity / Math.Max(1.0, s.PeakVelocity);
            double score = ratio * 100.0;

            return ClampScore(score);
        }

        private static double ScoreIdlePenalty(SessionSummary s)
        {
            double idlePct = Math.Max(0, s.IdlePercentage);
            double score = 100 - (idlePct * 1.5);
            return ClampScore(score);
        }

        // ══════════════════════════════════════════════════════════
        //  TARGET CM/360
        // ══════════════════════════════════════════════════════════

        private static double DetermineTargetCm360(SessionSummary s, GameProfile p, SensitivityRecommendation rec)
        {
            double min = p.RecommendedCm360Min > 0 ? p.RecommendedCm360Min : 20.0;
            double max = p.RecommendedCm360Max > min ? p.RecommendedCm360Max : min + 20.0;
            double proAvg = p.ProAverageCm360 > 0 ? p.ProAverageCm360 : (min + max) / 2.0;

            double target = proAvg;

            if (rec.JitterScore < 50) target += 5;
            if (rec.SmoothnessScore < 40) target += 3;
            if (rec.FlickControlScore < 40 && s.LargeFlickCount > s.SmallFlickCount) target += 4;

            if (rec.ConsistencyScore > 80 && rec.SmoothnessScore > 70)
            {
                if (s.CmPer360 >= min && s.CmPer360 <= max)
                    target = s.CmPer360;
            }

            target = Math.Round(target, 1);
            target = Math.Clamp(target, min, max);

            return target;
        }

        // ══════════════════════════════════════════════════════════
        //  DPI SELECTION
        // ══════════════════════════════════════════════════════════

        private static double PickBestDpi(double currentDpi, double targetCm360, double currentSens, GameProfile p)
        {
            double yaw = p.YawPerCount <= 0 ? 0.022 : p.YawPerCount;

            // Try to keep current DPI if it yields a reasonable sens
            if (StandardDpiSteps.Contains((int)currentDpi))
            {
                double testSens = CmPer360ToSensitivity(targetCm360, currentDpi, yaw);
                if (testSens >= p.TypicalSensMin && testSens <= p.TypicalSensMax)
                    return currentDpi;
            }

            double typicalMid = (p.TypicalSensMin + p.TypicalSensMax) / 2.0;
            if (typicalMid <= 0) typicalMid = currentSens > 0 ? currentSens : 1.0;

            double bestDpi = 800;
            double bestDiff = double.MaxValue;

            foreach (int dpi in StandardDpiSteps)
            {
                double sens = CmPer360ToSensitivity(targetCm360, dpi, yaw);
                if (sens <= 0 || double.IsNaN(sens) || double.IsInfinity(sens))
                    continue;

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
            if (recommended <= 0)
                return "Unable to determine an optimal DPI based on the current data.";

            if (Math.Abs(current - recommended) < 1)
                return "Your DPI is already optimal.";

            return current < recommended
                ? $"Increase DPI from {current:F0} to {recommended:F0} for finer control."
                : $"Decrease DPI from {current:F0} to {recommended:F0} for more precision.";
        }

        private static string BuildSensVerdict(double current, double recommended)
        {
            if (recommended <= 0)
                return "Unable to determine an optimal sensitivity based on the current data.";

            double pct = 0;

            if (current > 0)
            {
                pct = Math.Abs(recommended - current) / current * 100.0;
            }
            else
            {
                pct = 100.0;
            }

            if (double.IsNaN(pct) || double.IsInfinity(pct))
                pct = 100.0;

            if (pct < 5)
                return "Your sensitivity is already close to optimal.";

            return current > recommended
                ? $"Lower sensitivity from {current:F3} to {recommended:F3} ({pct:F0}% decrease)."
                : $"Raise sensitivity from {current:F3} to {recommended:F3} ({pct:F0}% increase).";
        }

        private static string BuildCm360Verdict(double current, double target, GameProfile p)
        {
            double min = p.RecommendedCm360Min > 0 ? p.RecommendedCm360Min : 20.0;
            double max = p.RecommendedCm360Max > min ? p.RecommendedCm360Max : min + 20.0;

            if (current <= 0)
                return $"Not enough data to evaluate sensitivity. Recommended mouse travel range is {min:F0}–{max:F0} cm per full turn.";

            if (current < min)
                return $"Your sensitivity is too high for this scenario — your mouse only travels {current:F1} cm per full turn. " +
                       $"The recommended range is {min:F0}–{max:F0} cm. Lowering your in-game sensitivity will help.";

            if (current > max)
                return $"Your sensitivity is too low for this scenario — your mouse travels {current:F1} cm per full turn. " +
                       $"The recommended range is {min:F0}–{max:F0} cm. Raising your in-game sensitivity will help.";

            return $"Your sensitivity is in the right range — {current:F1} cm per full turn fits this scenario well.";
        }

        private static string BuildOverallVerdict(SensitivityRecommendation rec)
        {
            double diag = rec.OverallDiagnostic;

            if (diag >= 80)
                return "Excellent aim fundamentals. Only minor adjustments are suggested.";

            if (diag >= 60)
                return "Good fundamentals with room for refinement.";

            if (diag >= 40)
                return "Aim mechanics need improvement. Sensitivity adjustments will help.";

            return "Significant aim issues detected. A sensitivity change is strongly recommended.";
        }

        // ══════════════════════════════════════════════════════════
        //  EXPLANATION PARAGRAPH
        // ══════════════════════════════════════════════════════════

        private static string BuildExplanation(SensitivityRecommendation r)
        {
            var parts = new List<string>();

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
            if (history == null || history.Count < 3)
                return "Not enough data for trends.";

            var recent = history
                .OrderByDescending(h => h.Timestamp)
                .Take(5)
                .ToList();

            if (recent.Count == 0)
                return "Not enough data for trends.";

            double avgQuality = recent.Average(s => s.OverallQualityScore);
            double avgSmoothness = recent.Average(s => s.SmoothnessScore);

            int splitIndex = recent.Count / 2;
            if (splitIndex == 0) splitIndex = 1;

            var older = recent.Skip(splitIndex).ToList();
            var newer = recent.Take(splitIndex).ToList();

            if (older.Count == 0 || newer.Count == 0)
                return $"Across your last {recent.Count} sessions: Average quality {avgQuality:F0}/100, smoothness {avgSmoothness:F0}/100.";

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
            if (cm360 <= 0 || dpi <= 0 || yaw <= 0)
                return 1.0;

            double sens = 914.4 / (cm360 * dpi * yaw);

            if (double.IsNaN(sens) || double.IsInfinity(sens))
                return 1.0;

            return sens;
        }
    }
}
