using CleanAimTracker.Windows;
using System;

namespace CleanAimTracker.Services
{
    public static class TrialService
    {
        // TASK-13: 30 free sessions instead of 7-day timer
        private const int FreeSessions = 30;

        public static void Initialize()
        {
            var settings = SettingsService.Load();
            if (settings.FirstLaunchDate == DateTime.MinValue ||
                settings.FirstLaunchDate == default)
            {
                settings.FirstLaunchDate = DateTime.UtcNow;
                SettingsService.Save(settings);
                LogService.Info("Trial started");
            }
        }

        // TASK-13: Active while session count ≤ 30 (or user is licensed)
        public static bool IsTrialActive()
        {
            if (IsFullVersion()) return true;
            return SessionsCompleted() < FreeSessions;
        }

        // Number of aim trainer drills the user has completed.
        // NOTE: SessionStorage holds raw mouse-movement tracker sessions (gameplay).
        //       AimTrainerStorage holds aim trainer drill results — these are separate
        //       stores and must not be interchanged. Trial progress is based on drills.
        public static int SessionsCompleted()
        {
            try { return AimTrainerStorage.LoadAll().Count; }
            catch { return 0; }
        }

        // AUDIT A7: SessionsRemaining() was removed. Its only caller rendered
        // "N sessions left in your free trial" on the LOCKED coach report, which
        // contradicted the coach's real gate (5 real drills, FreeCoachSessionService).
        //
        // ⚠️ THERE ARE STILL TWO GATES IN THIS APP, and that is a product decision, not
        // a bug, so it has been left alone:
        //   • the COACH unlocks/locks on 5 real drills   (FreeCoachSessionService)
        //   • Overlay, Session History, Export and Trends run on the 30-session trial
        //     below, via IsTrialActive() → CanAccessProFeature()
        // A free user therefore loses the coach at drill 5 but keeps Export until
        // session 30. Collapsing those onto one rule would change what free users get,
        // so it needs an explicit call rather than a silent refactor.

        // CAT_PAID_APP (2026-10-09): CAT is a PAID app now, with no add-ons. Owning the app
        // IS the licence. Windows won't launch a paid Store app without a valid entitlement,
        // so a running copy is already a licensed one, and checking again in here would
        // only add ways to lock out someone who paid (offline, Store hiccup).
        //
        // GRANDFATHERING comes from the same rule. Anyone who got CAT while it was free
        // keeps that Store entitlement after the price change, so they land here exactly
        // like a buyer and get everything, free, for good. Earlier Lifetime/Monthly add-on
        // buyers are covered too. Every free-tier gate and upsell below is dead now; they
        // stay only so the call sites keep compiling.
        //
        // ⚠️ If a Store FREE TRIAL is ever turned on in Partner Center, this must start
        // reading StoreAppLicense.IsTrial. Until then, unconditional true is correct.
        public static bool IsFullVersion() => true;

        public static bool CanAccessProFeature()
            => IsFullVersion() || IsTrialActive();

        public static bool RequestProAccess(string featureName)
        {
            if (CanAccessProFeature()) return true;

            UpgradeDialog.Show(featureName);
            return false;
        }

        public static string GetStatusText()
        {
            return IsFullVersion() ? "Pro" : "Free";
        }

        // AUDIT (2026-07-06): training is UNLIMITED — nothing stops a free user from
        // playing forever. The banner used to say "limit reached / N sessions left,"
        // which was a lie (there is no session wall). The gate is the COACH, so the
        // banner now honestly promotes the coach without inventing a countdown.
        public static string GetBannerText()
        {
            if (IsFullVersion()) return "";

            int completed = SessionsCompleted();
            if (completed == 0) return "";      // suppress until the first session is done

            return "🎯 Unlock your coach — see exactly what to fix every session";
        }

        /// <summary>
        /// A conversion MOMENT (not a wall). Fires once the user is clearly committed —
        /// training never stops, but a heavily-invested player is the best time to make
        /// the coach pitch. Kept named IsAtFreeLimit for its existing call sites.
        /// </summary>
        public static bool IsAtFreeLimit()
            => !IsFullVersion() && SessionsCompleted() >= FreeSessions;

        // Value-moment milestones — early nudges before the big committed-player moment.
        public static bool IsValueMoment(int sessionCount)
        {
            return sessionCount == 3 || sessionCount == 10 || sessionCount == 25;
        }

        // COACH-FIRST copy (AUDIT 2026-07-06): sell the OUTCOME (the coach tells you what
        // to fix), never a feature checklist, and never a fake "sessions left" countdown.
        public static string GetValueMomentMessage(int sessionCount)
        {
            return sessionCount switch
            {
                3  => "3 sessions in. Unlock your coach and it'll tell you the one thing holding your aim back — and the drill to fix it.",
                10 => "10 sessions — you're building real habits. Your coach can track them across every session and show you exactly where you're improving.",
                25 => "25 sessions. You're committed. Unlock the coach for good and never wonder what to practice again — it tells you, every session.",
                _  => ""
            };
        }

        public static string GetStoreLink()
        {
            return "ms-windows-store://pdp/?productid=9MVBDZBQ01DM";
        }
    }
}
