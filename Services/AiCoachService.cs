using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;
#nullable enable

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Local AI coaching engine — no API key, no internet, no cost.
    /// Generates personalized coaching reports using the player's
    /// actual session data, benchmarks, and trend analysis.
    /// </summary>
    public static class AiCoachService
    {
        // ── Benchmarks ────────────────────────────────────────────────
        // Public: TechniquePrescriptionLibrary signatures grade pace against the
        // same scenario benchmarks the coach uses (TASK-1.2).
        public static class Bench
        {
            // Accuracy — scenario-specific thresholds
            // Tracking is harder so thresholds are lower but not dramatically so
            // Precision is the purest accuracy test so thresholds are highest
            public static double AccuracyElite(string s) => s switch
            {
                "Tracking"  => 80,
                "Precision" => 88,
                "Flicking"  => 82,
                "Switching" => 80,
                "Adaptive"  => 80,
                "Sniper"    => 90,
                "Shotgun"   => 80,
                "SmgAr"     => 82,
                _           => 82,
            };

            public static double AccuracyGood(string s) => s switch
            {
                "Tracking"  => 65,
                "Precision" => 72,
                "Flicking"  => 68,
                "Switching" => 65,
                "Adaptive"  => 65,
                "Sniper"    => 75,
                "Shotgun"   => 65,
                "SmgAr"     => 68,
                _           => 68,
            };

            public static double AccuracyAvg(string s) => s switch
            {
                "Tracking"  => 50,
                "Precision" => 55,
                "Flicking"  => 52,
                "Switching" => 50,
                "Adaptive"  => 50,
                "Sniper"    => 55,
                "Shotgun"   => 45,
                "SmgAr"     => 50,
                _           => 52,
            };

            // Pace thresholds — scenario-specific. TASK-0.3: per the audit in
            // ReactionMetric, most scenarios measure TIME PER TARGET (previous
            // hit → this hit: travel + acquisition + click + spawn delay), not
            // stimulus reaction. Thresholds are calibrated to what each scenario
            // actually measures; the previous "_ => 220/350/500" default graded
            // StaticClicking's ~1100ms time-per-target as permanently "slow" —
            // a misdiagnosis, not a measurement.
            // Sniper: reaction is NOT the primary metric — benchmarks set to 0 so reaction always
            // grades "slow", and weapon-scenario coaching explicitly suppresses reaction tips.
            public static double ReactionElite(string s) => s switch
            {
                "Flicking"        => 200,
                "Switching"       => 220,
                "Tracking"        => 280,
                "Precision"       => 350,
                "Adaptive"        => 250,
                "Shotgun"         => 180,  // true reaction (spawn-anchored)
                "SmgAr"           => 220,
                "Sniper"          => 0,    // reaction coaching suppressed for Sniper
                // Time-per-target scenarios (includes built-in spawn delay):
                // StaticClicking observed intermediate ≈ 1100 avg / 880 best.
                "StaticClicking"  => 650,
                "DynamicClicking" => 700,
                "AirTracking"     => 350,
                "Evasive"         => 400,
                // True-reaction scenarios (stimulus-anchored):
                "Reactive"        => 300,
                "PeekTraining"    => 230,
                _                 => 220,
            };

            public static double ReactionGood(string s) => s switch
            {
                "Flicking"        => 300,
                "Switching"       => 350,
                "Tracking"        => 420,
                "Precision"       => 500,
                "Adaptive"        => 380,
                "Shotgun"         => 280,
                "SmgAr"           => 320,
                "Sniper"          => 0,    // reaction coaching suppressed for Sniper
                "StaticClicking"  => 850,
                "DynamicClicking" => 950,
                "AirTracking"     => 500,
                "Evasive"         => 560,
                "Reactive"        => 430,
                "PeekTraining"    => 340,
                _                 => 350,
            };

            public static double ReactionAverage(string s) => s switch
            {
                "Flicking"        => 450,
                "Switching"       => 500,
                "Tracking"        => 580,
                "Precision"       => 650,
                "Adaptive"        => 520,
                "Shotgun"         => 350,
                "SmgAr"           => 420,
                "Sniper"          => 0,    // reaction coaching suppressed for Sniper
                "StaticClicking"  => 1100,
                "DynamicClicking" => 1200,
                "AirTracking"     => 680,
                "Evasive"         => 760,
                "Reactive"        => 600,
                "PeekTraining"    => 490,
                _                 => 500,
            };

            public const int StreakElite   = 12;
            public const int StreakGood    = 6;
            public const int StreakAverage = 3;
        }

        // ── Public entry point ────────────────────────────────────────
        /// <summary>
        /// Primary overload — called from AimTrainerResultWindow with full CoachMemory.
        /// </summary>
        public static AiCoachReport Analyze(AimTrainerResult result, CoachMemory memory)
        {
            if (result.Hits == 0)
                return new AiCoachReport
                {
                    OverallRating       = "No Data",
                    Headline            = "No targets hit this session — run a drill to get coaching.",
                    Strengths           = new List<string> { "Click targets to generate coaching data." },
                    Weaknesses          = new List<string>(),
                    Advice              = new List<string> { "Use the Start button to begin a drill, then click the targets that appear on screen." },
                    NextDrillSuggestion = "Try Precision on Easy — it is the best starting point for new players.",
                    MotivationalClose   = "Every expert was a beginner once. Start clicking.",
                };

            var context = BuildContext(result, memory);
            var report  = GenerateReport(result, context, memory);
            var recentTracker = memory.RecentTrackerSessions.Count > 0
                ? memory.RecentTrackerSessions[0]
                : null;
            var prescriptions = DrillPrescriptionEngine.Prescribe(result, context, memory, recentTracker);
            report.Prescription = prescriptions.Count > 0 ? prescriptions[0] : null;
            return report;
        }

        // ── Context builder ───────────────────────────────────────────
        internal record CoachContext(
            string AccuracyGrade,
            string ReactionGrade,
            string StreakGrade,
            double AccuracyDelta,
            double ScoreDelta,
            double ReactionDelta,
            bool   IsFirstSession,
            bool   IsImproving,
            bool   IsConsistent,
            int    SessionCount,
            double PersonalBestAccuracy,
            double PersonalBestReaction,
            bool   NewAccuracyRecord,
            bool   NewReactionRecord,
            string WeakArea,
            string StrongArea,
            double OverallAvgAccuracy,
            double OverallAvgReaction,
            int    TotalSessionsAll,
            // New tracker fields — nullable because tracker session may not exist
            double? TrackerCmPer360,
            double? TrackerSmoothness,
            double? TrackerCorrectionSharpness
        );

        private static CoachContext BuildContext(AimTrainerResult r, CoachMemory memory)
        {
            // Derive same-scenario history from memory (excludes current session by Timestamp)
            var same = memory.AllDrills
                .Where(h => h.Scenario == r.Scenario && h.Timestamp != r.Timestamp)
                .OrderByDescending(h => h.Timestamp)
                .ToList();

            // Extract tracker data from memory — only use if session was long enough
            SessionSummary? tracker = memory.RecentTrackerSessions.Count > 0
                ? memory.RecentTrackerSessions[0]
                : null;

            double? trackerCmPer360            = null;
            double? trackerSmoothness          = null;
            double? trackerCorrectionSharpness = null;

            if (tracker != null && tracker.SessionSeconds >= 45)
            {
                trackerCmPer360            = tracker.CmPer360 > 0 ? tracker.CmPer360 : (double?)null;
                trackerSmoothness          = tracker.SmoothnessScore;
                trackerCorrectionSharpness = tracker.CorrectionSharpness;
            }

            bool isFirst = same.Count == 0;

            string accGrade = r.Accuracy >= Bench.AccuracyElite(r.Scenario) ? "elite"
                            : r.Accuracy >= Bench.AccuracyGood(r.Scenario)  ? "good"
                            : r.Accuracy >= Bench.AccuracyAvg(r.Scenario)   ? "average"
                            : "developing";

            string reactGrade = r.AvgReactionMs <= Bench.ReactionElite(r.Scenario)   ? "elite"
                              : r.AvgReactionMs <= Bench.ReactionGood(r.Scenario)    ? "good"
                              : r.AvgReactionMs <= Bench.ReactionAverage(r.Scenario) ? "average"
                              : "slow";

            string streakGrade = r.MaxStreak >= Bench.StreakElite   ? "elite"
                               : r.MaxStreak >= Bench.StreakGood    ? "good"
                               : r.MaxStreak >= Bench.StreakAverage ? "average"
                               : "low";

            double accDelta   = isFirst ? 0 : r.Accuracy      - same[0].Accuracy;
            double scoreDelta = isFirst ? 0 : r.Score         - same[0].Score;
            double reactDelta = isFirst ? 0 : r.AvgReactionMs - same[0].AvgReactionMs;

            double pbAccuracy = isFirst ? r.Accuracy      : Math.Max(r.Accuracy,      same.Max(h => h.Accuracy));
            double pbReaction = isFirst ? r.AvgReactionMs : Math.Min(r.AvgReactionMs, same.Min(h => h.AvgReactionMs));
            bool   newAccPB   = same.Count >= 2 && r.Accuracy >= 40.0
                                    && r.Accuracy > same.Max(h => h.Accuracy) + 1.5;
            bool   newReactPB = same.Count >= 2 && r.Accuracy >= 40.0
                                    && r.AvgReactionMs > 0
                                    && r.AvgReactionMs < same.Where(h => h.AvgReactionMs > 0)
                                                               .Select(h => h.AvgReactionMs)
                                                               .DefaultIfEmpty(double.MaxValue)
                                                               .Min() - 10.0;

            bool improving  = !isFirst && same.Count >= 2
                && r.Score > same.Take(3).Average(h => h.Score);
            bool consistent = !isFirst && same.Count >= 3
                && same.Take(3).Select(h => h.Accuracy).Max()
                 - same.Take(3).Select(h => h.Accuracy).Min() < 12;

            var areas = new Dictionary<string, double>
            {
                ["accuracy"]  = r.Accuracy,
                ["reaction"]  = r.AvgReactionMs > 0 ? 100 - Math.Min(100, r.AvgReactionMs / 6.0) : 50,
                ["streak"]    = Math.Min(100, r.MaxStreak * 7.0),
                ["endurance"] = r.Misses == 0 ? 100 : 100.0 * r.Hits / (r.Hits + r.Misses)
            };

            string weak   = areas.OrderBy(a => a.Value).First().Key;
            string strong = areas.OrderByDescending(a => a.Value).First().Key;

            // Cross-scenario context from ALL history (last 10 sessions, excludes current)
            var allRecent = memory.AllDrills
                .Where(h => h.Timestamp != r.Timestamp)
                .Take(10)
                .ToList();

            double overallAvgAccuracy = allRecent.Count > 0 ? allRecent.Average(h => h.Accuracy) : r.Accuracy;
            double overallAvgReaction = allRecent.Count > 0 ? allRecent.Average(h => h.AvgReactionMs) : r.AvgReactionMs;
            int    totalSessionsAll   = memory.AllDrills.Count;

            return new CoachContext(
                accGrade, reactGrade, streakGrade,
                accDelta, scoreDelta, reactDelta,
                isFirst, improving, consistent,
                same.Count + 1,
                pbAccuracy, pbReaction,
                newAccPB, newReactPB,
                weak, strong,
                overallAvgAccuracy, overallAvgReaction, totalSessionsAll,
                trackerCmPer360, trackerSmoothness, trackerCorrectionSharpness
            );
        }

        // ── Variant picker ────────────────────────────────────────────
        /// <summary>
        /// Rotates through variants by per-scenario session count so the same
        /// coaching line never repeats in consecutive same-scenario sessions.
        /// Using c.SessionCount (same-scenario history count + 1) rather than
        /// c.TotalSessionsAll means the rotation is tied to how many times the
        /// user has run THIS scenario, not their global session count — so
        /// playing a new scenario doesn't shift the rotation for existing ones.
        /// </summary>
        private static string Pick(int sessionIndex, params string[] variants)
            => variants[sessionIndex % variants.Length];

        // ── Global variant picker (rotates by total drill count + rule index) ───
        /// <summary>
        /// Used for new rules (TASK-08) — rotates by global session count + rule index
        /// so the same tip doesn't repeat across consecutive sessions of different scenarios.
        /// </summary>
        private static string PickGlobal(CoachMemory memory, int ruleIndex, params string[] variants)
            => variants[(memory.TotalDrillCount + ruleIndex) % variants.Length];

        // ── Report generator ──────────────────────────────────────────
        // TASK-2.2: every engine output becomes a CoachObservation candidate;
        // CoachReportComposer is the only path to user-facing coach text.
        private static AiCoachReport GenerateReport(AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            var candidates = new List<CoachObservation>();

            // Severity bases: Area (60) > Strength (50) > Tip (40) so a concern
            // wins a same-aspect tie — the safer coaching default. Within a
            // section, emission order encodes each engine's own priority.
            void AddAll(List<(string key, string text)> items,
                        CoachSection section, ObservationPolarity polarity, int baseSeverity)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    candidates.Add(new CoachObservation
                    {
                        FactKey       = items[i].key,
                        SourceEngine  = nameof(AiCoachService),
                        Section       = section,
                        Polarity      = polarity,
                        Severity      = baseSeverity - i,
                        RequiresBehaviorChange = section is CoachSection.Area or CoachSection.Tip,
                        Message       = items[i].text,
                        RequiredMetrics = MetricsFor(items[i].key)
                    });
                }
            }

            AddAll(GetWeaknessCandidates(r, c, memory), CoachSection.Area, ObservationPolarity.Concern, 60);
            AddAll(GetStrengthCandidates(r, c, memory), CoachSection.Strength, ObservationPolarity.Strength, 50);
            AddAll(GetAdviceCandidates(r, c, memory), CoachSection.Tip, ObservationPolarity.Neutral, 40);

            // ── TASK-2.2: verification loop for the open prescription ──────────
            // Improved → loop-closure strength (60, beats generic strengths).
            // Flat 3+ → honest escalation area (75, leads the report).
            // Drill not run → one nudge tip (45), once.
            var followUp = PrescriptionFollowUpService.Evaluate(
                memory, r, m => DrillMetricValid(r, m));
            if (followUp != null)
                candidates.Add(followUp);

            // ── TASK-2.1: technique prescription — at most ONE per report ──────
            // Selection is blocked while a loop is open AND on the session a
            // follow-up fires (a closed loop gets its celebration; a new
            // prescription starts next session — no churn). The instruction takes
            // the area slot (severity 70 beats generic weakness areas at 60); its
            // PracticeDrill takes the drill slot (severity 20 beats rx_next_drill
            // at 10) so the area and the suggested drill always agree.
            var technique = followUp != null
                ? null
                : TechniquePrescriptionSelector.Select(
                    new PrescriptionContext(r, memory, m => DrillMetricValid(r, m)));
            if (technique != null)
            {
                candidates.Add(new CoachObservation
                {
                    FactKey      = technique.Prescription.PrescriptionKey,
                    SourceEngine = nameof(TechniquePrescriptionLibrary),
                    Section      = CoachSection.Area,
                    Polarity     = ObservationPolarity.Concern,
                    Severity     = 70,
                    RequiresBehaviorChange = true,
                    RequiredMetrics = technique.Prescription.RequiredMetrics.ToList(),
                    Message      = technique.Prescription.Instruction
                });
                candidates.Add(new CoachObservation
                {
                    FactKey          = "rx_technique",
                    SourceEngine     = nameof(TechniquePrescriptionLibrary),
                    Section          = CoachSection.Prescription,
                    Polarity         = ObservationPolarity.Concern,
                    Severity         = 20,
                    PrescriptionType = PrescriptionType.Remedial,
                    RequiredMetrics  = technique.Prescription.RequiredMetrics.ToList(),
                    Message          = technique.Drill.ToString()
                });
            }

            // TASK-3.3: Voltaic-relative benchmark context. Severity 1 — it can
            // never displace a diagnostic tip, and the composer's validity filter
            // drops it when its metric is invalid.
            var benchmark = PercentileBenchmarks.BenchmarkObservation(r);
            if (benchmark != null)
                candidates.Add(benchmark);

            // Headline: the drill coach's scenario-aware single template enters as
            // a candidate; the composer's tone floor can still reject praise
            // polarity on an all-concern report.
            candidates.Add(new CoachObservation
            {
                FactKey      = "headline_drill",
                SourceEngine = nameof(AiCoachService),
                Section      = CoachSection.Headline,
                Polarity     = c.AccuracyGrade is "elite" or "good" || c.IsImproving
                                   ? ObservationPolarity.Strength
                                   : ObservationPolarity.Neutral,
                Severity     = 100,
                Message      = GetHeadline(r, c, memory)
            });

            // Prescription: typed so TASK-2.3 can match severity to the verdict.
            string nextDrill = GetNextDrill(r, c);
            candidates.Add(new CoachObservation
            {
                FactKey          = "rx_next_drill",
                SourceEngine     = nameof(AiCoachService),
                Section          = CoachSection.Prescription,
                Polarity         = ObservationPolarity.Neutral,
                Severity         = 10,
                PrescriptionType = ClassifyPrescription(r, c),
                Message          = nextDrill
            });

            var composed = CoachReportComposer.Compose(
                candidates,
                m => DrillMetricValid(r, m),
                new HeadlineContext(
                    QualityScore:  r.Accuracy,
                    BaselineDelta: null,
                    IsShortSession: false,
                    IsLowActivity: false));

            // TASK-2.1: open the verification loop only if the prescription
            // actually rendered (the composer is the arbiter, not the selector).
            if (technique != null
                && composed.SurvivingFactKeys.Contains(technique.Prescription.PrescriptionKey))
            {
                // Baseline = the verify metric's value right now. Smoothness is a
                // tracker metric — read it from the latest valid tracker session.
                double baseline = technique.Prescription.VerifyMetric == "SmoothnessScore"
                    ? memory.RecentTrackerSessions
                          .FirstOrDefault(s => s.IsMetricValid("SmoothnessScore"))?.SmoothnessScore ?? 0
                    : TechniquePrescriptionLibrary.ReadVerifyMetric(
                          technique.Prescription.VerifyMetric, r);

                TechniquePrescriptionSelector.RecordPrescribed(memory, technique, baseline);
                memory.ActivePrescription!.ScenarioContext = r.Scenario;
            }

            // TASK-2.2: rotation keys persisted once, for SURVIVORS only.
            var rotationKeys = composed.SurvivingFactKeys
                .Where(k => k != "headline_drill" && k != "rx_next_drill" && k != "session_note"
                         && k != "rx_technique")
                .ToList();
            if (rotationKeys.Count > 0)
            {
                memory.RecentTipKeys.InsertRange(0, rotationKeys);
                while (memory.RecentTipKeys.Count > 20)
                    memory.RecentTipKeys.RemoveAt(memory.RecentTipKeys.Count - 1);
            }

            // Tone floor extends to the rating label: an all-concern report may
            // not be stamped "Excellent"/"Great" no matter what the raw grades say.
            string rating = GetRating(r, c);
            if (composed.Strength == null && composed.Area != null
                && rating is "Excellent" or "Great")
            {
                rating = "Good";
            }

            return new AiCoachReport
            {
                OverallRating       = rating,
                Headline            = composed.Headline,
                Strengths           = composed.Strength != null ? new List<string> { composed.Strength } : new List<string>(),
                Weaknesses          = composed.Area != null ? new List<string> { composed.Area } : new List<string>(),
                Advice              = composed.Tips,
                NextDrillSuggestion = composed.Prescription ?? "",
                MotivationalClose   = GetMotivation(r, c),
            };
        }

        // Reaction-derived observations require a plausible reaction sample;
        // telemetry-derived ones require their field to have been captured.
        private static List<string> MetricsFor(string factKey) => factKey switch
        {
            "reaction_speed_assessment" or "reaction_trend" or "reaction_gap"
                => new List<string> { "AvgReactionMs" },
            "path_efficiency"  => new List<string> { "PathEfficiency" },
            "overshoot" or "undershoot" => new List<string> { "OvershootPct" },
            "direction_lag"    => new List<string> { "AvgDirectionChangeLagMs" },
            _ => new List<string>()
        };

        private static bool DrillMetricValid(AimTrainerResult r, string metric) => metric switch
        {
            // 0 = never measured; >= 2000ms = degenerate capture (same guard
            // CoachMemoryBuilder applies to baselines).
            "AvgReactionMs"           => r.AvgReactionMs > 0 && r.AvgReactionMs < 2000,
            // Validity floor: 5% efficiency means the mouse traveled 20× the
            // straight-line distance — a capture artifact (idle wander counted
            // into the path), not human movement. Observed live: 5% alongside
            // 98% accuracy. Below 15% the metric is degenerate, not "low".
            "PathEfficiency"          => r.PathEfficiency >= 0.15 && r.PathEfficiency <= 1.0,
            "OvershootPct"            => r.OvershootPct > 0,
            "AvgDirectionChangeLagMs" => r.AvgDirectionChangeLagMs > 0,
            _ => true
        };

        // TASK-2.3: classify GetNextDrill's outcome so the composer can refuse
        // a remedial prescription on a strength-led report.
        private static PrescriptionType ClassifyPrescription(AimTrainerResult r, CoachContext c)
        {
            if (c.AccuracyGrade == "elite" && r.Difficulty != "Hard" && r.Difficulty != "Nightmare")
                return PrescriptionType.Progression;             // "Move up to Hard"
            if (c.AccuracyGrade is "developing" or "average" && r.Difficulty == "Hard")
                return PrescriptionType.Remedial;                // "Drop to Medium"
            if (r.Scenario == "Flicking" && c.ReactionGrade == "slow")
                return PrescriptionType.Remedial;                // "Try Precision — removes time pressure"
            if (r.Scenario == "Tracking" && r.Accuracy < 55)
                return PrescriptionType.Remedial;                // "cut the duration to 30 seconds"
            return PrescriptionType.Maintenance;                 // repeat / reinforce
        }

        private static string GetRating(AimTrainerResult r, CoachContext c)
        {
            if (c.AccuracyGrade == "elite" && c.ReactionGrade is "elite" or "good") return "Excellent";
            if (c.AccuracyGrade is "elite" or "good") return "Great";
            if (c.AccuracyGrade == "average") return "Good";
            if (c.IsImproving) return "Developing";
            return "Needs Work";
        }

        private static string GetHeadline(AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            // ── WEAPON SCENARIO HEADLINE OVERRIDES ────────────────────────────
            // Fire before all generic rules so weapon-type context is always correct.
            if (r.Scenario == "Sniper")
            {
                // TASK-0.2: "That's the top tier" population claim removed — no population data exists.
                if (r.Accuracy >= 90)
                    return $"Elite precision — {r.Accuracy:F0}% on small distant targets.";
                if (r.Accuracy >= 75)
                    return $"Solid sniper session at {r.Accuracy:F0}%. The technique is there.";
                if (r.Accuracy < 55)
                    // TASK-0.2: ≤110-char budget — headlines must fit 2 full lines.
                    return "Tough session — sniper rewards patience. Let the crosshair settle before clicking.";
            }

            if (r.Scenario == "Shotgun")
            {
                if (r.AvgReactionMs > 0 && r.AvgReactionMs <= 200 && r.Accuracy >= 70)
                    return $"{r.AvgReactionMs:F0}ms average — elite shotgun speed. You're winning most fights before they start.";
                if (r.Accuracy >= 75)
                    return $"Strong shotgun session. {r.Accuracy:F0}% accuracy at close range is where it needs to be.";
                if (r.AvgReactionMs > 400)
                    return $"Speed is the limiting factor at {r.AvgReactionMs:F0}ms average — the accuracy is already there.";
            }

            if (r.Scenario == "SmgAr")
            {
                double cons = 0;
                memory.BaselineConsistency.TryGetValue(r.Scenario, out cons);
                if (cons >= 75 && r.Accuracy >= 78)
                    return $"Consistent and accurate — {r.Accuracy:F0}% with {cons:F0}/100 consistency. That combination is what wins SMG/AR fights.";
                if (r.Accuracy >= 75)
                    return "Good SMG/AR session. Keep the consistency above 65 and this accuracy holds in real games.";
                if (cons > 0 && cons < 50)
                    return $"Consistency is the gap at {cons:F0}/100 — keep the accuracy steady before pushing it higher.";
            }

            // ── RULE 1: ALL-TIME PERSONAL BEST (TASK-05) ─────────────────────
            if (memory.TotalDrillCount >= 3
                && r.Accuracy > 0
                && r.Accuracy > memory.PersonalBestAccuracy)
            {
                // RealDrillCount: "in N sessions" must not count calibration tests
                // — on the first real drill it read "best in 5 sessions".
                return memory.RealDrillCount >= 2
                    ? $"New personal best — {r.Accuracy:F0}% in {r.Scenario}. " +
                      $"That's the best you've hit in {memory.RealDrillCount} sessions."
                    : $"New personal best — {r.Accuracy:F0}% in {r.Scenario}. That's your number to beat now.";
            }

            // ── RULE 2: PLATEAU BREAK (TASK-05) ──────────────────────────────
            // Prior sessions were plateaued, but current session breaks through
            if (memory.IsAccuracyPlateaued
                && memory.PlateauLength >= 3
                && memory.PlateauAvgAccuracy > 0
                && r.Accuracy > memory.PlateauAvgAccuracy + 3.0)
            {
                return $"You broke through. After {memory.PlateauLength} sessions stuck around " +
                       $"{memory.PlateauAvgAccuracy:F0}%, you just hit {r.Accuracy:F0}%. " +
                       "Something clicked.";
            }

            // ── RULE 3: SIGNIFICANT IMPROVEMENT TREND (TASK-05) ──────────────
            if (memory.AccuracyTrend > 5.0 && memory.TotalDrillCount >= 6)
            {
                // TASK-0.2 budget; also dropped "Keep the same approach" — the
                // composer bans maintain-phrasing when a behavior change is asked.
                return $"Your {r.Scenario} accuracy is trending up — " +
                       $"{memory.AccuracyTrend:F1} points better than your last 5 sessions.";
            }

            // ── RULE 4: DECLINING TREND (TASK-05) ────────────────────────────
            // Never say "you're getting worse" — frame as diagnostic.
            // Gate: skip when the current session is Excellent or Great — the
            // player did well today and the headline must reflect that, not history.
            if (memory.AccuracyTrend < -5.0 && memory.TotalDrillCount >= 5
                && c.AccuracyGrade is not ("elite" or "good"))
            {
                int v = memory.TotalDrillCount % 3;
                return v switch
                {
                    0 => "Your last few sessions have been below your usual level. That's normal — here's what to check.",
                    1 => "You're running a little below your recent baseline. Worth checking your warmup and settings.",
                    _ => "The trend has dipped. That's information, not a verdict. Here's how to read it."
                };
            }

            // ── RULE 5: PLATEAU DETECTED (TASK-05) ───────────────────────────
            if (memory.IsAccuracyPlateaued
                && memory.PlateauLength >= 3
                && memory.PlateauAvgAccuracy > 0
                && r.Accuracy <= memory.PlateauAvgAccuracy + 3.0)
            {
                return $"You've held {memory.PlateauAvgAccuracy:F0}% for " +
                       $"{memory.PlateauLength} sessions — you're ready for the next challenge.";
            }

            // ── RULES 6-7: existing logic as fallback ─────────────────────────
            // TASK-0.3: prose matches the measurement — "reaction" only for
            // stimulus-anchored scenarios, "time per target" everywhere else.
            string pace = ReactionMetric.Noun(r.Scenario);
            if (c.NewAccuracyRecord && c.NewReactionRecord)
                return $"Personal best on accuracy and {pace} — {r.Accuracy:F0}%, {r.AvgReactionMs:F0}ms avg. Your best session yet.";
            if (c.NewAccuracyRecord)
                return $"New personal best accuracy — {r.Accuracy:F0}% in {r.Scenario}. That's a record for you.";
            if (c.NewReactionRecord)
                return $"New personal best {pace} — {r.AvgReactionMs:F0}ms average. Fastest you've been in {r.Scenario}.";
            // TASK-0.2: percentile suffix and "top tier" population claim disabled —
            // no population data exists. Voltaic-relative wording returns in TASK-3.3.
            if (c.AccuracyGrade == "elite")
                return $"{r.Accuracy:F0}% accuracy in {r.Scenario} — elite-grade session.";
            if (c.IsImproving && c.AccuracyDelta > 0)
                return $"Up {c.AccuracyDelta:+0.0;+0.0}% accuracy vs your last session — clear improvement.";
            if (c.AccuracyDelta < -5 && !c.IsFirstSession)
                return $"Accuracy dipped {Math.Abs(c.AccuracyDelta):F0}% from last session — happens to everyone. Here's how to bounce back.";
            if (c.IsFirstSession)
            {
                if (c.TotalSessionsAll > 3)
                    return $"First {r.Scenario} session — {r.Accuracy:F0}% accuracy. Your average across {c.TotalSessionsAll} sessions is {c.OverallAvgAccuracy:F0}%.";
                return $"First {r.Scenario} session logged — {r.Accuracy:F0}% accuracy gives you a solid baseline to build from.";
            }
            return $"{r.Accuracy:F0}% accuracy, {r.AvgReactionMs:F0}ms avg {pace}. {(c.AccuracyGrade == "good" ? "Solid session." : "Keep building.")}";
        }

        // TASK-2.2: emits ONE keyed strength candidate (weapon rules take priority,
        // then rotation-filtered general candidates). FactKeys are aspect-level.
        private static List<(string key, string text)> GetStrengthCandidates(AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            bool hasHistory = memory.TotalDrillCount >= 3;

            // ── WEAPON SCENARIO STRENGTHS — take priority when they fire ──────
            var weaponStrengths = new List<(string key, string text)>();

            // Rule S-1 (Sniper): clean first shots — aspect: correction commitment
            if (r.Scenario == "Sniper"
                && r.Accuracy >= 85
                && c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value <= 20)
            {
                weaponStrengths.Add(("correction_commitment",
                    $"Clean first shots at {r.Accuracy:F0}% — you're committing to your aim before pulling the trigger. " +
                    "That's the hardest sniper habit to build."));
            }

            // Rule SG-1 (Shotgun): fast + accurate — aspect: reaction speed verdict
            if (r.Scenario == "Shotgun"
                && r.AvgReactionMs > 0
                && r.AvgReactionMs <= 220
                && r.Accuracy >= 70)
            {
                weaponStrengths.Add(("reaction_speed_assessment",
                    $"{r.AvgReactionMs:F0}ms average with {r.Accuracy:F0}% accuracy — that combination wins shotgun fights. " +
                    "Fast and accurate is the hardest thing to train."));
            }

            // Rule AR-1 (SmgAr): sustained tracking — aspect: consistency verdict
            if (r.Scenario == "SmgAr")
            {
                memory.BaselineConsistency.TryGetValue(r.Scenario, out double cons);
                if (cons >= 70 && r.Accuracy >= 75)
                {
                    weaponStrengths.Add(("consistency_assessment",
                        $"Sustained {r.Accuracy:F0}% accuracy with {cons:F0}/100 consistency — you're staying on target through movement. " +
                        "That's the core SMG/AR skill."));
                }
            }

            // ── TASK-02: key-based rotation for general strengths ─────────────
            var strengthCandidates = new List<(string key, string text)>();
            var strengthRecentKeys = memory.RecentTipKeys.Take(12).ToList();

            // Accuracy strength
            if (c.AccuracyGrade == "elite")
            {
                if (hasHistory && memory.AccuracyTrend > 0 && r.Accuracy >= 40.0)
                {
                    strengthCandidates.Add(("accuracy_trend", PickGlobal(memory, 0,
                        $"Your accuracy has been consistently above {Bench.AccuracyGood(r.Scenario):F0}% for your last several sessions. That's becoming a reliable strength.",
                        $"Elite accuracy at {r.Accuracy:F0}% — and the trend is pointing up. That's not luck, that's the work compounding.",
                        $"{r.Accuracy:F0}% accuracy and still improving. You're in rare territory."
                    )));
                }
                else
                {
                    // TASK-0.2: "most competitive players" / "top tier" population-claim variants removed.
                    strengthCandidates.Add(("accuracy_assessment", Pick(c.SessionCount,
                        $"Elite accuracy at {r.Accuracy:F0}%. The consistency is the impressive part — anyone can have a good session, fewer can repeat it.",
                        $"Your {r.Accuracy:F0}% accuracy is the kind of number that shows up in ranked lobbies. Keep building on it."
                    )));
                }
            }
            else if (c.AccuracyGrade == "good")
            {
                if (hasHistory && memory.BaselineAccuracy.TryGetValue(r.Scenario, out double baseline) && r.Accuracy >= baseline && r.Accuracy >= 40.0)
                {
                    strengthCandidates.Add(("accuracy_assessment", PickGlobal(memory, 0,
                        $"Your {r.Scenario} accuracy has been consistently around {baseline:F0}% or better. That floor is real — build on it.",
                        $"{r.Accuracy:F0}% accuracy in {r.Scenario}, and your baseline is holding steady. Fundamentals are solid.",
                        $"Solid accuracy at {r.Accuracy:F0}% — above your recent average of {baseline:F0}%. You're clicking with intent."
                    )));
                }
                else
                {
                    strengthCandidates.Add(("accuracy_assessment", Pick(c.SessionCount,
                        $"{r.Accuracy:F0}% accuracy puts you in the good range for {r.Scenario}. You're making more right decisions than wrong ones.",
                        $"Solid {r.Accuracy:F0}% accuracy in {r.Scenario}. The fundamentals are there — now build consistency.",
                        $"{r.Accuracy:F0}% is a good number for {r.Scenario}. You're above average and trending in the right direction.",
                        $"Good accuracy at {r.Accuracy:F0}%. You're clicking with intent — that's the foundation everything else builds on."
                    )));
                }
            }
            else if (c.IsImproving && c.AccuracyDelta > 3 && r.Accuracy >= 40.0)
            {
                strengthCandidates.Add(("accuracy_trend", Pick(c.SessionCount,
                    $"Accuracy improved {c.AccuracyDelta:+0.0}% from your last session — that's real, measurable progress.",
                    $"Up {c.AccuracyDelta:F0}% accuracy since last session. The trajectory is pointing up.",
                    $"Accuracy trend: +{c.AccuracyDelta:F0}% from last time. Every improvement stacks.",
                    $"Your accuracy is climbing — {c.AccuracyDelta:F0}% better than last session. That's the definition of improving."
                )));
            }

            // Reaction trend — fires independently
            if (memory.TotalDrillCount >= 3
                && memory.ReactionTrend < -10
                && r.AvgReactionMs > 0
                && r.Accuracy >= 40.0)
            {
                strengthCandidates.Add(("reaction_trend",
                    memory.ReactionTrend < -20
                        ? $"Your average {ReactionMetric.Noun(r.Scenario)} has dropped {Math.Abs(memory.ReactionTrend):F0}ms over your recent sessions. " +
                          "That kind of improvement at this accuracy level almost never happens — it means your technique " +
                          "is genuinely getting better, not just your familiarity."
                        : $"{r.AvgReactionMs:F0}ms average, and trending lower. Speed and control improving together is rare."
                ));
            }

            // Reaction grade
            // TASK-0.2: percentile suffixes disabled — no population data exists.
            // Voltaic-relative wording returns through the composer in TASK-3.3.
            // TASK-0.3: "reaction" only for stimulus-anchored scenarios.
            string paceNoun = ReactionMetric.Noun(r.Scenario);
            if (c.ReactionGrade == "elite")
            {
                strengthCandidates.Add(("reaction_speed_assessment", Pick(c.SessionCount,
                    $"Your {r.AvgReactionMs:F0}ms average {paceNoun} is elite level.",
                    $"{r.AvgReactionMs:F0}ms average {paceNoun} is genuinely fast. You're in the range where raw speed becomes an advantage.",
                    $"Elite {paceNoun} at {r.AvgReactionMs:F0}ms average. Your reads are translating to clicks — that's the hard part.",
                    $"{r.AvgReactionMs:F0}ms average. At this speed, target acquisition is a real strength, not just a stat."
                )));
            }
            else if (c.ReactionGrade == "good")
            {
                strengthCandidates.Add(("reaction_speed_assessment", Pick(c.SessionCount,
                    $"{r.AvgReactionMs:F0}ms average {paceNoun} is competitive.",
                    $"Good {paceNoun} at {r.AvgReactionMs:F0}ms average. Speed isn't the bottleneck — build on this.",
                    $"{r.AvgReactionMs:F0}ms average {paceNoun} is a solid range. Consistent performance at this speed is what separates good from great.",
                    $"Average {paceNoun}: {r.AvgReactionMs:F0}ms — that's genuinely competitive. Use it."
                )));
            }
            else if (!c.IsFirstSession && c.ReactionDelta < -20)
            {
                strengthCandidates.Add(("reaction_trend",
                    c.ReactionGrade == "slow"
                        ? $"Your {paceNoun} improved {Math.Abs(c.ReactionDelta):F0}ms from last session — real progress. Your average is still {r.AvgReactionMs:F0}ms, so there's more room to go, but the direction is right."
                        : $"Your {paceNoun} improved {Math.Abs(c.ReactionDelta):F0}ms from last session — your reads are getting sharper."
                ));
            }

            // Streak and consistency
            if (c.StreakGrade == "elite")
                strengthCandidates.Add(("streak_pattern", $"A streak of {r.MaxStreak} is exceptional — it shows you can maintain focus and rhythm under pressure."));
            else if (c.StreakGrade == "good")
                strengthCandidates.Add(("streak_pattern", $"Your {r.MaxStreak}-hit streak shows you have the ability to lock in when it counts."));
            else if (c.IsConsistent && r.Accuracy >= 40.0)
                strengthCandidates.Add(("streak_pattern", "Your accuracy has been consistent across your last few sessions — that reliability is harder to build than people think."));

            // Telemetry improvement acknowledgement
            if (r.Accuracy >= 40.0)
            {
                var prevSameScenario = memory.AllDrills
                    .Where(h => h.Scenario == r.Scenario && h.Timestamp != r.Timestamp)
                    .OrderByDescending(h => h.Timestamp)
                    .FirstOrDefault();

                if (prevSameScenario != null)
                {
                    double effDelta = r.PathEfficiency - prevSameScenario.PathEfficiency;
                    double ovDelta  = prevSameScenario.OvershootPct - r.OvershootPct;
                    double lagDelta = prevSameScenario.AvgDirectionChangeLagMs - r.AvgDirectionChangeLagMs;

                    if (r.PathEfficiency >= 0.15 && effDelta > 0.05   // same degenerate floor as DrillMetricValid
                        && r.Scenario is "StaticClicking" or "DynamicClicking" or "Precision" or "Sniper")
                        strengthCandidates.Add(("improvement_ack", Pick(c.SessionCount,
                            $"Path efficiency improved {effDelta * 100:F0} points this session — your routes to targets are getting cleaner. That's the movement mechanics improving.",
                            $"Your movement is getting more direct — {effDelta * 100:F0}% better path efficiency vs last {r.Scenario} session. The technique is solidifying.",
                            $"Path efficiency up {effDelta * 100:F0} points. That kind of improvement doesn't happen by accident — your mouse movement is becoming more intentional.",
                            $"Movement quality trending up: {effDelta * 100:F0}% better path efficiency. What you're doing is working."
                        )));
                    else if (r.OvershootPct > 0 && ovDelta > 8)
                        strengthCandidates.Add(("improvement_ack", Pick(c.SessionCount,
                            $"Overshoot rate dropped {ovDelta:F0}% since your last {r.Scenario} session — your stopping mechanics are improving.",
                            $"{ovDelta:F0}% fewer overshoots than last time. The deceleration work is paying off.",
                            $"Overshoot is down {ovDelta:F0}% — your commits are getting more accurate. Keep the same approach.",
                            $"Less overshoot this session — {ovDelta:F0}% improvement. That tells me your first movement is getting more precise."
                        )));
                    else if (r.AvgDirectionChangeLagMs > 0 && lagDelta > 15)
                        strengthCandidates.Add(("improvement_ack", Pick(c.SessionCount,
                            $"Direction change response improved {lagDelta:F0}ms since last session — you're reading the patterns better.",
                            $"{lagDelta:F0}ms faster on direction changes. Your anticipation is kicking in.",
                            $"Direction lag dropped {lagDelta:F0}ms. You're starting to lead the movement instead of chasing it.",
                            $"{lagDelta:F0}ms improvement on direction changes. That's anticipation developing — keep it going."
                        )));
                }
            }

            // TASK-2.4: transfer single authority — the message comes from
            // TransferObservationSource only. The previous three local variants
            // ("smoothness is trending up", "improving alongside drill accuracy",
            // "quality is climbing") are deleted.
            var transferStrength = TransferObservationSource.Compute(memory);
            if (transferStrength != null && transferStrength.Section == CoachSection.Strength)
                strengthCandidates.Add((TransferObservationSource.FactKey, transferStrength.Message));

            // ── TASK-01: priority sort, cap at 1 ─────────────────────────────
            static int StrengthPriority(string key) => key switch
            {
                "improvement_ack"           => 1,  // improvement delta — most valuable signal
                "reaction_trend"            => 2,  // reaction improving over sessions
                "reaction_speed_assessment" => 2,  // reaction grade verdict
                "accuracy_trend"            => 3,  // accuracy trending up
                "accuracy_assessment"       => 3,  // accuracy grade verdict
                "streak_pattern"            => 4,  // streak / consistency pattern
                "transfer"                  => 5,  // cross-coach transfer (TASK-2.4 single authority)
                _                           => 6
            };

            // Weapon-scenario strengths take priority over general ones.
            if (weaponStrengths.Count > 0)
                return weaponStrengths.Take(1).ToList();

            // Filter by last 3 sessions' keys (~12 entries), sort by priority, take 1
            var filteredStrengths = strengthCandidates
                .Where(t => !strengthRecentKeys.Contains(t.key))
                .OrderBy(t => StrengthPriority(t.key))
                .Take(1)
                .ToList();

            // If filtering removed everything, fall back to highest-priority unfiltered candidate
            if (filteredStrengths.Count < 1)
            {
                var fallback = strengthCandidates
                    .OrderBy(t => StrengthPriority(t.key))
                    .FirstOrDefault();
                if (fallback != default)
                    filteredStrengths.Add(fallback);
            }

            if (filteredStrengths.Count == 0)
            {
                // Below 40% accuracy with no session-specific strengths gets a
                // neutral honest observation rather than a hollow positivity fallback.
                filteredStrengths.Add(("session_note", r.Accuracy < 40.0
                    ? "This session was below your usual level. The data below shows where to focus next."
                    : r.Hits > r.Misses
                        ? $"You hit more than you missed — {r.Hits} hits vs {r.Misses} misses. That's the foundation to build on."
                        : $"You completed a full {r.DurationSeconds}-second drill. Every session builds muscle memory, even the tough ones."));
            }

            // TASK-2.2: persistence moved to GenerateReport — only keys that
            // SURVIVE composition are recorded for rotation.
            return filteredStrengths;
        }

        // TASK-2.2: emits keyed area candidates with aspect-level FactKeys.
        // Selection priority stays here; arbitration and persistence happen in
        // GenerateReport via CoachReportComposer.
        private static List<(string key, string text)> GetWeaknessCandidates(AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            var list = new List<(string key, string text)>();

            // ── WEAPON SCENARIO WEAKNESSES ────────────────────────────────────

            // Rule S-2 (Sniper Standard): high correction sharpness = not settling before clicking
            if (r.Scenario == "Sniper"
                && r.SubVariant != "Moving"   // Moving has its own variant rule below
                && c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value > 30)
            {
                list.Add(("correction_commitment",
                         "Your correction sharpness is high for a sniper scenario — you're moving and then adjusting " +
                         "rather than settling first. Try this: before each click, stop your mouse completely for " +
                         "half a second. Clean stop, then shoot."));
            }

            // Rule SN-V1 (Sniper Moving): matching target speed before clicking
            if (r.Scenario == "Sniper"
                && r.SubVariant == "Moving"
                && c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value > 25)
            {
                list.Add(("correction_commitment",
                         "Moving targets require you to match the target's speed before clicking, not chase it. " +
                         "Move with it, settle, then commit. Clicking while still catching up is what's causing your corrections."));
            }

            // Rule SN-V2 (Sniper Wind): reading the drift angle
            if (r.Scenario == "Sniper"
                && r.SubVariant == "Wind"
                && r.Accuracy < 70)
            {
                list.Add(("weapon_technique",
                         "Wind drift has a constant direction — once you learn to read the drift in the first few seconds " +
                         "you can lead your aim ahead of it rather than reacting to where it is. " +
                         "Watch the first target for 2 seconds before clicking to get the drift angle."));
            }

            // Rule SG-2 (Shotgun): high correction = overshooting
            if (r.Scenario == "Shotgun"
                && c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value > 20)
            {
                list.Add(("correction_commitment",
                         "Your correction sharpness is too high for shotgun scenarios — you're overshooting and adjusting. " +
                         "In a real shotgun fight that second motion is too slow. Aim for your first movement to land on target. " +
                         "If you miss, that's fine — commit to the next shot instead of correcting."));
            }

            // Rule SG-V1 (Shotgun Duels): decision-making
            if (r.Scenario == "Shotgun"
                && r.SubVariant == "Duels"
                && r.Accuracy < 65)
            {
                list.Add(("decision_priority",
                         "In Duels, the decision matters more than the execution. Pick the closer target every time — " +
                         "don't evaluate both options. Closer target, instant commit. " +
                         "That one rule wins most shotgun duels."));
            }

            // Rule SG-V2 (Shotgun Peek): pre-aiming
            if (r.Scenario == "Shotgun"
                && r.SubVariant == "Peek"
                && r.AvgReactionMs > 300)
            {
                list.Add(("preaim_habit",
                         "Peek scenarios reward pre-aiming more than reaction speed. " +
                         "Position your crosshair at the edge before the target appears rather than moving to it after. " +
                         "That alone cuts your effective reaction time in half."));
            }

            // Rule SG-3 (Shotgun): slow reaction — aspect: reaction speed verdict
            if (r.Scenario == "Shotgun" && r.AvgReactionMs > 350)
            {
                list.Add(("reaction_speed_assessment",
                         $"Your average reaction is {r.AvgReactionMs:F0}ms — for shotgun scenarios you want to be under 280ms. " +
                         $"Your best was {r.BestReactionMs:F0}ms which shows the speed is there. " +
                         "The gap is hesitation before committing. Trust your aim and pull the trigger."));
            }

            // Rule AR-2 (SmgAr): low consistency across sessions
            if (r.Scenario == "SmgAr")
            {
                memory.BaselineConsistency.TryGetValue(r.Scenario, out double cons);
                if (cons > 0 && cons < 55)
                {
                    list.Add(("consistency_assessment",
                             $"Your consistency score is {cons:F0}/100 — your accuracy is dropping off over the session. " +
                             "SMG/AR scenarios reward players who maintain accuracy under pressure, not just in bursts. " +
                             "Try shorter sessions at the same difficulty until consistency stays above 65 throughout."));
                }
            }

            // Rule AR-V1 (SmgAr Spray): priority system
            if (r.Scenario == "SmgAr"
                && r.SubVariant == "Spray")
            {
                memory.BaselineConsistency.TryGetValue(r.Scenario, out double cons);
                if (cons > 0 && cons < 55)
                {
                    list.Add(("decision_priority",
                             "Three targets is cognitive overload until you build a priority system. " +
                             "Always click the closest target first. Always. Don't evaluate — just closest target, click, next closest. " +
                             "That system becomes automatic after 5–6 sessions."));
                }
            }

            // Rule AR-V2 (SmgAr Strafe): direction-change moment
            if (r.Scenario == "SmgAr"
                && r.SubVariant == "Strafe")
            {
                memory.BaselineConsistency.TryGetValue(r.Scenario, out double cons);
                if (cons > 0 && cons < 60)
                {
                    list.Add(("direction_change",
                             "Strafe scenarios are hardest at the direction change. " +
                             "If your accuracy is inconsistent it's almost always the reversal moment causing it. " +
                             "Focus on the instant the targets change direction — that's where the session is won or lost."));
                }
            }

            // Return early for weapon scenarios once we have observations
            if ((r.Scenario is "Sniper" or "Shotgun" or "SmgAr") && list.Count > 0)
                return list.Take(2).ToList();

            // ── TASK-02: key-based rotation setup (read-only — persistence is
            // post-composition in GenerateReport) ──────────────────────────────
            var weaknessRecentKeys = memory.RecentTipKeys.Take(12).ToList();

            // ── RULE 1: PRESCRIPTION FOLLOW-UP (always shows, never suppressed) ──
            if (memory.LastPrescriptionFollowed
                && !string.IsNullOrEmpty(memory.LastPrescribedScenario)
                && r.Scenario == memory.LastPrescribedScenario
                && memory.TotalDrillCount >= 3)
            {
                bool improved = false;
                string metricNote = "";
                if (memory.BaselineAccuracy.TryGetValue(r.Scenario, out double baseAcc))
                {
                    double delta = r.Accuracy - baseAcc;
                    if (delta > 1.5)
                    {
                        improved = true;
                        metricNote = $"accuracy went from {baseAcc:F0}% to {r.Accuracy:F0}%";
                    }
                    else if (delta < -1.5)
                    {
                        metricNote = $"accuracy is still at {r.Accuracy:F0}% vs your {baseAcc:F0}% average";
                    }
                }

                int v = memory.TotalDrillCount % 3;
                if (improved)
                {
                    list.Add(("prescription_followup", v switch
                    {
                        0 => $"Last time I told you to work on {memory.LastPrescribedScenario}. You did — and it shows. Your {metricNote}. Next step: push one difficulty higher.",
                        1 => $"You followed the {memory.LastPrescribedScenario} prescription and your {metricNote}. That improvement is yours to keep. Now: consolidate it at the same difficulty before moving up.",
                        _ => $"The {memory.LastPrescribedScenario} work paid off — {metricNote}. The habit is forming. Keep that scenario in your rotation."
                    }));
                    return list;
                }
                else if (!string.IsNullOrEmpty(metricNote))
                {
                    list.Add(("prescription_followup", v switch
                    {
                        0 => $"You ran the {memory.LastPrescribedScenario} drill I suggested. The {metricNote} — that's okay, it takes repetition. Here's a more specific focus for next time: commit to center before clicking, don't rush the first motion.",
                        1 => $"You followed the {memory.LastPrescribedScenario} prescription and {metricNote}. One session isn't enough for this to show up — keep going. The mechanics take 5-8 reps to settle.",
                        _ => $"Good that you ran {memory.LastPrescribedScenario}. The {metricNote} yet. Stick with it — this kind of improvement shows up on session 4-6, not session 2."
                    }));
                }
            }

            // ── RULE 2: PLATEAU INTERVENTION (suppressed if recently shown) ──
            if (list.Count == 0
                && memory.IsAccuracyPlateaued
                && memory.PlateauLength >= 3
                && !weaknessRecentKeys.Contains("plateau"))
            {
                int v = memory.TotalDrillCount % 3;
                bool reactionIsWeaker = c.ReactionGrade is "slow" or "average";
                string diagnosis = reactionIsWeaker
                    ? $"reaction time is at {r.AvgReactionMs:F0}ms — that's the next lever to pull"
                    : "sensitivity might be slightly off for this scenario";

                list.Add(("plateau", v switch
                {
                    0 => $"You've plateaued at {memory.PlateauAvgAccuracy:F0}% for {memory.PlateauLength} sessions. " +
                         $"The usual causes are sensitivity being slightly off, or sessions getting too long and losing focus. " +
                         $"Here's how to tell: run one 30-second drill at max focus. If your score jumps, it's focus. If it doesn't, check your sensitivity.",
                    1 => $"Your {r.Scenario} accuracy hasn't moved in {memory.PlateauLength} sessions. That means the current approach has a ceiling. " +
                         $"Two things to try: step up the difficulty for one session to expose gaps, or cut session length to 30 seconds to force concentration.",
                    _ => $"Stuck around {memory.PlateauAvgAccuracy:F0}% in {r.Scenario}. The plateau is real — your {diagnosis}. " +
                         $"Change one variable: difficulty, duration, or sensitivity. See which one moves the needle."
                }));
            }

            // ── RULE 3: CROSS-COACH INSIGHT (suppressed if recently shown) ───
            if (list.Count < 2
                && memory.RecentTrackerSessions.Count >= 2
                && !weaknessRecentKeys.Contains("fatigue_pattern"))
            {
                int v = memory.TotalDrillCount % 3;
                bool drillsImproving = memory.AccuracyTrend > 2;
                bool ingameDeclining = memory.ConsistencyTrend < -5;
                bool bothDeclining   = memory.AccuracyTrend < -3 && ingameDeclining;

                if (bothDeclining)
                {
                    // Condition-neutral instructions — the coach never diagnoses
                    // body state ("fatigue"/"overtraining" claims removed).
                    list.Add(("fatigue_pattern", v switch
                    {
                        0 => "Your consistency is dropping in drills and in-game. Cut session length for a week — 20 minutes max, then stop.",
                        1 => "Both your drill performance and in-game movement are trending down. Take one full rest day — it does more than one extra session.",
                        _ => "Drills and in-game quality both dipping. Before diagnosing mechanics, check the basics: sleep, session length, warmup. Fix those first."
                    }));
                }
                else if (drillsImproving && ingameDeclining)
                {
                    // TASK-2.4: transfer single authority — the previous three
                    // local variants are deleted; the one sanctioned negative
                    // transfer message comes from TransferObservationSource.
                    var transferTip = TransferObservationSource.Compute(memory);
                    if (transferTip != null && transferTip.Section == CoachSection.Tip)
                        list.Add((TransferObservationSource.FactKey, transferTip.Message));
                }
            }

            // ── Multi-signal combinations — data-specific, return early if matched ──

            // Combination A: High correction sharpness + slow reaction
            if (c.TrackerCorrectionSharpness.HasValue
                && c.TrackerCorrectionSharpness.Value > 60
                && c.ReactionGrade == "slow"
                && (r.Scenario == "Flicking" || r.Scenario == "Switching"))
            {
                // TASK-0.3: Flicking/Switching measure time per target, not reaction.
                list.Add(("reaction_speed_assessment", Pick(c.SessionCount,
                    $"Your time per target looks slow but your movement data tells a different story — you are overshooting " +
                    "and yanking back to correct. That correction cycle adds a significant delay artificially. " +
                    "The fix is smoother first motion, not faster reflexes.",

                    $"The gap between your best and average time per target is wider than expected. " +
                    "Your movement data points to overshoot-correction cycles — you can move fast, " +
                    "you just need to trust that first motion more instead of second-guessing it.",

                    "High correction activity with a slow average time per target is a specific pattern — " +
                    "your instinct is right but your follow-through overshoots. " +
                    $"Your best of {r.BestReactionMs:F0}ms proves the speed is there. Train the first motion, not the speed."
                )));
                return list;
            }

            // Combination B: Low smoothness + low cm/360 in Tracking
            if (c.TrackerSmoothness.HasValue
                && c.TrackerSmoothness.Value < 55
                && c.TrackerCmPer360.HasValue
                && c.TrackerCmPer360.Value < 22
                && r.Scenario == "Tracking")
            {
                // DISABLED per TASK-3.1 — RecommendationEngine is the sole
                // sensitivity authority; the drill coach formed its own opinion
                // here ("lower your sensitivity") which contradicted the engine
                // across surfaces. Coach prose may only echo the engine's
                // sensitivity_fit observation or stay silent — this stays silent.
                // list.Add(("sensitivity_fit", Pick(c.SessionCount,
                //     "Your mouse movement was inconsistent this session — and your sensitivity may be making it worse. ...",
                //     "Your movement data and your sensitivity are working against each other. ...",
                //     "Your movement inconsistency this session looks like a settings problem, not a skill problem. ..."
                // )));
                // return list;
            }

            // Combination C: Consistent but low accuracy in Precision
            if (c.IsConsistent
                && (c.AccuracyGrade == "average" || c.AccuracyGrade == "developing")
                && r.Scenario == "Precision")
            {
                list.Add(("click_placement", Pick(c.SessionCount,
                    $"Your accuracy is {r.Accuracy:F0}% but your consistency across sessions is actually good — " +
                    "you are consistently missing, which means the mechanics are repeatable. " +
                    "This usually points to crosshair placement — you are probably stopping just short of the target center. " +
                    "Aim for the center pixel, not the edge.",

                    $"Interesting pattern: consistent performance with below-average accuracy in Precision. " +
                    "Consistent means the movement is repeatable. Below average means it is landing in the wrong spot consistently. " +
                    "The most common cause is stopping the cursor at the edge of targets instead of center.",

                    $"Your consistency score is good but accuracy is {r.Accuracy:F0}%. " +
                    "In Precision that combination usually means your stopping point is slightly off, not your speed. " +
                    "Focus on committing to center — overshoot slightly until the centering becomes natural."
                )));
                return list;
            }

            switch (c.WeakArea)
            {
                case "accuracy":
                    // DISABLED per TASK-3.1 — RecommendationEngine is the sole
                    // sensitivity authority. Four independent "your sensitivity is
                    // too high/low" verdicts lived here; the coach now stays silent
                    // on sensitivity and coaches the accuracy itself instead.
                    // (Previously: Precision cm/360 < 20 → "too high", > 55 → "too low";
                    //  Tracking cm/360 < 15 → "too high", > 65 → "too low".)
                    list.Add(("accuracy_assessment", r.Accuracy < 50
                        ? $"At {r.Accuracy:F0}% accuracy you are missing more than half your shots — slow down and prioritize clicking when your cursor is actually on the target."
                        : $"{r.Accuracy:F0}% accuracy has room to grow. You are rushing some clicks — wait for the moment of confidence before clicking."));
                    break;

                case "reaction":
                    // Rule S-3: suppress ALL reaction coaching for Sniper
                    if (r.Scenario == "Sniper") break;

                    // TASK-2.1: aspect "reaction_speed_assessment" — ONE verdict.
                    // If the scenario-benchmark grade is elite/good, the strength
                    // path owns this aspect ("genuinely fast"); the slow-reaction
                    // concern may not also render. WeakArea can select "reaction"
                    // off a different comparison (vs personal best), which is how
                    // 240ms got called "on the slower side" and "genuinely fast"
                    // on the same card. The reaction-GAP aspect (best vs average)
                    // is distinct and remains allowed below for slow grades.
                    if (c.ReactionGrade is "elite" or "good") break;

                    // TASK-0.3: the prose noun follows the measurement.
                    string wPace = ReactionMetric.Noun(r.Scenario);

                    if (c.TrackerCorrectionSharpness.HasValue
                        && c.TrackerCorrectionSharpness.Value > 60
                        && c.ReactionGrade == "slow")
                    {
                        list.Add(("reaction_speed_assessment",
                                 $"Your {wPace} looks slow at {r.AvgReactionMs:F0}ms but your movement data " +
                                 "suggests you are overshooting and correcting — " +
                                 "that correction cycle adds a significant delay artificially. The fix is smoother initial movement, not more speed. " +
                                 "Focus on landing on the target in one motion instead of correcting after overshoot."));
                    }
                    else
                    {
                        string reactionKey = "reaction_speed_assessment";
                        string reactionMsg;
                        bool hasGapData = r.BestReactionMs > 0 && r.AvgReactionMs > 0 && r.Hits >= 5;
                        double reactionGap = hasGapData ? r.AvgReactionMs - r.BestReactionMs : -1;

                        if (hasGapData && reactionGap > 150)
                        {
                            // Distinct aspect: the spread between best and average,
                            // not the speed verdict — may coexist with reaction_trend.
                            reactionKey = "reaction_gap";
                            reactionMsg = Pick(c.SessionCount,
                                $"Your best {wPace} was {r.BestReactionMs:F0}ms but your average was {r.AvgReactionMs:F0}ms — a {reactionGap:F0}ms gap. That gap is the real problem, not your ceiling. Your best shots happen when you pre-aim the spawn zone before the target appears. Your slow shots happen when you react after. Spend the first few seconds of each session mapping where targets spawn and park your crosshair there.",
                                $"{reactionGap:F0}ms between your best ({r.BestReactionMs:F0}ms) and average ({r.AvgReactionMs:F0}ms) {wPace}. You have the speed — you're just not using it on every click. The fix is crosshair placement before the target appears, not more speed after.",
                                $"Your best {wPace} is {r.BestReactionMs:F0}ms. Your average is {r.AvgReactionMs:F0}ms. That {reactionGap:F0}ms gap means you're pre-aiming correctly on some clicks and starting from scratch on others. Make pre-aiming the default, not the exception.",
                                $"Best {wPace} {r.BestReactionMs:F0}ms, average {r.AvgReactionMs:F0}ms. The {reactionGap:F0}ms difference is your consistency gap. Close it by defaulting to anticipation — position your crosshair before the target spawns rather than after you see it."
                            );
                        }
                        else if (hasGapData && reactionGap <= 150 && r.AvgReactionMs > Bench.ReactionGood(r.Scenario))
                        {
                            reactionMsg = Pick(c.SessionCount,
                                $"Your {wPace} is consistently around {r.AvgReactionMs:F0}ms — your best and average are only {reactionGap:F0}ms apart. This is a true ceiling, not inconsistency. Reactive Blink sessions specifically target this — the teleporting target removes tracking and isolates pure reaction speed.",
                                $"Consistent {r.AvgReactionMs:F0}ms {wPace} with only {reactionGap:F0}ms variance. You're not inconsistent — you're at your current ceiling. Short dedicated Reactive Blink sessions (15 seconds, full focus) compress this number faster than longer sessions."
                            );
                        }
                        else
                        {
                            // TASK-0.2: "(bottom X%)" percentile suffix disabled — no population data exists.
                            reactionMsg = $"Your {r.AvgReactionMs:F0}ms average {wPace} is on the slower side. Focus less on speed and more on predicting target movement — anticipation is faster than reaction.";
                        }

                        if ((r.Scenario == "Flicking" || r.Scenario == "DynamicClicking") && r.AvgReactionMs > 500)
                            reactionMsg += " For Flicking specifically: a slow pace usually means waiting for the target to fully appear before starting to move. Start your mouse movement the instant you perceive the target — don't wait for it to register consciously.";

                        list.Add((reactionKey, reactionMsg));
                    }
                    break;

                case "streak":
                    list.Add(("streak_pattern", $"Your best streak was {r.MaxStreak} — breaking streaks early usually means rushing after a miss or losing focus mid-drill. Take a breath after each miss and reset."));
                    break;
                case "endurance":
                    list.Add(("fatigue_pattern", !c.IsFirstSession && c.AccuracyDelta < -8
                        ? $"Accuracy dropped {Math.Abs(c.AccuracyDelta):F0}% from last session. Note the conditions — time of day, warmup, and session length matter more than people realize."
                        : "Your later targets seem harder than early ones — this is a focus endurance issue, not a skill issue. Short drills help train consistent focus."));
                    break;
            }

            // Smoothness diagnosis — hardware root-cause, higher priority than generic scenario tips.
            // DISABLED pending TASK-1.1 — do not re-enable without validity gate
            // Built on TrackerSmoothness (unvalidated metric) with grip/surface causal claims.
            // if (c.TrackerSmoothness.HasValue
            //     && c.TrackerSmoothness.Value < 60
            //     && r.Scenario == "Tracking"
            //     && r.Accuracy < 65)
            // {
            //     list.Add("Your movement was noticeably choppy this session — that level of jitter makes consistent tracking physically harder. " +
            //              "Check your grip, surface friction, and make sure your mousepad is clean and flat.");
            // }

            // Scenario-specific tips
            if (list.Count < 2)
            {
                if (r.Scenario == "Tracking" && r.Accuracy < 65)
                    list.Add(("scenario_habit", "In Tracking, many players chase the center of the target — aim slightly ahead of where it's moving instead."));
                else if (r.Scenario == "Flicking" && r.AvgReactionMs > 450)
                    list.Add(("scenario_habit", "For Flicking, your eyes should land on the target before your mouse moves. The eyes lead, the hand follows."));
                else if (r.Scenario == "Precision" && r.Accuracy < 75)
                    list.Add(("scenario_habit", "Precision requires slowing down intentionally — if you feel rushed, you'll overshoot small targets every time."));
                else if (r.Scenario == "Switching" && r.MaxStreak < 4)
                    list.Add(("scenario_habit", "In Switching, scan for the next target while clicking the current one — don't wait until after you've clicked to look for what's next."));
            }

            if (list.Count == 0)
            {
                // TASK-0.3: the raw WeakArea key printed "reaction" for scenarios
                // that measure time per target — translate to honest display names.
                string weakLabel = c.WeakArea switch
                {
                    "reaction"  => ReactionMetric.IsTrueReaction(r.Scenario) ? "reaction" : "pace",
                    "accuracy"  => "accuracy",
                    "streak"    => "streak consistency",
                    "endurance" => "late-session focus",
                    _           => c.WeakArea
                };
                list.Add(("weak_area_generic", $"Your {weakLabel} is the area with the most room to grow — even small improvements there will lift your overall score significantly."));
            }

            // TASK-2.2: persistence moved to GenerateReport — only keys that
            // SURVIVE composition are recorded for rotation. (The old "routing
            // guard" RemoveAll hack is gone: aspect FactKeys + composer dedup
            // make cross-section misrouting structurally impossible.)
            return list.Take(1).ToList();   // TASK-02: max 1 area to improve
        }

        // TASK-2.2: emits keyed tip candidates — selection caps and rotation reads
        // stay here; arbitration, cross-section dedup, and persistence happen in
        // GenerateReport via CoachReportComposer.
        private static List<(string key, string text)> GetAdviceCandidates(AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            var candidates = new List<(string key, string text)>();
            bool hasHistory = memory.TotalDrillCount >= 3;

            // ── Prev-session values for IsSignificantlyChanged ────────────────
            var prevSame = memory.AllDrills
                .Where(d => d.Scenario == r.Scenario && d.Timestamp != r.Timestamp)
                .OrderByDescending(d => d.Timestamp)
                .FirstOrDefault();
            double prevAccuracy      = prevSame?.Accuracy      ?? r.Accuracy;
            double prevAvgReactionMs = prevSame?.AvgReactionMs ?? r.AvgReactionMs;
            bool   hasGapData   = r.BestReactionMs > 0 && r.AvgReactionMs > 0 && r.Hits >= 5;
            double reactionGap  = hasGapData ? r.AvgReactionMs - r.BestReactionMs : -1;
            double streakRatio  = r.Hits > 0 ? (double)r.MaxStreak / r.Hits : 0;
            double prevStreakRatio = prevSame != null && prevSame.Hits > 0
                ? (double)prevSame.MaxStreak / prevSame.Hits : streakRatio;

            bool IsSignificantlyChanged(string key) => key switch
            {
                "high_acc_slow_react"                    => Math.Abs(r.Accuracy - prevAccuracy) > 5,
                "difficulty_ceiling"                     => false,
                "streak_dominant" or "streak_consistent" => Math.Abs(streakRatio - prevStreakRatio) > 0.15,
                _                                        => false
            };

            // ── Scenario-specific tips ────────────────────────────────────────
            switch (r.Scenario)
            {
                case "Sniper":
                    candidates.Add(("scenario_sniper", Pick(c.SessionCount,
                        "Sniper accuracy is built in the last 20% of mouse movement, not the first 80%. The approach can be fast — the settle must be complete. Two distinct phases: move fast, stop completely, then click.",
                        "Your best sniper shots will feel like you waited too long. That discomfort of waiting is correct technique. If a click feels rushed it was.",
                        "On moving Sniper targets: match the speed first, then slow to zero relative motion, then click. You're not clicking the target — you're clicking the moment of stillness."
                    )));
                    break;

                case "Shotgun":
                    candidates.Add(("scenario_shotgun", Pick(c.SessionCount,
                        "Shotgun fights are won before the fight starts — pre-aim where opponents appear, not where they are when you see them.",
                        $"In Shotgun, crosshair placement before the engagement matters more than your reaction during it. At {r.AvgReactionMs:F0}ms you have the speed — pre-positioning at head height is what converts it.",
                        "One clean shot beats two sloppy ones in shotgun scenarios. Slow down slightly and watch your accuracy improve more than your reaction speed."
                    )));
                    break;

                case "SmgAr":
                    // TASK-0.1: grip/tension variants removed pending smoothness validation.
                    candidates.Add(("scenario_smgar",
                        "Two targets means you have to prioritize constantly. Train yourself to always be moving toward the next target " +
                        "before you've finished clicking the current one."
                    ));
                    break;

                case "Tracking":
                    // TASK-0.1/0.3: grip-tension causal variant and "lower your sensitivity" opinion removed
                    // (sensitivity is RecommendationEngine's authority per TASK-3.1).
                    if (r.Accuracy < 65)
                        candidates.Add(("scenario_tracking",
                            "Focus on smooth movement — Tracking rewards control more than speed."));
                    break;

                case "Flicking":
                {
                    string flicTip;
                    if (r.AvgReactionMs > 500)
                        flicTip = "For Flicking, reaction time is mostly about where your crosshair starts. From the moment a target spawns you have two choices: move from where you are, or be close already. Being close already wins every time. After each click, don't rest your crosshair — move it toward where the next target is likely to spawn.";
                    else if (r.Accuracy >= 88)
                        flicTip = $"At {r.Accuracy:F0}% Flicking accuracy your next limiter is target transition speed — how fast you move between targets not just how accurately you hit them. The gap between your clicks is where time is being lost now, not the clicks themselves.";
                    else
                        flicTip = r.AvgReactionMs > 400
                            ? "In Flicking, don't click as soon as you start moving — wait for your cursor to settle on the target. One confident click beats two rushed misses."
                            : $"Your reaction is already strong at {r.AvgReactionMs:F0}ms. Focus on reducing your miss rate to convert that speed into real accuracy.";
                    candidates.Add(("scenario_flicking", flicTip));
                    break;
                }

                case "Precision":
                    // Sensitivity tip fires when ALL three conditions are verified:
                    // 1. cm/360 is a real reading (> 0 and not above 200 — guards uninitialized/corrupted values)
                    // 2. cm/360 < 25 — genuinely high sensitivity territory
                    // 3. accuracy is actually being affected (< 75%)
                    if (c.TrackerCmPer360.HasValue
                        && c.TrackerCmPer360.Value > 0
                        && c.TrackerCmPer360.Value < 25
                        && c.TrackerCmPer360.Value <= 200
                        && r.Accuracy < 75)
                    {
                        candidates.Add(("scenario_precision",
                            $"Your sensitivity is on the higher end at {c.TrackerCmPer360.Value:F1} cm/360. " +
                            "On small Precision targets that can work against you — micro-corrections get amplified. " +
                            "The Recommend screen can show you a target range based on your movement patterns."));
                    }
                    else if (c.TrackerCorrectionSharpness.HasValue && c.TrackerCorrectionSharpness.Value > 25)
                    {
                        candidates.Add(("scenario_precision",
                            "Precision misses on small targets almost always come from micro-corrections after the initial movement. Your crosshair arrives close then drifts. The fix: stop your mouse completely before clicking. Dead stop, then fire. Any movement at the moment of click on a small target is a miss."));
                    }
                    break;

                case "Switching":
                    candidates.Add(("scenario_switching",
                        r.MaxStreak < 5
                            ? "Pre-aim the location where the next target will likely appear. In Switching, positioning your mouse early is faster than reacting late."
                            : "Add the Tracking scenario to your warmup — smooth movement between targets is what separates good Switching players from great ones."
                    ));
                    break;

                case "Reactive":
                    if (r.AvgDirectionChangeLagMs > 120)
                        candidates.Add(("scenario_reactive",
                            $"Your {r.AvgDirectionChangeLagMs:F0}ms direction change lag means you're watching the target switch directions and then starting to follow. The drill that compresses this fastest: watch the target's deceleration not its position. The moment it slows down it's about to change direction — start moving before it does."));
                    break;

                default:
                {
                    // Try telemetry-based observation first (data-specific > generic)
                    var telemetry = GetTelemetryObservations(r, c, memory);
                    if (telemetry.Count > 0)
                    {
                        candidates.AddRange(telemetry.Take(1));
                    }
                    else if (c.AccuracyGrade is "developing" or "average")
                    {
                        candidates.Add(("crosshair_placement", Pick(c.SessionCount,
                            $"At {r.Accuracy:F0}% in {r.Scenario}, prioritise placement over speed. A slow hit scores more than a fast miss.",
                            $"{r.Scenario} accuracy builds fastest when you commit fully to each click — hesitation between decision and execution is what costs you.",
                            $"Your {r.Accuracy:F0}% accuracy in {r.Scenario} has room to grow. Start with slowing the motion to target, then speed it up once the placement is consistent.",
                            $"In {r.Scenario}, the common miss pattern is moving to the target edge rather than center. Focus on committing to center pixel."
                        )));
                    }
                    else
                    {
                        var scenarioSessionsD = memory.RecentDrills
                            .Where(d => d.Scenario == r.Scenario)
                            .Take(5)
                            .ToList();

                        if (scenarioSessionsD.Count >= 3)
                        {
                            double minAccD = scenarioSessionsD.Min(d => d.Accuracy);
                            double maxAccD = scenarioSessionsD.Max(d => d.Accuracy);
                            double rangeD  = maxAccD - minAccD;

                            if (rangeD > 8)
                                candidates.Add(("variance_spread",
                                    $"Your {r.Scenario} scores have ranged from {minAccD:F0}% to {maxAccD:F0}% recently — " +
                                    $"a {rangeD:F0}-point spread. Closing that gap is the next challenge. " +
                                    "Consistency at this level is harder than getting here."));
                            else
                                candidates.Add(("variance_spread",
                                    $"Your {r.Scenario} scores are tightly grouped around {scenarioSessionsD.Average(d => d.Accuracy):F0}%. " +
                                    "That consistency is the foundation — now push the ceiling by moving up a difficulty."));
                        }
                        else if (r.Scenario != "Adaptive" || memory.SessionsPerScenario.GetValueOrDefault("Adaptive", 0) < 5)
                        {
                            candidates.Add(("variance_spread",
                                $"Run a few more {r.Scenario} sessions and the coach can give you a specific breakdown of your score range."));
                        }
                    }
                    break;
                }
            }

            // ── Combined accuracy + reaction diagnosis ────────────────────────
            // High accuracy + slow reaction → over-confirming before clicking
            if (r.Scenario != "Sniper"
                && r.Accuracy >= 85
                && r.AvgReactionMs > Bench.ReactionGood(r.Scenario))
            {
                string tPace = ReactionMetric.Noun(r.Scenario);
                candidates.Add(("high_acc_slow_react", PickGlobal(memory, 6,
                    $"At {r.Accuracy:F0}% accuracy your aim is good enough to click earlier than you are. Your {r.AvgReactionMs:F0}ms average {tPace} suggests you're waiting for full confidence before clicking. At this accuracy level you can trust your first instinct — clicking 0.1 seconds earlier won't drop your accuracy but will cut your {tPace} significantly.",
                    $"{r.Accuracy:F0}% accuracy with {r.AvgReactionMs:F0}ms {tPace}. You're trading speed for certainty. The trade is worth it while you're building accuracy — but at {r.Accuracy:F0}% you've built it. Start committing earlier. Your aim will hold.",
                    $"High accuracy and a slow {tPace} together mean you're confirming too long before clicking. {r.Accuracy:F0}% proves your aim is ready. Your speed will improve when you start trusting it."
                )));
            }
            // Low accuracy + fast reaction → clicking before cursor has settled
            else if (r.Accuracy < 60
                     && r.AvgReactionMs <= Bench.ReactionGood(r.Scenario)
                     && r.Hits >= 5)
            {
                string tPace2 = ReactionMetric.Noun(r.Scenario);
                candidates.Add(("low_acc_fast_react", PickGlobal(memory, 6,
                    $"Fast {tPace2} at {r.AvgReactionMs:F0}ms but {r.Accuracy:F0}% accuracy — you're clicking before your crosshair arrives. Speed is there. Aim isn't catching up. Slow your click by one deliberate beat and watch accuracy jump without losing much speed.",
                    $"Your {r.AvgReactionMs:F0}ms {tPace2} is competitive but {r.Accuracy:F0}% accuracy means the clicks are early. You're winning the race to the trigger but losing the shot. One conscious pause before each click will fix this."
                )));
            }

            // ── Streak ratio diagnosis ────────────────────────────────────────
            if (r.Hits >= 10 && r.MaxStreak >= 5)
            {
                if (streakRatio > 0.50)
                {
                    candidates.Add(("streak_dominant", PickGlobal(memory, 7,
                        $"Your {r.MaxStreak}-hit streak was {streakRatio * 100:F0}% of your total hits. That means one great period and scattered performance outside it. Zone sessions feel good but don't build the skill as fast as consistent sessions. Next session aim for a lower max streak with fewer cold periods.",
                        $"{r.MaxStreak} hits in a row out of {r.Hits} total — you peaked early and faded. One dominant streak with inconsistency elsewhere is a focus pattern, not an aim pattern. The mental reset between misses is what to work on."
                    )));
                }
                else if (streakRatio <= 0.35)
                {
                    candidates.Add(("streak_consistent", PickGlobal(memory, 7,
                        $"Your {r.MaxStreak}-hit streak against {r.Hits} total hits shows consistent performance rather than one hot period. That consistency is harder to build than peak accuracy and more valuable in a real game.",
                        $"Distributed hits with a {r.MaxStreak}-streak max — you stayed consistent throughout rather than peaking and fading. That's the pattern that transfers to ranked play."
                    )));
                }
            }

            // ── Miss pattern observation ──────────────────────────────────────
            if (r.Accuracy < 75 && r.MaxStreak >= 8)
            {
                candidates.Add(("miss_clustered", PickGlobal(memory, 8,
                    $"You hit {r.MaxStreak} in a row but only landed {r.Accuracy:F0}% overall. Your misses aren't random — they're clustered. When you miss once, you're likely missing the next 2-3 as well. The recovery after a miss is the specific thing to work on. After each miss, pause one beat before the next click.",
                    $"{r.MaxStreak}-hit streak but {r.Accuracy:F0}% overall means grouped misses not scattered ones. Something breaks your rhythm when you miss — rushing to recover, tensing up, or losing focus. Treat each click as independent of the last one."
                )));
            }
            else if (r.Accuracy < 60 && r.MaxStreak < 5 && r.Hits >= 8)
            {
                candidates.Add(("miss_scattered", PickGlobal(memory, 8,
                    $"At {r.Accuracy:F0}% with a {r.MaxStreak}-hit max streak, misses are scattered throughout rather than clustered. That pattern means the aim mechanics themselves need work rather than mental focus. Drop to Easy and run 3 sessions focusing exclusively on one motion per target — no corrections.",
                    $"Scattered misses and a low max streak ({r.MaxStreak}) at {r.Accuracy:F0}% accuracy. This is a mechanics problem not a focus problem. Slow everything down — Easy difficulty, deliberate single motion per target until the hit rate stabilizes above 75%."
                )));
            }

            // ── Difficulty ceiling / reaction / improving / off-session / else ─
            if (c.AccuracyGrade == "elite"
                && r.Difficulty != "Hard" && r.Difficulty != "Nightmare")
            {
                double scenarioAvg = memory.BaselineAccuracy.GetValueOrDefault(r.Scenario, 0);
                string nextDiff = r.Difficulty == "Easy" ? "Medium" : r.Difficulty == "Medium" ? "Hard" : "Nightmare";
                string ceilingTip;

                if (r.Accuracy >= 88 && scenarioAvg >= 85)
                {
                    ceilingTip = Pick(c.SessionCount,
                        $"Your {r.Scenario} average is {scenarioAvg:F0}% and you just hit {r.Accuracy:F0}%. {r.Difficulty} is no longer exposing your weaknesses. {nextDiff} introduces smaller faster targets — every miss will show you a specific habit {r.Difficulty} is hiding. The accuracy drop will feel bad but each miss is diagnostic in a way {r.Difficulty} hits aren't.",
                        $"{r.Accuracy:F0}% on {r.Difficulty} with a {scenarioAvg:F0}% average means you've optimized for this difficulty. Moving up won't just challenge you — it will show you micro-correction habits and timing patterns your current accuracy is masking.",
                        $"At {r.Accuracy:F0}% you're near the {r.Difficulty} ceiling for {r.Scenario}. The next difficulty doesn't just make it harder — it makes your specific weaknesses visible. Targets you currently hit by correcting mid-motion will become misses. That's useful information."
                    );
                }
                else if (hasHistory && memory.BaselineAccuracy.TryGetValue(r.Scenario, out double scBase))
                {
                    int aboveBaseCount = memory.AllDrills
                        .Where(d => d.Scenario == r.Scenario && d.Accuracy >= Bench.AccuracyElite(r.Scenario))
                        .Count();
                    ceilingTip = PickGlobal(memory, 2,
                        $"You've hit above {Bench.AccuracyElite(r.Scenario):F0}% in {aboveBaseCount} of your {r.Scenario} sessions. {r.Difficulty} is no longer challenging enough — move up.",
                        $"Your {r.Scenario} average is {scBase:F0}% and you just hit {r.Accuracy:F0}%. {nextDiff} difficulty will expose the gaps {r.Difficulty} is hiding.",
                        $"{r.Accuracy:F0}% on {r.Difficulty} with a {scBase:F0}% average — {nextDiff} is the next step.",
                        $"You've earned the next difficulty. {r.Accuracy:F0}% accuracy on {r.Difficulty} across multiple sessions is a clear signal."
                    );
                }
                else
                {
                    ceilingTip = Pick(c.SessionCount,
                        $"{r.Accuracy:F0}% on {r.Difficulty} — you're ready for {nextDiff}. Harder targets expose the gaps that {r.Difficulty} can't.",
                        $"You've outgrown {r.Difficulty} at {r.Accuracy:F0}%. Moving to {nextDiff} will tell you exactly what to work on next."
                    );
                }
                candidates.Add(("difficulty_ceiling", ceilingTip));
            }
            else if (c.ReactionGrade == "slow")
            {
                string sPace = ReactionMetric.Noun(r.Scenario);
                candidates.Add(("reaction_slow", PickGlobal(memory, 3,
                    $"Your average {sPace} is {r.AvgReactionMs:F0}ms. Instead of trying to move faster, work on predicting — move your cursor to where the target will be, not where it is.",
                    $"Anticipation beats raw speed. At {r.AvgReactionMs:F0}ms average {sPace} you're reacting after the fact. Watch the pattern and pre-aim instead.",
                    $"Your best {sPace} of {r.BestReactionMs:F0}ms proves the speed is there. The gap to your average ({r.AvgReactionMs:F0}ms) is a focus and anticipation problem, not a physical limit.",
                    $"{r.AvgReactionMs:F0}ms average {sPace}. Speed training works best when you stop trying to be fast. Focus on the target's movement, not on clicking."
                )));
            }
            else if (c.IsImproving)
            {
                candidates.Add(("improvement_ack", PickGlobal(memory, 3,
                    $"You've improved {Math.Abs(c.AccuracyDelta):F0}% in accuracy this session — keep the same routine. When something is working, don't change it.",
                    $"{Math.Abs(c.AccuracyDelta):F0}% improvement this session in {r.Scenario}. The current routine is producing results — protect it from disruption.",
                    $"Up {Math.Abs(c.AccuracyDelta):F0}% in {r.Scenario} this session. Stay on the same schedule and let the trend continue."
                )));
            }
            else if (!c.IsFirstSession && c.AccuracyDelta < -5
                     && (memory.AccuracyTrend < -3.0
                         || r.Accuracy < memory.BaselineAccuracy.GetValueOrDefault(r.Scenario, r.Accuracy + 1) * 0.85))
            {
                // Gate: off-session tips only when trend is also negative OR accuracy is
                // genuinely below baseline. Prevents them firing during an improving trend
                // where a single dip below last session doesn't indicate a real off session.
                candidates.Add(("off_session", PickGlobal(memory, 3,
                    "A dip in performance is normal. Check if your sensitivity feels right today — sometimes a slight DPI or sens change explains a sudden dip.",
                    $"Off sessions happen. Before your next {r.Scenario} session, check: same mouse surface, same sensitivity, same warmup duration? Even a 5% sensitivity change explains a sudden dip.",
                    "Performance variance is data. If this keeps happening, look at the conditions — time of day, warmup, sleep. Aim reacts to everything.",
                    "One bad session does not define the trend. Come back tomorrow with fresh hands and compare."
                )));
            }
            else
            {
                candidates.Add(("session_reset",
                    $"The first {r.Scenario} session of a day is always your worst. Use it as warmup data, not a performance benchmark — session 2 and 3 are where your real numbers live."));
            }

            // ── Session count / variance ──────────────────────────────────────
            if (c.SessionCount < 5)
            {
                candidates.Add(("progress_early", Pick(c.SessionCount,
                    "Track 3 sessions per week consistently. At 10 sessions in the same scenario you'll start seeing clear trend lines in your history.",
                    $"Log 10 {r.Scenario} sessions and the coach can show you your real accuracy range — until then, single-session results are just noise."
                )));
            }
            else if (c.IsConsistent)
            {
                var scenarioSessions = memory.RecentDrills
                    .Where(d => d.Scenario == r.Scenario)
                    .Take(5)
                    .ToList();

                if (scenarioSessions.Count >= 3)
                {
                    double minAcc = scenarioSessions.Min(d => d.Accuracy);
                    double maxAcc = scenarioSessions.Max(d => d.Accuracy);
                    double range  = maxAcc - minAcc;
                    double avg    = scenarioSessions.Average(d => d.Accuracy);

                    if (range > 8)
                        candidates.Add(("variance_spread",
                            $"Your {r.Scenario} scores have ranged from {minAcc:F0}% to {maxAcc:F0}% recently — " +
                            $"a {range:F0}-point spread. Closing that gap is the next challenge. " +
                            "Consistency at this level is harder than getting here."));
                    else
                        candidates.Add(("variance_spread",
                            $"Your {r.Scenario} scores are tightly grouped around {avg:F0}%. " +
                            "That consistency is the foundation — now push the ceiling by moving up a difficulty."));
                }
                else if (r.Scenario != "Adaptive" || memory.SessionsPerScenario.GetValueOrDefault("Adaptive", 0) < 5)
                {
                    candidates.Add(("variance_spread",
                        $"Run a few more {r.Scenario} sessions and the coach can give you a specific variance breakdown."));
                }
            }
            // TASK-03: removed generic "3 focused sessions per week" tip

            // ── Priority function ─────────────────────────────────────────────
            static int TipPriority(string key) => key switch
            {
                "high_acc_slow_react" or "low_acc_fast_react"                                 => 1,
                "streak_dominant" or "streak_consistent"                                     => 2,
                "difficulty_ceiling"                                                          => 3,
                "path_efficiency" or "overshoot" or "undershoot" or "direction_lag" or
                "axis_split" or "peek_early" or "peek_late"                                  => 4,
                "scenario_flicking" or "scenario_tracking" or "scenario_reactive" or
                "scenario_precision" or "scenario_sniper" or "scenario_shotgun" or
                "scenario_smgar" or "scenario_switching"                                     => 5,
                "improvement_ack"                                                             => 6,
                _                                                                             => 7
            };

            // ── TASK-03: Filter, sort, take top 2 (Tip 2 only if forward-looking) ──
            var recentKeys = memory.RecentTipKeys;
            var sortedCandidates = candidates
                .Where(t => !recentKeys.Contains(t.key) || IsSignificantlyChanged(t.key))
                .OrderBy(t => TipPriority(t.key))
                .ToList();

            var selected = new List<(string key, string text)>();
            if (sortedCandidates.Count > 0)
                selected.Add(sortedCandidates[0]);

            // Tip 2: only forward-looking keys — difficulty ceiling or streak pattern.
            // Generic fallbacks (reaction_slow, off_session, variance_spread, etc.) are omitted.
            if (sortedCandidates.Count > 1)
            {
                var tip2 = sortedCandidates.Skip(1).FirstOrDefault(t =>
                    t.key is "difficulty_ceiling" or "streak_dominant" or "streak_consistent"
                          or "high_acc_slow_react" or "low_acc_fast_react"
                          or "path_efficiency" or "overshoot" or "undershoot"
                          or "direction_lag" or "axis_split");
                if (tip2 != default)
                    selected.Add(tip2);
            }

            // TASK-2.2: persistence moved to GenerateReport — only keys that
            // SURVIVE composition are recorded for rotation.
            return selected;
        }

        private static string GetNextDrill(AimTrainerResult r, CoachContext c)
        {
            if (c.AccuracyGrade == "elite" && r.Difficulty != "Hard" && r.Difficulty != "Nightmare")
                return $"Move up to Hard {r.Scenario} — you've earned it with {r.Accuracy:F0}% accuracy. The increased pressure will expose new areas to work on.";
            if (c.AccuracyGrade is "developing" or "average" && r.Difficulty == "Hard")
                return $"Drop to Medium {r.Scenario} for your next 2-3 sessions. Build confidence at a manageable level before returning to Hard.";
            if (r.Scenario == "Flicking" && c.ReactionGrade == "slow")
                return "Try Precision next — it trains the same target acquisition skill as Flicking but removes the time pressure, helping you build a cleaner click habit.";
            if (r.Scenario == "Tracking" && r.Accuracy < 55)
                return $"Stay on {r.Difficulty} Tracking but cut the duration to 30 seconds. Shorter sessions help you stay focused for the whole drill instead of fading.";
            if (c.WeakArea == "streak")
                return $"Try Switching on {r.Difficulty} — it specifically trains target-to-target transitions which directly improves your streak consistency.";
            if (!c.IsFirstSession && c.AccuracyDelta > 5)
                return $"Repeat {r.Scenario} at {r.Difficulty} to reinforce today's improvement — back-to-back sessions on the same scenario lock in gains faster.";
            return $"Run {r.Scenario} again at {r.Difficulty} — consistency in the same scenario builds the muscle memory that transfers directly to your game.";
        }

        // ── TASK-26: Telemetry-based observations ─────────────────────
        //
        // COACHING LANGUAGE RULES (4):
        //   1. Specificity first — always embed the actual measured value in the observation.
        //   2. No observation without action — every line ends with a concrete next step.
        //   3. Rotate 4 variants per observation so the same line never repeats consecutively.
        //   4. Single diagnosis — return the highest-signal observation only; don't pile on.
        //
        // All guards use a 0-check because these fields are 0 when TASK-28 hasn't fired yet.
        // The ordering of checks is priority order: Path → Overshoot → Undershoot →
        //   DirectionLag → AxisSplit → PeekTiming → ImprovementAck.
        private static List<(string key, string text)> GetTelemetryObservations(
            AimTrainerResult r, CoachContext c, CoachMemory memory)
        {
            var obs = new List<(string key, string text)>();
            int idx  = c.SessionCount;   // rotation index per scenario session count

            // ── 1. PATH EFFICIENCY ────────────────────────────────────
            // Clicking pillar only (>= 5 hits) — low path efficiency (< 0.80) means
            // wobbly/indirect routes; tracking/peek scenarios have different movement intent.
            // Path efficiency coaching only applies to pure clicking scenarios where
            // route-to-target is the primary mechanic. Excluded: Reactive (reaction speed
            // is the metric there, not routing), tracking, peek, and weapon scenarios.
            // Falls back to scenario-name check for old sessions where Pillar is empty.
            if (r.PathEfficiency > 0 && r.PathEfficiency < 0.80
                && r.Scenario is "StaticClicking" or "DynamicClicking" or "Precision" or "Sniper"
                && r.Hits >= 5)
            {
                double pct = r.PathEfficiency * 100;
                obs.Add(("path_efficiency", Pick(idx,
                    $"Path efficiency is {pct:F0}% — your mouse is travelling further than the straight-line distance to each target. " +
                    "That extra distance is dead movement that costs time. Focus on driving directly to the center rather than approaching from the side.",
                    $"Your routing efficiency is {pct:F0}%. Every wasted curve adds 30–80ms to your time per target. " +
                    "Before each click, visualize the straight line to the target and commit to it.",
                    $"Movement efficiency at {pct:F0}% — you're orbiting targets before clicking rather than moving through them. " +
                    "Practice 5-second explosive move-and-click drills: move once, click, stop. No corrections.",
                    $"{pct:F0}% path efficiency means roughly {(100 - pct):F0}% of your mouse movement is wasted. " +
                    "Straighter paths start with a slower, deliberate first movement that goes exactly where you intend — then speed that up."
                )));
                return obs;
            }

            // ── 2. OVERSHOOT ──────────────────────────────────────────
            // High overshoot = consistent overreaching (> 35%)
            if (r.OvershootPct > 35)
            {
                obs.Add(("overshoot", Pick(idx,
                    $"You're overshooting {r.OvershootPct:F0}% of clicks — your hand is committing more force than the target requires. " +
                    "Reduce your swing amplitude slightly and let the cursor settle rather than correcting after overshoot.",
                    $"Overshoot rate: {r.OvershootPct:F0}%. That's the most common sign that sensitivity is slightly too high for this scenario. " +
                    "Try reducing in-game sensitivity by 5% and retest — the overshoot pattern usually disappears.",
                    $"{r.OvershootPct:F0}% of your clicks go past the target before landing. The fix is deceleration, not speed. " +
                    "Mentally aim for a point 20% before the target center — your natural momentum will carry you to center.",
                    $"High overshoot at {r.OvershootPct:F0}% indicates your stopping mechanics need work more than your speed. " +
                    "Practice stopping exercises: move to a target, stop completely, then move to the next. Train the deceleration phase."
                )));
                return obs;
            }

            // ── 3. UNDERSHOOT ─────────────────────────────────────────
            // High undershoot = consistently falling short (> 35%)
            if (r.UndershootPct > 35)
            {
                obs.Add(("undershoot", Pick(idx,
                    $"You're undershooting {r.UndershootPct:F0}% of clicks — you're stopping short of the target rather than committing through it. " +
                    "Aim for a point slightly past target center; natural deceleration will land you on it.",
                    $"Undershoot rate: {r.UndershootPct:F0}%. This often means sensitivity is slightly too low or you're hesitating at the end of each movement. " +
                    "Try committing fully to the motion — don't slow down in the last 20% of the movement.",
                    $"{r.UndershootPct:F0}% undershoot — you're braking too early. The target center should feel like the midpoint of your swing, not the endpoint. " +
                    "Extend your follow-through and the accuracy will improve.",
                    $"High undershoot at {r.UndershootPct:F0}% is a confidence pattern — you're not fully committing to the destination. " +
                    "In your next session, practice movements that intentionally go 10% past the target, then work backwards to center."
                )));
                return obs;
            }

            // ── 4. DIRECTION CHANGE LAG ───────────────────────────────
            // High lag between target direction change and player response (> 120ms)
            if (r.AvgDirectionChangeLagMs > 120)
            {
                obs.Add(("direction_lag", Pick(idx,
                    $"You're lagging {r.AvgDirectionChangeLagMs:F0}ms behind direction changes on average. " +
                    "That gap means you're reacting to changes rather than anticipating them. " +
                    "Study the bounce/reversal pattern for 2 seconds at the start of each drill before committing to it.",
                    $"Direction change response lag: {r.AvgDirectionChangeLagMs:F0}ms. " +
                    "Your current approach is 'track then correct' — the faster approach is 'anticipate and lead.' " +
                    "Watch the target's velocity, not just its position, and start moving before the reversal.",
                    $"{r.AvgDirectionChangeLagMs:F0}ms average lag on direction changes is the primary reason your tracking accuracy drops during reversals. " +
                    "Use the pattern's rhythm: if a target reverses every ~0.8s, start your counter-movement at 0.7s.",
                    $"Direction lag at {r.AvgDirectionChangeLagMs:F0}ms. The players who close this gap aren't reacting faster — they're reading the pattern. " +
                    "In your next session, spend the first 5 seconds observing without clicking to learn the reversal timing."
                )));
                return obs;
            }

            // ── 5. AXIS SPLIT ─────────────────────────────────────────
            // Significant H/V tracking imbalance (> 20 point gap between axes)
            double horizAcc = r.HorizontalTrackingAcc;
            double vertAcc  = r.VerticalTrackingAcc;
            if (horizAcc > 0 && vertAcc > 0)
            {
                double axisDelta = Math.Abs(horizAcc - vertAcc);
                if (axisDelta > 20)
                {
                    string weakAxis   = horizAcc < vertAcc ? "horizontal" : "vertical";
                    double weakScore  = Math.Min(horizAcc, vertAcc);
                    double strongScore = Math.Max(horizAcc, vertAcc);

                    obs.Add(("axis_split", Pick(idx,
                        $"You're {strongScore:F0}% accurate on the strong axis but only {weakScore:F0}% on {weakAxis} tracking — a {axisDelta:F0}-point split. " +
                        $"That gap usually means your wrist mechanics are weaker for {weakAxis} movement. " +
                        "Drill that axis specifically: use an Air Tracking Diagonal variant to isolate it.",
                        $"Axis imbalance detected: {axisDelta:F0} points between your horizontal and vertical tracking accuracy. " +
                        $"Your {weakAxis} axis is the bottleneck — all the tracking gains are stuck there. " +
                        "Use Air Tracking to isolate the weak axis and close the gap.",
                        $"A {axisDelta:F0}-point gap between your horizontal and vertical accuracy is a clear signal that one axis needs dedicated work. " +
                        $"Your {weakAxis} tracking is at {weakScore:F0}% — that's the ceiling you're working against. " +
                        "Add one Air Tracking session focused exclusively on that axis to your weekly routine.",
                        $"Your tracking splits {strongScore:F0}% vs {weakScore:F0}% across axes — the {weakAxis} side is limiting your ceiling. " +
                        "Axis imbalances rarely resolve on their own. Isolate it with horizontal-only or vertical-only drills for 2 sessions."
                    )));
                    return obs;
                }
            }

            // ── 6. PEEK TIMING ────────────────────────────────────────
            // High early or late click percentage in PeekTraining (> 30%)
            if (r.Scenario == "PeekTraining")
            {
                if (r.PeekEarlyClickPct > 30)
                {
                    obs.Add(("peek_early", Pick(idx,
                        $"You're clicking early {r.PeekEarlyClickPct:F0}% of the time — firing before the target is fully exposed. " +
                        "In real games that means whiffing on a peeking enemy. Wait for center-mass, then fire.",
                        $"{r.PeekEarlyClickPct:F0}% early clicks means you're anticipating the peek instead of reacting to it. " +
                        "The fix: don't pre-fire. Let the target fully appear, confirm it's there, then commit.",
                        $"Early click rate: {r.PeekEarlyClickPct:F0}%. You're trading for unpredictability when you fire early — and usually losing. " +
                        "Hold your shot until the center of the target is visible, even if it feels late.",
                        $"High early-click rate at {r.PeekEarlyClickPct:F0}%. That pattern means you're guessing the peek timing rather than reading it. " +
                        "In WideSwing and Jiggle variants, the peak exposure time is consistent — learn the window and fire inside it, not before."
                    )));
                    return obs;
                }
                if (r.PeekLateClickPct > 30)
                {
                    obs.Add(("peek_late", Pick(idx,
                        $"You're clicking late {r.PeekLateClickPct:F0}% of the time — firing after the target has already started retreating. " +
                        "In game that's shooting at where they were, not where they are. Tighten your window: commit when you first see center-mass.",
                        $"{r.PeekLateClickPct:F0}% late clicks means hesitation is costing you. You're seeing the target but waiting too long to pull the trigger. " +
                        "One session of deliberate early-commit practice closes this faster than volume alone.",
                        $"Late click rate: {r.PeekLateClickPct:F0}%. The target is giving you the window — you're just not using it. " +
                        "Work on the decision trigger: the moment you see chest/head, fire. Don't wait for 'perfect alignment.'",
                        $"High late-click rate at {r.PeekLateClickPct:F0}% indicates an over-cautious trigger. " +
                        "Trust the first moment of exposure more. Aim to halve your decision-to-fire time over the next 3 sessions."
                    )));
                    return obs;
                }
            }

            return obs;   // empty — caller will fall through to variance tips
        }

        private static string GetMotivation(AimTrainerResult r, CoachContext c)
        {
            // Rotate variants using session count so same line never repeats consecutively
            int v = c.TotalSessionsAll % 4;

            if (c.NewAccuracyRecord || c.NewReactionRecord)
                return v switch {
                    0 => "Personal records don't happen by accident — you earned this one.",
                    1 => "That's a new benchmark. Now you know what you're capable of.",
                    2 => c.NewAccuracyRecord
                             ? $"New accuracy record at {r.Accuracy:F0}% — that number belongs to you now."
                             : $"New {ReactionMetric.Noun(r.Scenario)} record at {r.AvgReactionMs:F0}ms — that's your new ceiling to beat.",
                    _ => "New personal best. That number is yours to beat now."
                };

            if (c.IsImproving && c.SessionCount >= 5)
                return v switch {
                    0 => $"Session {c.SessionCount} in the books — the trend is real and it's pointing up.",
                    1 => $"{c.SessionCount} sessions logged and the improvement is measurable. Stay consistent.",
                    2 => "The improvement is measurable and it's real. Trust the process.",
                    _ => $"Upward trend confirmed across {c.SessionCount} sessions. The data says keep doing what you're doing."
                };

            // TASK-0.2: population-claim variants ("most players", "top tier", "vast majority") removed.
            if (c.AccuracyGrade == "elite")
                return v switch {
                    0 => "Elite accuracy isn't luck — it's reps. You're putting them in.",
                    _ => "Elite accuracy isn't luck — it's reps. You're putting them in."
                };

            if (c.IsFirstSession)
                return v switch {
                    0 => "First session is just the start. Come back tomorrow and you'll already be better.",
                    1 => "Baseline set. Everything from here is measurable improvement.",
                    2 => "Day one done. The players who improve are the ones who come back for day two.",
                    _ => "First session logged. Now you have something to beat."
                };

            if (c.AccuracyDelta < -5)
                return v switch {
                    0 => "Bad sessions are data, not failure. You showed up — that's what separates players who improve from players who don't.",
                    1 => "Off sessions happen to everyone. What matters is you tracked it and came back.",
                    2 => "The dip is logged. That's how you find the pattern — show up again tomorrow.",
                    _ => "One bad session doesn't define the trend. Your history is longer than today."
                };

            if (c.TotalSessionsAll >= 10)
                return v switch {
                    0 => $"{c.TotalSessionsAll} sessions logged across all scenarios. Your overall average accuracy is {c.OverallAvgAccuracy:F0}% — that's your real benchmark.",
                    1 => $"Double digits in. At {c.TotalSessionsAll} sessions your data is starting to tell a real story.",
                    2 => $"{c.TotalSessionsAll} sessions tracked. The improvement is in the data whether you feel it today or not.",
                    _ => $"Your {c.TotalSessionsAll}-session history is your actual performance record. Trust it over any single session."
                };

            if (c.SessionCount >= 10)
                return v switch {
                    0 => $"{c.SessionCount} sessions tracked. The players who log 10+ sessions are the ones who actually improve — you're one of them.",
                    1 => $"10+ sessions in this scenario. Muscle memory is building whether you notice it yet or not.",
                    2 => "Consistency over 10 sessions is rarer than people think. You're in the group that improves.",
                    _ => $"{c.SessionCount} sessions and still showing up. That's the whole game."
                };

            return v switch {
                0 => "Every drill is a deposit in the bank. It adds up faster than you think.",
                1 => $"{r.Accuracy:F0}% this session. Every rep at this level is building the pattern that shows up when it counts.",
                2 => "Progress in aim training is slow and then suddenly obvious. Keep going.",
                _ => "The session is logged. That's one more data point working in your favor."
            };
        }
    }
}
