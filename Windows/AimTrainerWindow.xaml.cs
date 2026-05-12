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

        private readonly DispatcherTimer _gameTimer = new();
        private readonly DispatcherTimer _updateTimer = new();

        private int _secondsLeft;
        private int _durationSeconds = 30;

        // Score (kept at window level)
        private int _score;

        // UI flash timer
        private readonly DispatcherTimer _flashTimer = new();

        // Scenario selection
        private string _scenario = "Tracking";
        private string _difficulty = "Medium";
        private string _adaptiveWeakSpot = "Flicking";
        private string _variant = "Smooth";

        // Daily Warm-Up state
        private bool   _isWarmupMode       = false;
        private int    _warmupRound        = 0;
        private int    _warmupTotalHits    = 0;
        private int    _warmupTotalMisses  = 0;
        private double _warmupTotalReactionMs = 0;
        private double _warmupBestReaction = double.MaxValue;

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

            Loaded += (_, _) => _uiReady = true;

            _gameTimer.Interval = TimeSpan.FromSeconds(1);
            _gameTimer.Tick += GameTimer_Tick;

            _updateTimer.Interval = TimeSpan.FromMilliseconds(16);
            _updateTimer.Tick += UpdateScenario_Tick;

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

            foreach (var child in ((StackPanel)btn.Parent).Children.OfType<Border>())
                child.Background = Brushes.Transparent;

            btn.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0xE5, 0xFF));

            UpdateVariantCombo(_scenario);
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
            _isRunning = true;
            _score = 0;

            IdleMessage.Visibility = Visibility.Collapsed;
            StartStopBtn.Content = "■  Stop Drill";
            StartStopBtn.Background = new SolidColorBrush(Color.FromRgb(180, 40, 40));

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
                }
                _isWarmupMode = false;

                if (showResults && (_warmupTotalHits + _warmupTotalMisses) > 0)
                {
                    var result = BuildWarmupResult();
                    SaveResult(result);
                    new AimTrainerResultWindow(result) { Owner = this }.ShowDialog();
                    new RecommendationWindow(BuildRecommendation(result)) { Owner = this }.ShowDialog();
                }
            }
            else
            {
                _isWarmupMode = false;
                if (showResults && statsSource != null && (statsSource.Hits + statsSource.Misses) > 0)
                {
                    var result = BuildResult(statsSource);
                    SaveResult(result);
                    new AimTrainerResultWindow(result) { Owner = this }.ShowDialog();
                    new RecommendationWindow(BuildRecommendation(result)) { Owner = this }.ShowDialog();
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
                _score += 100;
                FlashHit();
            }
            else
            {
                FlashMiss();
            }

            UpdateLiveStats();
        }

        private void FlashHit()
        {
            TargetCanvas.Background = new SolidColorBrush(Color.FromArgb(25, 0, 255, 100));
            _flashTimer.Stop();
            _flashTimer.Start();
        }

        private void FlashMiss()
        {
            TargetCanvas.Background = new SolidColorBrush(Color.FromArgb(20, 255, 60, 60));
            _flashTimer.Stop();
            _flashTimer.Start();
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
        }

        private void UpdateLiveStats()
        {
            if (_scenarioInstance == null)
            {
                LiveHitsText.Text = "0";
                LiveMissesText.Text = "0";
                LiveScoreText.Text = _score.ToString("N0");
                LiveAccuracyText.Text = "--";
                LiveReactionText.Text = "--";
                StreakText.Text = "0";
                return;
            }

            int hits = _scenarioInstance.Hits;
            int misses = _scenarioInstance.Misses;
            int total = hits + misses;

            LiveHitsText.Text = hits.ToString();
            LiveMissesText.Text = misses.ToString();
            LiveScoreText.Text = _score.ToString("N0");

            LiveAccuracyText.Text = total > 0
                ? $"{(hits * 100.0 / total):F0}%"
                : "--";

            LiveReactionText.Text = _scenarioInstance.AvgReactionMs > 0
                ? $"{_scenarioInstance.AvgReactionMs:F0}ms"
                : "--";

            StreakText.Text = _scenarioInstance.MaxStreak.ToString();
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
                Timestamp = DateTime.Now,
                Scenario = _scenario,
                Difficulty = _difficulty,
                DurationSeconds = _durationSeconds,
                Hits = hits,
                Misses = misses,
                Accuracy = accuracy,
                Score = _score,
                AvgReactionMs = stats.AvgReactionMs,
                BestReactionMs = stats.BestReactionMs,
                MaxStreak = stats.MaxStreak,
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
                MaxStreak       = 0,
            };
        }

        private SensitivityRecommendation BuildRecommendation(AimTrainerResult result)
        {
            var settings = SettingsService.Load() ?? new UserSettings();

            var profile = GameProfileStorage.LoadByName(settings.SelectedProfile)
                ?? GameProfileStorage.Profiles.First();

            double yaw = profile.YawPerCount <= 0 ? 0.022 : profile.YawPerCount;
            double cm360 = 914.4 / (settings.DPI * settings.Sensitivity * yaw);

            var summary = new SessionSummary
            {
                Timestamp = result.Timestamp,
                DPI = settings.DPI,
                Sensitivity = settings.Sensitivity,
                CmPer360 = cm360,
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
            try { AimTrainerStorage.Save(result); }
            catch (Exception ex) { LogService.Error("Failed to save trainer result", ex); }
        }

        // ─────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────
        private void ClearTargets()
        {
            TargetCanvas.Children.Clear();
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
