using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// COACH_AIM_PROFILE Gate 1: turns session history into a <see cref="PlayerAimProfile"/> —
    /// the cross-session "who you are as a player" DATA. Validity-gated exactly like the
    /// per-session coach (invalid metrics are excluded from aggregation via
    /// <see cref="AiCoachService.DrillMetricValid"/>), and every conclusion is a boolean
    /// over real aggregates. No text is generated here; phrasing happens in
    /// <see cref="AimProfilePhrasing"/> from authored fragments selected by these booleans.
    /// </summary>
    public static class AimProfileBuilder
    {
        public const int MaxSessions = 20;   // window: the last N valid sessions
        public const int MinSessions = 5;    // below this, no profile — "need more sessions"
        public const int MinSamplesPerMetric = 3;   // a metric needs this many valid reads to characterize

        // ── Authored metric bands (good, poor) — hand-set, justified from real captures ──
        // higher-is-better metrics:
        public const double AccuracyGood = 80, AccuracyPoor = 60;
        public const double SmoothnessGood = 65, SmoothnessPoor = 45;
        public const double ConsistencyGood = 80, ConsistencyPoor = 55;
        public const double PathEffGood = 0.80, PathEffPoor = 0.55;
        // lower-is-better metrics — V2 directional scale (2026-07-06 captures):
        // neutral clicker reads 18-26; a real habit reads 35+.
        public const double OvershootGood = 25, OvershootPoor = 40;
        public const double UndershootGood = 25, UndershootPoor = 40;
        // in-flight overshoot: 0.20 is the click-point/in-flight boundary; ≥0.30 is clearly wild.
        public const double InFlightGood = 0.10, InFlightPoor = 0.30;

        // AUDIT FIX (2026-07-06): retired as a trigger — a best-vs-average gap of
        // 150ms is an order-statistic certainty, not a signal. Kept only because
        // tests exercise the constant; headroom now grades pace against the
        // authored per-scenario benchmarks (see the pace-headroom block below).
        public const double ReactionHeadroomMs = 150;

        // Pattern thresholds — V2 directional scale (see TechniquePrescriptionLibrary
        // calibration note): 35 minimum + 8-point dominance, mirroring the per-session
        // click-point gates so the profile and the session coach agree.
        public const double ClickDirectionMin       = 35;   // dominant direction reads at least this…
        public const double ClickDominanceMargin    = 8;    // …and clears the other direction by this
        public const double LateClickerCleanCeiling = 0.20; // …while motion arrived clean
        public const double CeilingGapDrop = 12;            // easy/med accuracy − hard accuracy

        private record Read(string Metric, bool LowerIsBetter, double Good, double Poor);

        // Banded metrics (the strengths/weaknesses sets are built from these).
        private static readonly Read[] Banded =
        {
            new("Accuracy",            false, AccuracyGood,    AccuracyPoor),
            new("MovementSmoothness",  false, SmoothnessGood,  SmoothnessPoor),
            new("MovementConsistency", false, ConsistencyGood, ConsistencyPoor),
            new("PathEfficiency",      false, PathEffGood,     PathEffPoor),
            new("OvershootPct",        true,  OvershootGood,   OvershootPoor),
            new("UndershootPct",       true,  UndershootGood,  UndershootPoor),
            new("MovementOvershoot",   true,  InFlightGood,    InFlightPoor),
        };

        public static PlayerAimProfile Build(CoachMemory memory)
        {
            // Real (non-assessment) sessions, newest first, capped to the window.
            var sessions = (memory?.AllDrills ?? new List<AimTrainerResult>())
                .Where(d => !d.IsAssessmentSession && d.Hits + d.Misses > 0)
                .Take(MaxSessions)
                .ToList();

            if (sessions.Count < MinSessions)
            {
                return new PlayerAimProfile
                {
                    HasEnoughData   = false,
                    SessionsAnalyzed = sessions.Count,
                    SessionsNeeded  = MinSessions - sessions.Count
                };
            }

            var metrics = new Dictionary<string, MetricAggregate>();
            foreach (var r in Banded)
            {
                var agg = Aggregate(sessions, r);
                if (agg != null) metrics[r.Metric] = agg;
            }

            var strengths = metrics.Values.Where(m => m.Band == MetricBand.Good)
                                          .Select(m => m.Metric).ToList();
            var weaknesses = metrics.Values.Where(m => m.Band == MetricBand.Poor)
                                           .Select(m => m.Metric).ToList();

            // ── Pace headroom — AUDIT FIX (2026-07-06) ──────────────────────────
            // Was: best « average gap ≥ 150ms. The session best is the minimum of
            // dozens of hits and ALWAYS sits hundreds of ms below the mean (order
            // statistics), so headroom was true for every player — and the profile
            // told every accurate player they were "slow with speed in reserve".
            // Now: headroom = at least half of valid sessions pace slower than the
            // authored per-scenario "good" benchmark — an actual pace reading.
            var gapSamples = sessions
                .Where(s => s.AvgReactionMs > 0 && s.BestReactionMs > 0
                         && s.AvgReactionMs < 2000 && s.AvgReactionMs >= s.BestReactionMs)
                .Select(s => new { s.AvgReactionMs, s.BestReactionMs })
                .ToList();
            double meanAvgRx = gapSamples.Count > 0 ? gapSamples.Average(g => g.AvgReactionMs) : 0;
            double meanBestRx = gapSamples.Count > 0 ? gapSamples.Average(g => g.BestReactionMs) : 0;
            var paceSamples = sessions
                .Where(s => s.AvgReactionMs > 0 && s.AvgReactionMs < 2000)
                .ToList();
            int slowSessions = paceSamples.Count(s =>
                s.AvgReactionMs > AiCoachService.Bench.ReactionGood(s.Scenario));
            bool headroom = paceSamples.Count >= MinSamplesPerMetric
                            && slowSessions * 2 >= paceSamples.Count;

            string primary = sessions.GroupBy(s => s.Scenario)
                                     .OrderByDescending(g => g.Count())
                                     .First().Key;

            // FUTURE: per-game fusion — when the in-game tracker tags WHICH game a session
            // belongs to (Fortnite, etc.), branch here to weave tagged real-match data into
            // the profile. Deferred: the tracker has no game tag yet and there's no tagged
            // in-game data to synthesize from. Prerequisite = add game-tagging upstream.

            // ceiling_gap evidence (0/0 when there isn't enough Hard data to judge).
            var easyMed = sessions.Where(s => s.Difficulty is "Easy" or "Medium").ToList();
            var hardN   = sessions.Where(s => s.Difficulty is "Hard" or "Nightmare").ToList();
            double easyMedAcc = easyMed.Count > 0 ? easyMed.Average(s => s.Accuracy) : 0;
            double hardAcc    = hardN.Count > 0 ? hardN.Average(s => s.Accuracy) : 0;

            // Trend on the primary scenario ONLY (newest-first), so a shift in scenario
            // mix can't masquerade as an accuracy trend.
            var primaryAcc = sessions.Where(s => s.Scenario == primary).Select(s => s.Accuracy).ToList();
            TrendDir primaryTrend = TrendOf(primaryAcc, minCount: 4);

            var profile = new PlayerAimProfile
            {
                HasEnoughData      = true,
                SessionsAnalyzed   = sessions.Count,
                SessionsNeeded     = 0,
                Metrics            = metrics,
                StrengthMetrics    = strengths,
                WeaknessMetrics    = weaknesses,
                ReactionGapMs      = meanAvgRx - meanBestRx,   // descriptive only — never a trigger (order-statistic)
                HasReactionHeadroom = headroom,
                MeanAvgReactionMs  = meanAvgRx,
                MeanBestReactionMs = meanBestRx,
                PrimaryScenario    = primary,
                CeilingEasyMedAcc  = easyMedAcc,
                CeilingHardAcc     = hardAcc,
                PrimaryAccuracyTrend = primaryTrend
            };

            profile.Patterns.AddRange(DetectPatterns(sessions, metrics, profile));
            return profile;
        }

        // ── Per-metric aggregation: validity-gated mean, band, trend ───────────
        private static MetricAggregate? Aggregate(List<AimTrainerResult> sessions, Read r)
        {
            // newest-first values that pass the real validity gate
            var values = sessions
                .Where(s => AiCoachService.DrillMetricValid(s, r.Metric))
                .Select(s => MetricValue(s, r.Metric))
                .ToList();
            if (values.Count < MinSamplesPerMetric) return null;

            double mean = values.Average();
            MetricBand band = r.LowerIsBetter
                ? (mean <= r.Good ? MetricBand.Good : mean >= r.Poor ? MetricBand.Poor : MetricBand.Neutral)
                : (mean >= r.Good ? MetricBand.Good : mean <= r.Poor ? MetricBand.Poor : MetricBand.Neutral);

            TrendDir trend = TrendOf(values, minCount: 4);

            return new MetricAggregate
            {
                Metric = r.Metric, Mean = mean, SampleCount = values.Count,
                Band = band, Trend = trend, LowerIsBetter = r.LowerIsBetter
            };
        }

        // Direction of a newest-first series: newer half vs older half, 5% relative band =
        // flat. Returns Flat unless there are at least minCount samples (a trend off 2-3
        // sessions is noise, not a trend).
        private static TrendDir TrendOf(List<double> newestFirst, int minCount)
        {
            if (newestFirst.Count < minCount) return TrendDir.Flat;
            int half = newestFirst.Count / 2;
            if (half < 1) return TrendDir.Flat;
            double newer = newestFirst.Take(half).Average();
            double older = newestFirst.Skip(newestFirst.Count - half).Average();
            double denom = Math.Abs(older) < 1e-9 ? 1 : Math.Abs(older);
            double rel = (newer - older) / denom;
            return rel > 0.05 ? TrendDir.Up : rel < -0.05 ? TrendDir.Down : TrendDir.Flat;
        }

        private static double MetricValue(AimTrainerResult s, string metric) => metric switch
        {
            "Accuracy"            => s.Accuracy,
            "MovementSmoothness"  => s.MovementSmoothness,
            "MovementConsistency" => s.MovementConsistency,
            "PathEfficiency"      => s.PathEfficiency,
            "OvershootPct"        => s.OvershootPct,
            "UndershootPct"       => s.UndershootPct,
            "MovementOvershoot"   => s.MovementOvershoot,
            _                     => 0
        };

        // ── T1.2: named cross-metric patterns — only TRUE ones, strongest first ─
        private static List<string> DetectPatterns(
            List<AimTrainerResult> sessions,
            Dictionary<string, MetricAggregate> m,
            PlayerAimProfile p)
        {
            var fired = new List<string>();

            double? Mean(string k) => m.TryGetValue(k, out var a) ? a.Mean : (double?)null;
            bool Band(string k, MetricBand b) => m.TryGetValue(k, out var a) && a.Band == b;

            double? overshoot = Mean("OvershootPct");
            double? inflight  = Mean("MovementOvershoot");
            double? accuracy  = Mean("Accuracy");

            // clean_mechanics (STRENGTH / rule-out): controlled movement — consistency or
            // smoothness genuinely good AND in-flight overshoot not in the wild band.
            bool cleanMech = (Band("MovementConsistency", MetricBand.Good) || Band("MovementSmoothness", MetricBand.Good))
                             && m.TryGetValue("MovementOvershoot", out var mo) && mo.Band != MetricBand.Poor;
            if (cleanMech) fired.Add("clean_mechanics");

            double? undershoot = Mean("UndershootPct");

            // late_clicker (CONCERN / the unlock): clicks land long while the path arrived
            // clean — a click-timing habit, not a movement one. Requires clear dominance
            // over the opposite direction (no coin-flip verdicts on near-ties).
            bool lateClicker = overshoot is double ov && ov >= ClickDirectionMin
                               && ov >= (undershoot ?? 0) + ClickDominanceMargin
                               && inflight is double inf && inf < LateClickerCleanCeiling;
            if (lateClicker) fired.Add("late_clicker");

            // short_clicker (CONCERN / the unlock): the mirror habit — clicks land SHORT
            // of center while motion arrived clean. The profile had no undershoot
            // pattern before 2026-07-06, so a genuine short-lander could never be named.
            bool shortClicker = undershoot is double un && un >= ClickDirectionMin
                                && un >= (overshoot ?? 0) + ClickDominanceMargin
                                && inflight is double inf2 && inf2 < LateClickerCleanCeiling;
            if (shortClicker) fired.Add("short_clicker");

            // ceiling_gap (CONCERN): strong on Easy/Medium, falls off on Hard/Nightmare.
            var easyMed = sessions.Where(s => s.Difficulty is "Easy" or "Medium").ToList();
            var hard    = sessions.Where(s => s.Difficulty is "Hard" or "Nightmare").ToList();
            if (easyMed.Count >= MinSamplesPerMetric && hard.Count >= MinSamplesPerMetric)
            {
                double emAcc = easyMed.Average(s => s.Accuracy);
                double hAcc  = hard.Average(s => s.Accuracy);
                if (emAcc >= AccuracyGood - 5 && emAcc - hAcc >= CeilingGapDrop)
                    fired.Add("ceiling_gap");
            }

            // accurate_but_slow (HEADROOM): accurate, with clear reaction headroom unused.
            bool accurateButSlow = accuracy is double acc && acc >= AccuracyGood && p.HasReactionHeadroom;
            if (accurateButSlow) fired.Add("accurate_but_slow");

            // safe_player (HEADROOM): accurate AND low error in BOTH directions (not pushing,
            // playing within the ceiling). Distinct from accurate_but_slow's pace read.
            bool safePlayer = accuracy is double a2 && a2 >= AccuracyGood
                              && Band("OvershootPct", MetricBand.Good)
                              && m.TryGetValue("UndershootPct", out var us) && us.Band != MetricBand.Poor;
            if (safePlayer) fired.Add("safe_player");

            return fired;
        }
    }
}
