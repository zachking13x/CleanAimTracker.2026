using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    public record SelectedPrescription(TechniquePrescription Prescription, PracticeDrill Drill);

    /// <summary>
    /// TASK-2.1: selects at most ONE technique prescription per report.
    /// Rules: validity-gated triggering (library), cooldown respected (a key on
    /// cooldown yields to the next-ranked), ranking by the session's weakest
    /// valid diagnostic dimension with CoachMemory weakest-scenario as tie-break,
    /// library order as the final fallback.
    /// </summary>
    public static class TechniquePrescriptionSelector
    {
        /// <summary>Maps each prescription to the diagnostic dimension it trains.</summary>
        public static string DimensionFor(string prescriptionKey) => prescriptionKey switch
        {
            "arm_over_wrist"          => "CloseRangeStatic",
            "decelerate_into_target"  => "CloseRangeStatic",
            "commit_full_motion"      => "CloseRangeStatic",
            "control_protocol"        => "CloseRangeStatic",
            "crosshair_preplacement"  => "CloseSwitching",
            "speed_progression"       => "CloseSwitching",
            "grip_tension_release"    => "HorizontalTracking",
            "eyes_lead_hands"         => "HorizontalTracking",
            "vertical_axis_training"  => "VerticalTracking",
            "peek_discipline_wait"    => "PeekReaction",
            "peek_commit_earlier"     => "PeekReaction",
            _                         => ""                     // miss_reset_routine: cross-dimension
        };

        private static double? DimensionScore(DiagnosticProfile? p, string dimension)
        {
            if (p == null || dimension.Length == 0) return null;
            double v = dimension switch
            {
                "CloseRangeStatic"   => p.CloseRangeStatic,
                "LongRangeStatic"    => p.LongRangeStatic,
                "HorizontalTracking" => p.HorizontalTracking,
                "VerticalTracking"   => p.VerticalTracking,
                "DiagonalTracking"   => p.DiagonalTracking,
                "CloseSwitching"     => p.CloseSwitching,
                "FarSwitching"       => p.FarSwitching,
                "PeekReaction"       => p.PeekReaction,
                _                    => 0
            };
            return v > 0 ? v : null;   // 0 = dimension never tested — not "weakest"
        }

        public static SelectedPrescription? Select(PrescriptionContext ctx)
        {
            // One open loop at a time: while a prescription is active and
            // unresolved, the verification loop owns the narrative (TASK-2.2) —
            // no new prescription is layered on top.
            if (ctx.Memory?.ActivePrescription != null) return null;

            var triggered = TechniquePrescriptionLibrary.Triggered(ctx);
            if (triggered.Count == 0) return null;

            // Cooldown: a key prescribed fewer than CooldownSessions drills ago
            // yields to the next-ranked candidate.
            int drillCount = ctx.Memory?.TotalDrillCount ?? 0;
            var cooldowns  = ctx.Memory?.PrescriptionCooldowns ?? new Dictionary<string, int>();
            var available  = triggered.Where(p =>
                    !cooldowns.TryGetValue(p.PrescriptionKey, out int lastAt)
                    || drillCount - lastAt >= p.CooldownSessions)
                .ToList();
            if (available.Count == 0) return null;

            // Rank: weakest valid diagnostic dimension first (lowest score);
            // prescriptions with no diagnostic data keep library order;
            // tie-break: practice drill targeting CoachMemory's weakest scenario.
            var diag = ctx.Memory?.LatestDiagnostic;
            string weakestScenario = ctx.Memory?.WeakestScenario ?? "";

            var ranked = available
                .Select((p, index) => new
                {
                    P = p,
                    Index = index,
                    Score = DimensionScore(diag, DimensionFor(p.PrescriptionKey)),
                    HitsWeakScenario = p.GetPracticeDrill(ctx).Scenario == weakestScenario
                })
                .OrderBy(x => x.Score ?? double.MaxValue)   // weakest dimension first
                .ThenByDescending(x => x.HitsWeakScenario)  // weakest-scenario tie-break
                .ThenBy(x => x.Index)                       // library order fallback
                .ToList();

            var chosen = ranked[0].P;
            return new SelectedPrescription(chosen, chosen.GetPracticeDrill(ctx));
        }

        /// <summary>
        /// Records the issued prescription: opens the verification loop and
        /// stamps the cooldown. BaselineValue is the verify metric's value now.
        /// </summary>
        public static void RecordPrescribed(
            CoachMemory memory, SelectedPrescription selected, double baselineValue)
        {
            memory.PrescriptionCooldowns[selected.Prescription.PrescriptionKey] = memory.TotalDrillCount;
            memory.ActivePrescription = new PrescriptionState
            {
                PrescriptionKey    = selected.Prescription.PrescriptionKey,
                PrescribedAt       = DateTime.Now,
                ScenarioContext    = "",   // set by the caller (the drill's scenario)
                VerifyMetric       = selected.Prescription.VerifyMetric,
                ExpectedDirection  = (int)selected.Prescription.ExpectedDirection,
                BaselineValue      = baselineValue,
                PracticeScenario   = selected.Drill.Scenario,
                PracticeVariant    = selected.Drill.Variant,
                PracticeDifficulty = selected.Drill.Difficulty,
                FocusCue           = selected.Drill.FocusCue
            };
        }
    }
}
