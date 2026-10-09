using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Session (in-game tracker) coach. TASK-2.2: emits CoachObservation candidates
    /// only — all arbitration, caps, dedup, validity filtering, and headline
    /// generation happen in CoachReportComposer. No user-facing coach text is
    /// assembled in this class outside candidate Message fields.
    /// </summary>
    public static class TrackerCoachService
    {
        public record TrackerCoachReport(
            string Headline,
            List<string> Observations,
            List<string> Suggestions,
            string NextDrillSuggestion
        );

        // TASK-1.3: the single neutral line shown for low-activity sessions.
        public const string LowActivityHeadline = CoachReportComposer.LowActivityHeadline;

        /// <summary>
        /// T4: maps a prescription VerifyMetric to its real SessionSummary field for
        /// the verify-loop baseline. Returns null for an unmapped metric — the caller
        /// then skips the loop rather than open it at a phantom 0 baseline. Tracker
        /// metrics only (drill metrics like MovementOvershoot have no SessionSummary
        /// field and correctly map to null here).
        /// </summary>
        public static double? TrackerVerifyBaseline(string verifyMetric, SessionSummary s) => verifyMetric switch
        {
            "SmoothnessScore"     => s.SmoothnessScore,
            "MovementConsistency" => s.MovementConsistency,
            "CorrectionSharpness" => s.CorrectionSharpness,
            _                     => null
        };

        public static TrackerCoachReport Analyze(
            SessionSummary session,
            List<SessionSummary> history,
            CoachMemory? memory = null)
        {
            int sessionIndex   = history?.Count ?? 0;
            bool isShortSession = session.SessionSeconds < 120;

            // ── TASK-06: tip rotation — suppress candidates shown recently ────
            var recentTrackerKeys = memory?.RecentTipKeys?.Take(9).ToList() ?? new List<string>();
            bool KeyRecent(string factKey) => recentTrackerKeys.Contains("t_" + factKey);

            // ── Baselines (low-activity sessions excluded — TASK-1.3) ─────────
            var prior = (history ?? new List<SessionSummary>())
                .Where(h => h.Timestamp < session.Timestamp && !h.IsLowActivitySession)
                .OrderByDescending(h => h.Timestamp)
                .ToList();

            double avgQuality = prior.Count >= 2 ? prior.Take(5).Average(h => h.OverallQualityScore) : 0;

            // ── Candidate emission ─────────────────────────────────────────────
            var candidates = new List<CoachObservation>();

            // STRENGTH — aspect "consistency_trend": quality steady above baseline.
            if (prior.Count >= 3 && avgQuality >= 60 && !KeyRecent("consistency_trend"))
            {
                bool allAbove = prior.Take(3).All(h => h.OverallQualityScore >= avgQuality * 0.9);
                if (allAbove && session.OverallQualityScore >= avgQuality * 0.9)
                {
                    int v = sessionIndex % 3;
                    candidates.Add(new CoachObservation
                    {
                        FactKey = "consistency_trend",
                        SourceEngine = nameof(TrackerCoachService),
                        Section = CoachSection.Strength,
                        Polarity = ObservationPolarity.Strength,
                        Severity = 3,
                        RequiredMetrics = { "OverallQualityScore" },
                        Message = v switch
                        {
                            0 => $"Your quality score has been consistently above {avgQuality:F0} for your last 3+ sessions. That consistency is harder to build than a single peak score.",
                            1 => $"Multiple sessions of quality around {avgQuality:F0}. Consistency at this level means the habits are becoming automatic.",
                            _ => $"Your session quality has been steady at {avgQuality:F0}+ for several sessions. That's a floor you've built — now push the ceiling."
                        }
                    });
                }
            }

            // TASK-2.4: transfer — single authority, single FactKey.
            if (!KeyRecent(TransferObservationSource.FactKey))
            {
                var transfer = TransferObservationSource.Compute(memory);
                if (transfer != null)
                    candidates.Add(transfer);
            }

            // ASPECT "movement_steadiness" — one aspect, two possible verdicts; the
            // composer guarantees only one can ever render.
            // T3: re-framed off the overshoot claim. CorrectionSharpness is per-event
            // speed variability (post-C2) — on the tracker (no targets) it measures how
            // steady vs jerky the movement speed is, NOT overshoot-and-correct (which
            // the tracker cannot see). Prose now matches the measurement; the metric
            // itself is valid, only the old interpretation was fiction.
            if (session.CorrectionSharpness > 0 && !KeyRecent("movement_steadiness"))
            {
                if (session.CorrectionSharpness < 30)
                {
                    candidates.Add(new CoachObservation
                    {
                        FactKey = "movement_steadiness",
                        SourceEngine = nameof(TrackerCoachService),
                        Section = CoachSection.Strength,
                        Polarity = ObservationPolarity.Strength,
                        Severity = 2,
                        RequiredMetrics = { "CorrectionSharpness" },
                        Message = $"Very low speed variability ({session.CorrectionSharpness:F0}) — your movement speed stayed smooth and even. That steadiness is good mechanics."
                    });
                }
                else if (session.CorrectionSharpness > 65)
                {
                    candidates.Add(new CoachObservation
                    {
                        FactKey = "movement_steadiness",
                        SourceEngine = nameof(TrackerCoachService),
                        Section = CoachSection.Area,
                        Polarity = ObservationPolarity.Concern,
                        Severity = 3,
                        RequiresBehaviorChange = true,
                        RequiredMetrics = { "CorrectionSharpness" },
                        Message = $"Speed variability was high at {session.CorrectionSharpness:F0} — your in-game movement was jerky, with big speed swings between motions. Smoother, more even movement keeps your crosshair steadier under pressure."
                    });
                }
            }

            // AREA — aspect "flick_pattern": large flicks dominate.
            if (session.FlickCount > 5 && !KeyRecent("flick_pattern"))
            {
                double largeRatio = session.LargeFlickCount / (double)session.FlickCount;
                if (largeRatio > 0.6)
                {
                    int v = sessionIndex % 4;
                    candidates.Add(new CoachObservation
                    {
                        FactKey = "flick_pattern",
                        SourceEngine = nameof(TrackerCoachService),
                        Section = CoachSection.Area,
                        Polarity = ObservationPolarity.Concern,
                        Severity = 2,
                        RequiresBehaviorChange = true,
                        Message = v switch
                        {
                            0 => "More than 60% of your flicks were large movements — that's a crosshair placement problem as much as an aim problem. Before your next game, consciously hold your crosshair at head-height where enemies typically appear.",
                            1 => $"Your ratio of large to small flicks is high ({session.LargeFlickCount} large vs {session.SmallFlickCount} small). Large flicks mean getting caught out of position. Focus on one common angle per map and hold head-height there.",
                            2 => "High large-flick count means you're reacting to targets rather than anticipating them. Practice holding crosshair at common head-height positions — it converts large flicks to small corrections.",
                            _ => $"{session.LargeFlickCount} large flicks this session. Pre-aim one common angle per map and you'll start converting those into small corrections."
                        }
                    });

                    // TIP — aspect "flick_drill_habit": the fix paired with the area.
                    if (!KeyRecent("flick_drill_habit"))
                    {
                        int t = sessionIndex % 4;
                        candidates.Add(new CoachObservation
                        {
                            FactKey = "flick_drill_habit",
                            SourceEngine = nameof(TrackerCoachService),
                            Section = CoachSection.Tip,
                            Polarity = ObservationPolarity.Neutral,
                            Severity = 2,
                            RequiresBehaviorChange = true,
                            Message = t switch
                            {
                                0 => "Running Flicking in your next drill session builds faster target initiation and reduces the need for large reactive movements.",
                                1 => "Practice holding crosshair at common head-height angles — it converts large flicks to small corrections.",
                                2 => "Flicking drills — Medium difficulty — specifically trains the reactive initiation that reduces large flick frequency.",
                                _ => "Pre-aim one common angle per map you play. That single habit converts large reactive flicks into small corrections."
                            }
                        });
                    }
                }
            }

            // PRESCRIPTIONS — typed so TASK-2.3 can match severity to verdict.
            // T3 (was M2): rx_correction DISABLED. It prescribed an overshoot remedy
            // ("clean first motion, no corrections") off CorrectionSharpness > 65 —
            // but post-C2 that metric is per-event speed variability, NOT overshoot,
            // and the tracker has no targets, so it cannot measure overshoot at all.
            // Firing an overshoot prescription off a non-overshoot signal is exactly
            // the dishonesty the audit kills. Overshoot is now prescribed correctly
            // drill-side via MovementOvershoot (A1/A2). The tracker keeps its valid
            // prescriptions (rx_flick_initiation, rx_maintain). Do not re-enable on
            // CorrectionSharpness — there is no honest tracker overshoot signal.
            // if (session.CorrectionSharpness > 65) { ...rx_correction... }
            if (session.LargeFlickCount > session.SmallFlickCount && session.FlickCount > 5)
            {
                candidates.Add(new CoachObservation
                {
                    FactKey = "rx_flick_initiation",
                    SourceEngine = nameof(TrackerCoachService),
                    Section = CoachSection.Prescription,
                    Polarity = ObservationPolarity.Concern,
                    Severity = 2,
                    PrescriptionType = PrescriptionType.Remedial,
                    Message = "Flicking Timed Pressure — Medium — builds faster target initiation"
                });
            }
            // Maintenance fallback — only renders when the report is not area-led
            // and no progression candidate exists.
            candidates.Add(new CoachObservation
            {
                FactKey = "rx_maintain",
                SourceEngine = nameof(TrackerCoachService),
                Section = CoachSection.Prescription,
                Polarity = ObservationPolarity.Neutral,
                Severity = 1,
                PrescriptionType = PrescriptionType.Maintenance,
                Message = "Tracking Standard — Medium — keep the rhythm you showed this session"
            });

            // ── VOICE TASK-2.3: the session coach consumes the technique library
            // through the same selector. The context carries a synthetic empty
            // drill result, so only tracker-metric signatures (grip) can fire —
            // drill-metric prescriptions see invalid metrics and stay silent.
            var rxCtx = new PrescriptionContext(
                new AimTrainerResult { Scenario = "Tracker" },
                memory ?? new CoachMemory(),
                session.IsMetricValid);

            // Verification loop for tracker-issued prescriptions (smoothness).
            CoachObservation? trackerFollowUp = null;
            var rxState = memory?.ActivePrescription;
            if (rxState != null && rxState.VerifyMetric == "SmoothnessScore"
                && session.IsMetricValid("SmoothnessScore"))
            {
                rxState.SessionsSince++;
                double oldV = rxState.BaselineValue;
                double newV = session.SmoothnessScore;

                if (newV > oldV * (1 + PrescriptionFollowUpService.ImprovementFactor))
                {
                    memory!.ActivePrescription = null;   // loop closed
                    trackerFollowUp = new CoachObservation
                    {
                        FactKey = "prescription_followup",
                        SourceEngine = nameof(TrackerCoachService),
                        Section = CoachSection.Strength,
                        Polarity = ObservationPolarity.Strength,
                        Severity = 60,
                        Message = $"Last session you worked on your grip pressure. " +
                                  $"Smoothness went {oldV:F0} → {newV:F0}. It's working — keep the change."
                    };
                }
                else if (rxState.SessionsSince >= PrescriptionFollowUpService.FlatSessionsBeforeEscalation
                         && !rxState.Escalated)
                {
                    rxState.Escalated = true;
                    trackerFollowUp = new CoachObservation
                    {
                        FactKey = "prescription_followup",
                        SourceEngine = nameof(TrackerCoachService),
                        Section = CoachSection.Area,
                        Polarity = ObservationPolarity.Concern,
                        Severity = 75,
                        RequiresBehaviorChange = true,
                        Message = $"Smoothness hasn't moved in {rxState.SessionsSince} sessions " +
                                  $"({oldV:F0} → {newV:F0}) — the jaggedness is still there. A lighter grip is the usual first thing to test. " +
                                  "Let's change the approach: shorter reps, full attention on the grip. " +
                                  $"{rxState.PracticeScenario} · {rxState.PracticeVariant} at {rxState.PracticeDifficulty}, 60 seconds at a time."
                    };
                }
            }
            if (trackerFollowUp != null)
                candidates.Add(trackerFollowUp);

            var selectedRx = trackerFollowUp == null
                ? TechniquePrescriptionSelector.Select(rxCtx)
                : null;
            if (selectedRx != null)
            {
                candidates.Add(new CoachObservation
                {
                    FactKey = selectedRx.Prescription.PrescriptionKey,
                    SourceEngine = nameof(TechniquePrescriptionLibrary),
                    Section = CoachSection.Area,
                    Polarity = ObservationPolarity.Concern,
                    Severity = 70,
                    RequiresBehaviorChange = true,
                    RequiredMetrics = selectedRx.Prescription.RequiredMetrics.ToList(),
                    Message = selectedRx.Prescription.ComposeMessage(rxCtx)
                });
                candidates.Add(new CoachObservation
                {
                    FactKey = "rx_technique",
                    SourceEngine = nameof(TechniquePrescriptionLibrary),
                    Section = CoachSection.Prescription,
                    Polarity = ObservationPolarity.Concern,
                    Severity = 20,
                    PrescriptionType = PrescriptionType.Remedial,
                    RequiredMetrics = selectedRx.Prescription.RequiredMetrics.ToList(),
                    Message = selectedRx.Drill.ToString()
                });
            }

            // ── Compose — the only path to user-facing text ────────────────────
            // TASK-3.4: the headline delta comes from the single TrendReport, so
            // it is exact arithmetic over the same baseline every surface shows.
            var trend = TrendAnalysisService.Compute(session, history);

            var composed = CoachReportComposer.Compose(
                candidates,
                session.IsMetricValid,
                new HeadlineContext(
                    QualityScore:  session.OverallQualityScore,
                    BaselineDelta: trend.VsRecentBaselineDelta,
                    IsShortSession: isShortSession,
                    IsLowActivity: session.IsLowActivitySession));

            // VOICE TASK-2.3: open the loop only if the prescription rendered.
            // T4 (was M3): the verify baseline maps each VerifyMetric to its real
            // SessionSummary field. The old `: 0` fallback made any non-smoothness
            // metric open the loop at baseline 0 → next session "improved 0 → N"
            // always read as success (phantom loop-closure). An UNMAPPED metric now
            // skips the loop rather than open a phantom 0 baseline.
            if (selectedRx != null && memory != null
                && composed.SurvivingFactKeys.Contains(selectedRx.Prescription.PrescriptionKey))
            {
                double? baseline = TrackerVerifyBaseline(selectedRx.Prescription.VerifyMetric, session);
                if (baseline.HasValue)
                {
                    TechniquePrescriptionSelector.RecordPrescribed(memory, selectedRx, baseline.Value);
                    memory.ActivePrescription!.ScenarioContext = "Tracker";
                }
                else
                {
                    LogService.Error(
                        $"Tracker prescription '{selectedRx.Prescription.PrescriptionKey}' has VerifyMetric " +
                        $"'{selectedRx.Prescription.VerifyMetric}' with no SessionSummary mapping — loop not opened.");
                }
            }

            // ── TASK-06: persist rotation keys for what actually rendered ──────
            if (memory != null && composed.SurvivingFactKeys.Count > 0)
            {
                memory.RecentTipKeys.InsertRange(0,
                    composed.SurvivingFactKeys
                        .Where(k => !k.StartsWith("rx_"))   // prescriptions always available
                        .Select(k => "t_" + k));
                while (memory.RecentTipKeys.Count > 20)
                    memory.RecentTipKeys.RemoveAt(memory.RecentTipKeys.Count - 1);
            }

            // ── Map to the window contract ─────────────────────────────────────
            // Observations = "WHAT I NOTICED" (strength); Suggestions = "WHAT TO
            // WORK ON" (area leads, then tips).
            var observations = new List<string>();
            if (composed.Strength != null) observations.Add(composed.Strength);

            var suggestions = new List<string>();
            if (composed.Area != null) suggestions.Add(composed.Area);
            suggestions.AddRange(composed.Tips);

            return new TrackerCoachReport(
                Headline:            composed.Headline,
                Observations:        observations,
                Suggestions:         suggestions,
                NextDrillSuggestion: composed.Prescription ?? "");
        }

        // ═════════════════════════════════════════════════════════════════════
        // DISABLED RULES — Gate 0. Do not re-enable without the named gate.
        // Preserved verbatim from the pre-composer implementation for reference.
        // ═════════════════════════════════════════════════════════════════════
        //
        // DISABLED pending TASK-1.1 validity gate (smoothness-derived narratives):
        //   • "smoothness_improving" strength — 4-session smoothness trend praise.
        //   • "smoothness_drop" area — "two common causes: grip tension or a
        //     surface/sensitivity change... try consciously relaxing your grip."
        //   • "consistency_decline" area — trend computed entirely from SmoothnessScore.
        //   • "drill_gap" area — divided SmoothnessScore (0-100) by drill accuracy (%).
        //   • "suggestion_tension"/"suggestion_smoothness" tips — "Low smoothness has
        //     a physical cause: tight grip, arm tension, or cold hands", breathing tips.
        //   • "suggestion_track_drill"/nextDrill smoothness branch — "Tracking Smooth
        //     Arc — Easy — reset muscle memory" justified solely by smoothness.
        //   When TASK-1.1's fix is validated in production data, these may return as
        //   CoachObservation candidates with RequiredMetrics = { "SmoothnessScore" }
        //   and NO unverified causal claims (no grip/tension/cold-hands assertions).
        //
        // DISABLED per TASK-0.3 / TASK-3.1 (sensitivity single authority):
        //   • "sensitivity_low"/"sensitivity_high" areas — "your sensitivity is
        //     actually too low... bump it up" / "lower your sensitivity slightly".
        //     The session coach must never opine on sensitivity; RecommendationEngine
        //     is the sole authority (TASK-3.1 wires its observation through the
        //     composer as FactKey "sensitivity_fit").
        //
        // DELETED per TASK-2.4 (transfer single authority):
        //   • "drill_transfer" strength — "training is transferring well" (one of
        //     three contradictory transfer positions). Replaced by
        //     TransferObservationSource, FactKey "transfer".
    }
}
