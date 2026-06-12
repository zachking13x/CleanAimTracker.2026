using CleanAimTracker.Models;
using System;
using System.Collections.Generic;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Manages gradual sensitivity transitions — breaks a large sens change into
    /// 3-session steps so muscle memory adapts gradually without a single jarring jump.
    /// </summary>
    public static class SensitivityTransitionService
    {
        // ── Constants ────────────────────────────────────────────────────────
        // Minimum delta (cm/360) worth creating a plan for
        private const double MinDeltaCm = 2.0;

        // Sessions required to "lock in" each step before advancing
        private const int SessionsPerStep = 3;

        // Number of intermediate steps for a gradual plan (not counting start/end)
        private const int GradualSteps = 4;

        // ── Plan generation ──────────────────────────────────────────────────
        /// <summary>
        /// Generates a new transition plan from <paramref name="currentCmPer360"/> to
        /// <paramref name="targetCmPer360"/>.  If the delta is under the minimum threshold,
        /// returns a single-step "Direct" plan.
        /// </summary>
        /// <param name="baseSensitivity">The sensitivity that PRODUCES
        /// <paramref name="targetCmPer360"/> (the recommendation's consistent
        /// pair). Step sensitivities are scaled from this anchor — pairing it
        /// with the CURRENT cm/360 produced step values outside the
        /// current→target range entirely (10.27 when the path runs 17.9→11.4).
        /// Hand-check: anchor (11.43 sens ↔ 18.0 cm); step at 12.8 cm →
        /// 11.43 × 18.0 / 12.8 = 16.07; final step (18.0 cm) → exactly 11.43.</param>
        public static SensitivityTransitionPlan GeneratePlan(
            double currentCmPer360,
            double targetCmPer360,
            int dpi,
            double baseSensitivity)
        {
            double delta = Math.Abs(targetCmPer360 - currentCmPer360);
            bool isGradual = delta >= MinDeltaCm;

            var plan = new SensitivityTransitionPlan
            {
                StartCmPer360   = currentCmPer360,
                TargetCmPer360  = targetCmPer360,
                TransitionType  = isGradual ? "Gradual" : "Direct",
                CurrentStepIndex = 0,
                IsComplete       = false,
                CreatedAt        = DateTime.Now,
            };

            if (!isGradual)
            {
                // Single direct step
                plan.Steps.Add(new SensitivityTransitionStep
                {
                    StepNumber        = 1,
                    TargetCmPer360    = targetCmPer360,
                    TargetSensitivity = CmPer360ToSens(targetCmPer360, dpi, baseSensitivity, targetCmPer360),
                    RequiredSessions  = SessionsPerStep,
                    CompletedSessions = 0,
                    IsComplete        = false,
                });
                return plan;
            }

            // Gradual: divide the gap into GradualSteps+1 equal segments
            int totalSteps = GradualSteps + 1;
            for (int i = 1; i <= totalSteps; i++)
            {
                double frac     = (double)i / totalSteps;
                double stepCm   = currentCmPer360 + (targetCmPer360 - currentCmPer360) * frac;
                double stepSens = CmPer360ToSens(stepCm, dpi, baseSensitivity, targetCmPer360);

                plan.Steps.Add(new SensitivityTransitionStep
                {
                    StepNumber        = i,
                    TargetCmPer360    = stepCm,
                    TargetSensitivity = stepSens,
                    RequiredSessions  = SessionsPerStep,
                    CompletedSessions = 0,
                    IsComplete        = false,
                });
            }

            return plan;
        }

        // ── Progress update ──────────────────────────────────────────────────
        /// <summary>
        /// Called after each training session to advance the active plan.
        /// Increments the current step's session count; advances to the next step
        /// once the required count is met.  Marks the plan complete when all steps
        /// are done, then saves settings.
        /// </summary>
        public static void UpdateProgress(UserSettings settings)
        {
            var plan = settings.ActiveTransitionPlan;
            if (plan == null || plan.IsComplete) return;

            if (plan.CurrentStepIndex >= plan.Steps.Count)
            {
                plan.IsComplete = true;
                SettingsService.Save(settings);
                return;
            }

            var step = plan.Steps[plan.CurrentStepIndex];
            step.CompletedSessions++;

            if (step.CompletedSessions >= step.RequiredSessions)
            {
                step.IsComplete = true;
                plan.CurrentStepIndex++;

                if (plan.CurrentStepIndex >= plan.Steps.Count)
                    plan.IsComplete = true;
            }

            SettingsService.Save(settings);
        }

        // ── Sensitivity query ─────────────────────────────────────────────────
        /// <summary>
        /// Returns the in-game sensitivity the user should be playing at right now,
        /// given the active plan's current step.  If there is no active plan, returns
        /// <paramref name="fallbackSensitivity"/>.
        /// </summary>
        public static double GetCurrentSensitivity(
            UserSettings settings,
            double fallbackSensitivity)
        {
            var plan = settings.ActiveTransitionPlan;
            if (plan == null || plan.IsComplete) return fallbackSensitivity;
            if (plan.CurrentStepIndex >= plan.Steps.Count) return fallbackSensitivity;

            return plan.Steps[plan.CurrentStepIndex].TargetSensitivity;
        }

        /// <summary>
        /// Converts a cm/360 value back to in-game sensitivity using a linear
        /// proportion relative to the user's current (known) sens / cm/360 pair.
        /// </summary>
        public static double GetGameSensitivity(
            double targetCmPer360,
            double knownCmPer360,
            double knownSensitivity)
        {
            if (knownCmPer360 <= 0) return knownSensitivity;
            // cm/360 is inversely proportional to sensitivity
            return knownSensitivity * (knownCmPer360 / targetCmPer360);
        }

        // ── Summary helpers ──────────────────────────────────────────────────
        /// <summary>
        /// Returns a progress description for UI display, e.g. "Step 2 / 5 · 1 / 3 sessions".
        /// </summary>
        public static string GetProgressSummary(SensitivityTransitionPlan plan)
        {
            if (plan.IsComplete) return "Transition complete ✓";
            if (plan.CurrentStepIndex >= plan.Steps.Count) return "Finalizing…";

            var step = plan.Steps[plan.CurrentStepIndex];
            return $"Step {plan.CurrentStepIndex + 1} / {plan.Steps.Count}  ·  " +
                   $"{step.CompletedSessions} / {step.RequiredSessions} sessions";
        }

        // ── Private helpers ───────────────────────────────────────────────────
        /// <summary>
        /// Converts cm/360 → in-game sens by proportional scaling from the user's
        /// starting reference point.
        /// </summary>
        private static double CmPer360ToSens(
            double targetCm,
            int    dpi,
            double baseSensitivity,
            double baseCmPer360)
        {
            // Fallback to GetGameSensitivity for the actual conversion
            if (baseCmPer360 <= 0) return baseSensitivity;
            return GetGameSensitivity(targetCm, baseCmPer360, baseSensitivity);
        }
    }
}
