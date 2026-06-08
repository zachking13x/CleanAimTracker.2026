using System;
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
    }
}
