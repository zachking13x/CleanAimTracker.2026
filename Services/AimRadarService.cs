using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_AIM_RADAR (2026-08-03): the six-axis skill shape.
    ///
    /// WHY: CAT could tell a player what was wrong with one session but never showed
    /// them WHO THEY ARE as an aimer. Competitors (Aim Lab's "aim profile") lead with
    /// exactly this: one glance, one shape, "I'm a tracking main with dead switching."
    /// It is the identity artifact — the thing worth screenshotting, and the thing that
    /// makes an obvious gap in the shape feel like a problem worth paying to fix.
    ///
    /// HONESTY RULES (these are the whole point — a fabricated shape is worse than none):
    ///   1. An axis with no sessions is NOT zero. It reports HasData=false and renders
    ///      as an empty rung, never as "you are bad at this".
    ///   2. The four category axes are scored against the SAME Voltaic-derived tables
    ///      the coach already uses (<see cref="PercentileBenchmarks"/>), at each
    ///      session's own difficulty. They are benchmark-relative, never percentiles.
    ///   3. Speed uses only scenarios with a reaction table and a real reading;
    ///      auto-fire drills report AvgReactionMs = 0 and drop out on their own.
    ///   4. Consistency is the one axis with NO external benchmark. It is explicitly
    ///      labelled as self-relative (your own session-to-session spread) so it is
    ///      never mistaken for a standard.
    /// </summary>
    public static class AimRadarService
    {
        /// <summary>Sessions per axis folded into the score — recent form, not lifetime.</summary>
        public const int RecentSessionsPerAxis = 10;

        /// <summary>Below this many sessions on an axis, the reading is shown as provisional.</summary>
        public const int ProvisionalBelow = 3;

        /// <summary>Axis is unlocked at 1 session; the whole radar needs this many overall.</summary>
        public const int MinSessionsForRadar = 3;

        // ── Axis definitions ─────────────────────────────────────────────────
        // Each category axis maps its member scenarios onto ONE PercentileBenchmarks
        // threshold family, so "Flick 72" always means the same thing regardless of
        // which flicking drill produced it.
        public const string AxisFlick       = "Flick";
        public const string AxisTracking    = "Tracking";
        public const string AxisSwitching   = "Switching";
        public const string AxisPrecision   = "Precision";
        public const string AxisSpeed       = "Speed";
        public const string AxisConsistency = "Consistency";

        /// <summary>Display order — this is the order the polygon's vertices are drawn in.</summary>
        public static readonly string[] AxisOrder =
        {
            AxisFlick, AxisTracking, AxisSwitching, AxisPrecision, AxisSpeed, AxisConsistency
        };

        /// <summary>
        /// The axes scored from a distinct family of DRILLS, as opposed to derived from
        /// whatever you already played. Speed and Consistency are read off the same
        /// sessions that feed these, so they are not independent evidence of breadth —
        /// playing only Flicking lights up both Flick AND Speed while covering one skill.
        /// Callers reasoning about how much of the player they've actually seen should
        /// count these, not <see cref="AxisOrder"/>.
        /// </summary>
        public static readonly string[] CategoryAxes =
        {
            AxisFlick, AxisTracking, AxisSwitching, AxisPrecision
        };

        // WHAT IS DELIBERATELY MISSING HERE: the bot drills (HeadshotStrafes, PeekClick,
        // HeadTrack), Shotgun and Sniper. Their accuracy is not the same measurement as an
        // orb drill's — a body shot takes two rounds, a head shot takes one, and pellet
        // spread scores differently again. Scoring a 39% bot-drill session against the
        // Voltaic flicking table (where 60% is the ENTRY rung) reported a 90%-accuracy
        // flicker as a 32/100 flicker. The fix is to leave them out until there are real
        // thresholds for them, not to pretend the scales are interchangeable.
        // The Speed axis is unaffected: it reads Shotgun and SmgAr, which DO have
        // published reaction tables.
        private static readonly Dictionary<string, string[]> AxisScenarios = new()
        {
            [AxisFlick]     = new[] { "Flicking", "DynamicClicking", "Adaptive" },
            [AxisTracking]  = new[] { "Tracking", "AirTracking" },
            [AxisSwitching] = new[] { "Switching", "SpeedSwitching" },
            [AxisPrecision] = new[] { "Precision", "StaticClicking" },
        };

        /// <summary>Which threshold family each category axis is judged against.</summary>
        private static readonly Dictionary<string, string> AxisBenchmarkFamily = new()
        {
            [AxisFlick]     = "Flicking",
            [AxisTracking]  = "Tracking",
            [AxisSwitching] = "Switching",
            [AxisPrecision] = "Precision",
        };

        public static readonly Dictionary<string, string> AxisBlurb = new()
        {
            [AxisFlick]       = "Snapping onto a new target and firing",
            [AxisTracking]    = "Staying glued to something that is moving",
            [AxisSwitching]   = "Moving between targets without resetting",
            [AxisPrecision]   = "Landing the shot on small or distant targets",
            [AxisSpeed]       = "How fast you get on target and shoot",
            [AxisConsistency] = "How closely your sessions resemble each other",
        };

        /// <summary>One spoke of the radar. Score is 0–100; ignore it entirely when !HasData.</summary>
        public sealed record RadarAxis(
            string Name,
            double Score,
            bool   HasData,
            int    SessionCount,
            bool   IsProvisional,
            bool   IsBenchmarkRelative,   // false = Consistency (self-relative only)
            string Blurb);

        public sealed record AimRadar(
            IReadOnlyList<RadarAxis> Axes,
            int    TotalSessions,
            bool   HasEnoughData,
            string StrongestAxis,     // "" when nothing is measured
            string WeakestAxis,       // "" when fewer than two axes have data
            double AverageScore);

        /// <summary>Build the radar from session history. Never throws on empty/garbage input.</summary>
        public static AimRadar Build(IEnumerable<AimTrainerResult>? history)
        {
            var all = (history ?? Enumerable.Empty<AimTrainerResult>())
                      .Where(r => r != null)
                      .OrderByDescending(r => r.Timestamp)
                      .ToList();

            var axes = new List<RadarAxis>();
            foreach (var name in AxisOrder)
                axes.Add(name switch
                {
                    AxisSpeed       => BuildSpeed(all),
                    AxisConsistency => BuildConsistency(all),
                    _               => BuildCategory(name, all),
                });

            var scored = axes.Where(a => a.HasData).ToList();

            // The headline number and the strength/weakness story are computed over the
            // BENCHMARK-RELATIVE axes only. Consistency is scored against the player's own
            // sessions, so ranking it beside the others compares two different rulers —
            // and a metronome-steady beginner would be told consistency is their strength
            // while sitting below the entry threshold on every skill that matters.
            var ranked = scored.Where(a => a.IsBenchmarkRelative).ToList();

            return new AimRadar(
                Axes:          axes,
                TotalSessions: all.Count,
                HasEnoughData: all.Count >= MinSessionsForRadar && scored.Count >= 2,
                StrongestAxis: ranked.Count >= 1 ? ranked.OrderByDescending(a => a.Score).First().Name : "",
                WeakestAxis:   ranked.Count >= 2 ? ranked.OrderBy(a => a.Score).First().Name : "",
                AverageScore:  ranked.Count >= 1 ? ranked.Average(a => a.Score) : 0);
        }

        // ── Category axes (accuracy vs. the Voltaic ladder) ──────────────────
        private static RadarAxis BuildCategory(string axis, List<AimTrainerResult> all)
        {
            string family    = AxisBenchmarkFamily[axis];
            var    scenarios = AxisScenarios[axis];

            var scores = new List<double>();
            foreach (var r in all)
            {
                if (!scenarios.Contains(r.Scenario)) continue;
                double? s = LadderScore(family, r.Difficulty, r.Accuracy);
                if (s.HasValue) scores.Add(s.Value);
                if (scores.Count >= RecentSessionsPerAxis) break;
            }

            return Finish(axis, scores, benchmarkRelative: true);
        }

        // ── Speed (pace / reaction vs. the Voltaic ladder, lower is better) ──
        private static RadarAxis BuildSpeed(List<AimTrainerResult> all)
        {
            var scores = new List<double>();
            foreach (var r in all)
            {
                // PaceStanding rejects Sniper, non-tabled scenarios, and any reading of
                // 0 — which is exactly how auto-fire drills report, so they self-exclude.
                var standing = PercentileBenchmarks.PaceStanding(r.Scenario, r.Difficulty, r.AvgReactionMs);
                if (standing == null) continue;
                double? s = LadderScoreFromTier(standing);
                if (s.HasValue) scores.Add(s.Value);
                if (scores.Count >= RecentSessionsPerAxis) break;
            }

            return Finish(AxisSpeed, scores, benchmarkRelative: true);
        }

        // ── Consistency (self-relative: spread of recent accuracy) ───────────
        private static RadarAxis BuildConsistency(List<AimTrainerResult> all)
        {
            // Compare like with like: the spread only means something inside a single
            // scenario+difficulty. Mixing Nightmare Precision with Easy Tracking would
            // measure the player's drill menu, not their steadiness.
            var group = all.Where(r => !string.IsNullOrEmpty(r.Scenario))
                           .GroupBy(r => r.Scenario + "|" + r.Difficulty)
                           .Where(g => g.Count() >= ProvisionalBelow)
                           .OrderByDescending(g => g.Count())
                           .FirstOrDefault();

            if (group == null)
                return new RadarAxis(AxisConsistency, 0, false, 0, true, false, AxisBlurb[AxisConsistency]);

            var acc = group.Take(RecentSessionsPerAxis).Select(r => r.Accuracy).ToList();
            double mean = acc.Average();
            if (mean <= 0)
                return new RadarAxis(AxisConsistency, 0, false, acc.Count, true, false, AxisBlurb[AxisConsistency]);

            double variance = acc.Sum(a => (a - mean) * (a - mean)) / acc.Count;
            double cv       = Math.Sqrt(variance) / mean * 100.0;   // coefficient of variation, %

            // Scale: <=3% spread reads as locked in (100), >=20% as wildly swingy (0).
            // Documented as a display scale, not a benchmark — there is no published
            // consistency standard to anchor against, and inventing one would be a lie.
            double score = 100.0 * (20.0 - Math.Clamp(cv, 3.0, 20.0)) / 17.0;

            return new RadarAxis(AxisConsistency, Math.Round(score, 1), true, acc.Count,
                                 acc.Count < RecentSessionsPerAxis / 2, false, AxisBlurb[AxisConsistency]);
        }

        private static RadarAxis Finish(string axis, List<double> scores, bool benchmarkRelative)
        {
            if (scores.Count == 0)
                return new RadarAxis(axis, 0, false, 0, true, benchmarkRelative, AxisBlurb[axis]);

            return new RadarAxis(axis, Math.Round(scores.Average(), 1), true, scores.Count,
                                 scores.Count < ProvisionalBelow, benchmarkRelative, AxisBlurb[axis]);
        }

        // ── Ladder → 0-100 ───────────────────────────────────────────────────
        // Anchors chosen so the four published rungs land at readable positions:
        //   entry 40 · intermediate 60 · high-intermediate 80 · advanced 95.
        // Everything between is linear interpolation, so the mapping is monotone and
        // a real improvement always moves the spoke outward.
        private static readonly double[] TierAnchor = { 95.0, 80.0, 60.0, 40.0 };  // index matches tier index

        internal static double? LadderScore(string family, string difficulty, double accuracy)
        {
            var standing = PercentileBenchmarks.AccuracyStanding(family, difficulty, accuracy);
            return standing == null ? null : LadderScoreFromTier(standing);
        }

        internal static double? LadderScoreFromTier(PercentileBenchmarks.BenchmarkStanding s)
        {
            int tier = s.TierIndex;

            // Below every threshold: scale 0 → 40 across the run-up to the entry rung.
            if (tier < 0)
            {
                if (s.NextThreshold is not double entry) return 0;
                double floor = s.LowerIsBetter ? entry * 1.8 : entry * 0.5;
                double frac  = s.LowerIsBetter
                    ? (floor - s.Value) / Math.Max(1e-6, floor - entry)
                    : (s.Value - floor) / Math.Max(1e-6, entry - floor);
                return Math.Clamp(frac, 0, 1) * 40.0;
            }

            double anchor = TierAnchor[tier];

            // Already at the top rung: 95 → 100 across the remaining headroom.
            if (s.NextThreshold is not double next)
            {
                double cap  = s.LowerIsBetter ? 0.0 : 100.0;
                double span = Math.Abs(cap - AnchorEdge(s));
                double over = Math.Abs(AnchorEdge(s) - s.Value);
                return 95.0 + 5.0 * Math.Clamp(span <= 1e-6 ? 0 : over / span, 0, 1);
            }

            // Between this rung and the next: interpolate anchor → next anchor.
            double nextAnchor = TierAnchor[tier - 1];
            double edge       = AnchorEdge(s);
            double progress   = s.LowerIsBetter
                ? (edge - s.Value) / Math.Max(1e-6, edge - next)
                : (s.Value - edge) / Math.Max(1e-6, next - edge);

            return anchor + (nextAnchor - anchor) * Math.Clamp(progress, 0, 1);
        }

        /// <summary>The threshold the player has just cleared — the low edge of their current rung.</summary>
        private static double AnchorEdge(PercentileBenchmarks.BenchmarkStanding s)
            => s.TierThreshold ?? s.Value;

        /// <summary>
        /// The drills that feed an axis, in the order they appear on the trainer.
        /// Empty for Speed and Consistency, which are derived rather than scenario-scoped.
        /// The detail view uses this to answer "so what do I actually play to move this?" —
        /// a score with no route to change it is a judgement, not coaching.
        /// </summary>
        public static IReadOnlyList<string> ScenariosFor(string axis)
        {
            // Speed is a DERIVED axis — it isn't scored from a scenario family, it's read
            // off the reaction tables. But it is still trainable, and the drills that
            // train it are exactly the ones that HAVE a reaction table and real per-shot
            // timing. Returning empty here made the routine say "Speed is your weakest
            // area" and then hand over a Flicking drill chosen by a fallback — a claim
            // and an action that didn't match.
            if (axis == AxisSpeed) return SpeedScenarios;

            // Consistency is self-relative across whatever you already play; there is no
            // "consistency drill", so it stays empty by design.
            return AxisScenarios.TryGetValue(axis, out var s) ? s : Array.Empty<string>();
        }

        /// <summary>Scenarios with a published reaction table AND real per-shot timing.</summary>
        private static readonly string[] SpeedScenarios = { "Flicking", "Switching", "Shotgun" };

        /// <summary>
        /// How an axis is scored, in one sentence, for display. Says out loud when a
        /// number is measured against the player themselves rather than a standard.
        /// </summary>
        public static string ScoringBasis(string axis) => axis switch
        {
            AxisSpeed => "Time on target, against the published threshold for each drill. " +
                         "Hold-to-fire drills have no per-shot timing, so they're excluded.",

            AxisConsistency => "How tightly your recent sessions cluster on your most-played " +
                               "drill. There is no published standard for consistency, so this " +
                               "one is measured against you — not against other players.",

            _ when AxisBenchmarkFamily.TryGetValue(axis, out var fam) =>
                $"Accuracy against the {fam} benchmark thresholds, at whatever difficulty you played.",

            _ => "",
        };

        /// <summary>Plain-English one-liner for the shape. "" when there isn't enough to say.</summary>
        public static string Summarize(AimRadar radar)
        {
            if (radar == null || !radar.HasEnoughData) return "";
            if (string.IsNullOrEmpty(radar.StrongestAxis) || string.IsNullOrEmpty(radar.WeakestAxis)) return "";

            var strong = radar.Axes.First(a => a.Name == radar.StrongestAxis);
            var weak   = radar.Axes.First(a => a.Name == radar.WeakestAxis);

            // A shape that's flat within noise isn't a strength/weakness story. Ties land
            // here too — strongest and weakest resolve to the same axis when scores match.
            if (radar.StrongestAxis == radar.WeakestAxis || strong.Score - weak.Score < 8)
                return "Your axes are close together — no single area is holding you back yet.";

            return $"{strong.Name} is your strongest axis. {weak.Name} is the one dragging the shape in.";
        }
    }
}
