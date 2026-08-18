using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_RANK (2026-08-12): one account-wide standing that goes up.
    ///
    /// WHY: the radar shows a shape but nothing that reads as progress over time. The
    /// entire aim-training community organises itself around a rank ladder — Voltaic's
    /// Iron→Celestial is the motivational engine of the scene — and CAT had no single
    /// number with a name on it.
    ///
    /// THE HONESTY CONSTRAINT — this is why the tiers are named the way they are:
    /// a ladder like "Celestial" is a RARITY claim. It says *few players reach this*.
    /// CAT has no population data and cannot make that claim, and inventing one would
    /// undo the whole coach-honesty pass. So every tier here is anchored to the
    /// Voltaic-derived STANDARDS already used by <see cref="PercentileBenchmarks"/>,
    /// and the wording says which standard you clear — never what percentile you're in.
    ///
    /// "You clear the intermediate standard across your profile" is checkable.
    /// "You're top 8% of players" is not, and would be a lie.
    /// </summary>
    public static class RankService
    {
        /// <summary>Named rungs, easiest first. Index maps to <see cref="Thresholds"/>.</summary>
        public static readonly string[] TierNames =
        {
            "Unranked", "Entry", "Intermediate", "High Intermediate", "Advanced",
        };

        /// <summary>
        /// Minimum overall radar score for each tier. These mirror the anchors in
        /// AimRadarService.TierAnchor (entry 40 · intermediate 60 · high-int 80 ·
        /// advanced 95) so a rank and a spoke can never tell different stories.
        /// </summary>
        public static readonly double[] Thresholds = { 0, 40, 60, 80, 95 };

        /// <summary>
        /// Distinct SKILL AREAS required before an account-wide rank means anything.
        ///
        /// Counted over AimRadarService.CategoryAxes only — the drill-backed ones.
        /// Counting every scored axis would let a player who has only ever run Flicking
        /// qualify, because Flicking populates both Flick and Speed off the same
        /// sessions. Two derived readings of one skill is not breadth, and a rank built
        /// on it would be an account-wide claim from a single-scenario sample.
        /// </summary>
        public const int MinScoredAxes = 2;

        public sealed record Standing(
            int     TierIndex,
            string  TierName,
            double  Overall,
            string? NextTierName,
            double? PointsToNext,
            bool    IsRanked,
            string  Explanation);

        /// <summary>Compute the account standing from history. Never throws.</summary>
        public static Standing Build(IEnumerable<AimTrainerResult>? history)
        {
            var radar = AimRadarService.Build(history);
            return FromRadar(radar);
        }

        /// <summary>Compute from an already-built radar, so callers don't rebuild it.</summary>
        public static Standing FromRadar(AimRadarService.AimRadar radar)
        {
            int skillAreas = radar?.Axes.Count(a => a.HasData
                                                 && AimRadarService.CategoryAxes.Contains(a.Name)) ?? 0;

            if (radar == null || skillAreas < MinScoredAxes)
                return new Standing(0, "Unranked", 0, TierNames[1], null, false,
                    $"Play at least {MinScoredAxes} different skill areas to get a standing.");

            double overall = radar.AverageScore;

            int tier = 0;
            for (int i = Thresholds.Length - 1; i >= 0; i--)
                if (overall >= Thresholds[i]) { tier = i; break; }

            string? nextName = tier < TierNames.Length - 1 ? TierNames[tier + 1] : null;
            double? toNext   = nextName == null ? null : Math.Max(0, Thresholds[tier + 1] - overall);

            // The claim is always about a STANDARD, never a population.
            string explain = tier == 0
                ? "You're below the entry standard across your scored areas — that's the first rung."
                : tier == TierNames.Length - 1
                    ? "You clear the advanced benchmark standard across your scored areas."
                    : $"You clear the {TierNames[tier].ToLowerInvariant()} benchmark standard across your scored areas.";

            return new Standing(tier, TierNames[tier], overall, nextName, toNext, true, explain);
        }

        /// <summary>
        /// True when a new standing is a promotion over the old one — the moment worth
        /// celebrating. Returns false on a first-ever rank so a brand-new player isn't
        /// shown a "promoted!" moment they didn't earn by improving.
        /// </summary>
        public static bool IsPromotion(Standing? before, Standing after)
        {
            if (after == null || !after.IsRanked) return false;
            if (before == null || !before.IsRanked) return false;
            return after.TierIndex > before.TierIndex;
        }

        /// <summary>Accent colour per tier, for the dashboard badge.</summary>
        public static string ColorFor(int tierIndex) => tierIndex switch
        {
            4 => "#FFD24A",   // advanced — gold
            3 => "#00D4FF",   // high intermediate — accent cyan
            2 => "#00E5A0",   // intermediate — green
            1 => "#9AA7B4",   // entry — steel
            _ => "#6B7683",   // unranked — muted
        };
    }
}
