using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    /// <summary>Where a metric's rolling average sits relative to its authored bands.</summary>
    public enum MetricBand { Poor, Neutral, Good }

    /// <summary>Direction the metric's value has moved over the window (raw, not "better/worse").</summary>
    public enum TrendDir { Down, Flat, Up }

    /// <summary>One metric's cross-session aggregate — the only numbers the profile may speak from.</summary>
    public class MetricAggregate
    {
        public string Metric { get; init; } = "";
        public double Mean { get; init; }
        public int SampleCount { get; init; }
        public MetricBand Band { get; init; }
        public TrendDir Trend { get; init; }
        /// <summary>True when lower values are better (overshoot, undershoot, in-flight overshoot).</summary>
        public bool LowerIsBetter { get; init; }
    }

    /// <summary>
    /// COACH_AIM_PROFILE: the cross-session characterization of a player, built ONLY
    /// from validated metric aggregates. (Named PlayerAimProfile to avoid colliding with
    /// the existing sensitivity-profile <see cref="AimProfile"/>.) This object carries
    /// DATA — aggregates, and which strengths/weaknesses/named patterns are true — never
    /// free text. The phrasing layer assembles user-facing sentences from authored
    /// fragments selected by these booleans, so nothing the profile says can outrun the data.
    /// </summary>
    public class PlayerAimProfile
    {
        /// <summary>False until MinSessions valid sessions exist; then the "need more" state shows.</summary>
        public bool HasEnoughData { get; init; }
        public int  SessionsAnalyzed { get; init; }
        public int  SessionsNeeded   { get; init; }   // 0 once HasEnoughData

        public Dictionary<string, MetricAggregate> Metrics { get; init; } = new();

        /// <summary>Metric keys whose rolling average sits in the good band.</summary>
        public List<string> StrengthMetrics { get; init; } = new();
        /// <summary>Metric keys whose rolling average sits in the poor band.</summary>
        public List<string> WeaknessMetrics { get; init; } = new();

        /// <summary>Named cross-metric patterns whose conditions are TRUE, strongest first.</summary>
        public List<string> Patterns { get; init; } = new();

        // ── The "headroom" relationship (best reaction « average) ──────────────
        public double ReactionGapMs       { get; init; }   // mean(avg − best) over valid sessions
        public bool   HasReactionHeadroom { get; init; }
        public double MeanBestReactionMs  { get; init; }
        public double MeanAvgReactionMs   { get; init; }

        // Most-played scenario, for per-scenario notes in the full read.
        public string PrimaryScenario { get; init; } = "";

        // ceiling_gap evidence (0 when the pattern didn't fire).
        public double CeilingEasyMedAcc { get; init; }
        public double CeilingHardAcc    { get; init; }

        // Accuracy trend within the PRIMARY scenario only (same-scenario, apples-to-apples)
        // — a cross-scenario accuracy trend is noise (Reactive vs StaticClicking baselines differ).
        public TrendDir PrimaryAccuracyTrend { get; init; } = TrendDir.Flat;

        // ── Rendered text (filled by AimProfilePhrasing — Gate 2) ──────────────
        public string Condensed { get; set; } = "";
        public List<string> FullLines { get; set; } = new();
    }
}
