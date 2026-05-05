using System;
using System.Windows;
using System.Windows.Controls;

namespace CleanAimTracker.Trainer.Scenarios
{
    public class AdaptiveScenario : IAimScenario
    {
        private readonly string _weakSpot;
        private IAimScenario _inner;

        public AdaptiveScenario(string weakSpot)
        {
            _weakSpot = weakSpot;
        }

        // Stats proxy to inner scenario
        public int Hits => _inner?.Hits ?? 0;
        public int Misses => _inner?.Misses ?? 0;
        public double BestReactionMs => _inner?.BestReactionMs ?? 0;
        public double AvgReactionMs => _inner?.AvgReactionMs ?? 0;
        public int MaxStreak => _inner?.MaxStreak ?? 0;

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            // Pick scenario based on weak spot
            _inner = _weakSpot switch
            {
                "Tracking" => new TrackingScenario(),
                "Switching" => new SwitchingScenario(),
                "Precision" => new StaticScenario(), // same logic, smaller target already handled by difficulty
                "Flicking" => new StaticScenario(),
                _ => new StaticScenario(),
            };

            _inner.Start(canvas, targetSize, moveSpeed, rng);
        }

        public void Update(Canvas canvas)
        {
            _inner?.Update(canvas);
        }

        public bool HandleClick(Point clickPos)
        {
            return _inner?.HandleClick(clickPos) ?? false;
        }

        public void Stop(Canvas canvas)
        {
            _inner?.Stop(canvas);
        }
    }
}
