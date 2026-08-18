using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CleanAimTracker.Trainer
{
    public interface IAimScenario
    {
        // Called when the scenario starts
        void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng);

        // Called every frame (~60fps)
        void Update(Canvas canvas);

        // Called when the user clicks
        bool HandleClick(Point clickPos);

        // Called when the scenario stops
        void Stop(Canvas canvas);

        // Basic stats for analytics
        int Hits { get; }
        int Misses { get; }
        double BestReactionMs { get; }
        double AvgReactionMs { get; }
        int MaxStreak { get; }

        /// <summary>
        /// Base score awarded per hit (before hot-streak multiplier).
        /// Default 100 — Shotgun overrides to 150.
        /// </summary>
        int ScorePerHit => 100;

        /// <summary>
        /// Canvas-space center of the most recently registered hit target.
        /// Scenarios that support click-offset telemetry override this property.
        /// Returns <c>new Point(double.NaN, double.NaN)</c> by default.
        /// </summary>
        Point LastHitCenter => new Point(double.NaN, double.NaN);

        /// <summary>
        /// Canvas-space center of the primary moving target, sampled each frame.
        /// Tracking-pillar scenarios override this to feed per-frame axis-split data.
        /// Returns <c>new Point(double.NaN, double.NaN)</c> by default.
        /// </summary>
        Point CurrentTargetCenter => new Point(double.NaN, double.NaN);

        /// <summary>
        /// T3.1: Stopwatch-tick timestamps of target direction-change / spawn events,
        /// for AvgDirectionChangeLagMs. Scenarios with genuine heading changes
        /// (Reactive spawn, Tracking-Evasive randomize, etc.) override this. Default
        /// empty → scenarios without the concept are simply skipped, never forced.
        /// </summary>
        IReadOnlyList<long> DirectionChangeTimestamps => Array.Empty<long>();

        // ── CAT_BOT_DRILLS: bot-target scenarios (head/body hit zones) ────────

        /// <summary>Total headshot kills this round. 0 for non-bot scenarios.</summary>
        int Headshots => 0;

        /// <summary>True when the hit registered by the last HandleClick was a headshot.</summary>
        bool LastHitWasHeadshot => false;

        /// <summary>
        /// True when the last HandleClick was swallowed by a weapon fire-rate lockout
        /// (TAP/SEMI archetypes). The caller should play NEITHER hit nor miss feedback.
        /// </summary>
        bool LastClickIgnored => false;

        /// <summary>
        /// AUTO weapon archetype: while the left button is held, the host window
        /// synthesizes shots at a fixed cadence via HandleClick. Default off.
        /// </summary>
        bool IsAutoFire => false;
    }
}
