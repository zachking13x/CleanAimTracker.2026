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
            "Reactive" or "PeekTraining" or "Shotgun" or "Sniper";

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
    }
}
