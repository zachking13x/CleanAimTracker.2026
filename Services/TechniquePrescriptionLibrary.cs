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

        // crosshair_preplacement — AUDIT FIX (2026-07-06): previously triggered on a
        // 150ms best-vs-average gap. The session best is the MINIMUM of dozens of
        // hits and always sits far below the mean (order statistics), so the gap
        // exceeded 150ms for essentially every session — the prescription fired on
        // an artifact, not a habit. It now anchors on the authored per-scenario
        // pace benchmark: pace genuinely slower than the scenario rewards.

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

        // A1 step 3 — MovementOvershoot trigger band, set from a maximally-separated
        // real capture pair (StaticClicking · Medium, 2026-06-15):
        //   surgical/clean session: 0.0618   wild-overshoot session: 0.6212  (10× apart)
        // 0.20 is the geometric midpoint √(0.0618·0.6212) ≈ 0.196 — a 3.2× margin above
        // clean play, well below the overshoot cluster so genuine overshoot still fires.
        public const double MovementOvershootBand = 0.20;

        // Click-point overshoot/undershoot: the CLICK lands off-center while the
        // MOTION arrived clean (low MovementOvershoot). A click-timing habit, not an
        // in-flight one — the opposite directions of one family. Gated on
        // MovementOvershoot being valid AND below the clean ceiling, so the coach
        // only blames the click after confirming the path was clean.
        // V2 CALIBRATION (2026-07-06, real captures): OvershootPct/UndershootPct are
        // DIRECTIONAL (approach-axis projection). Observed values:
        //   centered auto-clicker (neutral baseline): over 18 / under 26
        //   real StaticClicking session w/ a genuine short habit: over 27 / under 40
        //   real AirTracking session (trailing movers):           over 16 / under 62
        // → neutral noise band tops out ~26; a real habit reads 35+ with clear
        // dominance. Gates: 35 minimum + 8-point dominance margin, so a 36/34
        // coin-flip session gets NO directional verdict instead of a guess.
        // (The old 50/40 gates were calibrated on the inflated radial V1 metric and
        // sat above real V2 habits — a 39.6% undershoot session read as "nothing".)
        // < 0.20 to butt against the in-flight floor (MovementOvershootBand) — no dead
        // zone: ≥0.20 routes to in-flight overshoot, <0.20 to the click-point pair.
        public const double ClickPointMotionCleanCeiling = 0.20; // MovementOvershoot below this = path arrived clean
        public const double ClickPointOffsetMinPx        = 6;    // clicks land > this off-center
        public const double ClickPointOvershootMinPct    = 35;
        public const double ClickPointUndershootMinPct   = 35;
        public const double ClickPointDominanceMargin    = 8;    // dominant direction must clear the other by this

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

            // A2 — repointed from the old click-endpoint `arm_over_wrist` (OvershootPct
            // >= 25 → a guessed wrist/forearm cause) onto the real in-flight overshoot
            // signal MovementOvershoot (axial excursion). This is the "flies past and
            // pulls back, lands accurately" habit. OvershootPct stays a SEPARATE signal
            // (decelerate_into_target / commit_full_motion) for "final click lands wide"
            // — a different habit; the two are never merged.
            new()
            {
                PrescriptionKey = "movement_overshoot",
                RequiredMetrics = { "MovementOvershoot" },
                Signature = ctx =>
                    ctx.Result.MovementOvershoot >= MovementOvershootBand,
                Instruction = "Decelerate into the target: arrive once, click, instead of arrive-past-return.",
                InstructionShort = "decelerating into the target",
                CauseClause = "you're still flying past and pulling back",
                // Amendment voice (verbatim shape): evidence (segment %) → cause → instruction → stake.
                ComposeMessage = ctx =>
                    $"You overshot the target on {ctx.Result.OvershootSegmentPct:F0}% of acquisitions — " +
                    "you're flying past and pulling back to correct. That costs you the time between the pass " +
                    "and the recovery. Decelerate into the target: arrive once, click, instead of arrive-past-return. " +
                    "Your overshoot drops next session when it's landing.",
                GetPracticeDrill = ctx => new PracticeDrill(
                    "Precision", "Standard",
                    string.IsNullOrEmpty(ctx.Result.Difficulty) ? "Medium" : ctx.Result.Difficulty,
                    "arrive once — don't fly past"),
                VerifyMetric = "MovementOvershoot",
                ExpectedDirection = MetricDirection.Down
            },

            // Click-point overshoot — clicks land LONG (past center) while the motion
            // arrived clean (MovementOvershoot below the clean ceiling). A click-TIMING
            // habit, distinct from in-flight overshoot (movement_overshoot, above). Listed
            // before decelerate/commit so it wins selection when both fire on clean motion;
            // shares the "overshoot" aspect (GenerateReport) so only one family member renders.
            new()
            {
                PrescriptionKey = "click_point_overshoot",
                RequiredMetrics = { "OvershootPct", "MovementOvershoot" },
                Signature = ctx =>
                    ctx.Result.OvershootPct >= ClickPointOvershootMinPct
                    && ctx.Result.MovementOvershoot < ClickPointMotionCleanCeiling
                    && ctx.Result.AvgClickOffset > ClickPointOffsetMinPx
                    // Opposite directions of one family — overshoot must clearly dominate;
                    // a near-tie is off-center noise, not a directional habit (no coin-flips).
                    && ctx.Result.OvershootPct >= ctx.Result.UndershootPct + ClickPointDominanceMargin,
                Instruction = "Click the instant the crosshair reaches center, not after. If anything, stop just short and let the target come to you — don't drift onto it.",
                InstructionShort = "your click timing",
                CauseClause = "you're still committing the click a hair late",
                ComposeMessage = ctx =>
                    $"{ctx.Result.OvershootPct:F0}% of your clicks land past center — about {ctx.Result.AvgClickOffset:F0}px long. " +
                    "Your aim arrives clean but you're committing the click a hair after the cursor drifts past center — " +
                    "it's a click-timing habit, not a movement one. Click the instant the crosshair reaches center, not after. " +
                    "If anything, stop just short and let the target come to you — don't drift onto it. " +
                    "OvershootPct drops next session when the click and the settle line up.",
                GetPracticeDrill = ctx => new PracticeDrill(
                    "Precision", "Standard",
                    string.IsNullOrEmpty(ctx.Result.Difficulty) ? "Medium" : ctx.Result.Difficulty,
                    "click on arrival, not after"),
                VerifyMetric = "OvershootPct",
                ExpectedDirection = MetricDirection.Down
            },

            // Click-point undershoot — clicks land SHORT of center while the motion
            // arrived clean. The symmetric sibling of click_point_overshoot. NO sensitivity
            // mention (that is RecommendationEngine's lane) and NO hedge.
            new()
            {
                PrescriptionKey = "click_point_undershoot",
                RequiredMetrics = { "UndershootPct", "MovementOvershoot" },
                Signature = ctx =>
                    ctx.Result.UndershootPct >= ClickPointUndershootMinPct
                    && ctx.Result.MovementOvershoot < ClickPointMotionCleanCeiling
                    && ctx.Result.AvgClickOffset > ClickPointOffsetMinPx
                    // Undershoot must clearly dominate — same no-coin-flip rule as the sibling.
                    && ctx.Result.UndershootPct >= ctx.Result.OvershootPct + ClickPointDominanceMargin,
                Instruction = "Let the crosshair actually touch center, then click. Trust the arrival — one beat later, not one beat early.",
                InstructionShort = "your click timing",
                CauseClause = "you're still pulling the click early",
                ComposeMessage = ctx =>
                    $"{ctx.Result.UndershootPct:F0}% of your clicks land short of center. " +
                    "Your motion arrives fine, but you're pulling the click before the crosshair reaches center — " +
                    "you're committing early, not hesitating. Let the crosshair actually touch center, then click. " +
                    "Trust the arrival — one beat later, not one beat early. " +
                    "UndershootPct drops next session when you let the aim land first.",
                GetPracticeDrill = ctx => new PracticeDrill(
                    "Precision", "Standard",
                    string.IsNullOrEmpty(ctx.Result.Difficulty) ? "Medium" : ctx.Result.Difficulty,
                    "let it land, then click"),
                VerifyMetric = "UndershootPct",
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
                // CAT_PACE_SENTINEL: IsPaceMeasured first — auto-fire drills report 0ms
                // and would otherwise be prescribed a crosshair fix for a pace that was
                // never measured.
                Signature = ctx =>
                    ReactionMetric.IsPaceMeasured(ctx.Result.Scenario, ctx.Result.AvgReactionMs)
                    && ctx.Result.Hits >= 5
                    && ctx.Result.AvgReactionMs > AiCoachService.Bench.ReactionGood(ctx.Result.Scenario),
                Instruction = "Between targets, park your crosshair where the next one is likely to spawn, at target height.",
                InstructionShort = "pre-placing your crosshair",
                // CAT_CAUSAL_HONESTY (2026-08-12): was "your crosshair is still resting
                // where the last target died" — stated as fact. Nothing in the data
                // observes where the crosshair rests between targets; the only thing
                // measured is that the pace is slow. Crosshair placement is the most
                // COMMON cause, which is worth saying — but it has to be offered as the
                // likely explanation to check, not reported as a finding.
                CauseClause = "the usual cause at this pace is that you're leaving the crosshair where the last target died",
                ComposeMessage = ctx =>
                {
                    double avg = ctx.Result.AvgReactionMs;
                    string noun = ReactionMetric.Noun(ctx.Result.Scenario);
                    string good = AiCoachService.Bench.ReactionGood(ctx.Result.Scenario).ToString("F0");
                    return $"Your average {noun} is {avg:F0}ms — {ctx.Result.Scenario} rewards under {good}ms. " +
                           "At this pace the usual cause isn't slow hands, it's crosshair position: if you're " +
                           "leaving it where the last target died, every target starts from scratch. " +
                           "Park it where the next one is likely to spawn, at target height, and watch whether " +
                           "the average falls.";
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
                // CAT_AXIS_MOTION_GATE: only drills that actually move on both axes.
                // Without this, a sideways-strafing bot drill scores ~5 on vertical for
                // want of vertical motion and gets prescribed arm-tracking practice for
                // a weakness that was never measured.
                Signature = ctx =>
                    TelemetryCalculator.HasTwoAxisTracking(ctx.Result.Scenario)
                    && ctx.Result.HorizontalTrackingAcc > 0
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
            "MovementOvershoot"       => r.MovementOvershoot,
            // Hand-check: 30 hits, max streak 18 → (30−18)/30 = 0.4 outside the streak.
            "HitsOutsideStreakRatio"  => r.Hits > 0 ? (double)(r.Hits - r.MaxStreak) / r.Hits : 0,
            // T4: an unmapped VerifyMetric is a programming error (a prescription
            // declared a verify metric with no reader). It must surface loudly, never
            // fall through to 0 — a silent 0 baseline makes "improved 0 → N" always
            // read as success (phantom loop-closure). All 13 prescriptions' metrics
            // are mapped above; this guards future additions.
            _                         => throw new System.ArgumentException(
                                             $"No VerifyMetric reader for '{metric}' — add it to ReadVerifyMetric.", nameof(metric))
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
