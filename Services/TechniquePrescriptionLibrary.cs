using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-1.2: the seed technique library — 12 prescriptions, each completing
    /// the loop: diagnose (valid-metric signature) → instruct (imperative
    /// technique change) → verify (the number that proves it worked).
    ///
    /// LANGUAGE RULE (TASK-2.3): instructions tell the user what to DO with
    /// hand/arm/eyes/crosshair. They never read the user's body ("you're tense",
    /// "cold hands") — the coach instructs bodies, it never diagnoses them.
    /// </summary>
    public static class TechniquePrescriptionLibrary
    {
        // ── Named thresholds ─────────────────────────────────────────────────
        // grip_tension_release: smoothness < 40 across 2+ valid sessions is the
        // spec'd trigger; correction sharpness > 60 is the same "elevated" bar
        // the session coach uses for its overcorrection area.
        public const double SmoothnessLowThreshold      = 40;
        public const int    MinLowSmoothnessSessions    = 2;
        public const double ElevatedCorrectionSharpness = 60;

        // Overshoot/undershoot: OvershootPct is the % of measured motions that
        // overshot. 25% = one in four motions — a dominant pattern, not noise.
        public const double OvershootHighPct       = 25;
        public const double ShootDominanceMargin   = 10;   // over vs under must differ by this
        public const double MidAccuracyMin         = 50;
        public const double MidAccuracyMax         = 85;

        // crosshair_preplacement: same 150ms best-vs-average gap the reaction_gap
        // observation uses — the prescription is that observation with a "how".
        public const double PreplacementGapMs      = 150;

        // eyes_lead_hands: direction-change lag. 200ms ≈ a full human visual
        // reaction spent AFTER the target already turned — the hand is chasing.
        public const double DirectionLagHighMs     = 200;

        // vertical_axis_training: horizontal exceeding vertical by 15 accuracy
        // points is past normal axis variance (typically < 10).
        public const double AxisGapPoints          = 15;

        // miss_reset_routine: one streak holding > 50% of all hits with the rest
        // scattered = focus collapse after misses, not a mechanics gap.
        public const double DominantStreakRatio    = 0.5;
        public const int    MinHitsForStreakRead   = 10;

        // peek timing: >30% early/late clicks = a decision-timing pattern.
        public const double PeekTimingHighPct      = 30;

        // speed_progression: accuracy ≥ 90 with pace ≥ 1.2 × the scenario's
        // "average" benchmark = accuracy headroom not being spent.
        public const double SpeedProgressionAccuracy = 90;
        public const double SlowPaceFactor           = 1.2;

        // control_protocol: accuracy below 45 while pacing at/under the
        // scenario's "good" benchmark = spraying faster than control allows.
        public const double ControlAccuracyFloor     = 45;
        public const int    MinClicksForSprayRead    = 15;

        // ── The library ──────────────────────────────────────────────────────

        public static readonly IReadOnlyList<TechniquePrescription> All = new List<TechniquePrescription>
        {
            new()
            {
                PrescriptionKey = "grip_tension_release",
                RequiredMetrics = { "SmoothnessScore" },
                // Smoothness comes from tracker sessions; validity is read off
                // each session's own MetricValidities (V3 Gate 1).
                Signature = ctx =>
                {
                    var smoothSessions = (ctx.Memory?.RecentTrackerSessions ?? new List<SessionSummary>())
                        .Where(s => s.IsMetricValid("SmoothnessScore"))
                        .Take(3)
                        .ToList();
                    bool lowSmooth = smoothSessions.Count >= MinLowSmoothnessSessions
                        && smoothSessions.Take(MinLowSmoothnessSessions)
                                         .All(s => s.SmoothnessScore < SmoothnessLowThreshold);
                    bool elevatedCorrection = smoothSessions.Count > 0
                        && smoothSessions[0].IsMetricValid("CorrectionSharpness")
                        && smoothSessions[0].CorrectionSharpness > ElevatedCorrectionSharpness;
                    return lowSmooth && elevatedCorrection;
                },
                Instruction = "Loosen your grip — fingertip pressure, not palm squeeze. Your hand should be relaxed enough that someone could pull the mouse away.",
                InstructionShort = "your grip pressure",
                // Spec names "Smoothness • Standard"; the repo's smoothness drill
                // is Tracking • Smooth (actual name recorded per protocol rule 5).
                GetPracticeDrill = _ => new PracticeDrill("Tracking", "Smooth", "Easy", "glide, don't grab"),
                VerifyMetric = "SmoothnessScore",
                ExpectedDirection = MetricDirection.Up
            },

            new()
            {
                PrescriptionKey = "arm_over_wrist",
                RequiredMetrics = { "OvershootPct" },
                Signature = ctx =>
                    ctx.Result.OvershootPct >= OvershootHighPct
                    && ctx.Result.Scenario is "Flicking" or "Switching" or "DynamicClicking" or "StaticClicking",
                Instruction = "Drive large movements from your forearm, save the wrist for the last few degrees. Anchor lightly at the elbow, not the wrist.",
                InstructionShort = "driving big movements from the forearm",
                GetPracticeDrill = ctx => new PracticeDrill(
                    "DynamicClicking", "Arc",
                    string.IsNullOrEmpty(ctx.Result.Difficulty) ? "Medium" : ctx.Result.Difficulty,
                    "arm carries, wrist finishes"),
                VerifyMetric = "OvershootPct",
                ExpectedDirection = MetricDirection.Down
            },

            new()
            {
                PrescriptionKey = "decelerate_into_target",
                RequiredMetrics = { "OvershootPct", "UndershootPct" },
                Signature = ctx =>
                    ctx.Result.OvershootPct > ctx.Result.UndershootPct + ShootDominanceMargin
                    && ctx.Result.Accuracy >= MidAccuracyMin
                    && ctx.Result.Accuracy <= MidAccuracyMax,
                Instruction = "Slow the last 10% of every flick — arrive at the target, don't crash through it.",
                InstructionShort = "braking into targets",
                GetPracticeDrill = _ => new PracticeDrill("Precision", "Standard", "Medium", "brake before the click"),
                VerifyMetric = "OvershootPct",
                ExpectedDirection = MetricDirection.Down
            },

            new()
            {
                PrescriptionKey = "commit_full_motion",
                RequiredMetrics = { "OvershootPct", "UndershootPct" },
                Signature = ctx =>
                    ctx.Result.UndershootPct > ctx.Result.OvershootPct + ShootDominanceMargin,
                Instruction = "Make one confident motion to the target, then one micro-correction — never three timid steps. Trust the first move.",
                InstructionShort = "committing to one full motion",
                GetPracticeDrill = _ => new PracticeDrill("StaticClicking", "Standard", "Medium", "one motion, one fix, click"),
                VerifyMetric = "UndershootPct",
                ExpectedDirection = MetricDirection.Down
            },

            new()
            {
                PrescriptionKey = "crosshair_preplacement",
                RequiredMetrics = { "AvgReactionMs" },
                Signature = ctx =>
                    ctx.Result.BestReactionMs > 0
                    && ctx.Result.AvgReactionMs > 0
                    && ctx.Result.Hits >= 5
                    && ctx.Result.AvgReactionMs - ctx.Result.BestReactionMs > PreplacementGapMs,
                Instruction = "Between targets, park your crosshair where the next target is likely to spawn, at target height — your eyes find it, your crosshair is already there.",
                InstructionShort = "pre-placing your crosshair",
                GetPracticeDrill = ctx => new PracticeDrill(
                    ctx.Result.Scenario,
                    string.IsNullOrEmpty(ctx.Result.SubVariant) ? "Standard" : ctx.Result.SubVariant,
                    string.IsNullOrEmpty(ctx.Result.Difficulty) ? "Medium" : ctx.Result.Difficulty,
                    "crosshair lives at head height"),
                VerifyMetric = "AvgReactionMs",
                ExpectedDirection = MetricDirection.Down
            },

            new()
            {
                PrescriptionKey = "eyes_lead_hands",
                RequiredMetrics = { "AvgDirectionChangeLagMs" },
                Signature = ctx =>
                    ctx.Result.AvgDirectionChangeLagMs > DirectionLagHighMs,
                Instruction = "Look at where the target is going before you move the mouse — snap your eyes first, let the hand follow. Don't chase the trail.",
                InstructionShort = "leading with your eyes",
                GetPracticeDrill = _ => new PracticeDrill("Reactive", "Standard", "Medium", "eyes jump, hand follows"),
                VerifyMetric = "AvgDirectionChangeLagMs",
                ExpectedDirection = MetricDirection.Down
            },

            new()
            {
                PrescriptionKey = "vertical_axis_training",
                RequiredMetrics = { "HorizontalTrackingAcc", "VerticalTrackingAcc" },
                Signature = ctx =>
                    ctx.Result.HorizontalTrackingAcc > 0
                    && ctx.Result.VerticalTrackingAcc > 0
                    && ctx.Result.HorizontalTrackingAcc - ctx.Result.VerticalTrackingAcc > AxisGapPoints,
                Instruction = "Your vertical control lags your horizontal. Add deliberate up-down arcs to warm-up and track with the arm — wrists are horizontal creatures.",
                InstructionShort = "vertical tracking control",
                GetPracticeDrill = _ => new PracticeDrill("AirTracking", "Jump Arc", "Easy", "follow the arc, not the target's body"),
                VerifyMetric = "VerticalTrackingAcc",
                ExpectedDirection = MetricDirection.Up
            },

            new()
            {
                PrescriptionKey = "miss_reset_routine",
                RequiredMetrics = { },   // streak/hits are always captured
                Signature = ctx =>
                    ctx.Result.Hits >= MinHitsForStreakRead
                    && ctx.Result.MaxStreak > ctx.Result.Hits * DominantStreakRatio
                    && ctx.Result.Accuracy < 80,   // scattered outside the streak
                Instruction = "After every miss: exhale, re-center your crosshair to neutral, treat the next target as target #1. Misses compound only if you carry them.",
                InstructionShort = "your miss-reset routine",
                GetPracticeDrill = ctx => new PracticeDrill(
                    ctx.Result.Scenario,
                    string.IsNullOrEmpty(ctx.Result.SubVariant) ? "Standard" : ctx.Result.SubVariant,
                    string.IsNullOrEmpty(ctx.Result.Difficulty) ? "Medium" : ctx.Result.Difficulty,
                    "every target is the first target"),
                VerifyMetric = "HitsOutsideStreakRatio",
                ExpectedDirection = MetricDirection.Up
            },

            new()
            {
                PrescriptionKey = "peek_discipline_wait",
                RequiredMetrics = { "PeekEarlyClickPct" },
                Signature = ctx =>
                    ctx.Result.Scenario == "PeekTraining"
                    && ctx.Result.PeekEarlyClickPct > PeekTimingHighPct,
                Instruction = "You're firing before the target is fully exposed. Hold until you see the whole silhouette — a confirmed hit beats a fast whiff.",
                InstructionShort = "holding fire until full exposure",
                GetPracticeDrill = _ => new PracticeDrill("PeekTraining", "Counter Strafe", "Medium", "see it all, then shoot"),
                VerifyMetric = "PeekEarlyClickPct",
                ExpectedDirection = MetricDirection.Down
            },

            new()
            {
                PrescriptionKey = "peek_commit_earlier",
                RequiredMetrics = { "PeekLateClickPct" },
                Signature = ctx =>
                    ctx.Result.Scenario == "PeekTraining"
                    && ctx.Result.PeekLateClickPct > PeekTimingHighPct
                    // If both timing patterns fire, early-click discipline wins.
                    && ctx.Result.PeekEarlyClickPct <= PeekTimingHighPct,
                Instruction = "You're confirming too long — decide to shoot during the peek, not after it. Pre-commit: if the target appears, the click happens.",
                InstructionShort = "committing during the peek",
                GetPracticeDrill = _ => new PracticeDrill("PeekTraining", "Jiggle", "Medium", "decide before you peek"),
                VerifyMetric = "PeekLateClickPct",
                ExpectedDirection = MetricDirection.Down
            },

            new()
            {
                PrescriptionKey = "speed_progression",
                RequiredMetrics = { "AvgReactionMs" },
                Signature = ctx =>
                    ctx.Result.Accuracy >= SpeedProgressionAccuracy
                    && ctx.Result.AvgReactionMs >
                       AiCoachService.Bench.ReactionAverage(ctx.Result.Scenario) * SlowPaceFactor
                    && AiCoachService.Bench.ReactionAverage(ctx.Result.Scenario) > 0,
                Instruction = "Your accuracy has headroom to spend. Deliberately overspeed: accept dropping to ~75% accuracy while forcing faster commits — then rebuild accuracy at the new speed. Miss fast, not slow.",
                InstructionShort = "spending accuracy for speed",
                GetPracticeDrill = ctx => new PracticeDrill(
                    ctx.Result.Scenario,
                    string.IsNullOrEmpty(ctx.Result.SubVariant) ? "Standard" : ctx.Result.SubVariant,
                    StepDifficultyUp(ctx.Result.Difficulty),
                    "speed first this week, accuracy follows"),
                VerifyMetric = "AvgReactionMs",
                ExpectedDirection = MetricDirection.Down
            },

            new()
            {
                PrescriptionKey = "control_protocol",
                RequiredMetrics = { "AvgReactionMs" },
                Signature = ctx =>
                    ctx.Result.Accuracy < ControlAccuracyFloor
                    && ctx.Result.Hits + ctx.Result.Misses >= MinClicksForSprayRead
                    && ctx.Result.AvgReactionMs > 0
                    && ctx.Result.AvgReactionMs <=
                       AiCoachService.Bench.ReactionGood(ctx.Result.Scenario)
                    && AiCoachService.Bench.ReactionGood(ctx.Result.Scenario) > 0,
                Instruction = "You're outrunning your control. Drop to 90% of your natural speed and make every click deliberate — earn the speed back with hits.",
                InstructionShort = "slowing down for control",
                GetPracticeDrill = _ => new PracticeDrill("Precision", "Standard", "Easy", "slow is smooth, smooth is fast"),
                VerifyMetric = "Accuracy",
                ExpectedDirection = MetricDirection.Up
            },
        };

        /// <summary>
        /// All prescriptions whose signature fires for this context, in library
        /// order. A signature is never evaluated unless every RequiredMetrics
        /// entry is valid this session (V3 Gate 1 rule).
        /// </summary>
        public static List<TechniquePrescription> Triggered(PrescriptionContext ctx)
        {
            var result = new List<TechniquePrescription>();
            foreach (var p in All)
            {
                if (!p.RequiredMetrics.All(ctx.IsMetricValid)) continue;
                bool fired;
                try { fired = p.Signature(ctx); }
                catch { fired = false; }   // a broken signature must never crash a report
                if (fired) result.Add(p);
            }
            return result;
        }

        /// <summary>Reads the current value of a VerifyMetric from a drill result.</summary>
        public static double ReadVerifyMetric(string metric, AimTrainerResult r) => metric switch
        {
            "SmoothnessScore"         => 0, // tracker metric — read from SessionSummary by the caller
            "OvershootPct"            => r.OvershootPct,
            "UndershootPct"           => r.UndershootPct,
            "AvgReactionMs"           => r.AvgReactionMs,
            "AvgDirectionChangeLagMs" => r.AvgDirectionChangeLagMs,
            "VerticalTrackingAcc"     => r.VerticalTrackingAcc,
            "PeekEarlyClickPct"       => r.PeekEarlyClickPct,
            "PeekLateClickPct"        => r.PeekLateClickPct,
            "Accuracy"                => r.Accuracy,
            // Hand-check: 30 hits, max streak 18 → (30−18)/30 = 0.4 outside the streak.
            "HitsOutsideStreakRatio"  => r.Hits > 0 ? (double)(r.Hits - r.MaxStreak) / r.Hits : 0,
            _                         => 0
        };

        private static string StepDifficultyUp(string difficulty) => difficulty switch
        {
            "Easy"   => "Medium",
            "Medium" => "Hard",
            "Hard"   => "Nightmare",
            _        => "Medium"
        };
    }
}
