namespace CleanAimTracker.Models
{
    public class AimTrainerResult
    {
        public DateTime Timestamp { get; set; }

        public string Scenario    { get; set; } = "";
        public string SubVariant  { get; set; } = "";   // e.g. "Smooth", "Evasive", "Standard"
        public string Difficulty  { get; set; } = "";
        public int    DurationSeconds { get; set; }

        public int Hits { get; set; }
        public int Misses { get; set; }
        public double Accuracy { get; set; }

        public int Score { get; set; }

        public double AvgReactionMs { get; set; }
        public double BestReactionMs { get; set; }

        public int MaxStreak { get; set; }

        // CAT_BOT_DRILLS: headshot kills in bot scenarios (Headshot Strafes,
        // Peek & Click, Track the Head). 0 for orb scenarios and legacy results —
        // consumers must gate on the scenario name, not on this being > 0.
        public int Headshots { get; set; } = 0;

        // Raw input derived metrics
        public double PathEfficiency          { get; set; } = 0;
        public double AvgClickOffset          { get; set; } = 0;
        // V2 (directional): -1 = not computed. Legacy sessions carry V1 values
        // (radial-distance buckets — inflated, non-directional); consumers MUST
        // gate on ClickMetricVersion >= 2 before treating these as overshoot.
        public double OvershootPct            { get; set; } = -1;
        public double UndershootPct           { get; set; } = -1;

        // Version of the click-point classification that produced Overshoot/
        // UndershootPct. 0 = legacy radial-distance metric (deserialized old
        // sessions default here); 2 = directional (approach-axis projection).
        // Cross-version comparisons are meaningless — V1 read 57-77% for
        // everyone; V2 reads near-0 for a centered clicker.
        public int    ClickMetricVersion      { get; set; } = 0;

        // AUDIT 2026-08-19: which MovementConsistency formula produced this reading.
        // 0 = legacy running-mean (inflated: first event always scored 100, and an
        //     expanding mean shrank every deviation)
        // 1 = fixed session mean
        // v1 readings sit slightly LOWER than v0 for identical movement, so a trend that
        // straddles the change would show a fake decline. Anything comparing consistency
        // ACROSS sessions must require matching versions — same rule as ClickMetricVersion.
        public int    MovementMetricVersion   { get; set; } = 0;
        public double AvgDirectionChangeLagMs { get; set; } = 0;
        public double FirstMotionAccuracy     { get; set; } = 0;
        public double HorizontalTrackingAcc   { get; set; } = 0;
        public double VerticalTrackingAcc     { get; set; } = 0;
        public double PeekEarlyClickPct       { get; set; } = 0;
        public double PeekLateClickPct        { get; set; } = 0;

        // A1: in-flight overshoot — mean per-acquisition axial excursion beyond the
        // click endpoint (see TelemetryCalculator.CalculateMovementOvershoot).
        // Sentinel -1 = NOT COMPUTED / invalid (too few segments). A missing field on
        // an older saved result deserializes to this initializer, so legacy data reads
        // "—", never a false "0 = perfectly clean". Valid values are >= 0.
        public double MovementOvershoot       { get; set; } = -1;
        // % of acquisition segments whose overshoot ratio cleared the noise floor —
        // the secondary descriptor for the coach voice ("you overshot on 60%…").
        public double OvershootSegmentPct     { get; set; } = -1;

        // T6: movement-quality metrics on drills (same shared calculator the tracker
        // uses). -1 = not computed / too few samples → "—", never a false 0.
        public double MovementSmoothness      { get; set; } = -1;
        public double MovementConsistency     { get; set; } = -1;
        // GATE 2: velocity (speed) consistency during active movement. -1 = invalid.
        public double VelocityStability       { get; set; } = -1;

        // Scenario classification
        public string Pillar                  { get; set; } = "";

        // Diagnostic assessment flag
        public bool   IsAssessmentSession     { get; set; } = false;
        public string AssessmentDimension     { get; set; } = "";
    }
}
