using System.Collections.Generic;

namespace CleanAimTracker.Models
{
    /// <summary>
    /// Structured historical context the coaching engine receives before generating any report.
    /// Built by CoachMemoryBuilder from storage before every coaching call.
    /// </summary>
    public class CoachMemory
    {
        // ── Aim trainer history ───────────────────────────────────────
        public List<AimTrainerResult> RecentDrills { get; set; } = new();  // last 10, newest first
        public List<AimTrainerResult> AllDrills    { get; set; } = new();  // full history, newest first
        public int TotalDrillCount                 { get; set; } = 0;

        // ── Per-scenario baselines (calculated from history excluding current) ──
        public Dictionary<string, double> BaselineAccuracy    { get; set; } = new();
        public Dictionary<string, double> BaselineReactionMs  { get; set; } = new();
        public Dictionary<string, double> BaselineConsistency { get; set; } = new();
        public Dictionary<string, int>    SessionsPerScenario { get; set; } = new();

        // ── Trend data (last 5 vs previous 5) ────────────────────────
        public double AccuracyTrend    { get; set; } = 0;  // positive = improving
        public double ReactionTrend    { get; set; } = 0;  // negative = improving (lower is better)
        public double ConsistencyTrend { get; set; } = 0;  // positive = improving

        // ── Plateau detection (scenario-specific, based on prior sessions) ──
        public bool   IsAccuracyPlateaued { get; set; } = false;
        public bool   IsReactionPlateaued { get; set; } = false;
        public int    PlateauLength       { get; set; } = 0;
        public double PlateauAvgAccuracy  { get; set; } = 0;

        // ── Previous coaching context ─────────────────────────────────
        public string LastPrescribedScenario    { get; set; } = "";
        public string LastPrescribedDifficulty  { get; set; } = "";
        public bool   LastPrescriptionFollowed  { get; set; } = false;

        // ── Session tracker history ───────────────────────────────────
        public List<SessionSummary> RecentTrackerSessions  { get; set; } = new();
        public double TrackerSmoothnessBaseline            { get; set; } = 0;
        public double TrackerConsistencyBaseline           { get; set; } = 0;

        // ── Free coaching session state ───────────────────────────────
        public bool HasUsedFreeFullSession      { get; set; } = false;
        public int  SessionsSinceLastFullCoach  { get; set; } = 0;

        // ── Identity ──────────────────────────────────────────────────
        public string MostPlayedScenario    { get; set; } = "";
        public string WeakestScenario       { get; set; } = "";
        public string StrongestScenario     { get; set; } = "";
        public double PersonalBestAccuracy  { get; set; } = 0;  // best from PRIOR sessions only
        public string PersonalBestScenario  { get; set; } = "";

        // ── TASK-29: Telemetry averages (per-scenario, from prior sessions) ──
        /// <summary>Average path efficiency across the last 5 same-scenario drills (0..1).</summary>
        public double AvgPathEfficiency      { get; set; } = 0;
        /// <summary>Average click offset (px) across the last 5 same-scenario drills.</summary>
        public double AvgClickOffset         { get; set; } = 0;
        /// <summary>Average direction-change lag (ms) across the last 5 same-scenario drills.</summary>
        public double AvgDirectionChangeLag  { get; set; } = 0;

        // ── TASK-29: Previous-session telemetry (for delta comparisons) ───────
        /// <summary>PathEfficiency from the immediately preceding same-scenario session.</summary>
        public double PrevSessionPathEfficiency     { get; set; } = 0;
        /// <summary>OvershootPct from the immediately preceding same-scenario session.</summary>
        public double PrevSessionOvershootPct       { get; set; } = 0;
        /// <summary>DirectionChangeLag from the immediately preceding same-scenario session.</summary>
        public double PrevSessionDirectionChangeLag { get; set; } = 0;

        // ── TASK-29: Latest diagnostic profile ───────────────────────────────
        /// <summary>Most recent DiagnosticProfile, or null if no assessment has been run.</summary>
        public DiagnosticProfile? LatestDiagnostic  { get; set; } = null;
    }
}
