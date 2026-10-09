using System;
using System.Collections.Generic;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// CAT_GAME_TRANSFER (2026-08-19): translates a mechanical finding into what it looks
    /// like in the game the player actually plays.
    ///
    /// WHY: the coach measures general-purpose movement, but nobody trains to improve
    /// their "direction-change lag" — they train because they keep losing duels. A report
    /// that says "your second correction is late" is accurate and lands as jargon. The
    /// same finding as "in Valorant this is the re-adjust after a head-level flick, the
    /// one that gets you traded" is the same claim in the player's own language.
    ///
    /// ── THE HONESTY RULE THIS FILE MUST NEVER BREAK ──────────────────────────────────
    /// Every line here is DESCRIPTIVE, never PREDICTIVE. It says "this pattern shows up
    /// in your game as X". It must never say fixing it will raise a rank, win rate, K/D,
    /// or duel percentage — none of which this app measures, and all of which depend on
    /// positioning, utility, teamplay and game sense that aim training does not touch.
    /// A line that promises an outcome is a bug, not a copy tweak.
    ///
    /// NOT TO BE CONFUSED WITH <see cref="TransferObservationSource"/>, which answers a
    /// different and much stronger question — "is your training measurably showing up in
    /// your tracked sessions?" — and requires real cross-session data before it speaks.
    /// This class makes no measurement claim at all, so it needs no data and can never
    /// contradict that one. Three competing transfer phrasings shipped once before; keep
    /// the two responsibilities apart.
    /// </summary>
    public static class GameTransferPhrasing
    {
        /// <summary>
        /// Canonical game keys. These are matched loosely against the user's selected
        /// profile name so a renamed or custom profile ("Valorant (Custom)", "CS2") still
        /// resolves instead of silently falling back to generic.
        /// </summary>
        private const string Tactical = "tactical";   // CS2, Valorant, R6, Tarkov — hold angles, one-tap
        private const string Arena    = "arena";      // Apex, Overwatch, Halo — sustained tracking, movement
        private const string BuildBR  = "build";      // Fortnite — fast reposition, edit-peek timing
        private const string Generic  = "generic";

        /// <summary>
        /// Resolve a stored profile name to a phrasing family. Unknown or custom profiles
        /// fall back to <see cref="Generic"/>, which still says something mechanically
        /// specific — it just does not claim to know the player's game.
        /// </summary>
        public static string FamilyFor(string? profileName)
        {
            string n = (profileName ?? "").ToLowerInvariant();

            if (n.Contains("counter-strike") || n.Contains("cs2") || n.Contains("cs:go")
                || n.Contains("valorant") || n.Contains("siege") || n.Contains("rainbow")
                || n.Contains("tarkov"))
                return Tactical;

            if (n.Contains("apex") || n.Contains("overwatch") || n.Contains("halo")
                || n.Contains("call of duty") || n.Contains("warzone") || n.Contains("pubg"))
                return Arena;

            if (n.Contains("fortnite"))
                return BuildBR;

            return Generic;
        }

        // key → family → sentence. A missing family falls through to Generic; a missing
        // key returns null and the report simply says nothing, which is the correct
        // behaviour — filler that could apply to any finding teaches the reader to skip
        // the whole section.
        private static readonly Dictionary<string, Dictionary<string, string>> Lines = new()
        {
            ["click_point_overshoot"] = new()
            {
                [Tactical] = "In tactical shooters this is the flick that crosses the head and comes back — the shot lands a beat late because you are correcting after arriving, not before.",
                [Arena]    = "In arena shooters this is the flick that sails past a moving target, so you spend the first part of the fight hauling the crosshair back instead of firing.",
                [BuildBR]  = "In Fortnite this is the flick that overshoots after a reposition — you land past the target and burn the opening you just built.",
                [Generic]  = "This is the flick that travels past the target and needs a second motion to come back, which costs you the first shot.",
            },

            ["click_point_undershoot"] = new()
            {
                [Tactical] = "In tactical shooters this reads as stopping just short of the head and adding a small nudge — that nudge is the difference between a first-bullet kill and a trade.",
                [Arena]    = "In arena shooters this is arriving behind a strafing target and creeping onto it, which is why the first burst clips a shoulder instead of centre mass.",
                [BuildBR]  = "In Fortnite this shows up as landing just behind the target after a peek, then adjusting while they are already moving again.",
                [Generic]  = "This is stopping short of the target and adding a small correction, which delays the shot rather than missing it outright.",
            },

            ["movement_overshoot"] = new()
            {
                [Tactical] = "In tactical shooters this is why holding an angle feels fine but swinging one does not — the crosshair overshoots the moment you have to move it a long way.",
                [Arena]    = "In arena shooters this shows up on wide strafes: the crosshair keeps travelling after the target has already changed direction.",
                [BuildBR]  = "In Fortnite this is the crosshair drifting past a target during fast repositions, so every rotation costs you a re-aim.",
                [Generic]  = "This is the crosshair travelling further than the target actually moved, so every large adjustment needs a correction.",
            },

            ["decelerate_into_target"] = new()
            {
                [Tactical] = "In tactical shooters this is arriving at the head at full speed instead of settling onto it — the crosshair is technically on target for a moment, but not when you click.",
                [Arena]    = "In arena shooters this is why tracking feels twitchy: the hand never slows into the target, so the beam sits on either side of it rather than on it.",
                [BuildBR]  = "In Fortnite this is snapping to a target mid-reposition without ever settling, so the shot goes out while the crosshair is still moving.",
                [Generic]  = "This is arriving at the target at full speed rather than slowing into it, so the crosshair passes through the right spot instead of stopping there.",
            },

            ["crosshair_preplacement"] = new()
            {
                [Tactical] = "This is the one that costs tactical players the most: crosshair at the wrong height or off the angle before the duel starts, so every fight begins with a correction you did not need.",
                [Arena]    = "In arena shooters this is entering a fight with the crosshair low or trailing, giving up the opening moment while you bring it up.",
                [BuildBR]  = "In Fortnite this is peeking with the crosshair off the edge you are about to take, so the first shot is a correction rather than a shot.",
                [Generic]  = "This is starting a fight with the crosshair somewhere the target is not, which spends the opening moment on a correction.",
            },

            ["eyes_lead_hands"] = new()
            {
                [Tactical] = "In tactical shooters this is why a second target after the first kill feels slower — the hand starts moving before the eyes have actually locked the next head.",
                [Arena]    = "In arena shooters this shows up when switching between targets in a team fight: the hand leads, so it arrives roughly right and needs fixing.",
                [BuildBR]  = "In Fortnite this appears when a target breaks your line — the hand chases before the eyes have found where they went.",
                [Generic]  = "This is the hand starting before the eyes have locked the target, so the motion arrives near the target rather than on it.",
            },

            ["vertical_axis_training"] = new()
            {
                [Tactical] = "In tactical shooters this is head-height discipline — horizontal aim is fine, but elevation changes and stairs pull your shots off.",
                [Arena]    = "In arena shooters this is why targets on ramps, ziplines or in the air are harder for you than the same target on flat ground.",
                [BuildBR]  = "In Fortnite this matters constantly — builds make almost every fight a vertical one, and vertical is your weaker axis.",
                [Generic]  = "Your vertical aim lags your horizontal, so targets above or below you are harder than the same target at eye level.",
            },

            ["peek_discipline_wait"] = new()
            {
                [Tactical] = "In tactical shooters this is peeking before the crosshair is ready — you win the timing and lose the duel anyway.",
                [Arena]    = "In arena shooters this is committing out of cover before you are set, so the fight starts on the opponent's terms.",
                [BuildBR]  = "In Fortnite this is taking the peek before the edit is clean, firing while you are still resolving the window.",
                [Generic]  = "This is committing to the shot before the crosshair has settled, which turns a won timing into a lost duel.",
            },

            ["peek_commit_earlier"] = new()
            {
                [Tactical] = "In tactical shooters this is the opposite problem — you are ready, and you hesitate long enough for them to shoot first.",
                [Arena]    = "In arena shooters this is holding the peek too long once you are already set, letting the opponent reset the fight.",
                [BuildBR]  = "In Fortnite this is waiting past the window you opened, so the peek closes before you use it.",
                [Generic]  = "You are set and on target, then wait — the delay hands over an advantage you had already earned.",
            },

            ["miss_reset_routine"] = new()
            {
                [Tactical] = "In tactical shooters this is the spiral after a whiffed opening shot — one miss becomes three because the next shot is rushed.",
                [Arena]    = "In arena shooters this is a broken burst turning into a scramble instead of a reset.",
                [BuildBR]  = "In Fortnite this is panic-spraying after a missed first shot rather than repositioning and re-peeking.",
                [Generic]  = "One miss is making the next shot worse — the reset after a miss is costing you more than the miss did.",
            },

            ["commit_full_motion"] = new()
            {
                [Tactical] = "In tactical shooters this is the half-flick: the motion starts, doubts itself, and finishes as two small movements instead of one confident one.",
                [Arena]    = "In arena shooters this is hedging mid-motion, which reads as inconsistent tracking rather than a decision problem.",
                [BuildBR]  = "In Fortnite this is starting a turn and second-guessing it, arriving late to a target that has already moved on.",
                [Generic]  = "The motion is being started and then hedged, so one movement becomes two and the shot arrives late.",
            },

            ["grip_tension_release"] = new()
            {
                [Tactical] = "In tactical shooters this shows up late in a half — micro-corrections get shakier as you tense up, and holding a long angle gets harder.",
                [Arena]    = "In arena shooters tension shows up most in sustained tracking, where the beam judders instead of gliding.",
                [BuildBR]  = "In Fortnite this builds through a fast fight — the tighter the grip, the less precise the small adjustments become.",
                [Generic]  = "Grip tension is showing up in your small corrections, which get less precise the longer you play.",
            },

            ["movement_efficiency"] = new()
            {
                [Tactical] = "In tactical shooters this is wasted motion between angles — you get there, but by a longer route than the fight allows.",
                [Arena]    = "In arena shooters this is why tracking a strafing target feels like work: the crosshair covers more distance than the target does.",
                [BuildBR]  = "In Fortnite this compounds across a build fight, where every extra bit of crosshair travel is time you do not have.",
                [Generic]  = "Your crosshair is covering more distance than the target actually moved, which costs time on every adjustment.",
            },
        };

        /// <summary>
        /// The in-game translation for a prescription, or null when there is nothing
        /// specific and honest to say. Callers must treat null as "render nothing".
        /// </summary>
        public static string? For(string? prescriptionKey, string? profileName)
        {
            if (string.IsNullOrWhiteSpace(prescriptionKey)) return null;
            if (!Lines.TryGetValue(prescriptionKey, out var byFamily)) return null;

            string family = FamilyFor(profileName);

            if (byFamily.TryGetValue(family, out var line)) return line;
            if (byFamily.TryGetValue(Generic, out var fallback)) return fallback;

            return null;
        }

        /// <summary>Test/diagnostic helper: every prescription key this class can speak to.</summary>
        public static IReadOnlyCollection<string> KnownPrescriptionKeys => Lines.Keys;
    }
}
