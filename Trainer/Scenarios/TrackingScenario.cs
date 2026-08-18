using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// Follow a moving target.
    /// CAT_AUTO_WEAPONS: HOLD-TO-FIRE like a beam weapon — hold the left button and
    /// keep the crosshair on the target. The host window synthesizes shots at a
    /// fixed cadence via IsAutoFire; on-target shots are hits, off-target are misses,
    /// so ACCURACY = time-on-target. (The old model made you spam-click a mover.)
    /// Variants:
    ///   Smooth    — single target, smooth bounce (default)
    ///   Evasive   — single target, sharp random direction changes every ~800 ms
    ///   Two-Track — two targets moving simultaneously; keep your beam on either
    /// </summary>
    public class TrackingScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas = null!;
        private Random _rng    = null!;

        private double _targetSize;
        private double _moveSpeed;

        private readonly List<Ellipse> _targets = new();
        private readonly TargetMover   _mover   = new();

        private readonly Stopwatch _evasiveTimer  = new();
        private int _streak;

        public int    Hits            { get; private set; }
        public int    Misses          { get; private set; }
        public double BestReactionMs  { get; private set; } = double.MaxValue;
        // CAT_AUTO_WEAPONS honesty: hold-to-fire cadence has no per-shot timing.
        public double AvgReactionMs   => 0;
        public int    MaxStreak       { get; private set; }
        public bool   IsAutoFire      => true;

        // T1: per-frame center of the primary moving target — feeds axis-split.
        public Point CurrentTargetCenter
        {
            get
            {
                if (_targets.Count == 0) return new Point(double.NaN, double.NaN);
                var t = _targets[0];
                return new Point(Canvas.GetLeft(t) + t.Width / 2, Canvas.GetTop(t) + t.Height / 2);
            }
        }

        // T3.3 (revised): DirectionChangeLag measures FLICK latency after a discrete
        // stimulus (first mag≥8 movement). Continuous tracking has no such flick — the
        // response is a gradual heading adjustment — so wiring it here produced 0.
        // Re-pointed to SwitchingScenario (discrete flick on target switch). Tracking
        // keeps only its valid telemetry (CurrentTargetCenter → axis-split).

        public TrackingScenario(string variant = "Smooth")
        {
            _variant = variant;
        }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas     = canvas;
            _rng        = rng;
            _targetSize = targetSize;
            _moveSpeed  = _variant == "Evasive" ? moveSpeed * 1.45 : moveSpeed;

            int count = _variant == "Two-Track" ? 2 : 1;

            for (int i = 0; i < count; i++)
            {
                // Stagger start positions so Two-Track targets don't overlap
                double x = canvas.ActualWidth  / 2 + (i == 1 ?  targetSize * 3 : 0);
                double y = canvas.ActualHeight / 2 + (i == 1 ? -targetSize * 2 : 0);

                var target = TargetFactory.CreateTrackingTarget(_targetSize, x, y);
                _targets.Add(target);
                canvas.Children.Add(target);
                _mover.AddTarget(target, _moveSpeed);
            }

            if (_variant == "Evasive") _evasiveTimer.Restart();
        }

        public void Update(Canvas canvas)
        {
            _mover.Update(canvas);

            // Evasive: randomise velocity every 800 ms
            if (_variant == "Evasive" && _evasiveTimer.ElapsedMilliseconds >= 800)
            {
                foreach (var t in _targets)
                    _mover.RandomizeVelocity(t, _moveSpeed);
                _evasiveTimer.Restart();
            }
        }

        public bool HandleClick(Point clickPos)
        {
            foreach (var target in _targets)
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
                    // Hold-to-fire hit = a synthesized shot landed on the target this
                    // frame. No LastHitCenter — tracking must not feed the click-point
                    // (overshoot/undershoot) metric; that's for aimed clicks only.
                    Hits++;
                    _streak++;
                    MaxStreak = Math.Max(MaxStreak, _streak);
                    return true;
                }
            }

            Misses++;
            _streak = 0;
            return false;
        }

        public void Stop(Canvas canvas)
        {
            foreach (var t in _targets)
            {
                canvas.Children.Remove(t);
                _mover.RemoveTarget(t);
            }
            _targets.Clear();
        }
    }
}
