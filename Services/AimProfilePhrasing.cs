using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// COACH_AIM_PROFILE Gate 2: assembles the profile's user-facing text from a FIXED
    /// catalog of human-authored fragments. The hard constraint: variety comes ONLY from
    /// selecting among authored phrasings (and which true strength leads) — never from
    /// generating new claims. A fragment is selectable only when the underlying metric
    /// condition is true, and every number it cites comes from a validated aggregate.
    /// Every emitted line traces to a template in <see cref="Templates"/> (enforced by test).
    /// </summary>
    public static class AimProfilePhrasing
    {
        // ── Captions (fixed, authored) ─────────────────────────────────────────
        public const string RecomputeCaption =
            "Your aim profile reads your last {0} sessions, not just today — so it shifts as the bigger picture does, not on a single good or bad run.";
        public const string PerGameDisclaimer =
            "This reads your training data. It can't yet see how your aim holds up in an actual match — per-game analysis (Fortnite, etc.) is coming: soon you'll be able to run a session while you play and have the coach factor in the real game.";

        public static readonly string[] NeedMore =
        {
            "Keep going — your aim profile unlocks in {0} more session(s). It reads your last several sessions, not just today, so it needs a few to see the real pattern.",
            "Almost there — {0} more session(s) and your player profile comes online. It waits for a handful of sessions so the read is your pattern, not a one-off.",
            "Run {0} more session(s) and the coach can characterize how you play. A profile off one or two sessions would be guessing — this one won't.",
        };

        // ── Lead strength / rule-out (T1.3: strength before weakness) ──────────
        public static readonly string[] CleanMechConsistency =
        {
            "Start with what's working: your movement is controlled — shot-to-shot consistency around {0}/100, with no shaky-hand or wild-flick problem.",
            "The good news first — your mechanics are clean. Consistency sits near {0}/100 and your motion arrives without the over-flick most players fight.",
            "Your foundation is solid: movement consistency around {0}/100 means your hand repeats itself every rep — that's the hard part, and you have it.",
            "Let's name the strength — you're not shaky. Consistency near {0}/100 and controlled travel say your mechanics aren't the problem.",
            "First, the rule-out: your aim path is steady. Consistency around {0}/100 — no jitter, no wild corrections to clean up here.",
        };
        public static readonly string[] CleanMechGeneric =
        {
            "Start with the rule-out: your movement is controlled and repeatable — no shaky-hand or wild-flick problem to fix.",
            "The good news first — your mechanics are clean. The motion arrives without the over-flick most players fight.",
            "Your foundation is solid: the hand does the same thing every rep, and the path stays controlled.",
            "Let's name the strength — you're not shaky, and you're not crashing through targets. The mechanics are sound.",
            "First, what's NOT wrong: your travel is smooth and controlled — that's not where your points are leaking.",
        };
        public static readonly string[] StrengthAccuracy =
        {
            "Start with the strength: you're landing {0}% of your shots — the placement is genuinely there.",
            "What's working: {0}% accuracy says your aim finds the target reliably.",
            "Lead with the good: {0}% accuracy is a real strength — you put the crosshair where it needs to be.",
            "First, the strength — {0}% landing. The aim itself is dependable.",
            "Your placement is the asset here: {0}% accuracy, repeatable.",
        };
        public static readonly string[] NoStrength =
        {
            "Your fundamentals are still forming — too early to crown a standout strength, so the move is reps. The clearest read today is your {0}% accuracy.",
            "No single strength stands out yet — that's normal this early. The honest read is {0}% accuracy and a lot of room to grow.",
            "Still building the picture — nothing's a clear strength yet. Today's anchor is {0}% accuracy; keep stacking sessions.",
            "Too soon to name a standout — the data's young. What's solid right now is {0}% accuracy to build from.",
            "The profile's still forming — no headline strength yet, just {0}% accuracy and the reps ahead to shape it.",
        };

        // ── The unlock (one thing to work on) ──────────────────────────────────
        public static readonly string[] LateClicker =
        {
            "The one thing to work on: about {0}% of your clicks land past center while your motion arrives clean — you're committing the click a beat late, not missing with the aim. Click on arrival, don't drift onto it.",
            "Your unlock is timing, not movement: ~{0}% of clicks land long off a clean path. The fix is the click, not the flick — pull the trigger the instant the crosshair touches center.",
            "Here's the gap — {0}% of your clicks drift past center even though the aim got there clean. That's a late click. Fire on arrival and the number falls.",
            "The thing capping your score: clicks landing long on ~{0}% of shots, with clean motion underneath. You arrive right and click a hair late — tighten that and it's free accuracy.",
            "One fix, high payoff: about {0}% overshoot from a clean path means the click is late, not the aim. Commit on contact, not after.",
        };
        // short_clicker — the mirror of LateClicker: clicks land SHORT of center off a
        // clean path. Added 2026-07-06 with the directional V2 metric; before that the
        // profile had no way to name an undershoot habit.
        public static readonly string[] ShortClicker =
        {
            "The one thing to work on: about {0}% of your clicks land short of center while your motion arrives clean — you're easing off before the crosshair gets there. Finish the motion, then click.",
            "Your unlock is commitment, not movement: ~{0}% of clicks stop short off a clean path. Let the crosshair reach center before you fire — the aim is doing its job, the click is early.",
            "Here's the gap — {0}% of your clicks land short even though the motion underneath is clean. You're clicking on the way in instead of on arrival. Ride it all the way to center.",
            "The thing capping your score: clicks stopping short on ~{0}% of shots, with clean motion underneath. You slow down and fire before center — carry the motion through and it's free accuracy.",
            "One fix, high payoff: about {0}% undershoot from a clean path means you're bailing on the motion early. Commit the full distance, then click.",
        };
        public static readonly string[] CeilingGapUnlock =
        {
            "Your unlock is the speed ceiling: you hold {0}% on Easy/Medium but drop to {1}% when it speeds up. The accuracy is built — now push pace until it catches up.",
            "Here's the gap — {0}% accuracy at lower speed, {1}% when it's fast. The placement is there; the speed ceiling just hasn't caught up to it.",
            "The one thing: you're {0}% on Easy/Medium, {1}% on Hard. That fall-off is speed, not aim — train faster commits and the gap closes.",
            "Your read is a pace ceiling: {0}% drops to {1}% under speed. Step the difficulty up and let accuracy dip while it rebuilds at the new tempo.",
            "What's holding you: {0}% → {1}% as it speeds up. The aim's done; the speed is the next build.",
        };
        public static readonly string[] PushPaceOpportunity =
        {
            "The opportunity isn't a flaw — it's speed. You've got room to push the pace without losing the placement you've built.",
            "Nothing's broken — the next gain is tempo. Spend some of that accuracy headroom on faster commits.",
            "Your move is pace: the mechanics and placement hold, so push the speed until accuracy just starts to wobble, then settle there.",
            "The one thing left is speed, not correction. Force faster decisions — the accuracy can take it.",
            "You're past the fixing stage here — now it's about spending your control on more pace.",
        };

        // ── Headroom story (full read connective) ──────────────────────────────
        // AUDIT FIX (2026-07-06): these strings previously cited the best-vs-average
        // reaction gap as proof of "speed in reserve". A session best is the MINIMUM
        // of dozens of hits — it sits far below the mean for every human, always —
        // so the claim was a statistical artifact. The pattern now speaks only to
        // what the data supports: accuracy is high, so pace is the growth lever.
        public static readonly string[] AccurateButSlow =
        {
            "You're precise first, fast second — {0}% accuracy with pace to spare. Push the tempo and let the accuracy defend itself.",
            "Accurate with room to spend: {0}% landing. The next gain isn't tighter placement — it's making faster commits at the same placement.",
            "The read is simple — {0}% accuracy means the control is built. Growth now comes from speeding up, not tightening up.",
            "At {0}% accuracy you're playing inside your ceiling. Force quicker decisions until the number just starts to wobble — that edge is the next level.",
            "Precise and unhurried: {0}% accuracy. Spend some of that control on tempo — faster first moves, faster clicks, same placement.",
        };
        public static readonly string[] SafePlayer =
        {
            "You're playing well within your accuracy ceiling — {0}% with errors low both ways. There's room to push the pace without losing the placement.",
            "Safe and clean: {0}% accuracy with low error in both directions. You can afford to take more risk on speed.",
            "You're not leaking points to mistakes — {0}% accuracy, controlled both ways. The growth now is pace, not precision.",
            "A low-risk read: {0}% landing, few wild misses. That control is exactly what lets you push speed next.",
            "You stay inside your limits — {0}% accuracy, errors contained. Spend some of that safety on tempo.",
        };

        // ── The one fix (full read) ────────────────────────────────────────────
        public static readonly string[] LateClickerFix =
        {
            "Drill it on Precision: click the instant the crosshair reaches center — not after. The overshoot number is what tells you it's working.",
            "The rep: Precision, and a single cue — fire on arrival, don't ride onto the target. Watch your overshoot drop session over session.",
            "Practice it deliberately on Precision: arrive, click, stop. No drifting onto center. Overshoot is the metric that proves the change.",
        };
        public static readonly string[] ShortClickerFix =
        {
            "Drill it on Precision: carry the crosshair all the way to center, THEN click. The undershoot number is what tells you it's working.",
            "The rep: Precision, one cue — full motion, land on center, fire. No clicking on the approach. Watch undershoot fall session over session.",
            "Practice it deliberately on Precision: commit the whole distance, settle on center, click. Undershoot is the metric that proves the change.",
        };
        public static readonly string[] CeilingGapFix =
        {
            "The rep: step the difficulty up and let accuracy dip on purpose — then rebuild it at the faster pace. Miss fast, not slow.",
            "Drill it by overspeeding: push to the next difficulty, accept ~75% while you force quicker commits, then claw the accuracy back.",
            "Train the ceiling directly — faster scenarios, faster decisions. Let the score drop first; that's the growth.",
        };
        public static readonly string[] PaceFix =
        {
            "The rep: force faster commits until accuracy just wobbles, then hold there. That edge is where speed actually grows.",
            "Drill speed on purpose — quicker first move, quicker click — and let accuracy ride the line. Settle once it stabilizes.",
            "Push tempo deliberately: shave the hesitation between seeing the target and committing. The accuracy follows.",
        };

        // ── Per-scenario note (full) + trend notes ─────────────────────────────
        public static readonly string[] PrimaryScenarioNote =
        {
            "Most of this read comes from your {0} sessions — that's where your volume is, so it's where the signal is strongest.",
            "This is weighted toward {0}, since that's what you run most. Branch out and the profile widens with you.",
            "Your {0} reps carry this read — the most data, the clearest pattern.",
        };
        public static readonly string[] TrendUp =
        {
            "And it's trending the right way — your recent sessions read sharper than your earlier ones.",
            "Momentum's with you: the last stretch is stronger than where you started.",
            "The arrow's up — recent sessions are outpacing the older ones in the window.",
        };
        public static readonly string[] TrendDown =
        {
            "Heads up — your recent sessions have slipped a touch from earlier; worth a focused reset.",
            "One note: the last few are softer than your earlier ones. Not a trend yet, but watch it.",
            "Recent sessions dipped slightly from where you were — a clean warmup might bring it back.",
        };

        /// <summary>Every authored template — the test asserts no emitted line falls outside this set.</summary>
        public static readonly IReadOnlyList<string> Templates =
            new[] { RecomputeCaption, PerGameDisclaimer }
            .Concat(NeedMore)
            .Concat(CleanMechConsistency).Concat(CleanMechGeneric)
            .Concat(StrengthAccuracy).Concat(NoStrength)
            .Concat(LateClicker).Concat(ShortClicker).Concat(CeilingGapUnlock).Concat(PushPaceOpportunity)
            .Concat(AccurateButSlow).Concat(SafePlayer)
            .Concat(LateClickerFix).Concat(ShortClickerFix).Concat(CeilingGapFix).Concat(PaceFix)
            .Concat(PrimaryScenarioNote).Concat(TrendUp).Concat(TrendDown)
            .ToList();

        // ── Render ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Fills <paramref name="p"/>.Condensed and FullLines from authored fragments.
        /// <paramref name="rotationSeed"/> advances the variant choice so consecutive
        /// opens read differently (pass e.g. a per-open counter or TotalDrillCount).
        /// </summary>
        public static void Render(PlayerAimProfile p, int rotationSeed)
        {
            if (!p.HasEnoughData)
            {
                string nm = Fill(Pick(NeedMore, rotationSeed), p.SessionsNeeded);
                p.Condensed = nm;
                p.FullLines = new List<string> { nm };
                return;
            }

            double M(string k) => p.Metrics.TryGetValue(k, out var a) ? a.Mean : 0;
            bool Fired(string k) => p.Patterns.Contains(k);

            // ── Lead strength / rule-out (always strength before weakness) ──────
            string lead;
            if (Fired("clean_mechanics") && p.Metrics.ContainsKey("MovementConsistency")
                && p.Metrics["MovementConsistency"].Band == MetricBand.Good)
                lead = Fill(Pick(CleanMechConsistency, rotationSeed), M("MovementConsistency"));
            else if (Fired("clean_mechanics"))
                lead = Pick(CleanMechGeneric, rotationSeed);
            else if (p.StrengthMetrics.Contains("Accuracy"))
                lead = Fill(Pick(StrengthAccuracy, rotationSeed), M("Accuracy"));
            else if (p.StrengthMetrics.Count > 0)
                lead = Pick(CleanMechGeneric, rotationSeed);   // a genuine strength exists, phrase as rule-out
            else
                lead = Fill(Pick(NoStrength, rotationSeed), M("Accuracy"));

            // ── The unlock (one thing) ──────────────────────────────────────────
            string unlock;
            string fix;
            if (Fired("late_clicker"))
            {
                unlock = Fill(Pick(LateClicker, rotationSeed), M("OvershootPct"));
                fix    = Pick(LateClickerFix, rotationSeed);
            }
            else if (Fired("short_clicker"))
            {
                unlock = Fill(Pick(ShortClicker, rotationSeed), M("UndershootPct"));
                fix    = Pick(ShortClickerFix, rotationSeed);
            }
            else if (Fired("ceiling_gap"))
            {
                unlock = Fill(Pick(CeilingGapUnlock, rotationSeed), p.CeilingEasyMedAcc, p.CeilingHardAcc);
                fix    = Pick(CeilingGapFix, rotationSeed);
            }
            else
            {
                unlock = Pick(PushPaceOpportunity, rotationSeed);
                fix    = Pick(PaceFix, rotationSeed);
            }

            // ── Headroom story (connective, full read) ──────────────────────────
            string? headroom = null;
            if (Fired("accurate_but_slow"))
                headroom = Fill(Pick(AccurateButSlow, rotationSeed),
                                M("Accuracy"), p.MeanBestReactionMs, p.MeanAvgReactionMs);
            else if (Fired("safe_player"))
                headroom = Fill(Pick(SafePlayer, rotationSeed), M("Accuracy"));

            // ── Trend note (only on a clear PRIMARY-scenario accuracy trend) ────
            // Same-scenario so a change in what you're playing can't fake a trend.
            string? trend = p.PrimaryAccuracyTrend switch
            {
                TrendDir.Up   => Pick(TrendUp, rotationSeed),
                TrendDir.Down => Pick(TrendDown, rotationSeed),
                _             => null
            };

            // ── Condensed: lead strength + the one unlock + (optional) trend ────
            var condensed = new List<string> { lead, unlock };
            if (trend != null) condensed.Add(trend);
            p.Condensed = string.Join(" ", condensed);

            // ── Full: rule-out → headroom story → the unlock → the fix → scenario → trend ─
            var full = new List<string> { lead };
            if (headroom != null) full.Add(headroom);
            full.Add(unlock);
            full.Add(fix);
            if (!string.IsNullOrEmpty(p.PrimaryScenario))
                full.Add(Fill(Pick(PrimaryScenarioNote, rotationSeed), p.PrimaryScenario));
            if (trend != null) full.Add(trend);
            p.FullLines = full;
        }

        // Deterministic rotation: a different seed lands on a different variant.
        private static string Pick(string[] pool, int seed)
            => pool[((seed % pool.Length) + pool.Length) % pool.Length];

        private static string Fill(string template, params object[] args)
            => string.Format(CultureInfo.InvariantCulture, template,
                             args.Select(a => a is double d ? (object)d.ToString("F0", CultureInfo.InvariantCulture) : a)
                                 .ToArray());
    }
}
