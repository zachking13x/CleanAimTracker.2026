using System;
using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    public enum MetricDirection { Down, Up }

    /// <summary>The drill that practices the prescribed technique change.</summary>
    public record PracticeDrill(
        string Scenario,
        string Variant,
        string Difficulty,
        string FocusCue)
    {
        public override string ToString() =>
            $"{Scenario} · {Variant} — {Difficulty} — focus: \"{FocusCue}\"";
    }

    /// <summary>Everything a prescription signature may inspect.</summary>
    public record PrescriptionContext(
        AimTrainerResult Result,
        CoachMemory Memory,
        Func<string, bool> IsMetricValid);

    /// <summary>
    /// TASK-1.1: a technique prescription — the complete coaching loop in one
    /// object: the diagnostic trigger (Signature), the physical/technique change
    /// (Instruction — imperative, never a body-state diagnosis), the drill that
    /// practices it (PracticeDrill), and the number that proves it worked
    /// (VerifyMetric + ExpectedDirection).
    /// </summary>
    public class TechniquePrescription
    {
        public string PrescriptionKey { get; init; } = "";

        /// <summary>
        /// The diagnostic trigger: metric conditions with thresholds. Evaluated
        /// only when every RequiredMetrics entry is valid this session.
        /// </summary>
        public Func<PrescriptionContext, bool> Signature { get; init; } = _ => false;

        /// <summary>Metrics that must be VALID before the signature may fire.</summary>
        public List<string> RequiredMetrics { get; init; } = new();

        /// <summary>
        /// The physical/technique change — imperative and specific (what to do
        /// with hand/arm/eyes/crosshair), never a read of the user's body state.
        /// </summary>
        public string Instruction { get; init; } = "";

        /// <summary>Short noun phrase for follow-ups: "Last session you worked on
        /// [InstructionShort]" — e.g. "your grip pressure".</summary>
        public string InstructionShort { get; init; } = "";

        /// <summary>Practice drill, computed from context (some prescriptions use
        /// the current scenario/difficulty or a difficulty step-up).</summary>
        public Func<PrescriptionContext, PracticeDrill> GetPracticeDrill { get; init; } =
            _ => new PracticeDrill("Precision", "Standard", "Medium", "");

        /// <summary>The metric that proves the change worked.</summary>
        public string VerifyMetric { get; init; } = "";
        public MetricDirection ExpectedDirection { get; init; }

        /// <summary>Minimum sessions before this key may be prescribed again.</summary>
        public int CooldownSessions { get; init; } = 3;
    }
}
