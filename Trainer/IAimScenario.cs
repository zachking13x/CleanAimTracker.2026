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
    }
}
