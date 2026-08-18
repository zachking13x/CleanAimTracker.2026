using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CleanAimTracker.Trainer.Scenarios
{
    /// <summary>
    /// CAT_BOT_DRILLS: Peek &amp; Click — TAP weapon archetype.
    /// One bot at a time peeks at a random position and DISTANCE (scale 0.55–1.0,
    /// smaller = further), holds the peek briefly, then vanishes. One clean shot
    /// kills; head pays the headshot bonus. TAP fire-rate punishes panic clicking.
    ///
    /// TRUE-REACTION scenario: the reaction timer anchors at bot appearance
    /// (stimulus onset), like Reactive/PeekTraining. Spawn timestamps feed
    /// DirectionChangeTimestamps for the flick-lag metric.
    /// </summary>
    public class PeekClickScenario : IAimScenario
    {
        private const double TapLockoutMs = 550;
        private static readonly Color BotAccent = Color.FromRgb(0x00, 0xD4, 0xFF);

        private Canvas _canvas = null!;
        private Random _rng    = null!;
        private double _baseSize;
        private double _exposureMs;

        private BotTarget? _bot;
        private long _peekEndsAt;
        private long _nextPeekAt;
        private long _lastShotTicks;

        private readonly Stopwatch _reactionTimer = new();
        private readonly List<long> _spawnTimestamps = new();
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
        public IReadOnlyList<long> DirectionChangeTimestamps => _spawnTimestamps;

        public PeekClickScenario(string variant = "Standard") { _ = variant; }

        public void Start(Canvas canvas, double targetSize, double moveSpeed, Random rng)
        {
            _canvas   = canvas;
            _rng      = rng;
            _baseSize = targetSize;

            // Peek window scales with difficulty speed: Easy ~2.6 s → Nightmare ~1.0 s
            double speed = moveSpeed > 0 ? moveSpeed : 2.5;
            _exposureMs  = Math.Max(900, 3200 - speed * 380);

            SpawnPeek();
        }

        public void Update(Canvas canvas)
        {
            long now = Stopwatch.GetTimestamp();

            if (_bot != null && now >= _peekEndsAt)
            {
                // Peek expired unanswered — bot escapes. Not a click miss (accuracy
                // measures shots), but the streak breaks: you let one go.
                canvas.Children.Remove(_bot);
                _bot = null;
                _streak = 0;
                _nextPeekAt = now + MsToTicks(320 + _rng.NextDouble() * 420);
            }

            if (_bot == null && now >= _nextPeekAt)
                SpawnPeek();
        }

        public bool HandleClick(Point clickPos)
        {
            long now = Stopwatch.GetTimestamp();

            // TAP fire-rate lockout — the shot doesn't fire at all
            if (_lastShotTicks != 0 && TicksToMs(now - _lastShotTicks) < TapLockoutMs)
            {
                LastClickIgnored = true;
                return false;
            }
            LastClickIgnored = false;
            _lastShotTicks = now;
            LastHitWasHeadshot = false;

            if (_bot != null)
            {
                double left = Canvas.GetLeft(_bot);
                double top  = Canvas.GetTop(_bot);
                var zone = _bot.HitTest(clickPos, left, top);

                if (zone != BotHitZone.None)
                {
                    Hits++;
                    _streak++;
                    MaxStreak = Math.Max(MaxStreak, _streak);

                    double reaction = _reactionTimer.Elapsed.TotalMilliseconds;
                    _totalReactionMs += reaction;
                    if (reaction < BestReactionMs) BestReactionMs = reaction;

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

                    TargetFactory.Burst(_canvas, LastHitCenter, BotAccent, LastHitWasHeadshot);
                    _canvas.Children.Remove(_bot);
                    _bot = null;
                    _nextPeekAt = now + MsToTicks(320 + _rng.NextDouble() * 420);
                    return true;
                }
            }

            Misses++;
            _streak = 0;
            return false;
        }

        public void Stop(Canvas canvas)
        {
            if (_bot != null) canvas.Children.Remove(_bot);
            _bot = null;
            _spawnTimestamps.Clear();
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private void SpawnPeek()
        {
            // Distance illusion: smaller scale = further away = harder shot
            double scale = 0.55 + _rng.NextDouble() * 0.45;
            var bot = new BotTarget(_baseSize, scale, BotAccent) { BodyHp = 1 };

            double w = Math.Max(1, _canvas.ActualWidth  - bot.TotalWidth);
            double h = Math.Max(1, _canvas.ActualHeight - bot.TotalHeight);
            Canvas.SetLeft(bot, _rng.NextDouble() * w);
            Canvas.SetTop (bot, _rng.NextDouble() * h);
            _canvas.Children.Add(bot);
            _bot = bot;

            long now = Stopwatch.GetTimestamp();
            _peekEndsAt = now + MsToTicks(_exposureMs * (0.8 + _rng.NextDouble() * 0.4));
            _spawnTimestamps.Add(now);
            _reactionTimer.Restart();   // stimulus onset — TRUE reaction anchor
        }

        private static long   MsToTicks(double ms)  => (long)(ms / 1000.0 * Stopwatch.Frequency);
        private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}
