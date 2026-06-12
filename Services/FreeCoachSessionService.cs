using CleanAimTracker.Models;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Manages the one-time free full coaching session mechanics for both the
    /// aim trainer coach (session 5) and the tracker coach (tracker session 3).
    /// All state is persisted in UserSettings so it survives app restarts.
    /// </summary>
    public static class FreeCoachSessionService
    {
        // ── Aim trainer free full session ─────────────────────────────

        /// <summary>
        /// Returns true when the user has earned their one-time free full coaching
        /// report. TASK-0.3: fires at the FIRST session at-or-after the trigger
        /// count (the old exact-count match meant a suppressed/blank trigger
        /// session burned the moment forever — the count moved past it). The flag
        /// is consumed only AFTER a full report actually renders.
        /// </summary>
        public static bool ShouldTriggerFreeSession(UserSettings settings, CoachMemory memory)
        {
            if (settings.HasUsedFreeFullSession) return false;

            int trigger = settings.FreeFullSessionTrigger > 0
                ? settings.FreeFullSessionTrigger
                : 5;  // default guard

            return memory.TotalDrillCount >= trigger;
        }

        /// <summary>
        /// Marks the free session as used. TASK-0.3: call only AFTER the full
        /// preview report has actually rendered with content — a consumed flag on
        /// a blank report spends the best conversion moment on nothing.
        /// Loads fresh settings before saving to avoid overwriting concurrent
        /// XP saves that may have run on a parallel async path.
        /// </summary>
        public static void MarkFreeSessionUsed(UserSettings settings)
        {
            // Always load fresh — the passed-in settings may have been loaded
            // before XPService.AwardXP ran and saved TotalXP on a concurrent path.
            var fresh = SettingsService.Load();
            fresh.HasUsedFreeFullSession = true;
            SettingsService.Save(fresh);
        }

        /// <summary>
        /// Returns true when this session should show the full coaching report.
        /// True for: Pro users, or the free trigger session before it has been used.
        /// </summary>
        public static bool IsEligibleForFullCoach(UserSettings settings, CoachMemory memory)
        {
            if (TrialService.IsFullVersion()) return true;
            if (settings.HasUsedFreeFullSession) return false;

            int trigger = settings.FreeFullSessionTrigger > 0
                ? settings.FreeFullSessionTrigger
                : 5;

            return memory.TotalDrillCount >= trigger;
        }

        // ── Tracker coach free full session ───────────────────────────

        /// <summary>
        /// Returns true when the user has earned their one-time free full tracker
        /// coaching report. TASK-0.3: at-or-after tracker session 3, until consumed.
        /// </summary>
        public static bool ShouldTriggerFreeTrackerSession(UserSettings settings, int trackerSessionCount)
        {
            if (settings.HasUsedFreeFullTrackerSession) return false;
            return trackerSessionCount >= 3;
        }

        /// <summary>
        /// Marks the free tracker session as used. TASK-0.3: call only AFTER a
        /// full report with content has rendered — never on a suppressed report.
        /// </summary>
        public static void MarkFreeTrackerSessionUsed(UserSettings settings)
        {
            settings.HasUsedFreeFullTrackerSession = true;
            SettingsService.Save(settings);
        }
    }
}
