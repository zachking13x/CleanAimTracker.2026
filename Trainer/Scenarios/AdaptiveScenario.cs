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

        // T1: forward the telemetry hooks so the inner scenario's mechanical metrics
        // (click offsets → MovementOvershoot/PathEfficiency/OvershootPct, or axis-split
        // frames) actually flow. Without this Adaptive populated NO diagnostics — the
        // "94% Adaptive, no AREAS" report.
        public Point LastHitCenter      => _inner?.LastHitCenter      ?? new Point(double.NaN, double.NaN);
        public Point CurrentTargetCenter => _inner?.CurrentTargetCenter ?? new Point(double.NaN, double.NaN);

        // CAT_AUTO_WEAPONS: forward hold-to-fire so a wrapped Tracking drill still
        // holds-to-track and doesn't feed the click-point metric.
        public bool  IsAutoFire         => _inner?.IsAutoFire ?? false;
        public int   Headshots          => _inner?.Headshots ?? 0;
        public bool  LastHitWasHeadshot => _inner?.LastHitWasHeadshot ?? false;
        public bool  LastClickIgnored   => _inner?.LastClickIgnored ?? false;

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
