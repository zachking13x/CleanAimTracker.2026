using System.Collections.Generic;
using CleanAimTracker.Models;

namespace CleanAimTracker.Services
{
    /// <summary>
    /// Central lookup for every scenario + variant combination's training focus and
    /// mental cue text shown in the DrillInstructionCard overlay.
    /// All text is kept here so it can be updated without touching scenario logic.
    /// </summary>
    public static class ScenarioInfoRegistry
    {
        // Key: "ScenarioName|VariantName"
        private static readonly Dictionary<string, ScenarioInfo> _registry =
            new()
            {
                // ── Clicking pillar ──────────────────────────────────────────

                // StaticClicking
                ["StaticClicking|Standard"] = new ScenarioInfo
                {
                    Scenario      = "StaticClicking",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Clean first-shot placement on stationary targets",
                    MentalCue     = "Commit to the dot, not the edge"
                },
                ["StaticClicking|Micro"] = new ScenarioInfo
                {
                    Scenario      = "StaticClicking",
                    Variant       = "Micro",
                    Pillar        = "Clicking",
                    TrainingFocus = "Precision on small targets — calm your micro-corrections",
                    MentalCue     = "Slow is smooth, smooth is fast"
                },
                ["StaticClicking|Cluster"] = new ScenarioInfo
                {
                    Scenario      = "StaticClicking",
                    Variant       = "Cluster",
                    Pillar        = "Clicking",
                    TrainingFocus = "Rapid multi-target acquisition in dense groups",
                    MentalCue     = "Read the cluster, pick the cleanest angle"
                },
                ["StaticClicking|Confirmation"] = new ScenarioInfo
                {
                    Scenario      = "StaticClicking",
                    Variant       = "Confirmation",
                    Pillar        = "Clicking",
                    TrainingFocus = "Accuracy under self-imposed pressure — confirm before firing",
                    MentalCue     = "Feel the crosshair settle, then click"
                },

                // DynamicClicking
                ["DynamicClicking|Standard"] = new ScenarioInfo
                {
                    Scenario      = "DynamicClicking",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Lead a single moving target with consistent timing",
                    MentalCue     = "Match velocity, then click"
                },
                ["DynamicClicking|Bounce"] = new ScenarioInfo
                {
                    Scenario      = "DynamicClicking",
                    Variant       = "Bounce",
                    Pillar        = "Clicking",
                    TrainingFocus = "React to direction changes without panicking",
                    MentalCue     = "Predict the bounce, don't chase it"
                },
                ["DynamicClicking|Arc"] = new ScenarioInfo
                {
                    Scenario      = "DynamicClicking",
                    Variant       = "Arc",
                    Pillar        = "Clicking",
                    TrainingFocus = "Track curved paths — rotate wrist, not whole arm",
                    MentalCue     = "Arc with the target, click at the apex"
                },
                ["DynamicClicking|Accelerating"] = new ScenarioInfo
                {
                    Scenario      = "DynamicClicking",
                    Variant       = "Accelerating",
                    Pillar        = "Clicking",
                    TrainingFocus = "Adapt aim speed to a target that is constantly changing pace",
                    MentalCue     = "Let speed build before you commit"
                },

                // Reactive
                ["Reactive|Standard"] = new ScenarioInfo
                {
                    Scenario      = "Reactive",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Instant first-shot on unpredictable targets",
                    MentalCue     = "Eyes find it first, wrist follows"
                },
                ["Reactive|SpeedBurst"] = new ScenarioInfo
                {
                    Scenario      = "Reactive",
                    Variant       = "SpeedBurst",
                    Pillar        = "Clicking",
                    TrainingFocus = "Click at peak speed — minimum lag between vision and fire",
                    MentalCue     = "Don't think, just click the dot"
                },
                ["Reactive|Blink"] = new ScenarioInfo
                {
                    Scenario      = "Reactive",
                    Variant       = "Blink",
                    Pillar        = "Clicking",
                    TrainingFocus = "React to flashing targets — sharpen peripheral alertness",
                    MentalCue     = "See flash, click immediately"
                },
                ["Reactive|Chaotic"] = new ScenarioInfo
                {
                    Scenario      = "Reactive",
                    Variant       = "Chaotic",
                    Pillar        = "Clicking",
                    TrainingFocus = "Maintain composure when everything is moving at once",
                    MentalCue     = "Pick one, ignore the rest"
                },

                // Flicking
                ["Flicking|Standard"] = new ScenarioInfo
                {
                    Scenario      = "Flicking",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Large arm sweeps that land precisely on target",
                    MentalCue     = "Flick from the elbow, stop with the wrist"
                },
                ["Flicking|Micro"] = new ScenarioInfo
                {
                    Scenario      = "Flicking",
                    Variant       = "Micro",
                    Pillar        = "Clicking",
                    TrainingFocus = "Short sharp micro-flicks with high landing accuracy",
                    MentalCue     = "Small movement, big commitment"
                },
                ["Flicking|Wide"] = new ScenarioInfo
                {
                    Scenario      = "Flicking",
                    Variant       = "Wide",
                    Pillar        = "Clicking",
                    TrainingFocus = "Long-distance flicks across the full mousepad",
                    MentalCue     = "Drive with the shoulder, land with the wrist"
                },
                ["Flicking|Reaction"] = new ScenarioInfo
                {
                    Scenario      = "Flicking",
                    Variant       = "Reaction",
                    Pillar        = "Clicking",
                    TrainingFocus = "Flick on appearance — pure visual reaction speed",
                    MentalCue     = "Trust the movement before it's finished"
                },

                // Precision
                ["Precision|Standard"] = new ScenarioInfo
                {
                    Scenario      = "Precision",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Deliberate cursor placement — remove all extra movement",
                    MentalCue     = "One path, one click"
                },
                ["Precision|Small"] = new ScenarioInfo
                {
                    Scenario      = "Precision",
                    Variant       = "Small",
                    Pillar        = "Clicking",
                    TrainingFocus = "Consistent accuracy on the smallest hitboxes",
                    MentalCue     = "Breathe out, then click"
                },
                ["Precision|Speed"] = new ScenarioInfo
                {
                    Scenario      = "Precision",
                    Variant       = "Speed",
                    Pillar        = "Clicking",
                    TrainingFocus = "Balance speed and accuracy — don't sacrifice one for the other",
                    MentalCue     = "Fast enough to matter, slow enough to hit"
                },
                ["Precision|Chaos"] = new ScenarioInfo
                {
                    Scenario      = "Precision",
                    Variant       = "Chaos",
                    Pillar        = "Clicking",
                    TrainingFocus = "Maintain accuracy when targets appear unpredictably",
                    MentalCue     = "Stay centred, let targets come to you"
                },

                // Sniper
                ["Sniper|Standard"] = new ScenarioInfo
                {
                    Scenario      = "Sniper",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Long-range precision with high first-shot penalty for misses",
                    MentalCue     = "Wait for stillness, fire in the gap"
                },
                ["Sniper|Burst"] = new ScenarioInfo
                {
                    Scenario      = "Sniper",
                    Variant       = "Burst",
                    Pillar        = "Clicking",
                    TrainingFocus = "Quick scope acquisition without losing precision",
                    MentalCue     = "ADS — find — fire — reset"
                },
                ["Sniper|No-Scope"] = new ScenarioInfo
                {
                    Scenario      = "Sniper",
                    Variant       = "No-Scope",
                    Pillar        = "Clicking",
                    TrainingFocus = "Instinctive crosshair placement without scope dependency",
                    MentalCue     = "Hip-fire muscle memory over aim dependency"
                },
                ["Sniper|Moving"] = new ScenarioInfo
                {
                    Scenario      = "Sniper",
                    Variant       = "Moving",
                    Pillar        = "Clicking",
                    TrainingFocus = "Lead tracking on distant moving targets",
                    MentalCue     = "Follow the movement, fire ahead of it"
                },

                // Shotgun
                ["Shotgun|Standard"] = new ScenarioInfo
                {
                    Scenario      = "Shotgun",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Close-range timing accuracy — gap-close and click simultaneously",
                    MentalCue     = "Get close, confirm centre, fire"
                },
                ["Shotgun|Pump"] = new ScenarioInfo
                {
                    Scenario      = "Shotgun",
                    Variant       = "Pump",
                    Pillar        = "Clicking",
                    TrainingFocus = "Consistent pump-timing with accurate shot placement",
                    MentalCue     = "Reset — aim — fire at the right cadence"
                },
                ["Shotgun|Slug"] = new ScenarioInfo
                {
                    Scenario      = "Shotgun",
                    Variant       = "Slug",
                    Pillar        = "Clicking",
                    TrainingFocus = "Single-shot accuracy at medium range with shotgun patterns",
                    MentalCue     = "Treat every shot like a sniper shot at close range"
                },
                ["Shotgun|Aggressive"] = new ScenarioInfo
                {
                    Scenario      = "Shotgun",
                    Variant       = "Aggressive",
                    Pillar        = "Clicking",
                    TrainingFocus = "Maintain accuracy while closing distance rapidly",
                    MentalCue     = "Move and shoot without breaking aim"
                },

                // SmgAr
                ["SmgAr|Standard"] = new ScenarioInfo
                {
                    Scenario      = "SmgAr",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Sustained fire control with tight burst accuracy",
                    MentalCue     = "Control the spray, hit the head"
                },
                ["SmgAr|Spray"] = new ScenarioInfo
                {
                    Scenario      = "SmgAr",
                    Variant       = "Spray",
                    Pillar        = "Clicking",
                    TrainingFocus = "Full-auto spray pattern compensation at close range",
                    MentalCue     = "Pull down and in, follow the pattern"
                },
                ["SmgAr|Tap"] = new ScenarioInfo
                {
                    Scenario      = "SmgAr",
                    Variant       = "Tap",
                    Pillar        = "Clicking",
                    TrainingFocus = "Disciplined single-tap fire for medium-range accuracy",
                    MentalCue     = "One bullet, one target centre"
                },
                ["SmgAr|Burst"] = new ScenarioInfo
                {
                    Scenario      = "SmgAr",
                    Variant       = "Burst",
                    Pillar        = "Clicking",
                    TrainingFocus = "3-round burst accuracy — control the second and third shot",
                    MentalCue     = "First shot free, second shot guided"
                },

                // ── Tracking pillar ──────────────────────────────────────────

                // Tracking
                ["Tracking|Standard"] = new ScenarioInfo
                {
                    Scenario      = "Tracking",
                    Variant       = "Standard",
                    Pillar        = "Tracking",
                    TrainingFocus = "Smooth sustained cursor follow with minimal error",
                    MentalCue     = "Glide with the target, don't chase it"
                },
                ["Tracking|Smooth"] = new ScenarioInfo
                {
                    Scenario      = "Tracking",
                    Variant       = "Smooth",
                    Pillar        = "Tracking",
                    TrainingFocus = "Eliminate micro-corrections — maximise cursor smoothness",
                    MentalCue     = "Wrist loose, movement fluid"
                },
                ["Tracking|Evasive"] = new ScenarioInfo
                {
                    Scenario      = "Tracking",
                    Variant       = "Evasive",
                    Pillar        = "Tracking",
                    TrainingFocus = "Recover tracking quickly after unpredictable direction changes",
                    MentalCue     = "Don't panic — small correction, back on target"
                },
                ["Tracking|Reaction"] = new ScenarioInfo
                {
                    Scenario      = "Tracking",
                    Variant       = "Reaction",
                    Pillar        = "Tracking",
                    TrainingFocus = "Instant re-acquisition after the target warps position",
                    MentalCue     = "Find it fast, settle smooth"
                },

                // AirTracking
                ["AirTracking|Diagonal"] = new ScenarioInfo
                {
                    Scenario      = "AirTracking",
                    Variant       = "Diagonal",
                    Pillar        = "Tracking",
                    TrainingFocus = "Track targets on diagonal paths — balance X and Y simultaneously",
                    MentalCue     = "Equal pull on both axes"
                },
                ["AirTracking|Parabolic"] = new ScenarioInfo
                {
                    Scenario      = "AirTracking",
                    Variant       = "Parabolic",
                    Pillar        = "Tracking",
                    TrainingFocus = "Follow arc trajectories — anticipate gravity-like curves",
                    MentalCue     = "Track the arc, not the current position"
                },
                ["AirTracking|Jetpack"] = new ScenarioInfo
                {
                    Scenario      = "AirTracking",
                    Variant       = "Jetpack",
                    Pillar        = "Tracking",
                    TrainingFocus = "Rapid vertical acceleration bursts — stay on target through spikes",
                    MentalCue     = "Absorb the vertical, keep the horizontal"
                },
                ["AirTracking|Falling"] = new ScenarioInfo
                {
                    Scenario      = "AirTracking",
                    Variant       = "Falling",
                    Pillar        = "Tracking",
                    TrainingFocus = "Downward trajectory tracking — control downward pressure without over-pulling",
                    MentalCue     = "Follow gravity, don't fight it"
                },

                // ── Switching pillar ─────────────────────────────────────────

                // Switching
                ["Switching|Standard"] = new ScenarioInfo
                {
                    Scenario      = "Switching",
                    Variant       = "Standard",
                    Pillar        = "Switching",
                    TrainingFocus = "Efficient multi-target flicks — build scan-and-destroy rhythm",
                    MentalCue     = "Hit and immediately scan for the next"
                },
                ["Switching|Speed"] = new ScenarioInfo
                {
                    Scenario      = "Switching",
                    Variant       = "Speed",
                    Pillar        = "Switching",
                    TrainingFocus = "Maximise switch speed without sacrificing hit rate",
                    MentalCue     = "The next target is already selected in your mind"
                },
                ["Switching|Precision"] = new ScenarioInfo
                {
                    Scenario      = "Switching",
                    Variant       = "Precision",
                    Pillar        = "Switching",
                    TrainingFocus = "Clean first-shot on each switch — no spraying allowed",
                    MentalCue     = "Land clean or don't fire"
                },
                ["Switching|Chaos"] = new ScenarioInfo
                {
                    Scenario      = "Switching",
                    Variant       = "Chaos",
                    Pillar        = "Switching",
                    TrainingFocus = "Prioritise and eliminate among many simultaneous threats",
                    MentalCue     = "Closest threat first, then next closest"
                },

                // SpeedSwitching
                ["SpeedSwitching|Standard"] = new ScenarioInfo
                {
                    Scenario      = "SpeedSwitching",
                    Variant       = "Standard",
                    Pillar        = "Switching",
                    TrainingFocus = "Rapid target acquisition at high target density",
                    MentalCue     = "Flick clean and keep moving"
                },
                ["SpeedSwitching|Burst"] = new ScenarioInfo
                {
                    Scenario      = "SpeedSwitching",
                    Variant       = "Burst",
                    Pillar        = "Switching",
                    TrainingFocus = "Eliminate a burst of targets as fast as possible without misses",
                    MentalCue     = "Sprint through — accuracy must not drop"
                },
                ["SpeedSwitching|TwoTarget"] = new ScenarioInfo
                {
                    Scenario      = "SpeedSwitching",
                    Variant       = "TwoTarget",
                    Pillar        = "Switching",
                    TrainingFocus = "Optimise the back-and-forth flick between two fixed positions",
                    MentalCue     = "Build the muscle memory path between A and B"
                },
                ["SpeedSwitching|Grid"] = new ScenarioInfo
                {
                    Scenario      = "SpeedSwitching",
                    Variant       = "Grid",
                    Pillar        = "Switching",
                    TrainingFocus = "Navigate a structured grid — efficient angle routing",
                    MentalCue     = "Row by row, column by column — make it mechanical"
                },

                // Evasive
                ["Evasive|Standard"] = new ScenarioInfo
                {
                    Scenario      = "Evasive",
                    Variant       = "Standard",
                    Pillar        = "Switching",
                    TrainingFocus = "Chase a target that actively moves away from your cursor",
                    MentalCue     = "Lead ahead of where it's running to"
                },
                ["Evasive|Aggressive"] = new ScenarioInfo
                {
                    Scenario      = "Evasive",
                    Variant       = "Aggressive",
                    Pillar        = "Switching",
                    TrainingFocus = "High-speed evasion — commit to the path and don't back off",
                    MentalCue     = "Aggressive angle cuts through evasion"
                },
                ["Evasive|Predictive"] = new ScenarioInfo
                {
                    Scenario      = "Evasive",
                    Variant       = "Predictive",
                    Pillar        = "Switching",
                    TrainingFocus = "Anticipate the evasion pattern before it happens",
                    MentalCue     = "Read the pattern, cut it off"
                },
                ["Evasive|Teleport"] = new ScenarioInfo
                {
                    Scenario      = "Evasive",
                    Variant       = "Teleport",
                    Pillar        = "Switching",
                    TrainingFocus = "Instant re-acquisition after target warps — pure reaction",
                    MentalCue     = "New position, immediate response"
                },

                // PeekTraining
                ["PeekTraining|WideSwing"] = new ScenarioInfo
                {
                    Scenario      = "PeekTraining",
                    Variant       = "WideSwing",
                    Pillar        = "Switching",
                    TrainingFocus = "Fire in the optimal wide-peek window — not early, not late",
                    MentalCue     = "Wait for the shoulder, then fire"
                },
                ["PeekTraining|Jiggle"] = new ScenarioInfo
                {
                    Scenario      = "PeekTraining",
                    Variant       = "Jiggle",
                    Pillar        = "Switching",
                    TrainingFocus = "Resist flinch-firing on a jiggling target — time the exposure",
                    MentalCue     = "Ignore the fake — wait for full exposure"
                },
                ["PeekTraining|JumpPeek"] = new ScenarioInfo
                {
                    Scenario      = "PeekTraining",
                    Variant       = "JumpPeek",
                    Pillar        = "Switching",
                    TrainingFocus = "Track a target jumping over cover and fire mid-air",
                    MentalCue     = "Aim at the peak of the jump"
                },
                ["PeekTraining|CounterStrafe"] = new ScenarioInfo
                {
                    Scenario      = "PeekTraining",
                    Variant       = "CounterStrafe",
                    Pillar        = "Switching",
                    TrainingFocus = "Fire in the brief stationary window after a counter-strafe",
                    MentalCue     = "Stop — click — move. The stop is the shot"
                },

                // ── Special ──────────────────────────────────────────────────

                // Adaptive
                ["Adaptive|Standard"] = new ScenarioInfo
                {
                    Scenario      = "Adaptive",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Mixed scenario drill — adapt quickly between aim styles",
                    MentalCue     = "Reset your mental state between each transition"
                },

                // WarmUp
                ["WarmUp|Standard"] = new ScenarioInfo
                {
                    Scenario      = "WarmUp",
                    Variant       = "Standard",
                    Pillar        = "Clicking",
                    TrainingFocus = "Light full-body warm-up — loosen wrist and forearm",
                    MentalCue     = "No pressure — feel the mouse, not the target"
                },
            };

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the <see cref="ScenarioInfo"/> for the given scenario and variant,
        /// or a sensible default when the combination is not in the registry.
        /// </summary>
        public static ScenarioInfo Get(string scenario, string variant)
        {
            string key = $"{scenario}|{variant}";

            if (_registry.TryGetValue(key, out var info))
                return info;

            // Fallback: try the Standard variant of the same scenario
            string fallbackKey = $"{scenario}|Standard";
            if (_registry.TryGetValue(fallbackKey, out var fallback))
                return fallback;

            // Ultimate fallback
            return new ScenarioInfo
            {
                Scenario      = scenario,
                Variant       = variant,
                Pillar        = "",
                TrainingFocus = "Stay focused and aim accurately",
                MentalCue     = "Find the centre, click clean"
            };
        }
    }
}
