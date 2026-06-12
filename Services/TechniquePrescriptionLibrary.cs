using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// VOICE TASK-1.1: the technique library — 13 prescriptions, each speaking in
    /// four beats: EVIDENCE (the user's own numbers, this session, valid metrics
    /// only) → CAUSE (second-person mechanical attribution) → INSTRUCTION (the
    /// physical fix, imperative) → STAKE (the metric that proves it next session).
    ///
    /// Rule of attribution: the coach may assert any MECHANICAL BEHAVIOR the data
    /// can see (gripping, wrist-flicking, hesitating, orbiting, crashing). It may
    /// never assert BODY STATES the data cannot see (cold, tired, tense-as-mood).
    /// No number cited, no accusation allowed.
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

        // movement_efficiency: top players route > 0.85; below 0.55 nearly half
        // the motion is wasted orbiting. (The 0.15 degenerate floor lives in
        // DrillMetricValid — below that the metric is invalid, not low.)
        public const double PathEfficiencyLowFraction = 0.55;
        public const int    MinHitsForEfficiencyRead  = 5;

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
                    var smoothSessions = ValidSmoothnessSessions(ctx);
                    bool lowSmooth = smoothSessions.Count >= MinLowSmoothnessSessions
                        && smoothSessions.Take(MinLowSmoothnessSessions)
                                         .All(s => s.SmoothnessScore < SmoothnessLowThreshold);
                    bool elevatedCorrection = smoothSessions.Count > 0
                        && smoothSessions[0].IsMetricValid("CorrectionSharpness")
                        && smoothSessions[0].CorrectionSharpness > ElevatedCorrectionSharpness;
                    return lowSmooth && elevatedCorrection;
                },
                Instruction = "Loosen to fingertip pressure — relaxed enough that someone could pull the mouse out of your hand.",
                InstructionShort = "your grip pressure",
                CauseClause = "you're still gripping too tight",
                ComposeMessage = ctx =>
                {
                    var sessions = ValidSmoothnessSessions(ctx);
                    double latest = sessions.Count > 0 ? sessions[0].SmoothnessScore : 0;
                    int n = Math.Max(1, Math.Min(sessions.Count, MinLowSmoothnessSessions));
                    return $"Smoothness {latest:F0}/100 across your last {n} sessions — your corrections are jagged, not flowing. " +
                           "You're gripping too tight; the mouse is fighting you. Loosen to fingertip pressure — relaxed enough " +
                           "that someone could pull the mouse out of your hand. If it's working, smoothness climbs next session.";
                },
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
                Instruction = "Drive big movements from the forearm and anchor lightly at the elbow — the wrist only finishes the last few degrees.",
                InstructionShort = "driving big movements from the forearm",
                CauseClause = "you're still throwing big movements from the wrist",
                ComposeMessage = ctx =>
                    $"Overshoot hit {ctx.Result.OvershootPct:F0}% on large flicks — that's a wrist-flick signature. " +
                    "You're throwing big movements from the wrist. Drive them from the forearm and anchor lightly at the elbow — " +
                    "the wrist only finishes the last few degrees. Overshoot drops next session if the change is landing.",
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
                Instruction = "Brake the last 10% of every flick: arrive, settle, click.",
                InstructionShort = "braking into targets",
                CauseClause = "you're still crashing through targets",
                ComposeMessage = ctx =>
                    $"Overshoot {ctx.Result.OvershootPct:F0}% against {ctx.Result.UndershootPct:F0}% undershoot — " +
                    "you're crashing through targets instead of arriving at them. Brake the last 10% of every flick: " +
                    "arrive, settle, click. Watch overshoot fall next session.",
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
                Instruction = "Make one confident motion, one micro-correction, click. Trust the first move.",
                InstructionShort = "committing to one full motion",
                CauseClause = "you're still creeping in timid steps",
                ComposeMessage = ctx =>
                    $"Undershoot at {ctx.Result.UndershootPct:F0}% — you're creeping to targets in timid steps. " +
                    "Make one confident motion, one micro-correction, click. Trust the first move. " +
                    "Undershoot drops when you commit.",
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
                Instruction = "Between targets, park your crosshair where the next one is likely to spawn, at target height.",
                InstructionShort = "pre-placing your crosshair",
                CauseClause = "your crosshair is still resting where the last target died",
                ComposeMessage = ctx =>
                {
                    double best = ctx.Result.BestReactionMs, avg = ctx.Result.AvgReactionMs;
                    string noun = ReactionMetric.Noun(ctx.Result.Scenario);
                    // Displayed arithmetic: gap = avg − best, shown with both operands.
                    return $"Your best {noun} is {best:F0}ms but your average is {avg:F0}ms — a {avg - best:F0}ms gap. " +
                           "You're starting every target from scratch because your crosshair rests where the last target died. " +
                           "Between targets, park it where the next one is likely to spawn, at target height. " +
                           "The average closes toward your best when this sticks.";
                },
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
                Instruction = "Snap your eyes to where the target is going first; let the hand follow.",
                InstructionShort = "leading with your eyes",
                CauseClause = "you're still chasing the trail",
                ComposeMessage = ctx =>
                    $"Direction-change lag at {ctx.Result.AvgDirectionChangeLagMs:F0}ms — you're chasing the target's trail, " +
                    "moving your hand before your eyes have caught the turn. Snap your eyes to where it's going first; " +
                    "let the hand follow. Lag drops next session if your eyes are leading.",
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
                Instruction = "Track vertical arcs with the arm, not the fingers.",
                InstructionShort = "vertical tracking control",
                CauseClause = "you're still tracking vertical from the wrist",
                ComposeMessage = ctx =>
                {
                    double h = ctx.Result.HorizontalTrackingAcc, v = ctx.Result.VerticalTrackingAcc;
                    // Displayed arithmetic: split = h − v, both operands shown.
                    return $"Horizontal tracking {h:F0}/100, vertical {v:F0}/100 — a {h - v:F0}-point split. " +
                           "You're aiming from the wrist, and wrists are horizontal creatures; your vertical control is underbuilt. " +
                           "Track vertical arcs with the arm, not the fingers. Vertical accuracy climbs when the arm takes over.";
                },
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
                Instruction = "After every miss: exhale, re-center to neutral, treat the next target as target #1.",
                InstructionShort = "your miss-reset routine",
                CauseClause = "you're still carrying misses forward",
                ComposeMessage = ctx =>
                    $"A {ctx.Result.MaxStreak}-hit streak out of {ctx.Result.Hits} hits, scattered everywhere else — " +
                    "you're carrying misses into the next target. After every miss: exhale, re-center to neutral, " +
                    "treat the next target as target #1. Hits outside your best streak go up when the resets work.",
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
                Instruction = "Hold until you see the whole silhouette; a confirmed hit beats a fast whiff.",
                InstructionShort = "holding fire until full exposure",
                CauseClause = "you're still firing early",
                ComposeMessage = ctx =>
                    $"Early clicks on {ctx.Result.PeekEarlyClickPct:F0}% of peeks — you're firing before the target is fully out. " +
                    "Hold until you see the whole silhouette; a confirmed hit beats a fast whiff. " +
                    "Early-click rate falls when you wait it out.",
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
                Instruction = "Decide to shoot during the peek, not after it: if the target appears, the click happens.",
                InstructionShort = "committing during the peek",
                CauseClause = "you're still double-checking instead of committing",
                ComposeMessage = ctx =>
                    $"Late clicks on {ctx.Result.PeekLateClickPct:F0}% of peeks — you're double-checking when you should be committing. " +
                    "Decide to shoot during the peek, not after it: if the target appears, the click happens. " +
                    "Late-click rate drops when you pre-commit.",
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
                Instruction = "Overspeed on purpose: let accuracy fall to ~75% while you force faster commits, then rebuild it at the new speed. Miss fast, not slow.",
                InstructionShort = "spending accuracy for speed",
                CauseClause = "you're still buying certainty you don't need",
                ComposeMessage = ctx =>
                    $"{ctx.Result.Accuracy:F0}% accuracy at {ctx.Result.AvgReactionMs:F0}ms per target — " +
                    "you're buying certainty you no longer need to pay for. Overspeed on purpose: let accuracy fall to ~75% " +
                    "while you force faster commits, then rebuild it at the new speed. Miss fast, not slow. " +
                    "Time-per-target falls first; accuracy follows.",
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
                Instruction = "Drop to 90% of your natural speed and make every click deliberate; earn the speed back with hits.",
                InstructionShort = "slowing down for control",
                CauseClause = "you're still outrunning your control",
                ComposeMessage = ctx =>
                    $"{ctx.Result.Accuracy:F0}% accuracy at {ctx.Result.AvgReactionMs:F0}ms — you're outrunning your control. " +
                    "Drop to 90% of your natural speed and make every click deliberate; earn the speed back with hits. " +
                    "Accuracy climbs before speed returns.",
                GetPracticeDrill = _ => new PracticeDrill("Precision", "Standard", "Easy", "slow is smooth, smooth is fast"),
                VerifyMetric = "Accuracy",
                ExpectedDirection = MetricDirection.Up
            },

            new()
            {
                PrescriptionKey = "movement_efficiency",
                RequiredMetrics = { "PathEfficiency" },   // validity floor 0.15 in DrillMetricValid
                Signature = ctx =>
                    ctx.Result.PathEfficiency > 0
                    && ctx.Result.PathEfficiency < PathEfficiencyLowFraction
                    && ctx.Result.Hits >= MinHitsForEfficiencyRead
                    && ctx.Result.Scenario is "StaticClicking" or "DynamicClicking" or "Precision" or "Sniper",
                Instruction = "Move once, click, stop — no corrections. Run 5-second explosive move-and-click reps.",
                InstructionShort = "killing the orbit before the click",
                CauseClause = "you're still orbiting targets",
                ComposeMessage = ctx =>
                    $"Movement efficiency at {ctx.Result.PathEfficiency * 100:F0}% — you're orbiting targets before clicking " +
                    "instead of moving through them. Move once, click, stop — no corrections. " +
                    "Run 5-second explosive move-and-click reps. Efficiency climbs when the orbiting stops.",
                GetPracticeDrill = ctx => new PracticeDrill(
                    ctx.Result.Scenario,
                    string.IsNullOrEmpty(ctx.Result.SubVariant) ? "Standard" : ctx.Result.SubVariant,
                    string.IsNullOrEmpty(ctx.Result.Difficulty) ? "Medium" : ctx.Result.Difficulty,
                    "move once, click, stop"),
                VerifyMetric = "PathEfficiency",
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
            "PathEfficiency"          => r.PathEfficiency,
            // Hand-check: 30 hits, max streak 18 → (30−18)/30 = 0.4 outside the streak.
            "HitsOutsideStreakRatio"  => r.Hits > 0 ? (double)(r.Hits - r.MaxStreak) / r.Hits : 0,
            _                         => 0
        };

        private static List<SessionSummary> ValidSmoothnessSessions(PrescriptionContext ctx) =>
            (ctx.Memory?.RecentTrackerSessions ?? new List<SessionSummary>())
                .Where(s => s.IsMetricValid("SmoothnessScore"))
                .Take(3)
                .ToList();

        private static string StepDifficultyUp(string difficulty) => difficulty switch
        {
            "Easy"   => "Medium",
            "Medium" => "Hard",
            "Hard"   => "Nightmare",
            _        => "Medium"
        };
    }
}
