using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Trainer;
using CleanAimTracker.Trainer.Scenarios;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// Guided 8-test diagnostic assessment window.
    /// Runs each <see cref="DiagnosticAssessmentService.AssessmentTest"/> for 30 seconds,
    /// then shows a results card with per-dimension score bars and a recommended drill.
    /// </summary>
    public partial class DiagnosticAssessmentWindow : Window
    {
        // ── State ─────────────────────────────────────────────────────────────
        private int  _currentTestIndex = 0;
        private int  _secondsLeft      = 30;
        private bool _isTestRunning     = false;

        private IAimScenario?  _scenario;
        private readonly Random _rng = new();

        private readonly List<AimTrainerResult> _results = new();
        private DiagnosticProfile?              _profile;

        // Test row borders (for highlight)
        private readonly List<Border> _testRows = new();

        private readonly DispatcherTimer _gameTimer   = new();
        private readonly DispatcherTimer _updateTimer = new();

        // ── Constructor ──────────────────────────────────────────────────────

        public DiagnosticAssessmentWindow()
        {
            InitializeComponent();

            _gameTimer.Interval   = TimeSpan.FromSeconds(1);
            _gameTimer.Tick      += GameTimer_Tick;
            _updateTimer.Interval = TimeSpan.FromMilliseconds(16);
            _updateTimer.Tick    += UpdateTimer_Tick;

            TestCanvas.SizeChanged += (_, _) => { /* future: re-position crosshair */ };

            BuildTestListUI();
        }

        // ── UI builder ──────────────────────────────────────────────────────

        private void BuildTestListUI()
        {
            TestListPanel.Children.Clear();
            _testRows.Clear();

            for (int i = 0; i < DiagnosticAssessmentService.Tests.Count; i++)
            {
                var test = DiagnosticAssessmentService.Tests[i];
                int idx  = i; // capture for lambda

                var row = new Border
                {
                    CornerRadius  = new CornerRadius(6),
                    Padding       = new Thickness(10, 8, 10, 8),
                    Margin        = new Thickness(0, 2, 0, 2),
                    Background    = Brushes.Transparent,
                    Tag           = i
                };

                var rowGrid = new Grid();
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Status icon
                var statusDot = new TextBlock
                {
                    Text              = "○",
                    FontSize          = 13,
                    Foreground        = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Name              = $"dot{i}"
                };
                Grid.SetColumn(statusDot, 0);

                // Label block
                var labelStack = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
                labelStack.Children.Add(new TextBlock
                {
                    Text       = DiagnosticAssessmentService.GetDimensionLabel(test.Dimension),
                    FontSize   = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("PrimaryText")
                });
                labelStack.Children.Add(new TextBlock
                {
                    Text       = $"{test.Scenario} · {test.DurationSeconds}s",
                    FontSize   = 10,
                    Foreground = (Brush)FindResource("MutedText"),
                    Margin     = new Thickness(0, 1, 0, 0)
                });
                Grid.SetColumn(labelStack, 1);

                rowGrid.Children.Add(statusDot);
                rowGrid.Children.Add(labelStack);
                row.Child = rowGrid;

                TestListPanel.Children.Add(row);
                _testRows.Add(row);
            }
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            _currentTestIndex = 0;
            _results.Clear();
            BeginTest(_currentTestIndex);
        }

        private void NextTestBtn_Click(object sender, RoutedEventArgs e)
        {
            // Skip or advance — record empty result for skipped tests
            if (_isTestRunning)
                FinishCurrentTest();
        }

        private void StartRecommendedBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_profile == null) return;

            var (scenario, variant) = DiagnosticAssessmentService.GetRecommendedStartingScenario(_profile);

            var win = new AimTrainerWindow();
            win.PreSelectScenario(scenario, "Medium");
            win.Show();
            Close();
        }

        private void ShowFreeReportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_results.Count == 0) return;

            // Mark the entitlement used so the button doesn't reappear next time
            var settings = SettingsService.Load();
            settings.HasUsedFreeAssessmentReport = true;
            SettingsService.Save(settings);
            ShowFreeReportBtn.Visibility  = Visibility.Collapsed;
            FreeReportUsedText.Visibility = Visibility.Visible;

            // Use the result with the highest accuracy as the representative session
            var best = _results.Where(r => r.Accuracy > 0)
                               .OrderByDescending(r => r.Accuracy)
                               .FirstOrDefault()
                       ?? _results.FirstOrDefault();

            if (best == null) return;
            new AimTrainerResultWindow(best, isFullSession: true)
            {
                Owner = this
            }.ShowDialog();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private void TestCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isTestRunning || _scenario == null) return;

            var pos = e.GetPosition(TestCanvas);
            _scenario.HandleClick(pos);
            UpdateLiveStats();
        }

        // ── Game loop ────────────────────────────────────────────────────────

        private void GameTimer_Tick(object? sender, EventArgs e)
        {
            _secondsLeft--;
            UpdateTimerText();

            if (_secondsLeft <= 0)
                FinishCurrentTest();
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_isTestRunning && _scenario != null)
            {
                _scenario.Update(TestCanvas);
                UpdateLiveStats();
            }
        }

        // ── Assessment flow ──────────────────────────────────────────────────

        private void BeginTest(int index)
        {
            if (index >= DiagnosticAssessmentService.Tests.Count)
            {
                ShowResults();
                return;
            }

            var test = DiagnosticAssessmentService.Tests[index];

            // Transition panels
            IntroPanel.Visibility    = Visibility.Collapsed;
            ResultsPanel.Visibility  = Visibility.Collapsed;
            TestCanvas.Visibility    = Visibility.Visible;
            NextTestBtn.Visibility   = Visibility.Visible;
            LiveStatsPanel.Visibility = Visibility.Visible;
            StartRecommendedBtn.Visibility = Visibility.Collapsed;

            // Update header
            CurrentTestLabel.Text = $"Test {index + 1}/8 — {DiagnosticAssessmentService.GetDimensionLabel(test.Dimension)}";
            CurrentTestDesc.Text  = test.Description;

            // Highlight current row in test list
            for (int i = 0; i < _testRows.Count; i++)
                _testRows[i].Background = i == index
                    ? new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0xD4, 0xFF))
                    : Brushes.Transparent;

            // Build scenario
            _scenario = CreateScenario(test);
            TestCanvas.Children.Clear();

            // Use medium config
            double targetSize = 36;
            double moveSpeed  = 2.5;
            _scenario.Start(TestCanvas, targetSize, moveSpeed, _rng);

            _secondsLeft   = test.DurationSeconds;
            _isTestRunning = true;
            UpdateTimerText();
            UpdateLiveStats();

            _gameTimer.Start();
            _updateTimer.Start();


        }

        private void FinishCurrentTest()
        {
            _gameTimer.Stop();
            _updateTimer.Stop();
            _isTestRunning = false;

            // Capture result
            if (_scenario != null)
            {
                var test   = DiagnosticAssessmentService.Tests[_currentTestIndex];
                int hits   = _scenario.Hits;
                int misses = _scenario.Misses;
                int total  = hits + misses;

                var result = new AimTrainerResult
                {
                    Timestamp             = DateTime.Now,
                    Scenario              = test.Scenario,
                    SubVariant            = test.Variant,
                    Difficulty            = "Medium",
                    DurationSeconds       = test.DurationSeconds,
                    Hits                  = hits,
                    Misses                = misses,
                    Accuracy              = total > 0 ? hits * 100.0 / total : 0,
                    Score                 = hits * 100,
                    AvgReactionMs         = _scenario.AvgReactionMs,
                    BestReactionMs        = _scenario.BestReactionMs < double.MaxValue
                                              ? _scenario.BestReactionMs : 0,
                    MaxStreak             = _scenario.MaxStreak,
                    IsAssessmentSession   = true,
                    AssessmentDimension   = test.Dimension,
                };

                _results.Add(result);
                MarkRowComplete(_currentTestIndex);

                _scenario.Stop(TestCanvas);
                _scenario = null;
            }

            // Advance
            _currentTestIndex++;
            UpdateOverallProgress();

            if (_currentTestIndex < DiagnosticAssessmentService.Tests.Count)
            {
                // Brief pause before next test
                var pause = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                pause.Tick += (s, _) =>
                {
                    ((DispatcherTimer)s!).Stop();
                    BeginTest(_currentTestIndex);
                };
                pause.Start();
            }
            else
            {
                ShowResults();
            }
        }

        private void ShowResults()
        {
            _isTestRunning = false;

            _gameTimer.Stop();
            _updateTimer.Stop();

            TestCanvas.Visibility    = Visibility.Collapsed;
            LiveStatsPanel.Visibility = Visibility.Collapsed;
            NextTestBtn.Visibility   = Visibility.Collapsed;

            // Build profile and save
            var settings = SettingsService.Load();
            int sessionNo = settings.DiagnosticHistory.Count + 1;
            _profile = DiagnosticAssessmentService.BuildProfile(_results, sessionNo);

            settings.DiagnosticHistory.Add(_profile);
            SettingsService.Save(settings);

            // Show results UI
            ResultsPanel.Visibility        = Visibility.Visible;
            StartRecommendedBtn.Visibility = Visibility.Visible;

            // TASK-06: free coaching report — show once if the entitlement hasn't been used
            if (!settings.HasUsedFreeAssessmentReport)
                ShowFreeReportBtn.Visibility  = Visibility.Visible;
            else
                FreeReportUsedText.Visibility = Visibility.Visible;

            CurrentTestLabel.Text = "Assessment Complete!";
            CurrentTestDesc.Text  = $"Weakest: {DiagnosticAssessmentService.GetDimensionLabel(_profile.WeakestDimension)}  ·  Strongest: {DiagnosticAssessmentService.GetDimensionLabel(_profile.StrongestDimension)}";

            BuildResultsUI(_profile);
            UpdateOverallProgress();
        }

        // ── Results card builder ─────────────────────────────────────────────

        private void BuildResultsUI(DiagnosticProfile profile)
        {
            ResultsContent.Children.Clear();

            // Title
            ResultsContent.Children.Add(new TextBlock
            {
                Text       = "Dimension Scores",
                FontSize   = 16, FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("PrimaryText"),
                Margin     = new Thickness(0, 0, 0, 16)
            });

            // Score bars for each dimension
            var scores = new (string Dim, double Score)[]
            {
                ("CloseRangeStatic",    profile.CloseRangeStatic),
                ("LongRangeStatic",     profile.LongRangeStatic),
                ("HorizontalTracking",  profile.HorizontalTracking),
                ("VerticalTracking",    profile.VerticalTracking),
                ("DiagonalTracking",    profile.DiagonalTracking),
                ("CloseSwitching",      profile.CloseSwitching),
                ("FarSwitching",        profile.FarSwitching),
                ("PeekReaction",        profile.PeekReaction),
            };

            foreach (var (dim, score) in scores)
            {
                bool isWeakest   = dim == profile.WeakestDimension;
                bool isStrongest = dim == profile.StrongestDimension;

                var rowStack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

                // Label row
                var labelRow = new Grid();
                labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                labelRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                string label = DiagnosticAssessmentService.GetDimensionLabel(dim);
                if (isWeakest)   label += " 🔻";
                if (isStrongest) label += " ⭐";

                var nameText = new TextBlock
                {
                    Text       = label,
                    FontSize   = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = isWeakest
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x80))
                        : (Brush)FindResource("PrimaryText"),
                };
                Grid.SetColumn(nameText, 0);

                var scoreText = new TextBlock
                {
                    Text       = $"{score:F0}",
                    FontSize   = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = ScoreColor(score),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin     = new Thickness(8, 0, 0, 0)
                };
                Grid.SetColumn(scoreText, 1);

                labelRow.Children.Add(nameText);
                labelRow.Children.Add(scoreText);
                rowStack.Children.Add(labelRow);

                // Score bar
                var barTrack = new Border
                {
                    Height        = 7,
                    CornerRadius  = new CornerRadius(3.5),
                    Background    = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                    Margin        = new Thickness(0, 5, 0, 0)
                };

                var barFill = new Border
                {
                    Height        = 7,
                    CornerRadius  = new CornerRadius(3.5),
                    Background    = ScoreColor(score),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width         = 0  // will animate or set on layout
                };

                // Overlay fill inside track via nested grid
                var barContainer = new Grid { Margin = new Thickness(0, 5, 0, 0) };
                barContainer.Children.Add(new Border
                {
                    Height       = 7,
                    CornerRadius = new CornerRadius(3.5),
                    Background   = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF))
                });

                double fillPct = Math.Clamp(score / 100.0, 0, 1);
                barContainer.Children.Add(new Border
                {
                    Height              = 7,
                    CornerRadius        = new CornerRadius(3.5),
                    Background          = ScoreColor(score),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width               = fillPct * 580  // approximate — real width set post-layout
                });

                rowStack.Children.Add(barContainer);

                // Wrap in a highlight border for weakest
                if (isWeakest)
                {
                    var highlight = new Border
                    {
                        CornerRadius = new CornerRadius(6),
                        Background   = new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0x80, 0x80)),
                        BorderBrush  = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x80, 0x80)),
                        BorderThickness = new Thickness(1),
                        Padding      = new Thickness(10, 8, 10, 8),
                        Margin       = new Thickness(0, 0, 0, 0),
                        Child        = rowStack
                    };
                    ResultsContent.Children.Add(highlight);
                }
                else
                {
                    ResultsContent.Children.Add(rowStack);
                }
            }

            // Recommendation card
            var (recScenario, recVariant) = DiagnosticAssessmentService.GetRecommendedStartingScenario(profile);
            var recCard = new Border
            {
                CornerRadius    = new CornerRadius(8),
                Background      = new SolidColorBrush(Color.FromArgb(0x15, 0x00, 0xD4, 0xFF)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(0x44, 0x00, 0xD4, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(14, 12, 14, 12),
                Margin          = new Thickness(0, 20, 0, 8)
            };

            var recStack = new StackPanel();
            recStack.Children.Add(new TextBlock
            {
                Text       = "Recommended Starting Drill",
                FontSize   = 11, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF)),
                Margin     = new Thickness(0, 0, 0, 4)
            });
            recStack.Children.Add(new TextBlock
            {
                Text       = $"{recScenario}  ·  {recVariant}",
                FontSize   = 14, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("PrimaryText")
            });
            recStack.Children.Add(new TextBlock
            {
                Text       = $"Targets your weakest dimension: {DiagnosticAssessmentService.GetDimensionLabel(profile.WeakestDimension)}",
                FontSize   = 11,
                Foreground = (Brush)FindResource("MutedText"),
                Margin     = new Thickness(0, 4, 0, 0)
            });
            recCard.Child = recStack;
            ResultsContent.Children.Add(recCard);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private IAimScenario CreateScenario(DiagnosticAssessmentService.AssessmentTest test)
        {
            return (test.Scenario, test.Variant) switch
            {
                ("StaticClicking",  var v) => new StaticClickingScenario(v),
                ("DynamicClicking", var v) => new DynamicClickingScenario(v),
                ("Reactive",        var v) => new ReactiveScenario(v),
                ("AirTracking",     var v) => new AirTrackingScenario(v),
                ("Evasive",         var v) => new EvasiveScenario(v),
                ("PeekTraining",    var v) => new PeekTrainingScenario(v),
                ("Tracking",        var v) => new TrackingScenario(v),
                ("Switching",       var v) => new SwitchingScenario(v),
                ("Flicking",        var v) => new StaticScenario("Flicking", v),
                ("Precision",       var v) => new StaticScenario("Precision", v),
                _                          => new StaticScenario("Precision", "Standard"),
            };
        }

        private void UpdateTimerText()
        {
            TimerText.Text       = $"0:{_secondsLeft:D2}";
            TimerText.Foreground = _secondsLeft <= 5
                ? new SolidColorBrush(Colors.OrangeRed)
                : (Brush)FindResource("AccentBrush");
        }

        private void UpdateLiveStats()
        {
            if (_scenario == null) return;
            int hits   = _scenario.Hits;
            int misses = _scenario.Misses;
            int total  = hits + misses;

            LiveHitsText.Text  = hits.ToString();
            LiveAccText.Text   = total > 0 ? $"{hits * 100.0 / total:F0}%" : "--";
            LiveReactText.Text = _scenario.AvgReactionMs > 0
                ? $"{_scenario.AvgReactionMs:F0}ms"
                : "--";
        }

        private void UpdateOverallProgress()
        {
            int completed = Math.Min(_currentTestIndex, DiagnosticAssessmentService.Tests.Count);
            OverallProgressText.Text = $"{completed} / {DiagnosticAssessmentService.Tests.Count} complete";

            double maxW = OverallProgressBar.Parent is Grid g ? 200 : 200;
            OverallProgressBar.Width = completed * 1.0 / DiagnosticAssessmentService.Tests.Count * maxW;
        }

        private void MarkRowComplete(int index)
        {
            if (index < 0 || index >= _testRows.Count) return;

            // Find the status dot (first TextBlock child of the row's inner grid)
            if (_testRows[index].Child is Grid g &&
                g.Children.Count > 0 &&
                g.Children[0] is TextBlock dot)
            {
                dot.Text       = "●";
                dot.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xA0));
            }

            _testRows[index].Background = new SolidColorBrush(Color.FromArgb(0x10, 0x00, 0xE5, 0xA0));
        }

        private static Brush ScoreColor(double score)
        {
            if (score >= 75) return new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xA0)); // green
            if (score >= 50) return new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF)); // cyan
            if (score >= 30) return new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x47)); // orange
            return new SolidColorBrush(Color.FromRgb(0xFF, 0x60, 0x60));                  // red
        }

        protected override void OnClosed(EventArgs e)
        {
            _gameTimer.Stop();
            _updateTimer.Stop();
            _scenario?.Stop(TestCanvas);
            base.OnClosed(e);
        }
    }
}
