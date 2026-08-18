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
        // ── REVERSE TRIAL (CAT_REVERSE_TRIAL, 2026-08-03) ──────────────────────
        // Every new player gets the FULL coach for their first N real drills, then it
        // locks. This replaces the old "2 isolated tastes at drill 1 and drill 10".
        //
        // WHY THE OLD MODEL FAILED (the reason this app had 47 MAU and $0):
        // CAT's actual product is the LOOP — the coach names a habit, prescribes a drill,
        // then VERIFIES next session whether it worked. That loop needs two CONSECUTIVE
        // coached sessions to be visible. The old model gave drill 1, then locked drills
        // 2-9, so the verification beat could never render; and the average player runs
        // ~3 drills/month, so the drill-10 re-taste essentially never fired. Free users
        // saw a feature. Only payers saw the product. We were asking people to buy
        // something they had never experienced.
        //
        // WHY A REVERSE TRIAL: giving full access first and downgrading after converts
        // materially better than a permanently-thin free tier (industry data: reverse
        // trial ~7-21% vs freemium ~2-5%), because loss aversion is ~2-2.5x stronger
        // than gain motivation — people fight to keep what they've already had. The
        // guiding principle is "give away the aha, charge for the scale."
        //
        // WHY DRILL-COUNT AND NOT DAYS: a 7-day timer would be wasted on this audience —
        // at ~3 drills/month a calendar trial captures barely one session. Counting real
        // drills guarantees every player actually experiences the loop, whatever their pace.
        //
        // 5 gives: report → prescription → verification → and room to see a fix land twice.
        public const int FreeCoachedDrills = 5;

        // Retained so the old trigger points remain greppable/documented.
        public const int Taste1AtRealDrill = 1;

        /// <summary>
        /// True when this session should show a FREE full coach report — i.e. the player
        /// is still inside their opening run of fully-coached drills.
        ///
        /// Deliberately stateless: it reads the real drill count rather than a consumable
        /// counter, so a blank/suppressed report can never silently burn the entitlement,
        /// and the window can't be corrupted by a half-written settings file. Legacy
        /// installs migrate for free — anyone past the window simply reads as locked.
        /// </summary>
        public static bool ShouldTriggerFreeSession(UserSettings settings, CoachMemory memory)
        {
            // (Pro users render full via TrialService.IsFullVersion() in the caller's
            // showFull; this method governs only the free reverse-trial window.)
            return memory.RealDrillCount <= FreeCoachedDrills;
        }

        /// <summary>How many fully-coached drills remain in the opening window (0 = spent).</summary>
        public static int FreeCoachedDrillsRemaining(CoachMemory memory)
            => System.Math.Max(0, FreeCoachedDrills - memory.RealDrillCount);

        /// <summary>
        /// No-op under the reverse trial — the window is derived from the drill count, so
        /// there is nothing to consume. Kept so existing call sites stay valid, and it
        /// still retires the legacy flags on first pass.
        /// </summary>
        public static void MarkFreeSessionUsed(UserSettings settings)
        {
            try
            {
                var fresh = SettingsService.Load();
                if (fresh.FreeFullTastesUsed == 0 && !fresh.HasUsedFreeFullSession) return;
                fresh.FreeFullTastesUsed     = 0;      // legacy counters no longer govern access
                fresh.HasUsedFreeFullSession = false;
                SettingsService.Save(fresh);
            }
            catch { /* entitlement is derived, never blocking */ }
        }

        /// <summary>Pro users, or a session that should show a free full report.</summary>
        public static bool IsEligibleForFullCoach(UserSettings settings, CoachMemory memory)
            => TrialService.IsFullVersion() || ShouldTriggerFreeSession(settings, memory);

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
