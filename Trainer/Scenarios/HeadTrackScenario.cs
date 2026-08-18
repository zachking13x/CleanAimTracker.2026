using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// CAT_BOT_DRILLS: Track the Head — AUTO weapon archetype.
    /// One bot strafes with speed swings and direction changes. Hold the left
    /// button to spray (the host window synthesizes shots at a fixed cadence via
    /// IsAutoFire); every shot on the bot is a hit — head pays double via the
    /// headshot flag — every shot off it is a miss, so ACCURACY IS SPRAY CONTROL.
    /// Eight on-target shots kill the bot (burst + fresh spawn) so the loop still
    /// has kill moments.
    ///
    /// AvgReaction stays 0 ("--"): shots fire on a synthetic cadence, so a
    /// per-shot reaction number would be fabricated.
    /// CurrentTargetCenter feeds the axis-split telemetry like other trackers.
    /// </summary>
    public class HeadTrackScenario : IAimScenario
    {
        private const int ShotsToKill = 8;
        private static readonly Color BotAccent = Color.FromRgb(0x00, 0xD4, 0xFF);

        private Canvas _canvas = null!;
        private Random _rng    = null!;
        private double _baseSize;
        private double _moveSpeed;

        private BotTarget? _bot;
        private double _vx;
        private double _speedPhase;
        private long _nextTurnAt;
        private long _lastTrailTicks;
        private int _damageOnBot;

        private int _streak;

        public int    Hits            { get; private set; }
        public int    Misses          { get; private set; }
        public int    Headshots       { get; private set; }
        public double BestReactionMs  { get; private set; } = double.MaxValue;
        public double AvgReactionMs   => 0;   // no honest per-shot reaction on a synthetic cadence
        public int    MaxStreak       { get; private set; }
        public bool   LastHitWasHeadshot { get; private set; }
        public bool   IsAutoFire         => true;
        public Point  LastHitCenter      { get; private set; } = new Point(double.NaN, double.NaN);

        public Point CurrentTargetCenter
        {
            get
            {
                if (_bot == null) return new Point(double.NaN, double.NaN);
                return _bot.BodyCenter(Canvas.GetLeft(_bot), Canvas.GetTop(_bot));
            }
        }

        public HeadTrackScenario(string variant = "Standard") { _ = variant; }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas    = canvas;
            _rng       = rng;
            _baseSize  = targetSize;
            _moveSpeed = moveSpeed > 0 ? moveSpeed : 2.5;
            SpawnBot();
        }

        public void Update(Canvas canvas)
        {
            if (_bot == null) return;
            long now = Stopwatch.GetTimestamp();

            double left = Canvas.GetLeft(_bot);
            double w = Math.Max(1, canvas.ActualWidth);

            // Speed breathes (sine swing) so the strafe never has one learnable pace
            _speedPhase += 0.02;
            double speed = _moveSpeed * (0.65 + 0.5 * (Math.Sin(_speedPhase) + 1) / 2);

            if (now >= _nextTurnAt)
            {
                _vx = -Math.Sign(_vx == 0 ? 1 : _vx);
                _nextTurnAt = now + MsToTicks(700 + _rng.NextDouble() * 900);
            }

            left += _vx * speed;
            if (left <= 0)                        { left = 0;                                    _vx = 1;  }
            if (left >= w - _bot.TotalWidth)      { left = Math.Max(0, w - _bot.TotalWidth);     _vx = -1; }
            Canvas.SetLeft(_bot, left);

            if (TicksToMs(now - _lastTrailTicks) >= 90 && speed > _moveSpeed * 0.5)
            {
                _lastTrailTicks = now;
                BotTarget.TrailGhost(canvas, _bot.BodyCenter(left, Canvas.GetTop(_bot)),
                                     _bot.BodyWidth, BotAccent);
            }
        }

        public bool HandleClick(Point clickPos)
        {
            LastHitWasHeadshot = false;
            if (_bot == null) { Misses++; _streak = 0; return false; }

            double left = Canvas.GetLeft(_bot);
            double top  = Canvas.GetTop(_bot);
            var zone = _bot.HitTest(clickPos, left, top);

            if (zone == BotHitZone.None)
            {
                Misses++;
                _streak = 0;
                return false;
            }

            Hits++;
            _streak++;
            MaxStreak = Math.Max(MaxStreak, _streak);

            if (zone == BotHitZone.Head)
            {
                Headshots++;
                LastHitWasHeadshot = true;
                LastHitCenter = _bot.HeadCenter(left, top);
            }
            else
            {
                LastHitCenter = _bot.BodyCenter(left, top);
            }

            _damageOnBot += zone == BotHitZone.Head ? 2 : 1;   // head melts faster
            if (_damageOnBot >= ShotsToKill)
            {
                TargetFactory.Burst(_canvas, LastHitCenter, BotAccent, LastHitWasHeadshot);
                _canvas.Children.Remove(_bot);
                _bot = null;
                SpawnBot();
            }
            else
            {
                _bot.DamageFlash();
            }
            return true;
        }

        public void Stop(Canvas canvas)
        {
            if (_bot != null) canvas.Children.Remove(_bot);
            _bot = null;
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private void SpawnBot()
        {
            double heat  = 1.0 - Math.Min(_streak, 20) * 0.005;
            var bot = new BotTarget(_baseSize, 0.95 * heat, BotAccent) { BodyHp = ShotsToKill };

            double w = Math.Max(1, _canvas.ActualWidth  - bot.TotalWidth);
            double h = Math.Max(1, _canvas.ActualHeight - bot.TotalHeight);
            Canvas.SetLeft(bot, _rng.NextDouble() * w);
            Canvas.SetTop (bot, 0.15 * h + _rng.NextDouble() * 0.7 * h);
            _canvas.Children.Add(bot);

            _bot = bot;
            _damageOnBot = 0;
            _vx = _rng.Next(2) == 0 ? -1 : 1;
            _nextTurnAt = Stopwatch.GetTimestamp() + MsToTicks(700 + _rng.NextDouble() * 900);
        }

        private static long   MsToTicks(double ms)  => (long)(ms / 1000.0 * Stopwatch.Frequency);
        private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}
