using CleanAimTracker.Models;
using System.Collections.Generic;
using System.Linq;
using static CleanAimTracker.Services.AiCoachService;

namespace CleanAimTracker.Services
{
    public static class DrillPrescriptionEngine
    {
        /// <summary>
        /// Returns 1-2 specific drill prescriptions based on the diagnosed weak area
        /// and supporting metric context from the aim trainer result and tracker data.
        /// </summary>
        internal static List<DrillPrescription> Prescribe(
            AimTrainerResult result,
            CoachContext context,
            CoachMemory memory,
            SessionSummary? recentTracker = null)
        {
            var prescriptions = new List<DrillPrescription>();

            // ── RULE-NEW-1: WEAKNESS TARGETING ───────────────────────────────
            // Prescribe weakest scenario when it hasn't been trained enough recently
            if (!string.IsNullOrEmpty(memory.WeakestScenario)
                && memory.WeakestScenario != result.Scenario
                && memory.TotalDrillCount >= 3)
            {
                int recentWeakCount = memory.RecentDrills
                    .Count(d => d.Scenario == memory.WeakestScenario);

                if (recentWeakCount < 3)
                {
                    string weakReason = memory.WeakestScenario switch
                    {
                        "Sniper"         => "Your sniper accuracy is your lowest across all scenarios. " +
                                            "Regular precision work here will improve your patience and first-shot discipline — " +
                                            "skills that transfer to every other scenario.",
                        "Shotgun"        => "Shotgun is your weakest area right now. The reaction speed and commitment it builds " +
                                            "transfers directly to any scenario where you need to click fast and accurately.",
                        "SmgAr"          => "SMG / AR tracking is your weakest area. The sustained accuracy it trains is the " +
                                            "foundation of all tracking scenarios — time here raises your floor across everything.",
                        // TASK-33: New scenario prescriptions
                        "StaticClicking" => "Static Clicking is your weakest scenario. Precise placement on stationary targets " +
                                            "is the bedrock of all aim — fixing this raises your floor in every other scenario.",
                        "DynamicClicking"=> "Dynamic Clicking is where you're losing the most accuracy right now. " +
                                            "Clean clicks on moving targets reduce the noise in your overall aim profile.",
                        "Reactive"       => "Reactive is your weakest area. Building your recognition speed here directly " +
                                            "shortens your reaction time in all other scenarios.",
                        "AirTracking"    => "Air Tracking is your weakest pillar. The diagonal and parabolic paths expose " +
                                            "axis imbalances that can hide in standard tracking — worth targeting specifically.",
                        "Evasive"        => "Evasive targeting is your lowest score. The chase-and-corner pattern it builds " +
                                            "is the same skill you need when enemies strafe unpredictably in-game.",
                        "PeekTraining"   => "Peek Training is your weakest area. Refining your peek timing window is one of " +
                                            "the highest-value skills in any FPS — the return on these sessions is high.",
                        _                => $"You haven't trained {memory.WeakestScenario} much lately and it's where your accuracy is lowest. " +
                                            "Regular reps here will raise your overall ceiling.",
                    };

                    prescriptions.Add(new DrillPrescription
                    {
                        Scenario    = memory.WeakestScenario,
                        Difficulty  = result.Difficulty,
                        SubVariant  = "Standard",
                        DurationSec = result.DurationSeconds,
                        Reason      = weakReason,
                        FocusCue    = "Focus on accuracy over speed. This scenario is your growth zone right now."
                    });
                    return FinalizeAndReturn(prescriptions, memory);
                }
            }

            // ── RULE-NEW-2: PLATEAU BREAK ────────────────────────────────────
            if (memory.IsAccuracyPlateaued && memory.PlateauLength >= 3)
            {
                string nextDiff = result.Difficulty switch
                {
                    "Easy"   => "Medium",
                    "Medium" => "Hard",
                    "Hard"   => "Nightmare",
                    _        => result.Difficulty  // already Nightmare — handled by guard below
                };

                // Guard: do not prescribe Nightmare unless 5+ Hard sessions with avg accuracy >= 75%
                bool canPrescribeNightmare = false;
                if (nextDiff == "Nightmare")
                {
                    var hardSessions = memory.AllDrills
                        .Where(d => d.Scenario == result.Scenario && d.Difficulty == "Hard")
                        .ToList();
                    canPrescribeNightmare = hardSessions.Count >= 5
                        && hardSessions.Average(d => d.Accuracy) >= 75;
                    if (!canPrescribeNightmare)
                        nextDiff = "Hard"; // safe fallback
                }

                if (nextDiff != result.Difficulty)
                {
                    prescriptions.Add(new DrillPrescription
                    {
                        Scenario    = result.Scenario,
                        Difficulty  = nextDiff,
                        SubVariant  = "Standard",
                        DurationSec = result.DurationSeconds,
                        Reason      = $"You've been consistent at {result.Difficulty} for a while. " +
                                      $"Moving up to {nextDiff} will expose gaps that are hard to see at this level — " +
                                      "even if your score drops at first.",
                        FocusCue    = "Expect the score to drop. That's the point. Growth lives in the discomfort."
                    });
                    return FinalizeAndReturn(prescriptions, memory);
                }
            }

            // ── RULE-NEW-3: FOLLOW-UP REINFORCEMENT ─────────────────────────
            if (memory.LastPrescriptionFollowed
                && !string.IsNullOrEmpty(memory.LastPrescribedScenario))
            {
                // Check if the metric it targeted improved
                bool improved = false;
                if (memory.BaselineAccuracy.TryGetValue(memory.LastPrescribedScenario, out double baseAcc))
                    improved = result.Accuracy > baseAcc + 1.5;

                if (improved)
                {
                    string sameOrHigher = result.Difficulty == "Hard" ? "Hard" :
                        result.Difficulty switch
                        {
                            "Easy"   => "Medium",
                            "Medium" => "Medium",
                            _        => result.Difficulty
                        };
                    prescriptions.Add(new DrillPrescription
                    {
                        Scenario    = memory.LastPrescribedScenario,
                        Difficulty  = sameOrHigher,
                        SubVariant  = "Standard",
                        DurationSec = result.DurationSeconds,
                        Reason      = "What you did last session worked. Run it again and push a little harder this time.",
                        FocusCue    = "Same scenario, same focus — a little more intent."
                    });
                    return FinalizeAndReturn(prescriptions, memory);
                }
            }

            // ── RULE-NEW-4: CROSS-SCENARIO BALANCE ──────────────────────────
            if (memory.TotalDrillCount >= 8)
            {
                var untried = memory.SessionsPerScenario
                    .Where(kv => kv.Value == 0 && kv.Key != result.Scenario)
                    .Select(kv => kv.Key)
                    .FirstOrDefault();

                if (untried != null)
                {
                    string untriedReason = untried switch
                    {
                        "Sniper"          => "You haven't tried Sniper yet. It trains a completely different discipline — " +
                                             "patience and precision rather than speed. Worth seeing where you stand.",
                        "Shotgun"         => "You haven't tried Shotgun yet. Close-range reaction speed is a different skill " +
                                             "from everything else here — give it a session.",
                        "SmgAr"           => "You haven't tried SMG / AR yet. Dual target tracking is the hardest sustained " +
                                             "accuracy test in the trainer — see how your tracking holds up.",
                        // TASK-33: New scenario untried prescriptions
                        "StaticClicking"  => "You haven't tried Static Clicking yet. It's the purest accuracy test in the trainer — " +
                                             "a good baseline for your raw click precision.",
                        "DynamicClicking" => "You haven't tried Dynamic Clicking. It adds movement to accuracy testing " +
                                             "and exposes whether your aim holds under velocity changes.",
                        "Reactive"        => "You haven't tried Reactive yet. Timed exposure targets are one of the fastest ways " +
                                             "to measure and train your raw reaction time.",
                        "AirTracking"     => "You haven't tried Air Tracking. Non-linear movement paths are a different tracking " +
                                             "challenge from standard scenarios — worth testing your axis balance.",
                        "Evasive"         => "You haven't tried Evasive targeting. Targets that actively avoid your cursor test " +
                                             "a dimension of aim that standard scenarios don't cover.",
                        "PeekTraining"    => "You haven't tried Peek Training yet. It directly simulates the timing challenge " +
                                             "of dueling peeking enemies — highly transferable to real gameplay.",
                        _                 => $"You haven't tried {untried} yet. It trains a different part of your aim — worth seeing where you stand.",
                    };

                    prescriptions.Add(new DrillPrescription
                    {
                        Scenario    = untried,
                        Difficulty  = "Easy",
                        SubVariant  = "Standard",
                        DurationSec = 30,
                        Reason      = untriedReason,
                        FocusCue    = "Baseline session only. No expectations — just see what it feels like."
                    });
                    return FinalizeAndReturn(prescriptions, memory);
                }
            }

            // ── Rule 1: High correction sharpness → overshooting problem ──────
            if (context.WeakArea == "reaction"
                && recentTracker != null
                && recentTracker.CorrectionSharpness > 60)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Flicking",
                    Difficulty  = "Medium",
                    SubVariant  = "Single Target",
                    DurationSec = 60,
                    Reason      = "Your correction sharpness is high — you are overshooting and yanking back to correct. " +
                                  "Flicking Single Target trains you to land your first motion cleanly.",
                    FocusCue    = "Commit to one movement. Do not correct — if you miss, let it go and hit the next one."
                });
                return FinalizeAndReturn(prescriptions, memory);
            }

            // ── Rule 2: Low smoothness in Tracking ────────────────────────────
            if (context.WeakArea == "accuracy"
                && result.Scenario == "Tracking"
                && recentTracker != null
                && recentTracker.SmoothnessScore < 60)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Tracking",
                    Difficulty  = "Easy",
                    SubVariant  = "Smooth Arc",
                    DurationSec = 90,
                    Reason      = $"Your smoothness score is {recentTracker.SmoothnessScore:F0}/100 — " +
                                  "jitter is breaking your tracking. Easy Smooth Arc drills at lower speed " +
                                  "build the fluid movement pattern your muscle memory needs before adding speed.",
                    FocusCue    = "Keep your elbow on the desk. Move from the wrist only. Think fluid, not fast."
                });
                return FinalizeAndReturn(prescriptions, memory);
            }

            // ── Rule 3: Sensitivity too low for Precision ─────────────────────
            if (context.WeakArea == "accuracy"
                && result.Scenario == "Precision"
                && recentTracker != null
                && recentTracker.CmPer360 < 20)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Precision",
                    Difficulty  = "Easy",
                    SubVariant  = "Static",
                    DurationSec = 60,
                    Reason      = "Your sensitivity is too high for Precision — hitting small targets becomes " +
                                  "physically difficult at this sensitivity level. " +
                                  "Check the Recommend screen for your target range, adjust in-game, " +
                                  "then run Precision Static at Easy to recalibrate.",
                    FocusCue    = "Fix the settings first. More drilling at the wrong sensitivity builds bad habits."
                });
                return FinalizeAndReturn(prescriptions, memory);
            }

            // ── Rule 4: Sensitivity too high for Precision ────────────────────
            if (context.WeakArea == "accuracy"
                && result.Scenario == "Precision"
                && recentTracker != null
                && recentTracker.CmPer360 > 55)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Precision",
                    Difficulty  = "Easy",
                    SubVariant  = "Static",
                    DurationSec = 60,
                    Reason      = "Your sensitivity is too low for Precision — your cursor jumps too far per hand movement, " +
                                  "making small targets hard to land on consistently. " +
                                  "Lower your in-game sensitivity and retest on Easy before increasing difficulty.",
                    FocusCue    = "Slow is smooth. Smooth is accurate. Speed comes after the mechanics are clean."
                });
                return FinalizeAndReturn(prescriptions, memory);
            }

            // ── Rule 5: Low accuracy in Switching ─────────────────────────────
            if (context.WeakArea == "accuracy"
                && result.Scenario == "Switching"
                && result.Accuracy < 65)
            {
                string targetDiff = result.Difficulty == "Hard" || result.Difficulty == "Nightmare"
                    ? "Medium" : "Easy";
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Switching",
                    Difficulty  = targetDiff,
                    SubVariant  = "Two Target",
                    DurationSec = 60,
                    Reason      = $"Your Switching accuracy of {result.Accuracy:F0}% suggests you are rushing " +
                                  "the target acquisition — clicking before your crosshair is actually on target. " +
                                  $"Drop to {targetDiff} Two Target and prioritize click accuracy over speed.",
                    FocusCue    = "See the target clearly before you click. Speed will follow accuracy — not the other way around."
                });
                return FinalizeAndReturn(prescriptions, memory);
            }

            // ── Rule 6: Streak weakness → focus endurance ─────────────────────
            if (context.WeakArea == "streak"
                && result.MaxStreak < 8
                && result.DurationSeconds >= 60)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = result.Scenario,
                    Difficulty  = "Easy",
                    SubVariant  = "Standard",
                    DurationSec = 90,
                    Reason      = "Your streak breaks suggest focus dropping mid-session — " +
                                  "you start well but concentration fades. " +
                                  "Run a longer Easy session specifically to hold concentration the whole way through. " +
                                  "Endurance is a trainable skill.",
                    FocusCue    = "Set one goal: do not let your attention wander. Not speed, not accuracy — just presence."
                });
                return FinalizeAndReturn(prescriptions, memory);
            }

            // ── Rule 7: Slow reaction in Flicking ────────────────────────────
            if (context.WeakArea == "reaction"
                && result.Scenario == "Flicking"
                && result.AvgReactionMs > 400)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Flicking",
                    Difficulty  = "Medium",
                    SubVariant  = "Timed Pressure",
                    DurationSec = 60,
                    Reason      = $"Your {result.AvgReactionMs:F0}ms average reaction in Flicking is slow for this scenario. " +
                                  "Timed Pressure targets shrink if you wait — it forces faster target initiation " +
                                  "without training you to be sloppy.",
                    FocusCue    = "React to the appearance of the target, not to the position. Trust your first instinct."
                });
                return FinalizeAndReturn(prescriptions, memory);
            }

            // ── Rule 8: High idle percentage ──────────────────────────────────
            if (recentTracker != null && recentTracker.IdlePercentage > 40)
            {
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = "Tracking",
                    Difficulty  = "Medium",
                    SubVariant  = "Erratic",
                    DurationSec = 60,
                    Reason      = $"Your session had {recentTracker.IdlePercentage:F0}% idle time — " +
                                  "you are zoning out during training. Tracking Erratic forces constant active engagement " +
                                  "because the target changes direction unpredictably.",
                    FocusCue    = "No cruise control. Stay engaged every second."
                });
                return FinalizeAndReturn(prescriptions, memory);
            }

            // ── Rule 9: Good accuracy → difficulty progression ────────────────
            if (context.AccuracyGrade is "elite" or "good"
                && result.Difficulty != "Nightmare")
            {
                string nextDiff = result.Difficulty switch
                {
                    "Easy"   => "Medium",
                    "Medium" => "Hard",
                    "Hard"   => "Nightmare",
                    _        => "Hard"
                };
                prescriptions.Add(new DrillPrescription
                {
                    Scenario    = result.Scenario,
                    Difficulty  = nextDiff,
                    SubVariant  = "Standard",
                    DurationSec = result.DurationSeconds,
                    Reason      = $"You hit {result.Accuracy:F0}% accuracy on {result.Difficulty} — " +
                                  $"that earns a step up. {nextDiff} will expose the gaps that {result.Difficulty} hides.",
                    FocusCue    = "The difficulty is supposed to feel hard. That discomfort is where the growth happens."
                });
                return FinalizeAndReturn(prescriptions, memory);
            }

            // ── Rule 10: Fallback ─────────────────────────────────────────────
            prescriptions.Add(new DrillPrescription
            {
                Scenario    = result.Scenario,
                Difficulty  = result.Difficulty,
                SubVariant  = "Standard",
                DurationSec = 60,
                Reason      = $"Continue {result.Scenario} at {result.Difficulty} — " +
                              "build consistency at this level before stepping up.",
                FocusCue    = "Consistency over performance. Same scenario, same difficulty, until the accuracy stabilizes."
            });
            return FinalizeAndReturn(prescriptions, memory);
        }

        /// <summary>
        /// Saves the top prescription to UserSettings so follow-up coaching can reference it next session.
        /// Called at every return point to ensure the save always happens.
        /// </summary>
        private static List<DrillPrescription> FinalizeAndReturn(
            List<DrillPrescription> prescriptions,
            CoachMemory memory)
        {
            if (prescriptions.Count > 0)
            {
                try
                {
                    var s = SettingsService.Load();
                    s.LastPrescribedScenario   = prescriptions[0].Scenario;
                    s.LastPrescribedDifficulty = prescriptions[0].Difficulty;
                    s.LastPrescribedSessionIndex = memory.TotalDrillCount;
                    SettingsService.Save(s);
                }
                catch { /* non-critical — swallow */ }
            }
            return prescriptions;
        }
    }
}
