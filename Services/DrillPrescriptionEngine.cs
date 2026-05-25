using CleanAimTracker.Models;
using System.Collections.Generic;
using static CleanAimTracker.Services.AiCoachService;

namespace CleanAimTracker.Services
{
    public static class DrillPrescriptionEngine
    {
        /// <summary>
        /// Returns 1-2 specific drill prescriptions based on the diagnosed weak area
        /// and supporting metric context from the aim trainer result and tracker data.
        /// </summary>
        internal static List<DrillPrescription> Prescribe(
            AimTrainerResult result,
            CoachContext context,
            SessionSummary? recentTracker = null)
        {
            var prescriptions = new List<DrillPrescription>();

            // ── Rule 1: High correction sharpness → overshooting problem ──────
            if (context.WeakArea == "reaction"
                && recentTracker != null
                && recentTracker.CorrectionSharpness > 60)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Flicking",
                    Difficulty  = "Medium",
                    SubVariant  = "Single Target",
                    DurationSec = 60,
                    Reason      = "Your correction sharpness is high — you are overshooting and yanking back to correct. " +
                                  "Flicking Single Target trains you to land your first motion cleanly.",
                    FocusCue    = "Commit to one movement. Do not correct — if you miss, let it go and hit the next one."
                });
                return prescriptions;
            }

            // ── Rule 2: Low smoothness in Tracking ────────────────────────────
            if (context.WeakArea == "accuracy"
                && result.Scenario == "Tracking"
                && recentTracker != null
                && recentTracker.SmoothnessScore < 60)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Tracking",
                    Difficulty  = "Easy",
                    SubVariant  = "Smooth Arc",
                    DurationSec = 90,
                    Reason      = $"Your smoothness score is {recentTracker.SmoothnessScore:F0}/100 — " +
                                  "jitter is breaking your tracking. Easy Smooth Arc drills at lower speed " +
                                  "build the fluid movement pattern your muscle memory needs before adding speed.",
                    FocusCue    = "Keep your elbow on the desk. Move from the wrist only. Think fluid, not fast."
                });
                return prescriptions;
            }

            // ── Rule 3: Sensitivity too low for Precision ─────────────────────
            if (context.WeakArea == "accuracy"
                && result.Scenario == "Precision"
                && recentTracker != null
                && recentTracker.CmPer360 < 20)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Precision",
                    Difficulty  = "Easy",
                    SubVariant  = "Static",
                    DurationSec = 60,
                    Reason      = "Your sensitivity is too high for Precision — hitting small targets becomes " +
                                  "physically difficult at this sensitivity level. " +
                                  "Check the Recommend screen for your target range, adjust in-game, " +
                                  "then run Precision Static at Easy to recalibrate.",
                    FocusCue    = "Fix the settings first. More drilling at the wrong sensitivity builds bad habits."
                });
                return prescriptions;
            }

            // ── Rule 4: Sensitivity too high for Precision ────────────────────
            if (context.WeakArea == "accuracy"
                && result.Scenario == "Precision"
                && recentTracker != null
                && recentTracker.CmPer360 > 55)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Precision",
                    Difficulty  = "Easy",
                    SubVariant  = "Static",
                    DurationSec = 60,
                    Reason      = "Your sensitivity is too low for Precision — your cursor jumps too far per hand movement, " +
                                  "making small targets hard to land on consistently. " +
                                  "Lower your in-game sensitivity and retest on Easy before increasing difficulty.",
                    FocusCue    = "Slow is smooth. Smooth is accurate. Speed comes after the mechanics are clean."
                });
                return prescriptions;
            }

            // ── Rule 5: Low accuracy in Switching ─────────────────────────────
            if (context.WeakArea == "accuracy"
                && result.Scenario == "Switching"
                && result.Accuracy < 65)
            {
                string targetDiff = result.Difficulty == "Hard" || result.Difficulty == "Nightmare"
                    ? "Medium" : "Easy";
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Switching",
                    Difficulty  = targetDiff,
                    SubVariant  = "Two Target",
                    DurationSec = 60,
                    Reason      = $"Your Switching accuracy of {result.Accuracy:F0}% suggests you are rushing " +
                                  "the target acquisition — clicking before your crosshair is actually on target. " +
                                  $"Drop to {targetDiff} Two Target and prioritize click accuracy over speed.",
                    FocusCue    = "See the target clearly before you click. Speed will follow accuracy — not the other way around."
                });
                return prescriptions;
            }

            // ── Rule 6: Streak weakness → focus endurance ─────────────────────
            if (context.WeakArea == "streak"
                && result.MaxStreak < 8
                && result.DurationSeconds >= 60)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = result.Scenario,
                    Difficulty  = "Easy",
                    SubVariant  = "Standard",
                    DurationSec = 90,
                    Reason      = "Your streak breaks suggest focus dropping mid-session — " +
                                  "you start well but concentration fades. " +
                                  "Run a longer Easy session specifically to hold concentration the whole way through. " +
                                  "Endurance is a trainable skill.",
                    FocusCue    = "Set one goal: do not let your attention wander. Not speed, not accuracy — just presence."
                });
                return prescriptions;
            }

            // ── Rule 7: Slow reaction in Flicking ────────────────────────────
            if (context.WeakArea == "reaction"
                && result.Scenario == "Flicking"
                && result.AvgReactionMs > 400)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Flicking",
                    Difficulty  = "Medium",
                    SubVariant  = "Timed Pressure",
                    DurationSec = 60,
                    Reason      = $"Your {result.AvgReactionMs:F0}ms average reaction in Flicking is slow for this scenario. " +
                                  "Timed Pressure targets shrink if you wait — it forces faster target initiation " +
                                  "without training you to be sloppy.",
                    FocusCue    = "React to the appearance of the target, not to the position. Trust your first instinct."
                });
                return prescriptions;
            }

            // ── Rule 8: High idle percentage ──────────────────────────────────
            if (recentTracker != null && recentTracker.IdlePercentage > 40)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Tracking",
                    Difficulty  = "Medium",
                    SubVariant  = "Erratic",
                    DurationSec = 60,
                    Reason      = $"Your session had {recentTracker.IdlePercentage:F0}% idle time — " +
                                  "you are zoning out during training. Tracking Erratic forces constant active engagement " +
                                  "because the target changes direction unpredictably.",
                    FocusCue    = "No cruise control. Stay engaged every second."
                });
                return prescriptions;
            }

            // ── Rule 9: Good accuracy → difficulty progression ────────────────
            if (context.AccuracyGrade is "elite" or "good"
                && result.Difficulty != "Nightmare")
            {
                string nextDiff = result.Difficulty switch
                {
                    "Easy"   => "Medium",
                    "Medium" => "Hard",
                    "Hard"   => "Nightmare",
                    _        => "Hard"
                };
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = result.Scenario,
                    Difficulty  = nextDiff,
                    SubVariant  = "Standard",
                    DurationSec = result.DurationSeconds,
                    Reason      = $"You hit {result.Accuracy:F0}% accuracy on {result.Difficulty} — " +
                                  $"that earns a step up. {nextDiff} will expose the gaps that {result.Difficulty} hides.",
                    FocusCue    = "The difficulty is supposed to feel hard. That discomfort is where the growth happens."
                });
                return prescriptions;
            }

            // ── Rule 10: Fallback ─────────────────────────────────────────────
            prescriptions.Add(new DrillPrescription
            {
                Scenario    = result.Scenario,
                Difficulty  = result.Difficulty,
                SubVariant  = "Standard",
                DurationSec = 60,
                Reason      = $"Continue {result.Scenario} at {result.Difficulty} — " +
                              "build consistency at this level before stepping up.",
                FocusCue    = "Consistency over performance. Same scenario, same difficulty, until the accuracy stabilizes."
            });
            return prescriptions;
        }
    }
}
