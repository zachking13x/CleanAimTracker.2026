using System;

namespace CleanAimTracker.Models
{
    /// <summary>
    /// TASK-2.2: the open verification loop for the active technique prescription.
    /// Created when a prescription is issued; resolved when the verify metric
    /// improves (loop closes as a strength) or escalated when it stays flat.
    /// </summary>
    public class PrescriptionState
    {
        public string PrescriptionKey { get; set; } = "";
        public DateTime PrescribedAt { get; set; }

        /// <summary>Scenario the diagnosis came from — follow-up compares only in matching context.</summary>
        public string ScenarioContext { get; set; } = "";

        public string VerifyMetric { get; set; } = "";
        public int ExpectedDirection { get; set; }   // MetricDirection as int for JSON stability

        /// <summary>The verify metric's value at prescribe time — the displayed "old" number.</summary>
        public double BaselineValue { get; set; }

        /// <summary>Drill sessions completed since prescribing (any scenario).</summary>
        public int SessionsSince { get; set; }

        /// <summary>The prescribed practice drill (for did-they-run-it detection + nudge).</summary>
        public string PracticeScenario { get; set; } = "";
        public string PracticeVariant { get; set; } = "";
        public string PracticeDifficulty { get; set; } = "";
        public string FocusCue { get; set; } = "";

        public bool PracticeDrillRun { get; set; }
        public bool NudgeShown { get; set; }

        /// <summary>True once the flat-after-3 escalation fired (never escalate twice).</summary>
        public bool Escalated { get; set; }
    }
}
