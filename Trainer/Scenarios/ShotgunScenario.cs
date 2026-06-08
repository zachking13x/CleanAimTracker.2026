using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// Shotgun scenario — large close-range targets with a tight visible window.
    /// Variants:
    ///   Standard — single target, center canvas, expires if not clicked
    ///   Duels    — two simultaneous targets, click either, both expire on miss
    ///   Peek     — target enters from a canvas edge, moves inward then retreats
    /// Score per hit: 150 × multiplier.
    /// </summary>
    public class ShotgunScenario : IAimScenario
    {
        private readonly string _variant;

        private Canvas _canvas    = null!;
        private Random _rng       = null!;
        private double _targetSize;
        private double _moveSpeed;
        private int    _visibleMs;

        // ── Standard / Peek single-target state ──────────────────────
        private Ellipse? _target;
        private double   _cx, _cy;

        // ── Duels two-target state ────────────────────────────────────
        private readonly List<Ellipse>              _duelsTargets = new();
        private readonly List<(double cx, double cy)> _duelsCenters = new();

        // ── Peek state ────────────────────────────────────────────────
        private int    _edgeIndex;          // 0=Left 1=Top 2=Right 3=Bottom
        private int    _peekPhase;          // 0=inward 1=outward
        private double _peekHalfMs;         // ms for each inward / outward leg
        private double _peekMoveDistance;
        private readonly Stopwatch _peekTimer = new();
        private double _peekStartCx, _peekStartCy;
        private double _peekInnerCx, _peekInnerCy;

        // ── Shared spawn-gap timer ────────────────────────────────────
        private bool             _waitingToSpawn;
        private readonly Stopwatch _spawnDelay   = new();
        private readonly Stopwatch _visibleTimer  = new();

        // ── Reaction tracking ─────────────────────────────────────────
        private readonly Stopwatch _reactionTimer = new();
        private double _totalReactionMs;
        private int    _streak;

        public int    Hits           { get; private set; }
        public int    Misses         { get; private set; }
        public double BestReactionMs { get; private set; } = double.MaxValue;
        public double AvgReactionMs  => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak      { get; private set; }
        public int    ScorePerHit    => 150;

        public ShotgunScenario(string variant = "Standard")
        {
            _variant = variant;
        }

        // ─────────────────────────────────────────────────────────────
        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas = canvas;
            _rng    = rng;

            double maxSize = Math.Min(canvas.ActualWidth, canvas.ActualHeight) * 0.4;
            _targetSize = Math.Max(10, Math.Min(targetSize * 1.6,
                                   maxSize > 0 ? maxSize : targetSize * 1.6));
            _moveSpeed = moveSpeed;
            _visibleMs = _moveSpeed < 2 ? 900 : _moveSpeed < 3 ? 650 : _moveSpeed < 5 ? 400 : 200;

            // Peek timing: total exposure ≈ 600/400/250/150 ms at Easy/Med/Hard/NM
            _peekHalfMs      = moveSpeed < 2 ? 300 : moveSpeed < 3 ? 200 : moveSpeed < 5 ? 125 : 75;
            _peekMoveDistance = moveSpeed * 30;

            _waitingToSpawn = false;
            _edgeIndex      = 0;

            switch (_variant)
            {
                case "Duels":
                    SpawnDuelsPair();
                    _visibleTimer.Restart();
                    break;
                case "Peek":
                    SpawnPeekTarget();
                    break;
                default: // Standard
                    SpawnStandardTarget();
                    _visibleTimer.Restart();
                    break;
            }

            _reactionTimer.Restart();
        }

        // ─────────────────────────────────────────────────────────────
        public void Update(Canvas canvas)
        {
            switch (_variant)
            {
                case "Duels":  UpdateDuels(canvas);    break;
                case "Peek":   UpdatePeek(canvas);     break;
                default:       UpdateStandard(canvas); break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        public bool HandleClick(Point clickPos)
        {
            return _variant switch
            {
                "Duels" => HandleClickDuels(clickPos),
                "Peek"  => HandleClickPeek(clickPos),
                _       => HandleClickStandard(clickPos),
            };
        }

        // ─────────────────────────────────────────────────────────────
        public void Stop(Canvas canvas)
        {
            if (_target != null) { canvas.Children.Remove(_target); _target = null; }
            foreach (var t in _duelsTargets) canvas.Children.Remove(t);
            _duelsTargets.Clear();
            _duelsCenters.Clear();
        }

        // ══ STANDARD ════════════════════════════════════════════════

        private void UpdateStandard(Canvas canvas)
        {
            if (_waitingToSpawn)
            {
                if (_spawnDelay.ElapsedMilliseconds >= 200)
                {
                    SpawnStandardTarget();
                    _visibleTimer.Restart();
                    _reactionTimer.Restart();
                    _waitingToSpawn = false;
                }
                return;
            }
            if (_target != null && _visibleTimer.ElapsedMilliseconds >= _visibleMs)
            {
                Misses++; _streak = 0;
                canvas.Children.Remove(_target); _target = null;
                _waitingToSpawn = true; _spawnDelay.Restart();
            }
        }

        private bool HandleClickStandard(Point clickPos)
        {
            if (_target == null) return false;
            double dx = clickPos.X - _cx, dy = clickPos.Y - _cy, r = _targetSize / 2;
            if (dx * dx + dy * dy <= r * r)
            {
                RecordHit();
                _canvas.Children.Remove(_target); _target = null;
                _waitingToSpawn = true; _spawnDelay.Restart();
                return true;
            }
            Misses++; _streak = 0;
            return false;
        }

        private void SpawnStandardTarget()
        {
            (_cx, _cy) = GetCenterAreaPosition();
            _target = MakeTarget(_cx, _cy);
            _canvas.Children.Add(_target);
        }

        // ══ DUELS ═══════════════════════════════════════════════════

        private void UpdateDuels(Canvas canvas)
        {
            if (_waitingToSpawn)
            {
                if (_spawnDelay.ElapsedMilliseconds >= 200)
                {
                    SpawnDuelsPair();
                    _visibleTimer.Restart();
                    _reactionTimer.Restart();
                    _waitingToSpawn = false;
                }
                return;
            }
            // Neither target clicked within window → one miss
            if (_duelsTargets.Count > 0 && _visibleTimer.ElapsedMilliseconds >= _visibleMs)
            {
                Misses++; _streak = 0;
                ClearDuels(canvas);
                _waitingToSpawn = true; _spawnDelay.Restart();
            }
        }

        private bool HandleClickDuels(Point clickPos)
        {
            for (int i = 0; i < _duelsTargets.Count; i++)
            {
                var (cx, cy) = _duelsCenters[i];
                double dx = clickPos.X - cx, dy = clickPos.Y - cy, r = _targetSize / 2;
                if (dx * dx + dy * dy <= r * r)
                {
                    RecordHit();
                    ClearDuels(_canvas);        // remove both (hit and unclicked)
                    _waitingToSpawn = true; _spawnDelay.Restart();
                    return true;
                }
            }
            Misses++; _streak = 0;
            return false;
        }

        private void SpawnDuelsPair()
        {
            double w    = Math.Max(1, _canvas.ActualWidth);
            double h    = Math.Max(1, _canvas.ActualHeight);
            double half = _targetSize / 2;
            double xMin = Math.Max(half, w * 0.25), xMax = Math.Min(w - half, w * 0.75);
            double yMin = Math.Max(half, h * 0.25), yMax = Math.Min(h - half, h * 0.75);

            double cx1 = xMin + _rng.NextDouble() * Math.Max(0, xMax - xMin);
            double cy1 = yMin + _rng.NextDouble() * Math.Max(0, yMax - yMin);

            // Second target: at least 120 px separation
            double cx2 = cx1, cy2 = cy1;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                cx2 = xMin + _rng.NextDouble() * Math.Max(0, xMax - xMin);
                cy2 = yMin + _rng.NextDouble() * Math.Max(0, yMax - yMin);
                double sep = Math.Sqrt((cx2 - cx1) * (cx2 - cx1) + (cy2 - cy1) * (cy2 - cy1));
                if (sep >= 120) break;
            }
            // Hard-force if still too close
            {
                double sep = Math.Sqrt((cx2 - cx1) * (cx2 - cx1) + (cy2 - cy1) * (cy2 - cy1));
                if (sep < 120)
                {
                    double angle = _rng.NextDouble() * Math.PI * 2;
                    cx2 = Math.Clamp(cx1 + Math.Cos(angle) * 120, xMin, xMax);
                    cy2 = Math.Clamp(cy1 + Math.Sin(angle) * 120, yMin, yMax);
                }
            }

            ClearDuels(_canvas);
            var t1 = MakeTarget(cx1, cy1);
            var t2 = MakeTarget(cx2, cy2);
            _canvas.Children.Add(t1); _canvas.Children.Add(t2);
            _duelsTargets.Add(t1); _duelsTargets.Add(t2);
            _duelsCenters.Add((cx1, cy1)); _duelsCenters.Add((cx2, cy2));
        }

        private void ClearDuels(Canvas canvas)
        {
            foreach (var t in _duelsTargets) canvas.Children.Remove(t);
            _duelsTargets.Clear();
            _duelsCenters.Clear();
        }

        // ══ PEEK ════════════════════════════════════════════════════

        private void UpdatePeek(Canvas canvas)
        {
            if (_waitingToSpawn)
            {
                if (_spawnDelay.ElapsedMilliseconds >= 300)
                {
                    SpawnPeekTarget();
                    _waitingToSpawn = false;
                }
                return;
            }
            if (_target == null) return;

            double half = _targetSize / 2;
            double t = Math.Clamp(_peekTimer.ElapsedMilliseconds / _peekHalfMs, 0.0, 1.0);

            if (_peekPhase == 0) // inward
            {
                _cx = _peekStartCx + (_peekInnerCx - _peekStartCx) * t;
                _cy = _peekStartCy + (_peekInnerCy - _peekStartCy) * t;
                Canvas.SetLeft(_target, _cx - half);
                Canvas.SetTop (_target, _cy - half);

                if (t >= 1.0) { _peekPhase = 1; _peekTimer.Restart(); }
            }
            else // outward (phase 1)
            {
                _cx = _peekInnerCx + (_peekStartCx - _peekInnerCx) * t;
                _cy = _peekInnerCy + (_peekStartCy - _peekInnerCy) * t;
                Canvas.SetLeft(_target, _cx - half);
                Canvas.SetTop (_target, _cy - half);

                if (t >= 1.0)
                {
                    // Target fully retreated — miss
                    Misses++; _streak = 0;
                    canvas.Children.Remove(_target); _target = null;
                    _edgeIndex = (_edgeIndex + 1) % 4;
                    _waitingToSpawn = true; _spawnDelay.Restart();
                }
            }
        }

        private bool HandleClickPeek(Point clickPos)
        {
            if (_target == null) return false;
            double dx = clickPos.X - _cx, dy = clickPos.Y - _cy, r = _targetSize / 2;
            if (dx * dx + dy * dy <= r * r)
            {
                RecordHit();
                _canvas.Children.Remove(_target); _target = null;
                _edgeIndex = (_edgeIndex + 1) % 4;
                _waitingToSpawn = true; _spawnDelay.Restart();
                return true;
            }
            Misses++; _streak = 0;
            return false;
        }

        private void SpawnPeekTarget()
        {
            double w    = Math.Max(1, _canvas.ActualWidth);
            double h    = Math.Max(1, _canvas.ActualHeight);
            double half = _targetSize / 2;
            double dist = _peekMoveDistance;

            switch (_edgeIndex)
            {
                case 0: // Left
                    _peekStartCx = half;
                    _peekStartCy = h * 0.25 + _rng.NextDouble() * h * 0.5;
                    _peekInnerCx = Math.Min(half + dist, w - half);
                    _peekInnerCy = _peekStartCy;
                    break;
                case 1: // Top
                    _peekStartCx = w * 0.25 + _rng.NextDouble() * w * 0.5;
                    _peekStartCy = half;
                    _peekInnerCx = _peekStartCx;
                    _peekInnerCy = Math.Min(half + dist, h - half);
                    break;
                case 2: // Right
                    _peekStartCx = w - half;
                    _peekStartCy = h * 0.25 + _rng.NextDouble() * h * 0.5;
                    _peekInnerCx = Math.Max(w - half - dist, half);
                    _peekInnerCy = _peekStartCy;
                    break;
                default: // Bottom (3)
                    _peekStartCx = w * 0.25 + _rng.NextDouble() * w * 0.5;
                    _peekStartCy = h - half;
                    _peekInnerCx = _peekStartCx;
                    _peekInnerCy = Math.Max(h - half - dist, half);
                    break;
            }

            _cx = _peekStartCx; _cy = _peekStartCy;
            _peekPhase = 0;
            _peekTimer.Restart();

            _target = MakeTarget(_cx, _cy);
            _canvas.Children.Add(_target);
            _reactionTimer.Restart();
        }

        // ── Shared helpers ───────────────────────────────────────────

        private void RecordHit()
        {
            Hits++; _streak++;
            MaxStreak = Math.Max(MaxStreak, _streak);
            double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
            _totalReactionMs += reaction;
            if (reaction < BestReactionMs) BestReactionMs = reaction;
            _reactionTimer.Restart();
        }

        private (double cx, double cy) GetCenterAreaPosition()
        {
            double w    = Math.Max(1, _canvas.ActualWidth);
            double h    = Math.Max(1, _canvas.ActualHeight);
            double half = _targetSize / 2;
            double xMin = Math.Max(half, w * 0.25), xMax = Math.Min(w - half, w * 0.75);
            double yMin = Math.Max(half, h * 0.25), yMax = Math.Min(h - half, h * 0.75);
            return (xMin + _rng.NextDouble() * Math.Max(0, xMax - xMin),
                    yMin + _rng.NextDouble() * Math.Max(0, yMax - yMin));
        }

        private Ellipse MakeTarget(double cx, double cy)
            => TargetFactory.CreateTarget(_targetSize, cx, cy,
                   new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x35)));
    }
}
