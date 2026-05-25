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
            List<SessionSummary> history)
        {
            var observations = new List<string>();
            var suggestions  = new List<string>();

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

            // ── Observation 5: Drill accuracy context ─────────────────────────
            try
            {
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
            catch { /* non-critical — swallow and continue */ }

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

            // ── Headline ──────────────────────────────────────────────────────
            string headline;
            if (session.OverallQualityScore >= 75)
                headline = $"Strong session — quality {session.OverallQualityScore:F0}/100. Movement was controlled.";
            else if (session.OverallQualityScore >= 55)
                headline = $"Solid session — quality {session.OverallQualityScore:F0}/100. A few areas worth watching.";
            else
                headline = $"Tough session — quality {session.OverallQualityScore:F0}/100. " +
                           "Worth checking grip tension and sensitivity before next time.";

            return new TrackerCoachReport(
                Headline:            dataQualifier + headline,
                Observations:        observations,
                Suggestions:         suggestions,
                NextDrillSuggestion: nextDrill
            );
        }
    }
}
