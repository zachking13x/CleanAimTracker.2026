using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>Context the headline template selector needs.</summary>
    public record HeadlineContext(
        double QualityScore,
        double? BaselineDelta,   // session quality − displayed rolling baseline; null = no baseline yet
        bool IsShortSession,
        bool IsLowActivity);

    /// <summary>The only object that may carry user-facing coach text to a surface.</summary>
    public record ComposedCoachReport(
        string Headline,
        string? Strength,
        string? Area,
        List<string> Tips,
        string? Prescription,
        List<string> SurvivingFactKeys);

    /// <summary>
    /// TASK-2.2: the arbitration core. Every coaching engine emits CoachObservation
    /// candidates; nothing writes user-facing coach text except through Compose.
    ///
    /// Rules, in order:
    ///   1. Validity filter — discard observations whose RequiredMetrics include an
    ///      invalid metric; low-activity sessions collapse to one neutral line.
    ///   2. Aspect dedup — one FactKey, one verdict; higher severity wins.
    ///   3. Caps — 1 strength + 1 area + 2 tips + 1 prescription.
    ///   4. Headline — derived FROM surviving observations via a single template;
    ///      short-session caveat is a modifier inside the template, never a glued
    ///      second sentence.
    ///   5. Tone floor — all-concern reports get honest-effort framing,
    ///      never "Great"/"Excellent"/"Elite".
    /// </summary>
    public static class CoachReportComposer
    {
        /// <summary>TASK-1.3: the single neutral line for low-activity sessions.</summary>
        public const string LowActivityHeadline =
            "Not enough sustained movement this session to assess aim mechanics.";

        public static ComposedCoachReport Compose(
            IEnumerable<CoachObservation> candidates,
            Func<string, bool> isMetricValid,
            HeadlineContext ctx)
        {
            // ── Rule 1a: low-activity collapse ──────────────────────────────
            if (ctx.IsLowActivity)
                return new ComposedCoachReport(
                    LowActivityHeadline, null, null, new List<string>(), null, new List<string>());

            // ── Rule 1b: validity filter ────────────────────────────────────
            var valid = (candidates ?? Enumerable.Empty<CoachObservation>())
                .Where(o => !string.IsNullOrEmpty(o.Message))
                .Where(o => o.RequiredMetrics.All(isMetricValid))
                .ToList();

            // ── Rule 2: aspect dedup — one FactKey, one verdict ─────────────
            // Higher severity wins; ties resolved by emission order (first wins).
            var survivors = valid
                .GroupBy(o => o.FactKey)
                .Select(g => g.OrderByDescending(o => o.Severity).First())
                .ToList();

            // ── Rule 3: caps ────────────────────────────────────────────────
            var strength = survivors
                .Where(o => o.Section == CoachSection.Strength)
                .OrderByDescending(o => o.Severity)
                .FirstOrDefault();

            var area = survivors
                .Where(o => o.Section == CoachSection.Area)
                .OrderByDescending(o => o.Severity)
                .FirstOrDefault();

            var tips = survivors
                .Where(o => o.Section == CoachSection.Tip)
                .OrderByDescending(o => o.Severity)
                .Take(2)
                .ToList();

            // ── TASK-2.3: prescription severity must match the verdict ──────
            // Area-led: remedial allowed (the prescription treats the area).
            // Strength-led / neutral: progression or maintenance only — a report
            // that just told the user they did well may not hand them a reset drill.
            var prescriptionCandidates = survivors
                .Where(o => o.Section == CoachSection.Prescription)
                .ToList();

            CoachObservation? prescription;
            if (area != null)
            {
                prescription = prescriptionCandidates
                    .OrderByDescending(o => o.PrescriptionType == PrescriptionType.Remedial)
                    .ThenByDescending(o => o.Severity)
                    .FirstOrDefault();
            }
            else
            {
                prescription = prescriptionCandidates
                    .Where(o => o.PrescriptionType is PrescriptionType.Progression
                                                   or PrescriptionType.Maintenance)
                    .OrderByDescending(o => o.PrescriptionType == PrescriptionType.Progression)
                    .ThenByDescending(o => o.Severity)
                    .FirstOrDefault();
            }

            // ── Rules 4 + 5: headline from surviving observations ───────────
            bool anyBehaviorChange =
                   (strength?.RequiresBehaviorChange ?? false)
                || (area?.RequiresBehaviorChange ?? false)
                || tips.Any(t => t.RequiresBehaviorChange);

            // An engine may supply its own single-template headline as a candidate
            // (drill coach: PB callouts, first-session baseline framing — "keep,
            // do not regress" items). It passes validity/dedup like everything
            // else, and the tone floor still applies: a praise-polarity headline
            // may not crown an all-concern report.
            var headlineCandidate = survivors
                .Where(o => o.Section == CoachSection.Headline)
                .OrderByDescending(o => o.Severity)
                .FirstOrDefault();

            if (headlineCandidate != null
                && strength == null && area != null
                && headlineCandidate.Polarity == ObservationPolarity.Strength)
            {
                headlineCandidate = null; // tone floor — fall through to honest template
            }

            string headline = headlineCandidate?.Message
                ?? SelectHeadline(ctx, strength != null, area != null, anyBehaviorChange);

            var survivingKeys = new List<string>();
            if (headlineCandidate != null) survivingKeys.Add(headlineCandidate.FactKey);
            if (strength != null) survivingKeys.Add(strength.FactKey);
            if (area != null) survivingKeys.Add(area.FactKey);
            survivingKeys.AddRange(tips.Select(t => t.FactKey));
            if (prescription != null) survivingKeys.Add(prescription.FactKey);

            return new ComposedCoachReport(
                Headline:          headline,
                Strength:          strength?.Message,
                Area:              area?.Message,
                Tips:              tips.Select(t => t.Message).ToList(),
                Prescription:      prescription?.Message,
                SurvivingFactKeys: survivingKeys);
        }

        // ── Headline template selector ──────────────────────────────────────
        // ONE template per outcome. The short-session caveat is a modifier
        // within the template ("directional read"), never a concatenated
        // stand-alone sentence. Tone floor: when no strength survives, tier
        // praise words are not used regardless of the raw score.
        private static string SelectHeadline(
            HeadlineContext ctx, bool hasStrength, bool hasArea, bool behaviorChange)
        {
            double q = ctx.QualityScore;
            string deltaClause = ctx.BaselineDelta is double d
                ? (d >= 0
                    ? $", {d:F0} points above your recent average"
                    : $", {Math.Abs(d):F0} points below your recent average")
                : "";

            // Tone floor: praise tiers only when a strength survived.
            if (hasStrength && !hasArea)
            {
                string tier = QualityTier(q);
                return ctx.IsShortSession
                    ? $"{tier} pace for a short session — {q:F0}/100{deltaClause}, directional read."
                    : $"{tier} session — quality {q:F0}/100{deltaClause}.";
            }

            if (hasStrength && hasArea)
            {
                string tier = QualityTier(q);
                return ctx.IsShortSession
                    ? $"{tier} pace for a short session — {q:F0}/100{deltaClause}, directional read, one fix flagged below."
                    : $"{tier} session — quality {q:F0}/100{deltaClause}, one fix flagged below.";
            }

            if (hasArea)
            {
                // All-concern: honest-effort framing, no praise words.
                // behaviorChange is implicit here — the area demands a change,
                // so the template points at it rather than at preservation.
                return ctx.IsShortSession
                    ? $"Directional read from a short session — {q:F0}/100{deltaClause}. The area below is the priority."
                    : $"Quality {q:F0}/100{deltaClause}. The area below is the priority.";
            }

            // Nothing survived: neutral statement of the number only.
            return ctx.IsShortSession
                ? $"Quality {q:F0}/100{deltaClause} — short session, directional read."
                : $"Quality {q:F0}/100{deltaClause}.";
        }

        private static string QualityTier(double score) => score switch
        {
            >= 80 => "Elite",
            >= 70 => "Strong",
            >= 60 => "Solid",
            >= 50 => "Below-average",
            _     => "Rough"
        };
    }
}
