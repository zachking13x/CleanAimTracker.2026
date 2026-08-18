using CleanAimTracker.Models;
using System;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_REVIEW_PROMPT Gate 1: decides whether to ask for a Store rating after a
    /// genuine personal best. Pure logic (no UI/IO) so the gating is unit-tested. Rule:
    /// fire on a real PB only when the user is past the beginner phase, outside the
    /// 30-day cooldown, under the lifetime cap, and hasn't already rated/opted out.
    /// </summary>
    public static class ReviewPromptService
    {
        public const int MinSessions  = 3;    // was 5 — see the retune note below
        public const int CooldownDays = 14;   // was 30
        public const int LifetimeCap  = 3;    // unchanged — still at most 3 times ever

        // CAT_REVIEW_RETUNE (2026-08-03): the original gates (PB **and** ≥5 sessions
        // **and** 30-day cooldown) were calibrated for an app with far more usage than
        // CAT actually has. Real behaviour: ~3 drills per active user per month, so most
        // players never reached session 5 with a fresh PB inside a 30-day window — the
        // prompt effectively never fired, and the listing sat at 3 ratings. Ratings are
        // a direct input to Store conversion, so an ask that never happens is a real
        // cost. Loosened to 3 sessions / 14 days, and a strong session now also
        // qualifies — a genuinely good run is as honest a moment to ask as a PB.
        // The lifetime cap of 3 and the permanent opt-out are untouched: never nag.
        public const double StrongSessionAccuracy = 85.0;

        /// <summary>
        /// True only when every gate permits. A missing/zero counter defaults to
        /// "allowed" (MinValue last-prompt = never asked), never to spam.
        /// </summary>
        public static bool ShouldPrompt(bool isNewPb, int completedSessions, UserSettings s, DateTime nowUtc)
            => ShouldPrompt(isNewPb, completedSessions, s, nowUtc, accuracy: 0);

        /// <summary>
        /// Overload that also treats a genuinely strong session as a valid ask moment.
        /// Pass the session accuracy; 0 keeps the PB-only behaviour.
        /// </summary>
        public static bool ShouldPrompt(bool isNewPb, int completedSessions, UserSettings s, DateTime nowUtc, double accuracy)
        {
            if (s == null) return false;

            bool goodMoment = isNewPb || accuracy >= StrongSessionAccuracy;
            if (!goodMoment) return false;

            if (s.HasRatedOrDismissedPermanently) return false;
            if (completedSessions < MinSessions) return false;
            if (s.ReviewPromptCount >= LifetimeCap) return false;
            if (s.LastReviewPromptUtc != DateTime.MinValue
                && (nowUtc - s.LastReviewPromptUtc).TotalDays < CooldownDays)
                return false;
            return true;
        }

        /// <summary>Record that the prompt was shown (advances cooldown + lifetime count).</summary>
        public static void RecordShown(UserSettings s, DateTime nowUtc)
        {
            if (s == null) return;
            s.LastReviewPromptUtc = nowUtc;
            s.ReviewPromptCount++;
        }

        /// <summary>They tapped "Rate it" (or explicitly opted out) — never ask again.</summary>
        public static void RecordRatedOrOptedOut(UserSettings s)
        {
            if (s == null) return;
            s.HasRatedOrDismissedPermanently = true;
        }
    }
}
