using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Trainer;
using CleanAimTracker.Trainer.Scenarios;
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
        // ─────────────────────────────────────────────────────────────
        // STATE
        // ─────────────────────────────────────────────────────────────
        private IAimScenario? _scenarioInstance;
        private readonly Random _rng = new();

        private bool _isRunning = false;
        private bool _uiReady = false;

        // Pre-selection set by PreSelectScenario() before Show()
        private string? _preSelectScenario;
        private string? _preSelectDifficulty;

        private readonly DispatcherTimer _gameTimer = new();
        private readonly DispatcherTimer _updateTimer = new();

        private int _secondsLeft;
        private int _durationSeconds = 30;

        // Score (kept at window level)
        private int _score;

        // Hot streak system
        private int    _consecutiveHits  = 0;
        private bool   _isHotStreak      = false;
        private double _scoreMultiplier  = 1.0;

        // Scenario selection
        private string _scenario = "Tracking";
        private string _difficulty = "Medium";
        private string _adaptiveWeakSpot = "Flicking";
        private string _variant = "Smooth";

        // Per-scenario accent colors — matches DESIGN_SPEC.md
        private static readonly Dictionary<string, (byte R, byte G, byte B)> ScenarioColors = new()
        {
            ["Tracking"]  = (0x00, 0xD4, 0xFF),   // AccentPrimary
            ["Flicking"]  = (0xFF, 0xB3, 0x47),   // AccentWarm
            ["Precision"] = (0x00, 0xE5, 0xA0),   // AccentGreen
            ["Switching"] = (0xFF, 0x6B, 0x35),   // AccentOrange
            ["Adaptive"]  = (0xA8, 0x55, 0xF7),   // AccentPurple
            ["WarmUp"]    = (0x00, 0xC8, 0x53),
        };

        // Onboarding mode
        private bool _isOnboarding = false;
        public event Action? OnboardingSessionCompleted;

        // Daily Warm-Up state
        private bool   _isWarmupMode       = false;
        private int    _warmupRound        = 0;
        private int    _warmupTotalHits    = 0;
        private int    _warmupTotalMisses  = 0;
        private double _warmupTotalReactionMs = 0;
        private double _warmupBestReaction = double.MaxValue;
        private int    _warmupMaxStreak    = 0;

        private static readonly string[] WarmupScenarioOrder =
            new[] { "Precision", "Tracking", "Flicking", "Switching" };

        // Difficulty config
        private record DifficultyConfig(double TargetSize, double MoveSpeed, double SpawnDelayMs);
        private static readonly Dictionary<string, DifficultyConfig> DiffConfigs = new()
        {
            ["Easy"] = new(48, 1.5, 1200),
            ["Medium"] = new(36, 2.5, 900),
            ["Hard"] = new(24, 4.0, 600),
            ["Nightmare"] = new(16, 6.0, 350),
        };

        private DifficultyConfig _config = DiffConfigs["Medium"];

        // ─────────────────────────────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────────────────────────────
        public AimTrainerWindow()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                _uiReady = true;
                RefreshNightmareLock();
                if (_preSelectScenario != null)
                    ApplyPreSelection(_preSelectScenario, _preSelectDifficulty ?? "Medium");
            };

            _gameTimer.Interval = TimeSpan.FromSeconds(1);
            _gameTimer.Tick += GameTimer_Tick;

            _updateTimer.Interval = TimeSpan.FromMilliseconds(16);
            _updateTimer.Tick += UpdateScenario_Tick;

            TargetCanvas.SizeChanged += (_, _) =>
            {
                PositionCrosshair();
                UpdateTimerBar();
            };

            LoadAdaptiveWeakSpot();
        }

        // ─────────────────────────────────────────────────────────────
        // ONBOARDING
        // ─────────────────────────────────────────────────────────────
        public void BeginOnboardingSession()
        {
            _isOnboarding    = true;
            _scenario        = "Tracking";
            _variant         = "Smooth";
            _difficulty      = "Hard";
            _durationSeconds = 90;
            _config          = DiffConfigs["Hard"];

            ScenarioLabel.Text   = "Tracking";
            DifficultyLabel.Text = "Hard";

            StartDrill();
            OnboardingHintText.Visibility = Visibility.Visible;
        }

        // ─────────────────────────────────────────────────────────────
        // ADAPTIVE LOGIC
        // ─────────────────────────────────────────────────────────────
        private void LoadAdaptiveWeakSpot()
        {
            var last = SessionStorage.LoadLast();
            if (last == null) return;

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
        // PRE-SELECTION (daily challenge launch)
        // ─────────────────────────────────────────────────────────────

        /// <summary>Store scenario + difficulty to apply once the window finishes loading.</summary>
        public void PreSelectScenario(string scenario, string difficulty)
        {
            _preSelectScenario  = scenario;
            _preSelectDifficulty = difficulty;
        }

        private void ApplyPreSelection(string scenario, string difficulty)
        {
            // ── Scenario buttons ────────────────────────────────────────
            var parent = ScenarioBtn_Tracking.Parent as StackPanel;
            if (parent != null)
            {
                var match = parent.Children.OfType<Border>()
                                  .FirstOrDefault(b => b.Tag?.ToString() == scenario);
                if (match != null)
                    ApplyScenarioSelection(match);
            }

            _scenario          = scenario;
            ScenarioLabel.Text = scenario;
            UpdateVariantCombo(scenario);

            // ── Difficulty combo ────────────────────────────────────────
            foreach (ComboBoxItem item in DifficultyCombo.Items)
            {
                if (item.Tag?.ToString() == difficulty)
                {
                    DifficultyCombo.SelectedItem = item;
                    break;
                }
            }

            _difficulty      = difficulty;
            _config          = DiffConfigs.GetValueOrDefault(_difficulty, DiffConfigs["Medium"]);
            DifficultyLabel.Text = _difficulty;
        }

        // ─────────────────────────────────────────────────────────────
        private void ScenarioBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Border btn) return;

            _scenario = btn.Tag?.ToString() ?? "Tracking";

            ScenarioLabel.Text = _scenario switch
            {
                "Adaptive" => $"Adaptive → {_adaptiveWeakSpot}",
                "WarmUp"   => "☀️ Daily Warm-Up",
                _          => _scenario,
            };

            ApplyScenarioSelection(btn);
            UpdateVariantCombo(_scenario);
        }

        // Highlight selected scenario card — restores gradient on deselect, adds glow on select
        private void ApplyScenarioSelection(Border selected)
        {
            if (selected.Parent is not StackPanel parent) return;

            // Reset all: restore XAML gradient background, clear glow, dim accent bar
            foreach (var child in parent.Children.OfType<Border>())
            {
                child.ClearValue(Border.BackgroundProperty);
                child.ClearValue(UIElement.EffectProperty);
                string tag = child.Tag?.ToString() ?? "";
                if (ScenarioColors.TryGetValue(tag, out var c))
                    child.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, c.R, c.G, c.B));
            }

            // Selected: brighten left accent bar + glow shadow
            string selTag = selected.Tag?.ToString() ?? "";
            if (ScenarioColors.TryGetValue(selTag, out var sc))
            {
                selected.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, sc.R, sc.G, sc.B));
                selected.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = Color.FromRgb(sc.R, sc.G, sc.B),
                    BlurRadius  = 16,
                    ShadowDepth = 0,
                    Opacity     = 0.5
                };
            }
        }

        private void DifficultyCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;

            if (DifficultyCombo.SelectedItem is not ComboBoxItem item)
                return;

            string tag = item.Tag?.ToString() ?? "Medium";
            _difficulty = tag;
            _config = DiffConfigs.GetValueOrDefault(_difficulty, DiffConfigs["Medium"]);
            DifficultyLabel.Text = _difficulty;
        }

        private void UpdateVariantCombo(string scenario)
        {
            string[] variants = scenario switch
            {
                "Tracking"  => new[] { "Smooth", "Evasive", "Two-Track" },
                "Precision" => new[] { "Standard", "Micro", "Double" },
                "Flicking"  => new[] { "Standard", "Peripheral", "Pairs" },
                "Switching" => new[] { "4-Target", "6-Target", "Speed Rush" },
                _           => new[] { "Standard" },
            };

            VariantCombo.SelectionChanged -= VariantCombo_Changed;
            VariantCombo.Items.Clear();
            foreach (var v in variants)
                VariantCombo.Items.Add(new ComboBoxItem { Content = v });
            VariantCombo.SelectedIndex = 0;
            VariantCombo.IsEnabled = scenario is not "Adaptive" and not "WarmUp";
            VariantCombo.SelectionChanged += VariantCombo_Changed;

            _variant = variants[0];
        }

        private void VariantCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_uiReady) return;
            if (VariantCombo.SelectedItem is ComboBoxItem item)
                _variant = item.Content?.ToString() ?? "Standard";
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
        public void StartNewDrill() => StartStop_Click(this, new RoutedEventArgs());

        private void StartStop_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
                StopDrill(showResults: true);
            else
                StartDrill();
        }

        private void StartDrill()
        {
            _isRunning        = true;
            _score            = 0;
            _consecutiveHits  = 0;
            _isHotStreak      = false;
            _scoreMultiplier  = 1.0;
            HotStreakBanner.Visibility  = Visibility.Collapsed;
            CanvasGlowBorder.Visibility = Visibility.Collapsed;

            IdleMessage.Visibility = Visibility.Collapsed;
            StartStopBtn.Content = "■  Stop Drill";
            StartStopBtn.Background = new SolidColorBrush(Color.FromRgb(180, 40, 40));

            // TASK-3D+3F: Reset timer bar colour and show canvas overlay
            TimerBarFill.Background = new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF));
            LiveStatsOverlay.Visibility = Visibility.Visible;

            ClearTargets();
            UpdateLiveStats();

            if (_scenario == "WarmUp")
            {
                _isWarmupMode          = true;
                _warmupRound           = 1;
                _warmupTotalHits       = 0;
                _warmupTotalMisses     = 0;
                _warmupTotalReactionMs = 0;
                _warmupBestReaction    = double.MaxValue;
                _warmupMaxStreak       = 0;
                _secondsLeft           = 60;

                WarmUpRoundText.Text       = $"Round 1/4 — {WarmupScenarioOrder[0]}";
                WarmUpRoundText.Visibility = Visibility.Visible;

                _scenarioInstance = new StaticScenario("Precision", "Standard");
            }
            else
            {
                _isWarmupMode              = false;
                _secondsLeft               = _durationSeconds;
                WarmUpRoundText.Visibility = Visibility.Collapsed;

                if (_scenario == "Adaptive")
                {
                    _scenarioInstance = new AdaptiveScenario(_adaptiveWeakSpot);
                }
                else
                {
                    _scenarioInstance = _scenario switch
                    {
                        "Tracking"  => new TrackingScenario(_variant),
                        "Switching" => new SwitchingScenario(_variant),
                        "Flicking"  => new StaticScenario("Flicking", _variant),
                        _           => new StaticScenario("Precision", _variant),
                    };
                }
            }

            UpdateTimerDisplay();
            _scenarioInstance.Start(TargetCanvas, _config.TargetSize, _config.MoveSpeed, _rng);
            _gameTimer.Start();
            _updateTimer.Start();
        }

        private void StopDrill(bool showResults)
        {
            _isRunning = false;

            _gameTimer.Stop();
            _updateTimer.Stop();

            // Capture stats before stopping
            var statsSource = _scenarioInstance;
            _scenarioInstance?.Stop(TargetCanvas);
            _scenarioInstance = null;

            ClearTargets();

            StartStopBtn.Content = "▶  Start Drill";
            StartStopBtn.Background = (Brush)Application.Current.Resources["AccentBrush"];

            IdleMessage.Visibility         = Visibility.Visible;
            WarmUpRoundText.Visibility     = Visibility.Collapsed;
            LiveStatsOverlay.Visibility    = Visibility.Collapsed;
            TimerBarFill.Width             = 0;
            UpdateTimerDisplay();

            if (_isWarmupMode)
            {
                // Accumulate final round stats
                if (statsSource != null)
                {
                    _warmupTotalHits       += statsSource.Hits;
                    _warmupTotalMisses     += statsSource.Misses;
                    _warmupTotalReactionMs += statsSource.AvgReactionMs * statsSource.Hits;
                    if (statsSource.BestReactionMs < _warmupBestReaction)
                        _warmupBestReaction = statsSource.BestReactionMs;
                    if (statsSource.MaxStreak > _warmupMaxStreak)
                        _warmupMaxStreak = statsSource.MaxStreak;
                }
                _isWarmupMode = false;

                if (showResults && (_warmupTotalHits + _warmupTotalMisses) > 0)
                {
                    var result = BuildWarmupResult();
                    SaveResult(result);
                    new AimTrainerResultWindow(result) { Owner = this }.ShowDialog();
                }
            }
            else
            {
                _isWarmupMode = false;

                if (_isOnboarding)
                {
                    _isOnboarding = false;
                    OnboardingHintText.Visibility = Visibility.Collapsed;

                    if (statsSource != null && (statsSource.Hits + statsSource.Misses) > 0)
                    {
                        var result = BuildResult(statsSource);
                        SaveResult(result);
                        new AimTrainerResultWindow(result) { Owner = Application.Current.MainWindow }.ShowDialog();
                    }

                    Application.Current.MainWindow.Activate();
                    OnboardingSessionCompleted?.Invoke();
                    Close();
                    return;
                }

                if (showResults && statsSource != null && (statsSource.Hits + statsSource.Misses) > 0)
                {
                    var result = BuildResult(statsSource);
                    SaveResult(result);
                    CheckNightmareUnlock(result);
                    RefreshNightmareLock();
                    new AimTrainerResultWindow(result) { Owner = this }.ShowDialog();
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // GAME LOOP
        // ─────────────────────────────────────────────────────────────
        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            _secondsLeft--;
            UpdateTimerDisplay();

            if (_secondsLeft <= 0)
            {
                if (_isWarmupMode && _warmupRound < 4)
                    AdvanceWarmupRound();
                else
                    StopDrill(showResults: true);
            }
        }

        private void UpdateScenario_Tick(object? sender, EventArgs e)
        {
            if (!_isRunning || _scenarioInstance == null)
                return;

            _scenarioInstance.Update(TargetCanvas);
        }

        // ─────────────────────────────────────────────────────────────
        // CLICK HANDLING
        // ─────────────────────────────────────────────────────────────
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isRunning || _scenarioInstance == null)
                return;

            var pos = e.GetPosition(TargetCanvas);
            bool hit = _scenarioInstance.HandleClick(pos);

            if (hit)
            {
                _consecutiveHits++;
                if (!_isHotStreak && _consecutiveHits >= 5)
                    ActivateHotStreak();

                _score += (int)(100 * _scoreMultiplier);
                PlayHitEffect(pos);
            }
            else
            {
                _consecutiveHits = 0;
                if (_isHotStreak)
                    DeactivateHotStreak();
                PlayMissEffect(pos);
            }

            UpdateLiveStats();
        }

        // TASK-3C: Hit feedback — 3 expanding rings at the click point
        private void PlayHitEffect(Point pos)
        {
            if (!ScenarioColors.TryGetValue(_scenario, out var sc))
                sc = ((byte)0x00, (byte)0xD4, (byte)0xFF);
            var ringColor = Color.FromArgb(180, sc.R, sc.G, sc.B);

            for (int i = 0; i < 3; i++)
            {
                int    delay     = i * 30;
                double startSize = 20;
                var ring = new Ellipse
                {
                    Width            = startSize,
                    Height           = startSize,
                    Stroke           = new SolidColorBrush(ringColor),
                    StrokeThickness  = 1.5,
                    Fill             = Brushes.Transparent,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(ring, pos.X - startSize / 2);
                Canvas.SetTop (ring, pos.Y - startSize / 2);
                TargetCanvas.Children.Add(ring);

                var expandW = new DoubleAnimation(startSize, startSize * 2.2,
                    TimeSpan.FromMilliseconds(200))
                {
                    BeginTime      = TimeSpan.FromMilliseconds(delay),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var expandH  = expandW.Clone();
                var fadeRing = new DoubleAnimation(1.0, 0.0,
                    TimeSpan.FromMilliseconds(200))
                { BeginTime = TimeSpan.FromMilliseconds(delay) };

                ring.BeginAnimation(FrameworkElement.WidthProperty,   expandW);
                ring.BeginAnimation(FrameworkElement.HeightProperty,  expandH);
                ring.BeginAnimation(UIElement.OpacityProperty,        fadeRing);

                var cleanup = new DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(delay + 220) };
                cleanup.Tick += (s, _) =>
                {
                    ((DispatcherTimer)s!).Stop();
                    TargetCanvas.Children.Remove(ring);
                };
                cleanup.Start();
            }
        }

        // TASK-3C: Miss feedback — small red ring at click point
        private void PlayMissEffect(Point pos)
        {
            var ring = new Ellipse
            {
                Width            = 20,
                Height           = 20,
                Stroke           = new SolidColorBrush(Color.FromArgb(200, 220, 50, 50)),
                StrokeThickness  = 1.5,
                Fill             = Brushes.Transparent,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(ring, pos.X - 10);
            Canvas.SetTop (ring, pos.Y - 10);
            TargetCanvas.Children.Add(ring);

            var expand = new DoubleAnimation(20, 36,
                TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var expandH = expand.Clone();
            var fade    = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200));

            ring.BeginAnimation(FrameworkElement.WidthProperty,   expand);
            ring.BeginAnimation(FrameworkElement.HeightProperty,  expandH);
            ring.BeginAnimation(UIElement.OpacityProperty,        fade);

            var cleanup = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            cleanup.Tick += (s, _) =>
            {
                ((DispatcherTimer)s!).Stop();
                TargetCanvas.Children.Remove(ring);
            };
            cleanup.Start();
        }

        // ─────────────────────────────────────────────────────────────
        // HOT STREAK
        // ─────────────────────────────────────────────────────────────
        private void ActivateHotStreak()
        {
            _isHotStreak     = true;
            _scoreMultiplier = 2.0;
            HotStreakBanner.Visibility  = Visibility.Visible;
            CanvasGlowBorder.Visibility = Visibility.Visible;
        }

        private void DeactivateHotStreak()
        {
            _isHotStreak     = false;
            _scoreMultiplier = 1.0;
            HotStreakBanner.Visibility  = Visibility.Collapsed;
            CanvasGlowBorder.Visibility = Visibility.Collapsed;
        }

        // ─────────────────────────────────────────────────────────────
        // NIGHTMARE LOCK
        // ─────────────────────────────────────────────────────────────
        private void RefreshNightmareLock()
        {
            bool unlocked = IsNightmareUnlocked();
            NightmareItem.IsEnabled = unlocked;
            NightmareItem.Content   = unlocked ? "Nightmare" : "🔒 Nightmare";
            NightmareItem.ToolTip   = unlocked ? null : "Reach 80%+ accuracy on Hard to unlock";
        }

        private static bool IsNightmareUnlocked()
        {
            var all = AimTrainerStorage.LoadAll();
            return all.Any(r => r.Difficulty == "Hard" && r.Accuracy >= 80.0);
        }

        /// <summary>
        /// Checks whether the just-completed result is the first time the user
        /// unlocked Nightmare difficulty, and fires a toast notification if so.
        /// </summary>
        private static void CheckNightmareUnlock(AimTrainerResult result)
        {
            if (result.Difficulty != "Hard" || result.Accuracy < 80.0) return;

            // Only fire once — check whether Nightmare was already unlocked before this session
            var all    = AimTrainerStorage.LoadAll();
            var before = all.Where(r => r.Timestamp < result.Timestamp)
                            .Any(r => r.Difficulty == "Hard" && r.Accuracy >= 80.0);
            if (before) return; // Already unlocked previously

            // First time — fire the unlock toast
            try
            {
                var xml = global::Windows.UI.Notifications.ToastNotificationManager
                    .GetTemplateContent(global::Windows.UI.Notifications.ToastTemplateType.ToastText02);
                var nodes = xml.GetElementsByTagName("text");
                nodes[0].AppendChild(xml.CreateTextNode("Nightmare unlocked! 💀"));
                nodes[1].AppendChild(xml.CreateTextNode(
                    $"You hit {result.Accuracy:F0}% on Hard. Nightmare difficulty is now available."));
                global::Windows.UI.Notifications.ToastNotificationManager
                    .CreateToastNotifier()
                    .Show(new global::Windows.UI.Notifications.ToastNotification(xml));
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────────────
        // LIVE STATS
        // ─────────────────────────────────────────────────────────────
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

            UpdateTimerBar();
        }

        // TASK-3D: Update visual timer bar
        private void UpdateTimerBar()
        {
            if (!_uiReady || TimerBarFill == null) return;

            if (!_isRunning || _durationSeconds <= 0)
            {
                TimerBarFill.Width = 0;
                return;
            }

            double maxW = Math.Max(0, TargetCanvas.ActualWidth);
            double ratio = Math.Clamp((double)_secondsLeft / _durationSeconds, 0, 1);
            TimerBarFill.Width = maxW * ratio;

            // Turn orange in final 10 seconds
            TimerBarFill.Background = _secondsLeft <= 10
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x35))  // AccentOrange
                : new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF)); // AccentPrimary
        }

        private void UpdateLiveStats()
        {
            if (_scenarioInstance == null)
            {
                LiveHitsText.Text     = "0";
                LiveMissesText.Text   = "0";
                LiveScoreText.Text    = _score.ToString("N0");
                LiveAccuracyText.Text = "--";
                LiveReactionText.Text = "--";
                StreakText.Text       = "0";
                // Canvas overlay reset
                CanvasAccText.Text  = "—";
                CanvasHitsText.Text = "—";
                return;
            }

            int hits   = _scenarioInstance.Hits;
            int misses = _scenarioInstance.Misses;
            int total  = hits + misses;

            LiveHitsText.Text   = hits.ToString();
            LiveMissesText.Text = misses.ToString();
            LiveScoreText.Text  = _score.ToString("N0");

            string accStr = total > 0 ? $"{(hits * 100.0 / total):F0}%" : "--";
            LiveAccuracyText.Text = accStr;

            LiveReactionText.Text = _scenarioInstance.AvgReactionMs > 0
                ? $"{_scenarioInstance.AvgReactionMs:F0}ms"
                : "--";

            StreakText.Text = _scenarioInstance.MaxStreak.ToString();

            // TASK-3F: Canvas overlay (ACC + HIT only — streak shown in top bar)
            CanvasAccText.Text  = total > 0 ? $"{(hits * 100.0 / total):F0}%" : "—";
            CanvasHitsText.Text = hits.ToString();
        }

        // ─────────────────────────────────────────────────────────────
        // RESULT + RECOMMENDATION
        // ─────────────────────────────────────────────────────────────
        private AimTrainerResult BuildResult(IAimScenario stats)
        {
            int hits = stats.Hits;
            int misses = stats.Misses;
            int total = hits + misses;
            double accuracy = total > 0 ? hits * 100.0 / total : 0;

            return new AimTrainerResult
            {
                Timestamp       = DateTime.Now,
                Scenario        = _scenario,
                SubVariant      = _variant,
                Difficulty      = _difficulty,
                DurationSeconds = _durationSeconds,
                Hits            = hits,
                Misses          = misses,
                Accuracy        = accuracy,
                Score           = _score,
                AvgReactionMs   = stats.AvgReactionMs,
                BestReactionMs  = stats.BestReactionMs,
                MaxStreak       = stats.MaxStreak,
            };
        }

        private void AdvanceWarmupRound()
        {
            // Accumulate completed round stats
            if (_scenarioInstance != null)
            {
                _warmupTotalHits       += _scenarioInstance.Hits;
                _warmupTotalMisses     += _scenarioInstance.Misses;
                _warmupTotalReactionMs += _scenarioInstance.AvgReactionMs * _scenarioInstance.Hits;
                if (_scenarioInstance.BestReactionMs < _warmupBestReaction)
                    _warmupBestReaction = _scenarioInstance.BestReactionMs;
                if (_scenarioInstance.MaxStreak > _warmupMaxStreak)
                    _warmupMaxStreak = _scenarioInstance.MaxStreak;
                _scenarioInstance.Stop(TargetCanvas);
                _scenarioInstance = null;
            }

            _warmupRound++;
            _secondsLeft = 60;
            ClearTargets();

            string nextScenario = WarmupScenarioOrder[_warmupRound - 1];
            WarmUpRoundText.Text = $"Round {_warmupRound}/4 — {nextScenario}";

            _scenarioInstance = nextScenario switch
            {
                "Tracking"  => new TrackingScenario("Smooth"),
                "Flicking"  => new StaticScenario("Flicking", "Standard"),
                "Switching" => new SwitchingScenario("4-Target"),
                _           => new StaticScenario("Precision", "Standard"),
            };
            _scenarioInstance.Start(TargetCanvas, _config.TargetSize, _config.MoveSpeed, _rng);
            UpdateTimerDisplay();
        }

        private AimTrainerResult BuildWarmupResult()
        {
            int    total      = _warmupTotalHits + _warmupTotalMisses;
            double accuracy   = total > 0 ? _warmupTotalHits * 100.0 / total : 0;
            double avgReact   = _warmupTotalHits > 0 ? _warmupTotalReactionMs / _warmupTotalHits : 0;
            double bestReact  = _warmupBestReaction == double.MaxValue ? 0 : _warmupBestReaction;

            return new AimTrainerResult
            {
                Timestamp       = DateTime.Now,
                Scenario        = "Daily Warm-Up",
                Difficulty      = _difficulty,
                DurationSeconds = 240,
                Hits            = _warmupTotalHits,
                Misses          = _warmupTotalMisses,
                Accuracy        = accuracy,
                Score           = _score,
                AvgReactionMs   = avgReact,
                BestReactionMs  = bestReact,
                MaxStreak       = _warmupMaxStreak,
            };
        }

        private SensitivityRecommendation BuildRecommendation(AimTrainerResult result)
        {
            var settings = SettingsService.Load() ?? new UserSettings();

            // Use the single canonical profile system (GameProfile.GetAllProfiles) so that
            // name lookups here always match what MainWindow uses — avoids the "CS2" vs
            // "Counter-Strike 2" mismatch that was silently falling back to Valorant.
            var allProfiles = GameProfile.GetAllProfiles(ProfileStorage.LoadProfiles());
            var profile     = allProfiles.FirstOrDefault(p => p.Name == settings.SelectedProfile)
                           ?? allProfiles.First();

            double yaw  = profile.YawPerCount <= 0 ? 0.022 : profile.YawPerCount;
            double dpi  = settings.DPI > 0 ? settings.DPI : 800;
            double sens = settings.Sensitivity > 0 ? settings.Sensitivity : 1.0;

            // Correct formula: sens IS the in-game sensitivity (e.g. 11.1 for Fortnite)
            // cm/360 = (360 / (gameSens * DPI * yaw)) * 2.54
            double cm360 = (360.0 / (sens * dpi * yaw)) * 2.54;

            var summary = new SessionSummary
            {
                Timestamp       = result.Timestamp,
                DPI             = (int)Math.Round(dpi),
                Sensitivity     = sens,
                GameSensitivity = sens,  // user input IS the game sensitivity
                CmPer360        = cm360,
                SmoothnessScore = result.Accuracy,
                MovementConsistency = result.Accuracy,
                JitterAmount = 100 - result.Accuracy,
                TotalSamples = result.Hits + result.Misses,
                FlickCount = result.Hits,
                SmallFlickCount = result.Hits,
                LargeFlickCount = result.Misses,
                CorrectionSharpness = 100 - result.Accuracy,
                PeakVelocity = 1,
                AverageVelocity = 1,
                IdlePercentage = 0,
                SessionSeconds = result.DurationSeconds
            };

            return RecommendationEngine.Analyze(summary, profile);
        }

        private static void SaveResult(AimTrainerResult result)
        {
            try
            {
                AimTrainerStorage.Save(result);
                // Reset re-engagement flag so it can fire again after the next absence gap
                var settings = SettingsService.Load();
                if (settings.ReEngagementNotificationSent)
                {
                    settings.ReEngagementNotificationSent = false;
                    SettingsService.Save(settings);
                }
            }
            catch (Exception ex) { LogService.Error("Failed to save trainer result", ex); }
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────
        private void ClearTargets()
        {
            TargetCanvas.Children.Clear();
        }

        // TASK-3E: Position crosshair elements (in the separate crosshair overlay Canvas)
        private void PositionCrosshair()
        {
            if (!_uiReady) return;
            double cx = TargetCanvas.ActualWidth  / 2;
            double cy = TargetCanvas.ActualHeight / 2;

            // Ring (14×14 centered)
            Canvas.SetLeft(CrosshairRing, cx - 7);
            Canvas.SetTop (CrosshairRing, cy - 7);

            // Top arm: 1.5 wide × 6 tall, 2px gap above ring
            Canvas.SetLeft(CrosshairTop, cx - 0.75);
            Canvas.SetTop (CrosshairTop, cy - 7 - 2 - 6);   // cy - 15

            // Bottom arm: starts 9px below center
            Canvas.SetLeft(CrosshairBottom, cx - 0.75);
            Canvas.SetTop (CrosshairBottom, cy + 7 + 2);     // cy + 9

            // Left arm: 6 wide × 1.5 tall, 2px gap left of ring
            Canvas.SetLeft(CrosshairLeft, cx - 7 - 2 - 6);  // cx - 15
            Canvas.SetTop (CrosshairLeft, cy - 0.75);

            // Right arm: starts 9px right of center
            Canvas.SetLeft(CrosshairRight, cx + 7 + 2);     // cx + 9
            Canvas.SetTop (CrosshairRight, cy - 0.75);
        }

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
            => new AimTrainerHistoryWindow { Owner = this }.ShowDialog();

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _gameTimer.Stop();
            _updateTimer.Stop();
            Close();
        }

        private void RecommendedDrillsBtn_Click(object sender, RoutedEventArgs e)
        {
            var last = AimTrainerStorage.LoadLast();
            if (last == null)
            {
                MessageBox.Show("No trainer results found. Run a drill first.",
                    "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var rec = BuildRecommendation(last);

            var win = new RecommendedDrillsWindow(rec);
            win.Owner = this;
            win.ShowDialog();
        }

        protected override void OnClosed(EventArgs e)
        {
            _gameTimer.Stop();
            _updateTimer.Stop();
            base.OnClosed(e);
        }
    }
}
