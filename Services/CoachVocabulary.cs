using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_COACH_VOCABULARY (2026-08-19) — the single list of things the coach may never
    /// claim, in one place so every surface is held to the same standard.
    ///
    /// WHY THIS EXISTS: <c>RankService</c> already had a test forbidding "rare", "elite",
    /// "percentile" and friends in a rank explanation, because CAT has no player
    /// population data. That rule was correct — and it was enforced on exactly one code
    /// path. <c>AiCoachService</c> walked straight around it and shipped "you're in rare
    /// territory", "the kind of number that shows up in ranked lobbies", "that kind of
    /// improvement almost never happens" and "every session builds muscle memory".
    ///
    /// A rule enforced in one file is not a rule, it is a coincidence. The banned lists
    /// live here now, and the tests scan the authored coach SOURCE rather than only the
    /// strings a given test input happens to trigger — the "ranked lobbies" line sat in a
    /// <c>Pick()</c> variant that an earlier manual sweep never fired.
    ///
    /// ── THE STANDARD ────────────────────────────────────────────────────────────────
    /// The coach may state what the telemetry can see (accuracy, timing, movement shape),
    /// and may name an authored per-scenario band as a STANDARD the player clears. It may
    /// not claim how rare something is, what other players do, what the player's body or
    /// mind is doing, or what will happen in a real match. When a cause is plausible but
    /// unobservable, it must be offered as a hypothesis to test, never asserted.
    /// </summary>
    public static class CoachVocabulary
    {
        /// <summary>
        /// Frequency / population claims. CAT stores no cross-player distribution, so it
        /// cannot know that anything is rare, typical, or top-anything.
        /// </summary>
        public static readonly string[] PopulationClaims =
        {
            "rare territory", "is rare", "almost never happens", "percentile",
            "ranked lobbies", "most players", "few players", "players reach",
            "top tier", "most competitive players",
        };

        /// <summary>
        /// Body and mind states. The app sees mouse movement. It does not see grip
        /// pressure, which joint is driving, where the eyes are, fatigue, or confidence.
        /// Each of these was a real shipped sentence at some point.
        /// </summary>
        /// <remarks>
        /// These are ASSERTIONS, and only assertions. Naming a body part inside an
        /// INSTRUCTION ("track the arc with the arm, not the fingers") or a hedged
        /// HYPOTHESIS ("leading from the wrist is the usual cause — try the arm, and if
        /// that was it the split narrows") is legitimate coaching: it tells the player
        /// what to change without claiming to have seen what they were doing.
        ///
        /// A bare "from the wrist" entry was tried first and rejected — it failed the
        /// hedged vertical-axis prescription, which is exactly the phrasing this standard
        /// is meant to produce. Ban the claim, not the anatomy.
        /// </remarks>
        public static readonly string[] BodyAndMindClaims =
        {
            "gripping too tight", "grip is too tight", "muscle memory",
            "you're aiming from the wrist", "you are aiming from the wrist",
            "your eyes have not caught", "your eyes haven't caught",
            "cold hands", "you're tired", "you are tired",
            "you're nervous", "you are nervous", "timid",
        };

        /// <summary>
        /// Outcome promises. A drill result cannot forecast a match, a rank, or a win.
        /// </summary>
        public static readonly string[] OutcomeClaims =
        {
            "rank up", "ranked up", "climb the ranks", "win more fights",
            "winning most fights", "guarantee", "guaranteed", "will improve your rank",
        };

        /// <summary>Everything above, for a single sweep.</summary>
        public static IEnumerable<string> All =>
            PopulationClaims.Concat(BodyAndMindClaims).Concat(OutcomeClaims);

        /// <summary>
        /// Returns every banned phrase present in <paramref name="text"/>, or an empty
        /// list. Case-insensitive substring match: these are multi-word phrases, so the
        /// word-boundary problem that bit <see cref="GameTransferPhrasing"/> ("elo" inside
        /// "below") does not arise here — but keep them phrases, not bare words, for that
        /// reason.
        /// </summary>
        public static IReadOnlyList<string> Violations(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

            return All.Where(p => text.Contains(p, StringComparison.OrdinalIgnoreCase))
                      .ToList();
        }

        /// <summary>True when the text makes no banned claim.</summary>
        public static bool IsClean(string? text) => Violations(text).Count == 0;
    }
}
