using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-2.2: the verification loop. On every drill report with an open
    /// prescription, compares the verify metric against the stored baseline and
    /// emits exactly one high-priority candidate:
    ///   improved → loop-closure strength (beats generic strengths), loop closes
    ///   flat 3+ sessions → honest escalation with a varied practice drill
    ///   practice drill not run → one nudge, once, no guilt
    /// Old and new values are displayed; the delta is their exact arithmetic.
    /// </summary>
    public static class PrescriptionFollowUpService
    {
        /// <summary>Relative change that counts as "moved": 5%.</summary>
        public const double ImprovementFactor = 0.05;

        /// <summary>Sessions of no movement before the approach changes.</summary>
        public const int FlatSessionsBeforeEscalation = 3;

        public static CoachObservation? Evaluate(
            CoachMemory memory, AimTrainerResult result, Func<string, bool> isMetricValid)
        {
            var state = memory?.ActivePrescription;
            if (state == null || memory == null) return null;

            var prescription = TechniquePrescriptionLibrary.All
                .FirstOrDefault(p => p.PrescriptionKey == state.PrescriptionKey);
            if (prescription == null)
            {
                memory.ActivePrescription = null;   // library changed — drop stale state
                return null;
            }

            state.SessionsSince++;
            if (result.Scenario == state.PracticeScenario)
                state.PracticeDrillRun = true;

            // ── Compare only in matching scenario context with a valid metric ──
            bool contextMatches = result.Scenario == state.ScenarioContext
                               || result.Scenario == state.PracticeScenario;
            bool metricValid = prescription.RequiredMetrics.All(isMetricValid);

            if (contextMatches && metricValid)
            {
                double oldV = state.BaselineValue;
                double newV = TechniquePrescriptionLibrary.ReadVerifyMetric(state.VerifyMetric, result);

                bool improved = (MetricDirection)state.ExpectedDirection == MetricDirection.Down
                    ? newV < oldV * (1 - ImprovementFactor)
                    : newV > oldV * (1 + ImprovementFactor);

                if (improved && newV > 0)
                {
                    memory.ActivePrescription = null;   // loop closed
                    return new CoachObservation
                    {
                        FactKey      = "prescription_followup",
                        SourceEngine = nameof(PrescriptionFollowUpService),
                        Section      = CoachSection.Strength,
                        Polarity     = ObservationPolarity.Strength,
                        Severity     = 60,   // beats generic strengths (base 50)
                        RequiredMetrics = prescription.RequiredMetrics.ToList(),
                        // Displayed-arithmetic rule: old and new shown, delta exact.
                        Message = $"Last session you worked on {prescription.InstructionShort}. " +
                                  $"{MetricLabel(state.VerifyMetric)} went {oldV:F0} → {newV:F0}. " +
                                  "It's working — keep the change."
                    };
                }

                if (!improved && state.SessionsSince >= FlatSessionsBeforeEscalation && !state.Escalated)
                {
                    // Escalate, never repeat: vary the practice drill difficulty
                    // (down — make the rep easier to isolate the technique) and
                    // say plainly that the first approach hasn't moved the number.
                    state.Escalated = true;
                    string easier = StepDifficultyDown(state.PracticeDifficulty);
                    state.PracticeDifficulty = easier;

                    return new CoachObservation
                    {
                        FactKey      = "prescription_followup",
                        SourceEngine = nameof(PrescriptionFollowUpService),
                        Section      = CoachSection.Area,
                        Polarity     = ObservationPolarity.Concern,
                        Severity     = 75,   // honesty about a stalled loop leads the report
                        RequiresBehaviorChange = true,
                        RequiredMetrics = prescription.RequiredMetrics.ToList(),
                        Message = $"{MetricLabel(state.VerifyMetric)} hasn't moved in {state.SessionsSince} sessions " +
                                  $"({oldV:F0} → {newV:F0}) — let's change the approach. Same focus, easier rep: " +
                                  $"{state.PracticeScenario} · {state.PracticeVariant} at {easier}. {prescription.Instruction}"
                    };
                }
            }

            // ── Practice drill not run: one nudge, once, no guilt ─────────────
            if (!state.PracticeDrillRun && !state.NudgeShown && state.SessionsSince >= 1)
            {
                state.NudgeShown = true;
                return new CoachObservation
                {
                    FactKey      = "prescription_followup",
                    SourceEngine = nameof(PrescriptionFollowUpService),
                    Section      = CoachSection.Tip,
                    Polarity     = ObservationPolarity.Neutral,
                    Severity     = 45,
                    Message = $"The {state.PracticeScenario} · {state.PracticeVariant} drill from last time " +
                              $"is still the fastest path to fixing {prescription.InstructionShort}."
                };
            }

            return null;
        }

        public static string MetricLabel(string verifyMetric) => verifyMetric switch
        {
            "SmoothnessScore"         => "Smoothness",
            "OvershootPct"            => "Overshoot",
            "UndershootPct"           => "Undershoot",
            "AvgReactionMs"           => "Your average pace",
            "AvgDirectionChangeLagMs" => "Direction-change lag",
            "VerticalTrackingAcc"     => "Vertical tracking accuracy",
            "PeekEarlyClickPct"       => "Early-click rate",
            "PeekLateClickPct"        => "Late-click rate",
            "Accuracy"                => "Accuracy",
            "HitsOutsideStreakRatio"  => "Your hit spread",
            _                         => verifyMetric
        };

        private static string StepDifficultyDown(string difficulty) => difficulty switch
        {
            "Nightmare" => "Hard",
            "Hard"      => "Medium",
            "Medium"    => "Easy",
            _           => "Easy"
        };
    }
}
