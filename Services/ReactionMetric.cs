namespace CleanAimTracker.Services
{
    /// <summary>
    /// TASK-0.3: honest labeling for the per-scenario "reaction" value.
    ///
    /// Audit (timer-restart anchor per scenario):
    ///   TRUE REACTION (timer restarts at stimulus onset → value = stimulus-to-hit):
    ///     Reactive      — restart in SpawnTarget()
    ///     PeekTraining  — restart when state enters Exposed
    ///     Shotgun       — restart in UpdateStandard when the target spawns
    ///     Sniper        — restart when a new still-window opens
    ///   TIME PER TARGET (timer restarts on the PREVIOUS hit with targets already
    ///   on screen → value = travel + acquisition + click + spawn delay):
    ///     StaticClicking, DynamicClicking, Flicking, Precision, Switching,
    ///     Tracking, AirTracking, Evasive, SmgAr, Adaptive (wraps the above)
    ///
    /// 1101ms in StaticClicking vs 240ms in Reactive was the same label on two
    /// different measurements. The label follows the measurement now.
    /// </summary>
    public static class ReactionMetric
    {
        public static bool IsTrueReaction(string scenario) => scenario is
            "Reactive" or "PeekTraining" or "Shotgun" or "Sniper"
            or "PeekClick";   // CAT_BOT_DRILLS: timer anchors at bot appearance

        /// <summary>Stat-card label, e.g. "AVG REACTION" vs "AVG TIME/TARGET".</summary>
        public static string CardLabel(string scenario) =>
            IsTrueReaction(scenario) ? "AVG REACTION" : "AVG TIME/TARGET";

        public static string BestCardLabel(string scenario) =>
            IsTrueReaction(scenario) ? "BEST REACTION" : "BEST TIME/TARGET";

        /// <summary>Prose noun for coach templates: "reaction" vs "time per target".</summary>
        public static string Noun(string scenario) =>
            IsTrueReaction(scenario) ? "reaction" : "time per target";

        /// <summary>Short prose noun: "reaction" vs "pace".</summary>
        public static string ShortNoun(string scenario) =>
            IsTrueReaction(scenario) ? "reaction" : "pace";

        /// <summary>
        /// CAT_PACE_SENTINEL (2026-08-12): true only when this scenario HAS per-shot
        /// timing AND this session actually produced a reading.
        ///
        /// THE BUG THIS EXISTS TO KILL: auto-fire scenarios (HeadTrack, SmgAr) and
        /// Sniper report AvgReactionMs = 0 because there is no per-shot timing to
        /// record. The coach's benchmark table already tried to suppress pace coaching
        /// for them by setting every threshold to 0 — but the guards were written as
        /// `avgMs &lt;= Bench.ReactionGood(scenario)`, and **0 &lt;= 0 is true**. The
        /// sentinel satisfied its own suppressor, so every one of those sessions graded
        /// as maximum speed and the coach told players holding a spray button that they
        /// were "clicking before the crosshair arrives".
        ///
        /// Any code that reads AvgReactionMs as a SIGNAL must call this first. Code that
        /// merely displays the number does not (the stat card already renders "—").
        /// </summary>
        public static bool IsPaceMeasured(string scenario, double avgReactionMs)
        {
            if (avgReactionMs <= 0) return false;      // no reading this session
            return HasPerShotTiming(scenario);         // …and the scenario can even have one
        }

        /// <summary>
        /// False for scenarios with no per-shot timing concept at all: auto-fire drills
        /// (the host synthesises shots on a fixed cadence, so "time per target" is the
        /// cadence, not the player) and Sniper (scored on placement, not speed).
        /// </summary>
        public static bool HasPerShotTiming(string scenario) => scenario is not
            ("HeadTrack" or "SmgAr" or "Sniper");

        /// <summary>
        /// CAT_ACQUISITION_GATE (2026-08-19): true only for scenarios where a shot marks
        /// the END OF AN AIMED APPROACH — i.e. where the gaps between shots are real
        /// target acquisitions rather than a fixed fire cadence.
        ///
        /// THE BUG THIS EXISTS TO KILL: PathEfficiency and MovementOvershoot segment the
        /// raw movement buffer at click boundaries, and those boundaries come from
        /// `_clickOffsets` — which HandleShot fills for EVERY hit, including the shots a
        /// hold-to-spray scenario synthesises at a ~110ms metronome. On HeadTrack, SmgAr,
        /// Tracking and AirTracking the "acquisitions" were therefore spray ticks, and
        /// MovementOvershoot could report "you're flying past the target and pulling back"
        /// from a player who never flicked at all.
        ///
        /// This is the same shape as the pace sentinel above: a metric that is meaningless
        /// for a scenario, computed anyway, then read by the coach as fact. The 2026-08-12
        /// axis-split fix proved the write-side gate alone is not enough — 236 already
        /// stored sessions kept fabricating because the coach reads from disk — so this is
        /// applied on BOTH sides: at capture in AimTrainerWindow.BuildResult, and on read
        /// in AiCoachService.DrillMetricValid.
        ///
        /// Sniper is INCLUDED here (unlike HasPerShotTiming): its shots are slow and
        /// deliberate, which is exactly a discrete aimed approach — it simply is not
        /// scored on speed.
        /// </summary>
        public static bool HasDiscreteAcquisitions(string scenario) => scenario is not
            ("HeadTrack" or "SmgAr" or "Tracking" or "AirTracking");
    }
}
