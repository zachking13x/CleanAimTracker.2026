using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_ROUTINE (2026-08-12): builds today's session plan — an ordered list of drills
    /// with a stated reason for each.
    ///
    /// WHY IT EXISTS: opening CAT means facing a menu (scenario → variant → difficulty)
    /// at the exact moment motivation is lowest. Every engaged user of every competitor
    /// solves this by following a routine someone else designed — Voltaic publishes them,
    /// KovaaK's has playlists. It is also the finished form of what CAT already sells: a
    /// coach hands you a workout, not a single exercise.
    ///
    /// THE HONESTY GATE — the reason this class is careful rather than clever:
    /// a routine that CLAIMS to be personalised when there is no data behind it is the
    /// same lie as a coach diagnosing a habit it never measured. So:
    ///   • Every step carries the reason it was chosen, and the reason must be TRUE.
    ///   • "Your weakest area" is only ever said when a benchmark-scored axis actually
    ///     has data. Provisional axes are named as provisional.
    ///   • The verification step only appears when a prescription is genuinely open.
    ///     No open loop, no step — never a fabricated "the fix we're checking".
    ///   • With too little history the plan is honestly labelled a STARTER routine that
    ///     covers the main skills, not a personalised one.
    ///   • Difficulty never exceeds what the player has actually unlocked.
    ///
    /// This is DELIBERATELY not the default path. The dashboard offers it beside "pick
    /// your own" with equal weight — players who know what they want to train are not
    /// served by being funnelled, and taking that choice away would be a regression.
    /// </summary>
    public static class RoutineService
    {
        /// <summary>Below this many real drills, a plan cannot honestly claim to be personalised.</summary>
        public const int MinDrillsForPersonalised = 5;

        /// <summary>Approximate seconds per drill, used for the "N minutes" estimate.</summary>
        public const int SecondsPerDrill = 60;

        public enum StepKind { WarmUp, Weakness, Verify, Breadth }

        /// <summary>One drill in the plan. Reason is shown to the player verbatim.</summary>
        public sealed record RoutineStep(
            string    Scenario,
            string    Variant,
            string    Difficulty,
            StepKind  Kind,
            string    Reason);

        public sealed record Routine(
            IReadOnlyList<RoutineStep> Steps,
            bool   IsPersonalised,
            string Headline,
            string Subtitle)
        {
            public int EstimatedSeconds => Steps.Count * SecondsPerDrill;
        }

        /// <summary>
        /// Build today's routine. Never throws and never returns an empty plan — the
        /// worst case is a starter routine, honestly labelled as one.
        /// </summary>
        public static Routine Build(IEnumerable<AimTrainerResult>? history, UserSettings? settings)
        {
            settings ??= new UserSettings();
            var all = (history ?? Enumerable.Empty<AimTrainerResult>())
                      .Where(r => r != null && !r.IsAssessmentSession)
                      .OrderByDescending(r => r.Timestamp)
                      .ToList();

            var steps = new List<RoutineStep>();

            // ── Not enough history to personalise anything ────────────────────
            if (all.Count < MinDrillsForPersonalised)
            {
                foreach (var (scen, why) in StarterPlan())
                    steps.Add(new RoutineStep(scen, "Standard",
                        SafeDifficulty(scen, "Standard", "Easy", settings), StepKind.Breadth, why));

                return new Routine(steps, IsPersonalised: false,
                    Headline: "Starter session",
                    Subtitle: $"A spread across the main skills so your profile has something to read. " +
                              $"After {MinDrillsForPersonalised} drills this becomes personal to you.");
            }

            var radar = AimRadarService.Build(all);

            // ── 1. Warm-up: something they're genuinely GOOD at ───────────────
            // Confidence first, and it must be a drill they've actually played —
            // opening a routine with an unfamiliar scenario is the opposite of a warm-up.
            var strongAxis = radar.Axes
                .Where(a => a.HasData && a.IsBenchmarkRelative)
                .OrderByDescending(a => a.Score)
                .FirstOrDefault();

            string? warmScenario = strongAxis == null ? null : MostPlayedIn(strongAxis.Name, all);
            if (warmScenario != null)
            {
                steps.Add(new RoutineStep(warmScenario, VariantFor(warmScenario, all),
                    EasierThanUsual(warmScenario, all, settings), StepKind.WarmUp,
                    $"Warm up on your strongest area — {strongAxis!.Name.ToLowerInvariant()}."));
            }

            // ── 2 & 3. The weakness: two reps, second one harder ──────────────
            var weakAxis = radar.Axes
                .Where(a => a.HasData && a.IsBenchmarkRelative)
                .OrderBy(a => a.Score)
                .FirstOrDefault();

            if (weakAxis != null && weakAxis.Name != strongAxis?.Name)
            {
                // The drill MUST come from the axis being named. Falling back to an
                // arbitrary scenario would reproduce the split-brain defect where the
                // stated reason and the prescribed drill disagree.
                string? weakScenario = MostPlayedIn(weakAxis.Name, all)
                                    ?? AimRadarService.ScenariosFor(weakAxis.Name).FirstOrDefault();

                if (weakScenario != null)
                {
                    string variant = VariantFor(weakScenario, all);
                    string working = WorkingDifficulty(weakScenario, variant, all, settings);

                    // A provisional axis is named as provisional — a two-session reading
                    // is not a diagnosis, and saying so costs nothing.
                    string qualifier = weakAxis.IsProvisional
                        ? $"{weakAxis.Name} looks like your weakest area so far"
                        : $"{weakAxis.Name} is your weakest area";

                    // Speed isn't a drill, it's how you play one — so the instruction has
                    // to differ or the step reads as a non-sequitur.
                    string how = weakAxis.Name == AimRadarService.AxisSpeed
                        ? $"{DisplayName(weakScenario)} is where it's measured — push the pace, don't sit on targets."
                        : "This is the work.";

                    steps.Add(new RoutineStep(weakScenario, variant, working, StepKind.Weakness,
                        $"{qualifier} — {weakAxis.Score:F0}/100. {how}"));

                    string harder = StepUp(weakScenario, variant, working, all, settings);
                    if (harder != working)
                        steps.Add(new RoutineStep(weakScenario, variant, harder, StepKind.Weakness,
                            "Same drill one step up, while you're warm."));
                }
            }

            // ── 4. The verification drill — ONLY when a loop is actually open ──
            var open = settings.ActiveTechniquePrescription;
            if (open != null && !string.IsNullOrWhiteSpace(open.PracticeScenario))
            {
                string vScen = open.PracticeScenario;
                string vVar  = string.IsNullOrWhiteSpace(open.PracticeVariant) ? "Standard" : open.PracticeVariant;
                string vDiff = SafeDifficulty(vScen, vVar,
                    string.IsNullOrWhiteSpace(open.PracticeDifficulty) ? "Medium" : open.PracticeDifficulty,
                    settings, all);

                bool duplicate = steps.Any(s => s.Scenario == vScen && s.Difficulty == vDiff);
                if (!duplicate)
                    steps.Add(new RoutineStep(vScen, vVar, vDiff, StepKind.Verify,
                        "The fix your coach is tracking — this session is the check."));
            }

            // Degenerate case: nothing resolved (e.g. only auto-fire history).
            if (steps.Count == 0)
            {
                foreach (var (scen, why) in StarterPlan())
                    steps.Add(new RoutineStep(scen, "Standard",
                        SafeDifficulty(scen, "Standard", "Easy", settings), StepKind.Breadth, why));

                return new Routine(steps, IsPersonalised: false,
                    Headline: "Starter session",
                    Subtitle: "Not enough comparable data yet to target a weakness — this covers the main skills.");
            }

            string sub = weakAxis != null
                ? $"Built around {weakAxis.Name.ToLowerInvariant()}, your lowest benchmark-scored area."
                : "Built from your recent sessions.";

            return new Routine(steps, IsPersonalised: true,
                Headline: "Today's session", Subtitle: sub);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static (string Scenario, string Why)[] StarterPlan() => new[]
        {
            ("StaticClicking", "Start still — this is the baseline everything else builds on."),
            ("Flicking",       "Snapping onto a new target."),
            ("Tracking",       "Staying glued to something that moves."),
        };

        /// <summary>The scenario the player has actually played most within an axis, or null.</summary>
        private static string? MostPlayedIn(string axis, List<AimTrainerResult> all)
        {
            var scenarios = AimRadarService.ScenariosFor(axis);
            if (scenarios.Count == 0) return null;

            return all.Where(r => scenarios.Contains(r.Scenario))
                      .GroupBy(r => r.Scenario)
                      .OrderByDescending(g => g.Count())
                      .Select(g => g.Key)
                      .FirstOrDefault();
        }

        /// <summary>Their most-used variant for a scenario, defaulting to Standard.</summary>
        private static string VariantFor(string scenario, List<AimTrainerResult> all)
        {
            string? v = all.Where(r => r.Scenario == scenario && !string.IsNullOrWhiteSpace(r.SubVariant))
                           .GroupBy(r => r.SubVariant)
                           .OrderByDescending(g => g.Count())
                           .Select(g => g.Key)
                           .FirstOrDefault();
            return string.IsNullOrWhiteSpace(v) ? "Standard" : v;
        }

        /// <summary>The difficulty they most recently played a scenario at, clamped to unlocked.</summary>
        private static string WorkingDifficulty(string scenario, string variant,
                                                List<AimTrainerResult> all, UserSettings settings)
        {
            string? recent = all.Where(r => r.Scenario == scenario && !string.IsNullOrWhiteSpace(r.Difficulty))
                                .Select(r => r.Difficulty)
                                .FirstOrDefault();
            return SafeDifficulty(scenario, variant, recent ?? "Medium", settings, all);
        }

        /// <summary>One rung below their working level — a warm-up should not be a fight.</summary>
        private static string EasierThanUsual(string scenario, List<AimTrainerResult> all, UserSettings settings)
        {
            string variant = VariantFor(scenario, all);
            string working = WorkingDifficulty(scenario, variant, all, settings);
            int i = Array.IndexOf(Order, working);
            return i <= 0 ? working : Order[i - 1];
        }

        /// <summary>One rung up, but never past what they can actually reach.</summary>
        private static string StepUp(string scenario, string variant, string from,
                                     List<AimTrainerResult> all, UserSettings settings)
        {
            int i = Array.IndexOf(Order, from);
            if (i < 0 || i >= Order.Length - 1) return from;
            return SafeDifficulty(scenario, variant, Order[i + 1], settings, all);
        }

        private static readonly string[] Order = { "Easy", "Medium", "Hard", "Nightmare" };

        /// <summary>
        /// Clamp a desired difficulty to the hardest rung the player can actually reach.
        ///
        /// "Can reach" is the union of the formal unlock flags AND difficulties they have
        /// demonstrably COMPLETED. Those two disagree in real data: Zach's settings list
        /// only Easy as unlocked for Flicking while his history has 21 Flicking sessions
        /// logged at Medium. Trusting the flags alone handed a 236-session player a
        /// routine of nothing but Easy drills — technically safe, obviously wrong, and
        /// exactly the kind of inaccuracy that makes a routine feel like it isn't
        /// paying attention.
        ///
        /// Evidence of completion is the stronger signal: you cannot fake having
        /// finished a drill at a difficulty.
        /// </summary>
        private static string SafeDifficulty(string scenario, string variant, string desired,
                                             UserSettings settings,
                                             List<AimTrainerResult>? all = null)
        {
            int want = Array.IndexOf(Order, desired);
            if (want < 0) want = 1;

            // Walk down to the hardest tier the trainer will actually let them start.
            for (int i = want; i >= 0; i--)
                if (ScenarioDifficultyService.IsSelectableInTrainer(Order[i], all))
                    return Order[i];

            return "Easy";
        }

        /// <summary>Trainer-facing scenario name for prose.</summary>
        private static string DisplayName(string scenario) => scenario switch
        {
            "StaticClicking"  => "Static Clicking",
            "DynamicClicking" => "Dynamic Clicking",
            "SpeedSwitching"  => "Speed Switching",
            "AirTracking"     => "Air Tracking",
            "PeekTraining"    => "Peek Training",
            "HeadshotStrafes" => "Headshot Strafes",
            "PeekClick"       => "Peek & Click",
            "HeadTrack"       => "Track the Head",
            "SmgAr"           => "SMG / AR",
            _                 => scenario,
        };
    }
}
