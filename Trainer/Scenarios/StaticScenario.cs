using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// Non-moving target scenario — used for both Precision and Flicking.
    ///
    /// Precision variants : Standard (1 target) | Micro (60 % size) | Double (2 targets, click both)
    /// Flicking  variants : Standard (1 target) | Peripheral (edge spawn) | Pairs (2 opposite-side targets)
    /// </summary>
    public class StaticScenario : IAimScenario
    {
        private readonly string _baseScenario; // "Precision" or "Flicking"
        private readonly string _variant;

        private Canvas _canvas = null!;
        private Random _rng    = null!;

        private double _targetSize; // after Micro adjustment

        private readonly List<Ellipse> _activeTargets = new();
        private int _hitsOnCurrentSet;
        private int _hitsNeededToAdvance = 1;

        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;
        private int _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak      { get; private set; }

        public StaticScenario(string baseScenario = "Precision", string variant = "Standard")
        {
            _baseScenario = baseScenario;
            _variant      = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas = canvas;
            _rng    = rng;

            _targetSize          = _variant == "Micro" ? targetSize * 0.6 : targetSize;
            _hitsNeededToAdvance = (_variant is "Double" or "Pairs") ? 2 : 1;

            SpawnSet();
            _reactionTimer.Restart();
        }

        public void Update(Canvas canvas) { }

        public bool HandleClick(Point clickPos)
        {
            foreach (var target in _activeTargets.ToList())
            {
                double left = Canvas.GetLeft(target);
                double top  = Canvas.GetTop(target);
                double size = target.Width;
                double cx   = left + size / 2;
                double cy   = top  + size / 2;
                double dx   = clickPos.X - cx;
                double dy   = clickPos.Y - cy;

                if (dx * dx + dy * dy <= (size / 2) * (size / 2))
                {
                    Hits++;
                    _hitsOnCurrentSet++;
                    _streak++;
                    MaxStreak = Math.Max(MaxStreak, _streak);

                    double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
                    _totalReactionMs += reaction;
                    if (reaction < BestReactionMs) BestReactionMs = reaction;
                    _reactionTimer.Restart();

                    _canvas.Children.Remove(target);
                    _activeTargets.Remove(target);

                    // Advance to next set when all required targets are hit
                    if (_hitsOnCurrentSet >= _hitsNeededToAdvance)
                    {
                        _hitsOnCurrentSet = 0;
                        foreach (var rem in _activeTargets)
                            _canvas.Children.Remove(rem);
                        _activeTargets.Clear();
                        SpawnSet();
                    }

                    return true;
                }
            }

            Misses++;
            _streak = 0;
            return false;
        }

        public void Stop(Canvas canvas)
        {
            foreach (var t in _activeTargets)
                canvas.Children.Remove(t);
            _activeTargets.Clear();
        }

        // ── Spawn helpers ────────────────────────────────────────────
        private void SpawnSet()
        {
            int count = (_variant is "Double" or "Pairs") ? 2 : 1;

            for (int i = 0; i < count; i++)
            {
                (double x, double y) = GetSpawnPosition(i);
                var target = TargetFactory.CreateTarget(_targetSize, x, y);
                _activeTargets.Add(target);
                _canvas.Children.Add(target);
            }
        }

        private (double x, double y) GetSpawnPosition(int index)
        {
            double w = Math.Max(1, _canvas.ActualWidth  - _targetSize);
            double h = Math.Max(1, _canvas.ActualHeight - _targetSize);

            // Peripheral: restrict to outer 22 % band on each axis edge
            if (_variant == "Peripheral")
            {
                bool useX = _rng.NextDouble() > 0.5;
                if (useX)
                {
                    double band = w * 0.22;
                    double x = _rng.NextDouble() > 0.5
                        ? _rng.NextDouble() * band
                        : w - _rng.NextDouble() * band;
                    return (x, _rng.NextDouble() * h);
                }
                else
                {
                    double band = h * 0.22;
                    double y = _rng.NextDouble() > 0.5
                        ? _rng.NextDouble() * band
                        : h - _rng.NextDouble() * band;
                    return (_rng.NextDouble() * w, y);
                }
            }

            // Pairs: second target spawns on the opposite side of the canvas
            if (_variant == "Pairs" && index == 1 && _activeTargets.Count > 0)
            {
                var first = _activeTargets[0];
                double fx = Canvas.GetLeft(first) + _targetSize / 2;
                double fy = Canvas.GetTop(first)  + _targetSize / 2;
                double ox = Math.Clamp(_canvas.ActualWidth  - fx, 0, w);
                double oy = Math.Clamp(_canvas.ActualHeight - fy, 0, h);
                return (ox, oy);
            }

            // Standard / Micro / Double: random anywhere
            return (_rng.NextDouble() * w, _rng.NextDouble() * h);
        }
    }
}
