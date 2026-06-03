using CleanAimTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CleanAimTracker.Services
{
    public static class TrackerCoachService
    {
        public record TrackerCoachReport(
            string Headline,
            List<string> Observations,
            List<string> Suggestions,
            string NextDrillSuggestion
        );

        public static TrackerCoachReport Analyze(
            SessionSummary session,
            List<SessionSummary> history,
            CoachMemory? memory = null)
        {
            var observations = new List<string>();
            var suggestions  = new List<string>();

            int sessionIndex = history?.Count ?? 0; // for variant rotation

            // ── Context qualifier ─────────────────────────────────────────────
            bool isShortSession = session.SessionSeconds < 120;
            string dataQualifier = isShortSession
                ? "Short session — observations are directional only. "
                : "";

            // ── Observation 1: Smoothness vs recent average ────────────────────
            if (history.Count >= 3)
            {
                double avgSmooth = history
                    .Where(h => h.Timestamp < session.Timestamp)
                    .OrderByDescending(h => h.Timestamp)
                    .Take(5)
                    .Average(h => h.SmoothnessScore);

                double delta = session.SmoothnessScore - avgSmooth;

                if (session.SmoothnessScore < 55)
                    observations.Add(
                        $"Smoothness was {session.SmoothnessScore:F0}/100 this session " +
                        $"(your recent average is {avgSmooth:F0}). " +
                        "Lower smoothness during gameplay often reflects tension or fatigue — " +
                        "not necessarily a mechanics problem.");
                else if (delta > 10)
                    observations.Add(
                        $"Smoothness was {session.SmoothnessScore:F0}/100 — " +
                        $"{delta:F0} points above your recent average. " +
                        "Good session for movement quality.");
            }

            // ── Observation 2: Correction sharpness ───────────────────────────
            if (session.CorrectionSharpness > 65)
                observations.Add(
                    $"Correction sharpness was high at {session.CorrectionSharpness:F0} — " +
                    "more post-shot corrections than usual. " +
                    "This can mean overshoot from adrenaline or trying to move faster than your mechanics allow.");
            else if (session.CorrectionSharpness < 30)
                observations.Add(
                    $"Very low correction sharpness ({session.CorrectionSharpness:F0}) — " +
                    "you were committing to first motions cleanly. That is good mechanics.");

            // ── Observation 3: Flick pattern ──────────────────────────────────
            if (session.FlickCount > 0)
            {
                double largeRatio = session.LargeFlickCount / (double)session.FlickCount;
                if (largeRatio > 0.5)
                    observations.Add(
                        $"{session.LargeFlickCount} large flicks out of {session.FlickCount} total — " +
                        "more than half were large movements. " +
                        "Large flicks in gameplay usually mean reactive repositioning or getting caught out of position. " +
                        "Could be a game sense issue as much as an aim issue.");
                else if (session.FlickCount > 20 && largeRatio < 0.2)
                    observations.Add(
                        $"{session.FlickCount} flicks, mostly small corrections — " +
                        "suggests controlled, precise target acquisition this session.");
            }

            // ── Observation 4: Idle percentage ────────────────────────────────
            if (session.IdlePercentage > 50)
                observations.Add(
                    $"{session.IdlePercentage:F0}% of the session was low-movement time — " +
                    "normal for games with a lot of navigation and positioning. " +
                    "The aim metrics reflect only the active movement windows.");

            // ── Observation 5: Drill accuracy context — TASK-05: recency guard ─
            // Only meaningful if drill data is recent (within 14 days)
            try
            {
                if (memory != null && memory.RecentDrills.Count >= 3)
                {
                    bool isRecent = (DateTime.Now - memory.RecentDrills[0].Timestamp).TotalDays <= 14;
                    if (isRecent)
                    {
                        double drillAvg = memory.RecentDrills.Take(5).Average(d => d.Accuracy);
                        observations.Add(
                            $"Your recent drill accuracy averages {drillAvg:F0}%. " +
                            "That is your mechanics baseline — your in-game performance reflects " +
                            "both your aim mechanics and game sense combined.");
                    }
                    else
                    {
                        observations.Add(
                            "Your drill data is a few weeks old. Run a session in the aim trainer to give the coach " +
                            "fresh data to compare against your in-game performance.");
                    }
                }
                else if (memory == null)
                {
                    // Fallback when no memory context (e.g. legacy call path)
                    var drillHistory = AimTrainerStorage.LoadAll();
                    if (drillHistory.Count >= 3)
                    {
                        double drillAvgAccuracy = drillHistory
                            .OrderByDescending(r => r.Timestamp)
                            .Take(5)
                            .Average(r => r.Accuracy);
                        observations.Add(
                            $"Your recent drill accuracy averages {drillAvgAccuracy:F0}%. " +
                            "That is your mechanics baseline — your in-game performance reflects " +
                            "both your aim mechanics and game sense combined.");
                    }
                }
            }
            catch { /* non-critical — swallow and continue */ }

            // ── TASK-10 NEW OBSERVATIONS ──────────────────────────────────────

            // OBSERVATION-NEW-1: IN-GAME VS DRILL COMPARISON
            if (memory != null
                && memory.RecentDrills.Count >= 3
                && history?.Count >= 2
                && memory.MostPlayedScenario.Length > 0
                && memory.BaselineAccuracy.TryGetValue(memory.MostPlayedScenario, out double drillBaseline))
            {
                double smoothRatio = drillBaseline > 0
                    ? session.SmoothnessScore / drillBaseline
                    : 1.0;

                int v = sessionIndex % 3;
                if (smoothRatio < 0.7)
                {
                    observations.Add(v switch
                    {
                        0 => "Your in-game movement is rougher than your drill performance suggests it should be. " +
                             "That gap usually means nerves, game speed, or sensitivity feeling different in a real match. " +
                             "Your drills are working — the transfer just needs more time.",
                        1 => $"Your drills average {drillBaseline:F0}% accuracy but in-game smoothness is lower than expected. " +
                             "Real matches add pressure that drills don't — your mechanics are there, the consistency will follow.",
                        _ => "In-game movement is below your drill baseline. That's normal at this stage. " +
                             "The gap closes as the muscle memory builds. Keep drilling before you play."
                    });
                }
                else if (smoothRatio >= 0.9)
                {
                    observations.Add(v switch
                    {
                        0 => "Your in-game movement matches your drill performance closely. The training is transferring well.",
                        1 => $"In-game smoothness is tracking with your drill accuracy of {drillBaseline:F0}%. The habits are holding under real match pressure.",
                        _ => "Good alignment between your drill performance and in-game mechanics. That's the outcome you're training for."
                    });
                }
            }

            // OBSERVATION-NEW-2: SESSION FATIGUE DETECTION
            if (history?.Count >= 3)
            {
                var recentHistory = history
                    .Where(h => h.Timestamp < session.Timestamp)
                    .OrderByDescending(h => h.Timestamp)
                    .Take(5)
                    .ToList();

                if (recentHistory.Count >= 3)
                {
                    double recentAvg = recentHistory.Average(h => h.OverallQualityScore);
                    if (recentAvg > 0 && session.OverallQualityScore < recentAvg * 0.85)
                    {
                        int v = sessionIndex % 3;
                        observations.Add(v switch
                        {
                            0 => "This session was noticeably below your recent average. One off session isn't a concern — " +
                                 "but if it happens two or three times in a row, it's worth checking if you're playing tired or on tilt. Quality over quantity always.",
                            1 => $"Quality {session.OverallQualityScore:F0}/100 vs your recent average of {recentAvg:F0}. " +
                                 "That kind of drop is usually conditions, not skill. Note the time of day and how you felt.",
                            _ => "Below your recent baseline this session. Fatigue, tilt, and time of day affect aim more than most people expect. " +
                                 "Track the pattern — if it keeps happening at the same time, that's your answer."
                        });
                    }
                }
            }

            // OBSERVATION-NEW-3: CONSISTENCY TREND
            if (history?.Count >= 4)
            {
                var prior4 = history
                    .Where(h => h.Timestamp < session.Timestamp)
                    .OrderByDescending(h => h.Timestamp)
                    .Take(4)
                    .ToList();

                if (prior4.Count >= 4)
                {
                    // Check if smoothness has been improving or declining
                    double oldest = (prior4[2].SmoothnessScore + prior4[3].SmoothnessScore) / 2.0;
                    double newest = (prior4[0].SmoothnessScore + prior4[1].SmoothnessScore) / 2.0;
                    double trend  = newest - oldest;

                    int v = sessionIndex % 3;
                    if (trend > 8)
                    {
                        observations.Add(v switch
                        {
                            0 => "Your in-game movement has been getting smoother over your last four sessions. " +
                                 "That's the kind of improvement that shows up in your accuracy in ranked.",
                            1 => $"Smoothness trending up — {oldest:F0} to {newest:F0} over your last four sessions. The fundamentals are compounding.",
                            _ => "Consistent improvement in movement quality over four sessions. That's the trajectory you want."
                        });
                    }
                    else if (trend < -8)
                    {
                        // Count consecutive declining sessions
                        int decliningCount = 0;
                        for (int i = 0; i < prior4.Count - 1; i++)
                        {
                            if (prior4[i].SmoothnessScore < prior4[i + 1].SmoothnessScore) decliningCount++;
                            else break;
                        }

                        if (decliningCount >= 2)
                        {
                            observations.Add(v switch
                            {
                                0 => "Your in-game consistency has dipped three sessions in a row. " +
                                     "Before your next session, run a quick Tracking drill to reset your baseline.",
                                1 => "Three consecutive sessions of declining smoothness. That pattern points to accumulated tension or overtraining. " +
                                     "One rest day, then drill before you play.",
                                _ => "Movement quality has been dropping session over session. Take a reset day — one easy drill, no pressure, just feel."
                            });
                        }
                    }
                }
            }

            // OBSERVATION-NEW-4: SENSITIVITY FLAG
            if (session.CmPer360 > 0)
            {
                if (session.CmPer360 < 20 && session.SmoothnessScore < 60)
                {
                    observations.Add(
                        "Your sensitivity is on the low end and your in-game movement is rough. " +
                        "Those two things together usually mean your sensitivity is actually too low for how you naturally move. " +
                        "Try bumping it up slightly and see if the roughness decreases.");
                }
                else if (session.CmPer360 > 55 && session.CorrectionSharpness > 70)
                {
                    observations.Add(
                        "Your sensitivity is high and you're making a lot of corrections. " +
                        "High sensitivity amplifies small hand movements — if your aim feels twitchy in game, that's likely why.");
                }
            }

            // ── Suggestions ───────────────────────────────────────────────────
            if (session.CorrectionSharpness > 65 && session.SmoothnessScore < 60)
                suggestions.Add(
                    "High corrections and low smoothness together suggest tension. " +
                    "Before your next session try 2 minutes of slow Tracking Easy — " +
                    "it primes fluid movement before you need precision.");

            if (session.SmoothnessScore < 50)
                suggestions.Add(
                    "Low smoothness in this session is worth tracking over time. " +
                    "If it consistently drops during gameplay compared to drills, " +
                    "it points to grip tension under pressure — a very common and very fixable pattern.");

            if (session.LargeFlickCount > session.SmallFlickCount && session.FlickCount > 5)
                suggestions.Add(
                    "More large flicks than small suggests reactive aiming — " +
                    "running Flicking Timed Pressure in your next drill session " +
                    "builds faster target initiation which reduces the need for large corrections.");

            // ── TASK-04: Guaranteed fallback — suggestions must never be empty ──
            if (suggestions.Count == 0)
            {
                // Check if quality trend is declining
                bool trendDeclining = false;
                if (history?.Count >= 3)
                {
                    var priorSessions = history
                        .Where(h => h.Timestamp < session.Timestamp)
                        .OrderByDescending(h => h.Timestamp)
                        .Take(4)
                        .ToList();
                    if (priorSessions.Count >= 3)
                    {
                        double recent2 = priorSessions.Take(2).Average(h => h.OverallQualityScore);
                        double older   = priorSessions.Skip(2).Take(2).Where(h => h.OverallQualityScore > 0)
                                             .Select(h => h.OverallQualityScore)
                                             .DefaultIfEmpty(recent2)
                                             .Average();
                        trendDeclining = recent2 < older - 5;
                    }
                }

                if (trendDeclining)
                {
                    suggestions.Add($"Your quality trend has been declining over your last {Math.Min(history!.Count, 4)} sessions. " +
                                    "Before your next session run a quick 30-second Tracking drill to reset your baseline — " +
                                    "it takes less than a minute and usually fixes a drift in movement habits.");
                }
                else
                {
                    // Pick lowest-scoring metric and give a targeted suggestion
                    double smoothScore  = session.SmoothnessScore;
                    double velocScore   = session.MovementConsistency;
                    double idleScore    = 100.0 - session.IdlePercentage; // higher idle% = lower score

                    if (smoothScore <= velocScore && smoothScore <= idleScore)
                    {
                        suggestions.Add("Your next focus is smoothness — try to make each mouse movement one deliberate motion " +
                                        "rather than a move and correct. Slow down slightly and see if the smoothness score rises.");
                    }
                    else if (velocScore <= smoothScore && velocScore <= idleScore)
                    {
                        suggestions.Add("Your movement speed varies a lot between actions. Try to maintain a more consistent pace — " +
                                        "same speed for both precise aim and repositioning.");
                    }
                    else
                    {
                        suggestions.Add("You had a lot of idle time this session. If that was between games that's fine — " +
                                        "but if it was mid-game, staying active even when not shooting keeps your mechanics warm.");
                    }
                }
            }

            // Absolute safety net
            if (suggestions.Count == 0)
                suggestions.Add("Keep the consistency going. The best thing you can do right now is run another session tomorrow — " +
                                "habits build faster with regular short sessions than occasional long ones.");

            // ── Next drill suggestion ─────────────────────────────────────────
            string nextDrill;
            if (session.CorrectionSharpness > 65)
                nextDrill = "Flicking Single Target — Medium — focus on clean first motion, no corrections";
            else if (session.SmoothnessScore < 55)
                nextDrill = "Tracking Smooth Arc — Easy — slow deliberate movement to reset muscle memory";
            else if (session.LargeFlickCount > session.SmallFlickCount)
                nextDrill = "Flicking Timed Pressure — Medium — builds faster target initiation";
            else
                nextDrill = "Tracking Standard — Medium — maintain the consistency you showed this session";

            // ── Headline — TASK-03: diagnostic before calling session "tough" ───
            // IdlePercentage > 40 is a meaningful idle penalty driver
            // MovementConsistency < 40 indicates low velocity stability
            bool highIdlePenalty = session.IdlePercentage > 40;
            bool lowVelocity     = session.MovementConsistency < 40;
            bool lowSmoothness   = session.SmoothnessScore < 50;
            bool lowOverall      = session.OverallQualityScore < 60;

            string headline;
            if (lowOverall && highIdlePenalty && !lowSmoothness)
            {
                // Score is dragged down by idle time — movement itself was fine
                headline = $"Your movement quality was strong this session — smoothness was {session.SmoothnessScore:F0}/100. " +
                           "The overall score is lower because of idle time, which usually means pausing mid-session " +
                           "or time between games. Not a performance concern.";
            }
            else if (lowOverall && lowVelocity && !lowSmoothness)
            {
                // Score dragged by velocity inconsistency, not mechanics
                headline = $"Strong movement quality this session at {session.SmoothnessScore:F0}/100. " +
                           "The score dip came from velocity consistency — your speed varied a lot between movements. " +
                           "That can mean switching between careful aim and fast repositioning, which is normal in a real game.";
            }
            else if (lowOverall && lowSmoothness && lowVelocity)
            {
                // Genuinely rough — call it honestly
                headline = "Rough session overall — smoothness and consistency were both below your recent average. " +
                           "One off session is normal. If it happens again check if you were fatigued or on tilt.";
            }
            else if (!lowOverall && session.OverallQualityScore >= 75)
            {
                headline = $"Strong session — quality {session.OverallQualityScore:F0}/100. Movement was controlled.";
            }
            else if (!lowOverall)
            {
                headline = $"Solid session — quality {session.OverallQualityScore:F0}/100. A few areas worth watching.";
            }
            else
            {
                // Fallback: lowOverall but none of the specific patterns matched
                headline = $"Below-average session — quality {session.OverallQualityScore:F0}/100. " +
                           "Worth checking grip tension and sensitivity before next time.";
            }

            return new TrackerCoachReport(
                Headline:            dataQualifier + headline,
                Observations:        observations,
                Suggestions:         suggestions,
                NextDrillSuggestion: nextDrill
            );
        }
    }
}
