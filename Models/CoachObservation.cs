using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    public enum ObservationPolarity { Strength, Concern, Neutral }

    /// <summary>Which report section a candidate competes for.</summary>
    public enum CoachSection { Strength, Area, Tip, Prescription, Headline }

    /// <summary>
    /// TASK-2.3: prescriptions carry a type so severity can match the verdict —
    /// a strength-led report may never receive a Remedial prescription.
    /// </summary>
    public enum PrescriptionType { None, Remedial, Progression, Maintenance }

    /// <summary>
    /// TASK-2.1: the single candidate unit every coaching engine emits.
    /// Nothing renders user-facing coach text except observations that survive
    /// CoachReportComposer.
    ///
    /// FactKey rule — keys encode the ASPECT, not the stat:
    ///   "reaction_speed_assessment" — is the user's reaction fast or slow (one verdict, ever)
    ///   "reaction_trend"            — is reaction improving over sessions (distinct aspect, may coexist)
    ///   "reaction_gap"              — best-vs-average spread (distinct aspect, may coexist)
    /// Litmus: would a human coach say both statements in the same breath without
    /// arguing with himself? If no → same FactKey.
    /// </summary>
    public class CoachObservation
    {
        public string FactKey { get; set; } = "";
        public string SourceEngine { get; set; } = "";
        public CoachSection Section { get; set; } = CoachSection.Tip;
        public ObservationPolarity Polarity { get; set; } = ObservationPolarity.Neutral;

        /// <summary>Higher severity wins within a FactKey; orders candidates within a section.</summary>
        public int Severity { get; set; } = 0;

        /// <summary>True when acting on this observation requires the user to change behavior.</summary>
        public bool RequiresBehaviorChange { get; set; } = false;

        public string Message { get; set; } = "";

        /// <summary>
        /// Metric property names ("SmoothnessScore", "MovementConsistency", ...) this
        /// observation depends on. If ANY is invalid this session, the observation is
        /// discarded by the composer's validity filter.
        /// </summary>
        public List<string> RequiredMetrics { get; set; } = new();

        /// <summary>Only meaningful when Section == Prescription.</summary>
        public PrescriptionType PrescriptionType { get; set; } = PrescriptionType.None;
    }
}
