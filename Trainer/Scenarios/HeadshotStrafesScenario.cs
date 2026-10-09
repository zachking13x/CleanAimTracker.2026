using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// CAT_BOT_DRILLS: Headshot Strafes — SEMI weapon archetype.
    /// Three capsule bots strafe horizontally with random direction jukes.
    /// Head = instant kill (headshot). Body = 2 shots to kill.
    /// SEMI fire-rate: clicks faster than the cadence are swallowed (no miss).
    /// Rounds heat up: spawn size shrinks slightly as the streak climbs.
    /// </summary>
    public class HeadshotStrafesScenario : IAimScenario
    {
        private const double SemiLockoutMs = 130;
        private static readonly Color BotAccent = Color.FromRgb(0x00, 0xD4, 0xFF);

        private Canvas _canvas = null!;
        private Random _rng    = null!;
        private double _baseSize;
        private double _moveSpeed;

        private class Bot
        {
            public BotTarget Visual = null!;
            public double Vx;
            public long NextJukeAt;
        }

        private readonly List<Bot> _bots = new();
        private readonly Queue<long> _pendingSpawns = new();

        private readonly Stopwatch _reactionTimer = new();
        private long _lastShotTicks;
        private long _lastTrailTicks;
        private double _totalReactionMs;
        private int _streak;

        public int    Hits            { get; private set; }
        public int    Misses          { get; private set; }
        public int    Headshots       { get; private set; }
        public double BestReactionMs  { get; private set; } = double.MaxValue;
        public double AvgReactionMs   => Hits == 0 ? 0 : _totalReactionMs / Hits;
        public int    MaxStreak       { get; private set; }
        public bool   LastHitWasHeadshot { get; private set; }
        public bool   LastClickIgnored   { get; private set; }
        public Point  LastHitCenter      { get; private set; } = new Point(double.NaN, double.NaN);

        public HeadshotStrafesScenario(string variant = "Standard") { _ = variant; }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas    = canvas;
            _rng       = rng;
            _baseSize  = targetSize;
            _moveSpeed = moveSpeed > 0 ? moveSpeed : 2.5;

            for (int i = 0; i < 3; i++) SpawnBot();
            _reactionTimer.Restart();
            _lastShotTicks = 0;
        }

        public void Update(Canvas canvas)
        {
            long now = Stopwatch.GetTimestamp();

            while (_pendingSpawns.Count > 0 && _pendingSpawns.Peek() <= now)
            {
                _pendingSpawns.Dequeue();
                SpawnBot();
            }

            double w = Math.Max(1, canvas.ActualWidth);
            bool dropTrail = TicksToMs(now - _lastTrailTicks) >= 90;
            if (dropTrail) _lastTrailTicks = now;

            foreach (var b in _bots)
            {
                double left = Canvas.GetLeft(b.Visual);
                double top  = Canvas.GetTop(b.Visual);

                // Random juke: flip direction (and sometimes speed) on a jittered clock
                if (now >= b.NextJukeAt)
                {
                    b.Vx = Math.Sign(b.Vx) * -1 * StrafeSpeed();
                    b.NextJukeAt = now + MsToTicks(350 + _rng.NextDouble() * 550);
                }

                left += b.Vx * FrameClock.Scale;
                if (left <= 0)                              { left = 0;                              b.Vx = Math.Abs(b.Vx); }
                if (left >= w - b.Visual.TotalWidth)        { left = Math.Max(0, w - b.Visual.TotalWidth); b.Vx = -Math.Abs(b.Vx); }
                Canvas.SetLeft(b.Visual, left);

                if (dropTrail && Math.Abs(b.Vx) > 1.2)
                    BotTarget.TrailGhost(canvas, b.Visual.BodyCenter(left, top),
                                         b.Visual.BodyWidth, BotAccent);
            }
        }

        public bool HandleClick(Point clickPos)
        {
            long now = Stopwatch.GetTimestamp();

            // SEMI fire-rate lockout — the shot simply doesn't fire
            if (_lastShotTicks != 0 && TicksToMs(now - _lastShotTicks) < SemiLockoutMs)
            {
                LastClickIgnored = true;
                return false;
            }
            LastClickIgnored = false;
            _lastShotTicks = now;
            LastHitWasHeadshot = false;

            foreach (var b in _bots.ToList())
            {
                double left = Canvas.GetLeft(b.Visual);
                double top  = Canvas.GetTop(b.Visual);
                var zone = b.Visual.HitTest(clickPos, left, top);
                if (zone == BotHitZone.None) continue;

                RegisterHit();

                if (zone == BotHitZone.Head)
                {
                    Headshots++;
                    LastHitWasHeadshot = true;
                    LastHitCenter = b.Visual.HeadCenter(left, top);
                    KillBot(b);
                }
                else
                {
                    LastHitCenter = b.Visual.BodyCenter(left, top);
                    b.Visual.BodyHp--;
                    if (b.Visual.BodyHp <= 0) KillBot(b);
                    else b.Visual.DamageFlash();
                }
                return true;
            }

            Misses++;
            _streak = 0;
            return false;
        }

        public void Stop(Canvas canvas)
        {
            foreach (var b in _bots) canvas.Children.Remove(b.Visual);
            _bots.Clear();
            _pendingSpawns.Clear();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private void RegisterHit()
        {
            Hits++;
            _streak++;
            MaxStreak = Math.Max(MaxStreak, _streak);

            double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
            _totalReactionMs += reaction;
            if (reaction < BestReactionMs) BestReactionMs = reaction;
            _reactionTimer.Restart();
        }

        private void KillBot(Bot b)
        {
            TargetFactory.Burst(_canvas, LastHitCenter, BotAccent, LastHitWasHeadshot);
            _canvas.Children.Remove(b.Visual);
            _bots.Remove(b);
            _pendingSpawns.Enqueue(Stopwatch.GetTimestamp() + MsToTicks(280));
        }

        private void SpawnBot()
        {
            // Streak heat: up to 12% smaller as the streak climbs
            double heat  = 1.0 - Math.Min(_streak, 15) * 0.008;
            double scale = (0.82 + _rng.NextDouble() * 0.28) * heat;   // mild size variety

            var visual = new BotTarget(_baseSize, scale, BotAccent) { BodyHp = 2 };

            double w = Math.Max(1, _canvas.ActualWidth  - visual.TotalWidth);
            double h = Math.Max(1, _canvas.ActualHeight - visual.TotalHeight);
            Canvas.SetLeft(visual, _rng.NextDouble() * w);
            Canvas.SetTop (visual, _rng.NextDouble() * h);
            _canvas.Children.Add(visual);

            var bot = new Bot
            {
                Visual     = visual,
                Vx         = (_rng.Next(2) == 0 ? -1 : 1) * StrafeSpeed(),
                NextJukeAt = Stopwatch.GetTimestamp() + MsToTicks(350 + _rng.NextDouble() * 550),
            };
            _bots.Add(bot);
        }

        private double StrafeSpeed() => _moveSpeed * (0.75 + _rng.NextDouble() * 0.6);

        private static long   MsToTicks(double ms)   => (long)(ms / 1000.0 * Stopwatch.Frequency);
        private static double TicksToMs(long ticks)  => ticks * 1000.0 / Stopwatch.Frequency;
    }
}
