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
        /// report. Fires exactly at the trigger session, never again.
        /// </summary>
        public static bool ShouldTriggerFreeSession(UserSettings settings, CoachMemory memory)
        {
            if (settings.HasUsedFreeFullSession) return false;

            int trigger = settings.FreeFullSessionTrigger > 0
                ? settings.FreeFullSessionTrigger
                : 5;  // default guard

            // Fires at exactly the trigger count — TotalDrillCount includes the current session
            return memory.TotalDrillCount == trigger;
        }

        /// <summary>
        /// Marks the free session as used immediately. Must be called when the
        /// window loads — not when it closes — to prevent showing it twice.
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

            return memory.TotalDrillCount == trigger;
        }

        // ── Tracker coach free full session ───────────────────────────

        /// <summary>
        /// Returns true when the user has earned their one-time free full tracker
        /// coaching report. Fires exactly at tracker session 3.
        /// </summary>
        public static bool ShouldTriggerFreeTrackerSession(UserSettings settings, int trackerSessionCount)
        {
            if (settings.HasUsedFreeFullTrackerSession) return false;
            return trackerSessionCount == 3;
        }

        /// <summary>
        /// Marks the free tracker session as used. Must be called immediately on window load.
        /// </summary>
        public static void MarkFreeTrackerSessionUsed(UserSettings settings)
        {
            settings.HasUsedFreeFullTrackerSession = true;
            SettingsService.Save(settings);
        }
    }
}
