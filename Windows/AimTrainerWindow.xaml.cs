using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CleanAimTracker.Windows
{
    public partial class AimTrainerWindow : Window
    {
        // ── Scenario / difficulty ────────────────────────────────────
        private string _scenario = "Tracking";
        private string _difficulty = "Medium";
        private int _durationSeconds = 30;
        private bool _uiReady = false;


        // ── Session state ────────────────────────────────────────────
        private bool _isRunning = false;
        private readonly DispatcherTimer _gameTimer = new();
        private readonly DispatcherTimer _targetMoveTimer = new();
        private int _secondsLeft;
        private int _hits;
        private int _misses;
        private int _streak;
        private int _score;
        private readonly List<double> _reactionTimes = new();
        private DateTime _targetSpawnTime;

        // ── Target rendering ─────────────────────────────────────────
        private Ellipse? _activeTarget;
        private Ellipse? _trackingTarget;
        private readonly List<Ellipse> _switchTargets = new();
        private double _trackDx = 2.5;
        private double _trackDy = 1.8;

        // ── Difficulty config ────────────────────────────────────────
        private record DifficultyConfig(double TargetSize, double MoveSpeed, double SpawnDelayMs);
        private static readonly Dictionary<string, DifficultyConfig> DiffConfigs = new()
        {
            ["Easy"] = new(48, 1.5, 1200),
            ["Medium"] = new(36, 2.5, 900),
            ["Hard"] = new(24, 4.0, 600),
            ["Nightmare"] = new(16, 6.0, 350),
        };

        private DifficultyConfig _config = DiffConfigs["Medium"];
        private readonly Random _rng = new();

        // ── Hit flash timer ──────────────────────────────────────────
        private readonly DispatcherTimer _flashTimer = new();

        public AimTrainerWindow()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                _uiReady = true;
            };

            _gameTimer.Interval = TimeSpan.FromSeconds(1);
            _gameTimer.Tick += GameTimer_Tick;

            _targetMoveTimer.Interval = TimeSpan.FromMilliseconds(16);
            _targetMoveTimer.Tick += TargetMove_Tick;

            _flashTimer.Interval = TimeSpan.FromMilliseconds(80);
            _flashTimer.Tick += (_, _) =>
            {
                TargetCanvas.Background = new SolidColorBrush(Color.FromRgb(10, 10, 10));
                _flashTimer.Stop();
            };

            TargetCanvas.SizeChanged += (_, _) => PositionCenterDot();

            LoadAdaptiveWeakSpot();
        }



        // ─────────────────────────────────────────────────────────────
        // ADAPTIVE — pull weak spot from last tracking session
        // ─────────────────────────────────────────────────────────────
        private string _adaptiveWeakSpot = "Flicking";

        private void LoadAdaptiveWeakSpot()
        {
            var last = SessionStorage.LoadLast();
            if (last == null) return;

            // Find the lowest scoring metric and map to a scenario
            var scores = new Dictionary<string, double>
            {
                ["Flicking"] = last.SmoothnessScore,
                ["Precision"] = last.MovementConsistency,
                ["Tracking"] = 100 - (last.JitterAmount / Math.Max(1, last.TotalSamples) * 100),
                ["Switching"] = last.CorrectionSharpness,
            };

            _adaptiveWeakSpot = scores.OrderBy(kv => kv.Value).First().Key;
        }

        // ─────────────────────────────────────────────────────────────
        // UI EVENTS
        // ─────────────────────────────────────────────────────────────
        private void ScenarioBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border btn) return;
            _scenario = btn.Tag?.ToString() ?? "Tracking";
            ScenarioLabel.Text = _scenario == "Adaptive" ? $"Adaptive → {_adaptiveWeakSpot}" : _scenario;

            // Highlight selected
            foreach (var child in ((StackPanel)btn.Parent).Children.OfType<Border>())
                child.Background = Brushes.Transparent;
            btn.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0xE5, 0xFF));
        }

        private void DifficultyCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady)
                return;

            if (DifficultyCombo.SelectedItem is not ComboBoxItem item)
                return;

            string tag = item.Tag?.ToString();
            if (string.IsNullOrWhiteSpace(tag))
                tag = "Medium";

            _difficulty = tag;
            _config = DiffConfigs.GetValueOrDefault(_difficulty, DiffConfigs["Medium"]);
            DifficultyLabel.Text = _difficulty;
        }


        private void DurationCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (DurationCombo.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Tag?.ToString(), out int secs))
            {
                _durationSeconds = secs;
                if (_uiReady && !_isRunning)
                    UpdateTimerDisplay();

            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) StartStop_Click(this, new RoutedEventArgs());
            if (e.Key == Key.Escape && _isRunning) StopDrill(showResults: true);
        }

        // ─────────────────────────────────────────────────────────────
        // START / STOP
        // ─────────────────────────────────────────────────────────────
        private void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
                StopDrill(showResults: true);
            else
                StartDrill();
        }

        private void StartDrill()
        {
            _isRunning = true;
            _hits = 0; _misses = 0; _streak = 0; _score = 0;
            _reactionTimes.Clear();
            _secondsLeft = _durationSeconds;

            string activeScenario = _scenario == "Adaptive" ? _adaptiveWeakSpot : _scenario;
            ScenarioLabel.Text = _scenario == "Adaptive" ? $"Adaptive → {activeScenario}" : _scenario;

            UpdateLiveStats();
            UpdateTimerDisplay();

            IdleMessage.Visibility = Visibility.Collapsed;
            StartStopBtn.Content = "■  Stop Drill";
            StartStopBtn.Background = new SolidColorBrush(Color.FromRgb(180, 40, 40));

            ClearTargets();

            _config = DiffConfigs.GetValueOrDefault(_difficulty, DiffConfigs["Medium"]);

            switch (activeScenario)
            {
                case "Tracking":
                    SpawnTrackingTarget();
                    _targetMoveTimer.Start();
                    break;
                case "Switching":
                    SpawnSwitchingTargets();
                    break;
                default:
                    SpawnStaticTarget();
                    break;
            }

            _gameTimer.Start();
        }

        private void StopDrill(bool showResults)
        {
            _isRunning = false;
            _gameTimer.Stop();
            _targetMoveTimer.Stop();
            ClearTargets();

            StartStopBtn.Content = "▶  Start Drill";
            StartStopBtn.Background =
                (Brush)Application.Current.Resources["AccentBrush"];

            IdleMessage.Visibility = Visibility.Visible;
            UpdateTimerDisplay();

            if (showResults && (_hits + _misses) > 0)
            {
                var result = BuildResult();
                SaveResult(result);
                new AimTrainerResultWindow(result) { Owner = this }.ShowDialog();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // GAME TIMER (1-second tick)
        // ─────────────────────────────────────────────────────────────
        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            _secondsLeft--;
            UpdateTimerDisplay();

            if (_secondsLeft <= 0)
                StopDrill(showResults: true);
        }

        private void UpdateTimerDisplay()
        {
            if (!_uiReady || TimerText == null)
                return;

            int secs = _isRunning ? _secondsLeft : _durationSeconds;
            TimerText.Text = $"{secs / 60}:{secs % 60:D2}";

            if (_isRunning && _secondsLeft <= 5)
                TimerText.Foreground = Brushes.OrangeRed;
            else
                TimerText.Foreground = Brushes.White;
        }


        // ─────────────────────────────────────────────────────────────
        // TARGET SPAWNING
        // ─────────────────────────────────────────────────────────────
        private void SpawnStaticTarget()
        {
            ClearActiveTarget();
            if (!_isRunning) return;

            double w = TargetCanvas.ActualWidth;
            double h = TargetCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double size = _config.TargetSize;
            double x = _rng.NextDouble() * (w - size * 2) + size;
            double y = _rng.NextDouble() * (h - size * 2) + size;

            _activeTarget = CreateTargetEllipse(size, x, y);
            TargetCanvas.Children.Add(_activeTarget);
            _targetSpawnTime = DateTime.Now;
        }

        private void SpawnTrackingTarget()
        {
            double w = TargetCanvas.ActualWidth;
            double h = TargetCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            double size = _config.TargetSize * 1.4; // tracking targets slightly larger
            _trackingTarget = CreateTargetEllipse(size, w / 2, h / 2,
                new SolidColorBrush(Color.FromRgb(255, 165, 0)));
            TargetCanvas.Children.Add(_trackingTarget);

            _trackDx = (_rng.NextDouble() * 2 - 1) * _config.MoveSpeed;
            _trackDy = (_rng.NextDouble() * 2 - 1) * _config.MoveSpeed;
            _targetSpawnTime = DateTime.Now;
        }

        private void SpawnSwitchingTargets()
        {
            ClearSwitchTargets();
            double w = TargetCanvas.ActualWidth;
            double h = TargetCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            int count = _difficulty == "Easy" ? 2 : _difficulty == "Medium" ? 3 : 4;
            double size = _config.TargetSize;

            for (int i = 0; i < count; i++)
            {
                double x = _rng.NextDouble() * (w - size * 2) + size;
                double y = _rng.NextDouble() * (h - size * 2) + size;
                bool isActive = i == 0;

                var el = CreateTargetEllipse(size, x, y,
                    isActive
                        ? new SolidColorBrush(Color.FromRgb(0, 229, 255))
                        : new SolidColorBrush(Color.FromArgb(120, 100, 100, 100)));

                el.Tag = isActive ? "active" : "inactive";
                TargetCanvas.Children.Add(el);
                _switchTargets.Add(el);
            }

            _targetSpawnTime = DateTime.Now;
        }

        private Ellipse CreateTargetEllipse(double size, double cx, double cy,
            Brush? fill = null)
        {
            fill ??= new SolidColorBrush(Color.FromRgb(0, 229, 255));

            var el = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fill,
                Stroke = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                StrokeThickness = 1.5,
                Opacity = 0,
            };

            Canvas.SetLeft(el, cx - size / 2);
            Canvas.SetTop(el, cy - size / 2);

            // Fade in
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(80));
            el.BeginAnimation(OpacityProperty, fadeIn);

            return el;
        }

        // ─────────────────────────────────────────────────────────────
        // TRACKING TARGET MOVEMENT (~60fps)
        // ─────────────────────────────────────────────────────────────
        private void TargetMove_Tick(object? sender, EventArgs e)
        {
            if (_trackingTarget == null) return;

            double w = TargetCanvas.ActualWidth;
            double h = TargetCanvas.ActualHeight;
            double size = _trackingTarget.Width;

            double x = Canvas.GetLeft(_trackingTarget) + _trackDx * _config.MoveSpeed;
            double y = Canvas.GetTop(_trackingTarget) + _trackDy * _config.MoveSpeed;

            if (x <= 0 || x + size >= w) _trackDx *= -1;
            if (y <= 0 || y + size >= h) _trackDy *= -1;

            x = Math.Clamp(x, 0, w - size);
            y = Math.Clamp(y, 0, h - size);

            Canvas.SetLeft(_trackingTarget, x);
            Canvas.SetTop(_trackingTarget, y);
        }

        // ─────────────────────────────────────────────────────────────
        // CLICK / HIT DETECTION
        // ─────────────────────────────────────────────────────────────
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isRunning) return;

            var pos = e.GetPosition(TargetCanvas);
            string activeScenario = _scenario == "Adaptive" ? _adaptiveWeakSpot : _scenario;

            switch (activeScenario)
            {
                case "Tracking":
                    HandleTrackingClick(pos);
                    break;
                case "Switching":
                    HandleSwitchingClick(pos);
                    break;
                default:
                    HandleStaticClick(pos);
                    break;
            }

            UpdateLiveStats();
        }

        private void HandleStaticClick(Point pos)
        {
            if (_activeTarget == null) { RegisterMiss(); return; }

            if (IsHit(pos, _activeTarget))
            {
                RegisterHit();
                SpawnHitEffect(pos);
                SpawnStaticTarget();
            }
            else
            {
                RegisterMiss();
            }
        }

        private void HandleTrackingClick(Point pos)
        {
            if (_trackingTarget == null) { RegisterMiss(); return; }

            if (IsHit(pos, _trackingTarget))
                RegisterHit();
            else
                RegisterMiss();
        }

        private void HandleSwitchingClick(Point pos)
        {
            bool hitActive = false;

            foreach (var t in _switchTargets)
            {
                if (IsHit(pos, t) && t.Tag?.ToString() == "active")
                {
                    hitActive = true;
                    RegisterHit();
                    SpawnHitEffect(pos);

                    // Rotate active to next target
                    t.Tag = "inactive";
                    t.Fill = new SolidColorBrush(Color.FromArgb(120, 100, 100, 100));

                    var next = _switchTargets.FirstOrDefault(x => x.Tag?.ToString() == "inactive");
                    if (next != null)
                    {
                        next.Tag = "active";
                        next.Fill = new SolidColorBrush(Color.FromRgb(0, 229, 255));
                        _targetSpawnTime = DateTime.Now;
                    }
                    break;
                }
            }

            if (!hitActive) RegisterMiss();
        }

        private bool IsHit(Point click, Ellipse target)
        {
            double cx = Canvas.GetLeft(target) + target.Width / 2;
            double cy = Canvas.GetTop(target) + target.Height / 2;
            double r = target.Width / 2 + 4; // small forgiveness radius
            double dist = Math.Sqrt(Math.Pow(click.X - cx, 2) + Math.Pow(click.Y - cy, 2));
            return dist <= r;
        }

        // ─────────────────────────────────────────────────────────────
        // HIT / MISS REGISTRATION
        // ─────────────────────────────────────────────────────────────
        private void RegisterHit()
        {
            _hits++;
            _streak++;

            double reactionMs = (DateTime.Now - _targetSpawnTime).TotalMilliseconds;
            _reactionTimes.Add(reactionMs);

            // Score: base 100 + streak bonus + reaction bonus
            int streakBonus = Math.Min(_streak * 5, 50);
            int reactionBonus = reactionMs < 300 ? 50 : reactionMs < 600 ? 25 : 0;
            _score += 100 + streakBonus + reactionBonus;

            StreakText.Text = _streak.ToString();

            // Green flash
            TargetCanvas.Background = new SolidColorBrush(Color.FromArgb(25, 0, 255, 100));
            _flashTimer.Stop();
            _flashTimer.Start();
        }

        private void RegisterMiss()
        {
            _misses++;
            _streak = 0;
            StreakText.Text = "0";

            // Red flash
            TargetCanvas.Background = new SolidColorBrush(Color.FromArgb(20, 255, 60, 60));
            _flashTimer.Stop();
            _flashTimer.Start();
        }

        // ─────────────────────────────────────────────────────────────
        // HIT EFFECT
        // ─────────────────────────────────────────────────────────────
        private void SpawnHitEffect(Point pos)
        {
            var ring = new Ellipse
            {
                Width = 20,
                Height = 20,
                Stroke = new SolidColorBrush(Color.FromRgb(0, 229, 255)),
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(ring, pos.X - 10);
            Canvas.SetTop(ring, pos.Y - 10);
            TargetCanvas.Children.Add(ring);

            var expand = new DoubleAnimation(20, 50, TimeSpan.FromMilliseconds(200));
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));

            fade.Completed += (_, _) => TargetCanvas.Children.Remove(ring);

            ring.BeginAnimation(WidthProperty, expand);
            ring.BeginAnimation(HeightProperty,
                new DoubleAnimation(20, 50, TimeSpan.FromMilliseconds(200)));
            ring.BeginAnimation(OpacityProperty, fade);
        }

        // ─────────────────────────────────────────────────────────────
        // LIVE STATS UPDATE
        // ─────────────────────────────────────────────────────────────
        private void UpdateLiveStats()
        {
            LiveHitsText.Text = _hits.ToString();
            LiveMissesText.Text = _misses.ToString();
            LiveScoreText.Text = _score.ToString("N0");

            int total = _hits + _misses;
            LiveAccuracyText.Text = total > 0 ? $"{(_hits * 100.0 / total):F0}%" : "--";

            LiveReactionText.Text = _reactionTimes.Count > 0
                ? $"{_reactionTimes.Average():F0}ms"
                : "--";
        }

        // ─────────────────────────────────────────────────────────────
        // RESULT BUILDING + SAVING
        // ─────────────────────────────────────────────────────────────
        private AimTrainerResult BuildResult()
        {
            int total = _hits + _misses;
            double accuracy = total > 0 ? _hits * 100.0 / total : 0;
            double avgReaction = _reactionTimes.Count > 0 ? _reactionTimes.Average() : 0;
            double bestReaction = _reactionTimes.Count > 0 ? _reactionTimes.Min() : 0;

            string activeScenario = _scenario == "Adaptive" ? _adaptiveWeakSpot : _scenario;

            return new AimTrainerResult
            {
                Timestamp = DateTime.Now,
                Scenario = activeScenario,
                Difficulty = _difficulty,
                DurationSeconds = _durationSeconds,
                Hits = _hits,
                Misses = _misses,
                Accuracy = accuracy,
                Score = _score,
                AvgReactionMs = avgReaction,
                BestReactionMs = bestReaction,
                MaxStreak = _streak,
            };
        }

        private static void SaveResult(AimTrainerResult result)
        {
            try { AimTrainerStorage.Save(result); }
            catch (Exception ex) { LogService.Error("Failed to save trainer result", ex); }
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────
        private void ClearTargets()
        {
            ClearActiveTarget();
            if (_trackingTarget != null)
            {
                TargetCanvas.Children.Remove(_trackingTarget);
                _trackingTarget = null;
            }
            ClearSwitchTargets();
        }

        private void ClearActiveTarget()
        {
            if (_activeTarget != null)
            {
                TargetCanvas.Children.Remove(_activeTarget);
                _activeTarget = null;
            }
        }

        private void ClearSwitchTargets()
        {
            foreach (var t in _switchTargets)
                TargetCanvas.Children.Remove(t);
            _switchTargets.Clear();
        }

        private void PositionCenterDot()
        {
            Canvas.SetLeft(CenterDot, TargetCanvas.ActualWidth / 2 - 3);
            Canvas.SetTop(CenterDot, TargetCanvas.ActualHeight / 2 - 3);
        }

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
            => new AimTrainerHistoryWindow { Owner = this }.ShowDialog();

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _gameTimer.Stop();
            _targetMoveTimer.Stop();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _gameTimer.Stop();
            _targetMoveTimer.Stop();
            base.OnClosed(e);
        }
    }
}