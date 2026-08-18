using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>Title + body for one scheduled nudge.</summary>
    public record NudgeCopy(string Title, string Body);

    /// <summary>Everything a notification may reference — all REAL user data.</summary>
    public class NudgeContext
    {
        public bool   HasAnySession;
        public int    SessionCount;
        public int    CurrentStreak;
        public double LastAccuracy;
        public string LastScenario = "";
        public int    LastScore;
        public int    ScenarioBestScore;     // PB for LastScenario
        public double OvershootDeltaPct;     // last-session overshoot DROP (positive = improved)
        public bool   HasProfileUpdate;
        public bool   ChallengeDoneToday;
        public string ChallengeText = "";
    }

    /// <summary>
    /// CAT_RETENTION_NOTIFICATIONS Gate 2: nudge copy assembled from a FIXED authored
    /// pool, every line referencing a REAL number from the user's data. A competitive
    /// player returns for status/curiosity/ego, never chores — so the chore/guilt
    /// register ("come back", "don't forget", "we miss you", "time to train") is banned
    /// (enforced by test). Loss-aversion (a live streak ending) is the strongest return
    /// driver and takes priority. Variants rotate by seed so consecutive days differ.
    /// </summary>
    public static class NotificationMessages
    {
        // ── Authored pools (title, body); {0} is a real data value ─────────────
        public static readonly (string T, string B)[] StreakAtRisk =
        {
            ("Your {0}-day streak ends tonight 🔥", "One drill keeps it alive. Don't let {0} days go to zero."),
            ("{0} days on the line 🔥",             "Your streak resets at midnight. A single session saves it."),
            ("Streak check: day {0} 🔥",            "You're {0} days deep. Skip tonight and it's back to zero."),
            ("Don't drop the {0}-day streak 🔥",    "It's still alive — but only until midnight. One run holds it."),
        };
        public static readonly (string T, string B)[] PbProximity =
        {
            ("One session from a new best 🎯", "You're within reach of your {0} {1} record. Today might be the day."),
            ("Your {1} best is {0} — beatable", "Last run put you close. One clean session takes it."),
            ("{0} to beat in {1} 🎯",           "You're knocking on your own personal best. Go take it."),
            ("So close to a new {1} PB",        "Your record sits at {0}. You've been right under it — finish the job."),
        };
        public static readonly (string T, string B)[] OvershootProgress =
        {
            ("Your overshoot dropped {0}% 📉", "The click timing is clicking. One more session locks it in."),
            ("{0}% cleaner last session",      "Your overshoot is falling — keep the streak of improvement going."),
            ("The fix is working: −{0}% 📉",   "Overshoot down {0}%. Another rep makes it muscle memory."),
            ("Down {0}% on overshoot 🎯",      "You're tightening up. Don't let the progress cool off."),
        };
        public static readonly (string T, string B)[] ProfileCuriosity =
        {
            ("Your aim profile updated 📊", "{0} sessions in — the read on how you play just shifted. See what changed."),
            ("New read on your aim 📊",     "Your profile moved after {0} sessions. Curious what it says now?"),
            ("Your player profile changed", "{0} sessions logged. The coach sees a new pattern — take a look."),
            ("Something shifted in your profile 📊", "After {0} sessions the picture's different. Worth a glance."),
        };
        public static readonly (string T, string B)[] DailyChallenge =
        {
            ("Today's challenge is live 🎯", "{0} Beat it and your streak's safe."),
            ("New daily challenge 🎯",       "{0} A few minutes is all it takes."),
            ("Challenge ready for you 🎯",   "{0} Clear it before midnight."),
            ("Your daily is waiting 🎯",     "{0} Knock it out and stay sharp."),
        };
        public static readonly (string T, string B)[] AccuracyReference =
        {
            ("You hit {0}% last session 🎯", "See if today's you can top it."),
            ("Last run: {0}% accuracy",      "One session to find out if you've leveled up."),
            ("{0}% is your number to beat",  "Today's session is the test."),
            ("Your last drill was {0}% 🎯",  "Quick session to push it higher."),
        };
        // Win-back — curiosity, not guilt. References a real last-session stat.
        public static readonly (string T, string B)[] Winback =
        {
            ("Your aim's been waiting 🎯",     "Last session: {0}% in {1}. Pick up right where you left off."),
            ("Unfinished business in {1}",     "You left off at {0}% accuracy. One session gets you back in rhythm."),
            ("Your {1} run is still warm",     "{0}% last time out — your muscle memory hasn't forgotten. Prove it."),
            ("{0}% — and climbing? 🎯",        "That was your last {1} session. See where you're at now."),
        };

        private static readonly string[] BannedPhrases =
            { "come back", "don't forget", "we miss you", "time to train", "come train", "miss you" };

        /// <summary>Test hook: true if any banned chore/guilt phrase appears.</summary>
        public static bool ContainsBannedPhrase(string text) =>
            BannedPhrases.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));

        // ── Builders ────────────────────────────────────────────────────────────
        /// <summary>
        /// The once-a-day nudge. Priority: a live streak at risk (loss aversion) →
        /// near a personal best → fresh overshoot progress → profile update →
        /// daily challenge → last-accuracy reference. Always cites a real number.
        /// </summary>
        public static NudgeCopy BuildDailyNudge(NudgeContext c, int seed)
        {
            if (c.CurrentStreak >= 2)
                return Fill(Pick(StreakAtRisk, seed), c.CurrentStreak);

            if (c.ScenarioBestScore > 0 && c.LastScore >= c.ScenarioBestScore * 0.9
                && c.LastScore < c.ScenarioBestScore)
                return Fill(Pick(PbProximity, seed), c.ScenarioBestScore, c.LastScenario);

            if (c.OvershootDeltaPct > 0)
                return Fill(Pick(OvershootProgress, seed), Math.Round(c.OvershootDeltaPct));

            if (!c.ChallengeDoneToday && !string.IsNullOrWhiteSpace(c.ChallengeText))
                return Fill(Pick(DailyChallenge, seed), c.ChallengeText);

            if (c.HasProfileUpdate && c.SessionCount > 0)
                return Fill(Pick(ProfileCuriosity, seed), c.SessionCount);

            return Fill(Pick(AccuracyReference, seed), Math.Round(c.LastAccuracy));
        }

        // CAT_RETENTION_HOLES (2026-07-30): the "installed, opened, never trained" pool.
        // These users were previously EXCLUDED from every nudge by the HasAnySession gate —
        // i.e. the group at the highest churn risk was the one group we never spoke to.
        // No real numbers exist for them yet, so the copy sells the OUTCOME (the coach) and
        // the tiny time cost. Same bans apply: no guilt, no chores, no "we miss you".
        public static readonly (string T, string B)[] FirstDrill =
        {
            ("Your aim, scored in 30 seconds 🎯", "One drill and the coach tells you the single thing holding your aim back."),
            ("You haven't run a drill yet",       "30 seconds is all it takes to find out what's actually wrong with your aim."),
            ("Find your weak spot 🎯",            "One short drill, and the coach names the habit costing you shots."),
            ("The coach is waiting",              "Run a single 30-second drill and it'll tell you exactly what to fix."),
        };

        /// <summary>
        /// Nudge for a user who has opened CAT but never completed a drill. No live data
        /// exists, so nothing is fabricated — the copy promises only what one drill delivers.
        /// </summary>
        public static NudgeCopy BuildFirstDrillNudge(int seed) => Fill(Pick(FirstDrill, seed));

        /// <summary>The win-back nudge (idle 2 / 4 days) — curiosity, never guilt.</summary>
        public static NudgeCopy BuildWinback(NudgeContext c, int seed)
        {
            // Always backed by the user's real last session.
            string scenario = string.IsNullOrWhiteSpace(c.LastScenario) ? "your last scenario" : c.LastScenario;
            return Fill(Pick(Winback, seed), Math.Round(c.LastAccuracy), scenario);
        }

        private static (string T, string B) Pick((string T, string B)[] pool, int seed)
            => pool[((seed % pool.Length) + pool.Length) % pool.Length];

        private static NudgeCopy Fill((string T, string B) tpl, params object[] args)
        {
            var a = args.Select(x => x is double d ? (object)d.ToString("F0", CultureInfo.InvariantCulture) : x).ToArray();
            return new NudgeCopy(
                string.Format(CultureInfo.InvariantCulture, tpl.T, a),
                string.Format(CultureInfo.InvariantCulture, tpl.B, a));
        }
    }
}
