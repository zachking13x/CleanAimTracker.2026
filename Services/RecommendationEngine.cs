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

        // TASK-3.1: below this confidence the engine recommends no change.
        public const int ActionableConfidenceFloor = 70;

        // TASK-3.1: cm/360 changes above this percentage must render a transition plan.
        public const double TransitionPlanThresholdPct = 15.0;

        // ══════════════════════════════════════════════════════════
        //  MAIN ENTRY
        // ══════════════════════════════════════════════════════════
        public static SensitivityRecommendation Analyze(SessionSummary s, GameProfile p)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (p == null) throw new ArgumentNullException(nameof(p));

            // Basic sanity clamps to avoid garbage values
            double safeDpi  = s.DPI <= 0 ? 800 : s.DPI;
            // Prefer GameSensitivity (actual in-game value) over raw Sensitivity.
            // GameSensitivity is populated by BuildSessionSummary and OpenRecommendation_Click;
            // older serialised sessions that lack it fall through to Sensitivity.
            double safeSens = (s.GameSensitivity > 0) ? s.GameSensitivity
                            : (s.Sensitivity     > 0) ? s.Sensitivity
                            : 1.0;
            double safeCm360 = s.CmPer360 <= 0 ? 30.0 : s.CmPer360;

            var rec = new SensitivityRecommendation
            {
                CurrentDPI = (int)Math.Round(safeDpi),
                CurrentSensitivity = safeSens,
                CurrentCm360 = safeCm360,
                GameName = p.Name ?? "Unknown Game",
                ProAverageCm360 = p.ProAverageCm360
                // TASK-3.2: Cm360RangeMin/Max are NO LONGER the game profile's
                // recommended range — that independent source produced ranges like
                // 18.0–45.0 next to a recommendation of 18.0 (the recommendation
                // sitting on its own range edge was the tell). They are now derived
                // from the recommended sensitivity range via the cm/360 formula —
                // see section 4 below.
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
            // TASK-3.1: sensitivity carries 2 decimals everywhere — stored rounded
            // so no surface can render "7.6208" or "13.0000".
            sens = Math.Round(sens, 2);

            rec.RecommendedSensitivity = sens;

            // Sensitivity range (±5%)
            rec.RecommendedSensitivityMin = Math.Round(sens * 0.95, 2);
            rec.RecommendedSensitivityMax = Math.Round(sens * 1.05, 2);

            // TASK-3.2: displayed cm/360 range DERIVED from the sensitivity range
            // endpoints via cm = (360 / (sens × dpi × yaw)) × 2.54 = 914.4 / (sens × dpi × yaw).
            // cm/360 is inversely proportional to sens, so the LOWER sens endpoint
            // gives the HIGHER cm endpoint and vice versa.
            // Hand-verified: 1200 DPI, Fortnite yaw 0.005555 → cm = 914.4/(1200×0.005555×sens)
            //   = 137.16/sens. Sens range 7.24–8.00 → 137.16/8.00 = 17.1 cm (min),
            //   137.16/7.24 = 18.9 cm (max). The recommended value always falls
            //   strictly inside because the range is sens ± 5%.
            double rangeYaw = p.YawPerCount <= 0 ? 0.022 : p.YawPerCount;
            rec.Cm360RangeMin = Math.Round(
                914.4 / (rec.RecommendedSensitivityMax * rec.RecommendedDPI * rangeYaw), 1);
            rec.Cm360RangeMax = Math.Round(
                914.4 / (rec.RecommendedSensitivityMin * rec.RecommendedDPI * rangeYaw), 1);

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
            // 6b. TASK-3.1: CONFIDENCE GATE
            // Below 70% the engine prescribes NOTHING: recommended values
            // collapse to current values and every verdict carries the
            // collecting-data message. A 41% sensitivity change was recommended
            // at 61% confidence off a session with a dead smoothness metric —
            // a pro coach does not prescribe major changes from bad data.
            // ══════════════════════════════════════════════════════

            // TASK-0.2: a low-activity session carries no settings signal — it can
            // neither raise nor lower the recommendation's standing (observed:
            // confidence 95% → 77% after a 0.9cm junk session).
            rec.IsActionable = rec.Confidence >= ActionableConfidenceFloor
                && !s.IsLowActivitySession;

            if (!rec.IsActionable)
            {
                // N sessions estimate: a clean full-length session removes the
                // short-session penalty (-20) and lifts the diagnostic component;
                // ~15 confidence points per clean session is the observed rate.
                int sessionsNeeded = Math.Max(1,
                    (int)Math.Ceiling((ActionableConfidenceFloor - rec.Confidence) / 15.0));
                string collecting =
                    $"Collecting data — run {sessionsNeeded} more active session{(sessionsNeeded == 1 ? "" : "s")} " +
                    "for a reliable recommendation.";

                rec.RecommendedCm360         = Math.Round(safeCm360, 1);
                rec.RecommendedDPI           = (int)Math.Round(safeDpi);
                rec.RecommendedSensitivity   = Math.Round(safeSens, 2);
                rec.RecommendedSensitivityMin = Math.Round(safeSens * 0.95, 2);
                rec.RecommendedSensitivityMax = Math.Round(safeSens * 1.05, 2);
                double gateYaw = p.YawPerCount <= 0 ? 0.022 : p.YawPerCount;
                rec.Cm360RangeMin = Math.Round(914.4 / (rec.RecommendedSensitivityMax * rec.RecommendedDPI * gateYaw), 1);
                rec.Cm360RangeMax = Math.Round(914.4 / (rec.RecommendedSensitivityMin * rec.RecommendedDPI * gateYaw), 1);
                rec.MinimalChangeRecommended = true;
                rec.RequiresTransitionPlan   = false;

                rec.DpiVerdict     = collecting;
                rec.SensVerdict    = collecting;
                rec.Cm360Verdict   = collecting;
                rec.OverallVerdict = collecting;
                rec.Explanation    = "There isn't enough clean movement data yet to recommend a settings change. " +
                                     "Keep your current settings and log more active sessions.";
                rec.Tips           = new List<string> { "Keep your current sensitivity until the coach has enough data to evaluate it properly." };

                var historyEarly = SessionStorage.LoadAll() ?? new List<SessionSummary>();
                rec.HasTrendData = historyEarly.Count >= 3;
                rec.TrendSummary = rec.HasTrendData
                    ? BuildTrendSummary(historyEarly)
                    : "Not enough session data for trend analysis (need at least 3 sessions).";
                return rec;
            }

            // TASK-3.1: a recommended cm/360 change > 15% MUST ship with the
            // step-by-step transition plan — surfaces check this flag.
            double cmChangePct = safeCm360 > 0
                ? Math.Abs(targetCm360 - safeCm360) / safeCm360 * 100.0
                : 0;
            rec.RequiresTransitionPlan = cmChangePct > TransitionPlanThresholdPct;

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
            double min    = p.RecommendedCm360Min > 0 ? p.RecommendedCm360Min : 20.0;
            double max    = p.RecommendedCm360Max > min ? p.RecommendedCm360Max : min + 20.0;
            double proAvg = p.ProAverageCm360 > 0 ? p.ProAverageCm360 : (min + max) / 2.0;
            double current = s.CmPer360 > 0 ? s.CmPer360 : (min + max) / 2.0;

            // If session was too short for reliable diagnostics, return current cm/360
            // clamped to the profile range — do not recommend a change on bad data
            if (s.SessionSeconds < 45)
                return Math.Round(Math.Clamp(current, min, max), 1);

            // ── Step 1: Determine the ideal direction ──────────────────────────
            // Signs suggest the user needs MORE cm/360 (lower sensitivity)
            bool needsHigher =
                rec.JitterScore < 50 ||          // High jitter = sensitivity too high
                rec.SmoothnessScore < 40 ||      // Low smoothness = too twitchy
                (rec.FlickControlScore < 40 &&
                 s.LargeFlickCount > s.SmallFlickCount); // Overshooting on flicks

            // Signs suggest the user needs LESS cm/360 (higher sensitivity)
            bool needsLower =
                rec.ConsistencyScore > 85 &&     // Very consistent already
                rec.SmoothnessScore > 80 &&      // Smooth already
                current > max;                   // And they are above the recommended range

            // If already in the good range and performing well — stay put
            bool alreadyGood =
                current >= min && current <= max &&
                rec.SmoothnessScore >= 60 &&
                rec.ConsistencyScore >= 60;

            if (alreadyGood)
            {
                // Small refinement only — stay close to current
                double refinement = current;
                if (rec.JitterScore < 50) refinement = Math.Min(current + 2.0, max);
                if (rec.SmoothnessScore > 80 && rec.ConsistencyScore > 80)
                    refinement = current; // Perfect, do not change
                return Math.Round(Math.Clamp(refinement, min, max), 1);
            }

            // ── Step 2: Calculate target with gradual stepping ─────────────────
            // Never recommend more than 20% change from current in one session
            double maxStep = current * 0.20;
            double target;

            if (needsHigher && !needsLower)
            {
                // User needs more cm/360 (lower sensitivity)
                // Step toward min of recommended range first, then toward proAvg
                double stepTarget = current < min
                    ? Math.Min(current + maxStep, min)     // Step toward min
                    : Math.Min(current + maxStep, proAvg); // Already in range, nudge toward pro
                target = stepTarget;
            }
            else if (needsLower && !needsHigher)
            {
                // User needs less cm/360 (higher sensitivity)
                double stepTarget = current > max
                    ? Math.Max(current - maxStep, max)     // Step toward max
                    : Math.Max(current - maxStep, proAvg); // In range, nudge toward pro
                target = stepTarget;
            }
            else
            {
                // Mixed signals or no clear direction — step gently toward proAvg
                if (current < proAvg)
                    target = Math.Min(current + Math.Min(maxStep, (proAvg - current) * 0.4), proAvg);
                else if (current > proAvg)
                    target = Math.Max(current - Math.Min(maxStep, (current - proAvg) * 0.4), proAvg);
                else
                    target = current;
            }

            // ── Step 3: Clamp to profile limits and round ──────────────────────
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

            double pct = current > 0
                ? Math.Abs(recommended - current) / current * 100.0
                : 100.0;

            if (double.IsNaN(pct) || double.IsInfinity(pct)) pct = 100.0;

            if (pct < 3)
                return "Your sensitivity is already at the recommended value.";

            // TASK-3.1: sensitivity renders at 2 decimals everywhere.
            if (pct < 8)
                return current > recommended
                    ? $"Minor adjustment: lower sensitivity from {current:F2} to {recommended:F2}."
                    : $"Minor adjustment: raise sensitivity from {current:F2} to {recommended:F2}.";

            return current > recommended
                ? $"Lower your sensitivity from {current:F2} to {recommended:F2} — " +
                  $"this is a {pct:F0}% change. Give yourself a few sessions to adjust."
                : $"Raise your sensitivity from {current:F2} to {recommended:F2} — " +
                  $"this is a {pct:F0}% change. Give yourself a few sessions to adjust.";
        }

        private static string BuildCm360Verdict(double current, double target, GameProfile p)
        {
            double min    = p.RecommendedCm360Min > 0 ? p.RecommendedCm360Min : 20.0;
            double max    = p.RecommendedCm360Max > min ? p.RecommendedCm360Max : min + 20.0;
            double proAvg = p.ProAverageCm360 > 0 ? p.ProAverageCm360 : (min + max) / 2.0;

            if (current <= 0)
                return $"Not enough data to evaluate sensitivity. " +
                       $"Recommended mouse travel range for {p.Name} is {min:F0}–{max:F0} cm per full turn.";

            bool inRange = current >= min && current <= max;
            bool targetMatchesCurrent = Math.Abs(target - current) < 1.5;

            if (targetMatchesCurrent && inRange)
                return $"Your sensitivity is in a good range at {current:F1} cm/360. " +
                       $"No change needed — focus on consistency.";

            if (targetMatchesCurrent && !inRange)
            {
                string direction = current < min ? "a bit low" : "a bit high";
                return $"Your sensitivity is {direction} at {current:F1} cm/360. " +
                       $"The recommended range for {p.Name} is {min:F0}–{max:F0} cm/360.";
            }

            // Gradual change recommendation
            double changePct = Math.Abs(target - current) / current * 100.0;
            string changeDir = target > current ? "increase" : "decrease";

            string verdict = $"Recommended change: {changeDir} from {current:F1} to {target:F1} cm/360 " +
                             $"({changePct:F0}% adjustment).";

            // Add long-term goal if target is not yet at pro average
            if (Math.Abs(target - proAvg) > 3.0 && !inRange)
                verdict += $" Long-term target for {p.Name} is around {proAvg:F0} cm/360 " +
                           $"— get there gradually over several sessions.";

            return verdict;
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

        // TASK-3.4: trend math lives in TrendAnalysisService — the single source.
        // Low-activity sessions are excluded; the smoothness average was dropped
        // (legacy zero-valued sessions made it misleading, and OverallQuality is
        // THE quality verdict per TASK-1.4).
        private static string BuildTrendSummary(List<SessionSummary> history)
        {
            var (avgQuality, direction, count) = TrendAnalysisService.Summarize(history);

            if (direction == "insufficient")
                return "Not enough data for trends.";

            return $"Across your last {count} sessions: average quality {avgQuality:F0}/100. Trend: {direction}.";
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
