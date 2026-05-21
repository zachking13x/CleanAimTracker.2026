using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace CleanAimTracker.Windows
{
    public partial class AimTrainerResultWindow : Window
    {
        private readonly AimTrainerResult _result;
        private readonly bool _isReplay;
        private List<Achievement>? _newlyUnlocked;

        /// <param name="result">The drill result to display.</param>
        /// <param name="isReplay">True when opened via "Last Report" — hides Play Again, changes title.</param>
        public AimTrainerResultWindow(AimTrainerResult result, bool isReplay = false)
        {
            InitializeComponent();
            _result   = result;
            _isReplay = isReplay;

            if (isReplay)
            {
                Title             = "Last Coaching Report";
                PlayAgainBtn.Visibility = Visibility.Collapsed;
            }

            PopulateStats(result);
            _ = LoadCoachingAsync(result);

            if (!isReplay)
            {
                _ = EvaluateAchievementsAsync(result);
                _ = LoadPersonalBestsAsync(result);
                _ = ShowXpAsync(result);

                // Show share button when accuracy warrants it
                if (result.Accuracy >= 75)
                    ShareBtn.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Opens the most recent coaching report from storage, or shows a message if none exists.</summary>
        public static void OpenLastReport(Window owner)
        {
            var last = AimTrainerStorage.LoadLast();
            if (last == null)
            {
                MessageBox.Show(
                    "No coaching report yet. Complete an Aim Trainer drill to generate your first report.",
                    "No Report Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var win = new AimTrainerResultWindow(last, isReplay: true) { Owner = owner };
            win.Show();
        }

        // ── Achievement + daily challenge evaluation ──────────────────
        private async Task EvaluateAchievementsAsync(AimTrainerResult result)
        {
            try
            {
                var allResults = await Task.Run(() => AimTrainerStorage.LoadAll());

                // Reload settings after the async disk I/O so any save that happened
                // on the UI thread (e.g. Close_Click writing LastTomorrowPromptDate)
                // is not overwritten by a stale copy.
                var settings = SettingsService.Load();

                // Daily challenge — TryComplete increments settings.ChallengesCompleted on success
                var challenge = DailyChallengeService.GetToday();
                DailyChallengeService.TryComplete(challenge, result, settings);

                _newlyUnlocked = await Task.Run(() =>
                    AchievementService.EvaluateAfterSession(
                        result,
                        allResults,
                        settings.CurrentStreak,
                        settings.ChallengesCompleted));

                // Show achievement unlock popup (never on replay)
                if (_newlyUnlocked != null && _newlyUnlocked.Count > 0 && !_isReplay)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var popup = new AchievementUnlockWindow(_newlyUnlocked)
                        {
                            Owner = Window.GetWindow(this) ?? Application.Current.MainWindow
                        };
                        popup.ShowDialog();
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Error("Achievement evaluation failed", ex);
            }
        }

        // ── TASK-16 + TASK-17: Personal Bests + Top 5 ────────────────
        private async Task LoadPersonalBestsAsync(AimTrainerResult result)
        {
            try
            {
                var all = await Task.Run(() => AimTrainerStorage.LoadAll());

                // ── TASK-16: PB badges ────────────────────────────────────
                var same = all.Where(r => r.Scenario == result.Scenario).ToList();

                bool isBestScore    = same.Count == 0 || result.Score        >= same.Max(r => r.Score);
                bool isBestAccuracy = same.Count == 0 || result.Accuracy     >= same.Max(r => r.Accuracy);
                bool isBestStreak   = same.Count == 0 || result.MaxStreak    >= same.Max(r => r.MaxStreak);
                bool isBestReaction = result.BestReactionMs > 0 &&
                                      (same.Count == 0 ||
                                       result.BestReactionMs <= same.Where(r => r.BestReactionMs > 0)
                                                                    .Select(r => r.BestReactionMs)
                                                                    .DefaultIfEmpty(double.MaxValue)
                                                                    .Min());

                Dispatcher.Invoke(() =>
                {
                    PBBadgesPanel.Children.Clear();

                    // Collect which badges earned this session
                    var badgesToShow = new List<(string label, string bg)>();
                    if (isBestScore)    badgesToShow.Add(("🏆 New Best Score!", "#1B5E20"));
                    if (isBestAccuracy) badgesToShow.Add(("🎯 Best Accuracy!",  "#0D47A1"));
                    if (isBestReaction) badgesToShow.Add(("⚡ Best Reaction!",  "#E65100"));
                    if (isBestStreak)   badgesToShow.Add(("🔥 Best Streak!",    "#4A148C"));

                    if (badgesToShow.Count > 0)
                    {
                        PBBadgesPanel.Visibility = Visibility.Visible;

                        // Stagger each badge 150 ms apart — turns a data dump into a reveal
                        for (int i = 0; i < badgesToShow.Count; i++)
                        {
                            var (label, bg) = badgesToShow[i];
                            int delayMs = i * 150;

                            var b = new Border
                            {
                                Background   = new SolidColorBrush(
                                    (Color)ColorConverter.ConvertFromString(bg)),
                                CornerRadius = new CornerRadius(6),
                                Padding      = new Thickness(10, 4, 10, 4),
                                Margin       = new Thickness(0, 0, 8, 0),
                                Opacity      = 0,   // start invisible; animation reveals it
                            };
                            b.Child = new TextBlock
                            {
                                Text       = label,
                                FontSize   = 11,
                                FontWeight = FontWeights.Bold,
                                Foreground = Brushes.White,
                            };
                            PBBadgesPanel.Children.Add(b);

                            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                            {
                                BeginTime      = TimeSpan.FromMilliseconds(delayMs),
                                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                            };
                            b.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                        }
                    }
                    else
                    {
                        PBBadgesPanel.Visibility = Visibility.Collapsed;
                    }
                });

                // Delayed gold celebration banner for any new personal best
                bool anyPB = isBestScore || isBestAccuracy || isBestReaction || isBestStreak;
                if (anyPB)
                    _ = CelebratePersonalBests(isBestScore, isBestAccuracy, isBestReaction, isBestStreak);

                // ── TASK-17: Top 5 by score for this scenario ─────────────
                var top5 = all
                    .Where(r => r.Scenario == result.Scenario)
                    .OrderByDescending(r => r.Score)
                    .Take(5)
                    .Select((r, i) => new Top5Row
                    {
                        RankDisplay     = $"#{i + 1}",
                        DateDisplay     = r.Timestamp.ToString("MMM d"),
                        AccuracyDisplay = $"{r.Accuracy:F0}%",
                        ReactionDisplay = r.AvgReactionMs > 0 ? $"{r.AvgReactionMs:F0}ms" : "—",
                        ScoreDisplay    = r.Score.ToString("N0"),
                    })
                    .ToList();

                Dispatcher.Invoke(() =>
                {
                    if (top5.Count > 1)  // only show if there's more than 1 session to compare
                    {
                        Top5HeaderText.Text    = $"TOP {top5.Count} — {result.Scenario.ToUpperInvariant()}";
                        Top5List.ItemsSource   = top5;
                        Top5Panel.Visibility   = Visibility.Visible;
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.Error("Personal bests load failed", ex);
            }
        }

        private class Top5Row
        {
            public string RankDisplay     { get; set; } = "";
            public string DateDisplay     { get; set; } = "";
            public string AccuracyDisplay { get; set; } = "";
            public string ReactionDisplay { get; set; } = "";
            public string ScoreDisplay    { get; set; } = "";
        }

        // ── XP system ────────────────────────────────────────────────
        private async Task ShowXpAsync(AimTrainerResult result)
        {
            try
            {
                var (xpEarned, oldLevel, newLevel) = await Task.Run(() => XPService.AwardXP(result));
                var settings = await Task.Run(() => SettingsService.Load());
                var (current, range) = XPService.LevelProgress(settings.TotalXP, newLevel);

                Dispatcher.Invoke(() =>
                {
                    XpEarnedText.Text = $"+{xpEarned} XP";
                    XpLevelText.Text  = $"LEVEL {newLevel}";

                    // Animate XP bar fill
                    double maxWidth  = (XpProgressBar.Parent as FrameworkElement)?.ActualWidth ?? 400;
                    double fillRatio = Math.Clamp((double)current / range, 0, 1);
                    var anim = new DoubleAnimation(0, maxWidth * fillRatio,
                        TimeSpan.FromMilliseconds(900))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                        BeginTime      = TimeSpan.FromMilliseconds(300),
                    };
                    XpProgressBar.BeginAnimation(FrameworkElement.WidthProperty, anim);

                    // Show level-up banner when level increased
                    if (newLevel > oldLevel)
                    {
                        LevelUpText.Text           = $"LEVEL UP!  You reached Level {newLevel}";
                        LevelUpBanner.Visibility   = Visibility.Visible;
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.Error("XP display failed", ex);
            }
        }

        // ── Personal best gold banner (delayed) ──────────────────────
        private async Task CelebratePersonalBests(
            bool isBestScore, bool isBestAccuracy, bool isBestReaction, bool isBestStreak)
        {
            try
            {
                await Task.Delay(800);

                Dispatcher.Invoke(() =>
                {
                    // Build detail string
                    var parts = new System.Collections.Generic.List<string>();
                    if (isBestScore)    parts.Add("score");
                    if (isBestAccuracy) parts.Add("accuracy");
                    if (isBestReaction) parts.Add("reaction time");
                    if (isBestStreak)   parts.Add("streak");

                    PBBannerDetail.Text = parts.Count > 0
                        ? $"New record: {string.Join(", ", parts)}"
                        : string.Empty;

                    PBBanner.Visibility = Visibility.Visible;

                    // Fade in
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    PBBanner.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                });
            }
            catch (Exception ex)
            {
                LogService.Error("PB celebration failed", ex);
            }
        }

        // ── Share result card ─────────────────────────────────────────
        private void ShareResult_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bmp = BuildShareCard();
                Clipboard.SetImage(bmp);
                MessageBox.Show(
                    "Session card copied to clipboard — paste it anywhere!",
                    "Copied",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                LogService.Error("Share card build failed", ex);
                MessageBox.Show("Couldn't build share card.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private BitmapSource BuildShareCard()
        {
            // Build a small styled card in memory and render it
            var card = new Border
            {
                Width           = 500,
                Height          = 220,
                Background      = new SolidColorBrush(Color.FromRgb(0x0D, 0x11, 0x17)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(12),
                Padding         = new Thickness(28, 20, 28, 20),
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header row: scenario badge + score
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new TextBlock
            {
                Text       = _result.Scenario.ToUpperInvariant() + $"  •  {_result.Difficulty}  •  {_result.DurationSeconds}s",
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xAE)),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(badge, 0);

            var scoreLabel = new TextBlock
            {
                Text       = _result.Score.ToString("N0"),
                FontSize   = 36,
                FontWeight = FontWeights.Black,
                Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF)),
            };
            Grid.SetColumn(scoreLabel, 1);

            headerGrid.Children.Add(badge);
            headerGrid.Children.Add(scoreLabel);
            Grid.SetRow(headerGrid, 0);

            // Stats row
            var statsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin      = new Thickness(0, 12, 0, 12),
            };

            void AddStat(string label, string value, Color color)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 28, 0) };
                sp.Children.Add(new TextBlock
                {
                    Text       = label,
                    FontSize   = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x5A, 0x6A)),
                });
                sp.Children.Add(new TextBlock
                {
                    Text       = value,
                    FontSize   = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(color),
                });
                statsPanel.Children.Add(sp);
            }

            AddStat("ACCURACY",     $"{_result.Accuracy:F0}%",        Color.FromRgb(0x00, 0xE5, 0xA0));
            AddStat("AVG REACTION", $"{_result.AvgReactionMs:F0}ms",  Color.FromRgb(0xF0, 0xF4, 0xF8));
            AddStat("BEST STREAK",  _result.MaxStreak.ToString(),     Color.FromRgb(0xF5, 0xC8, 0x42));

            Grid.SetRow(statsPanel, 1);

            // Branding
            var brandText = new TextBlock
            {
                Text       = "Clean Aim Tracker",
                FontSize   = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0xD4, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            Grid.SetRow(brandText, 3);

            grid.Children.Add(headerGrid);
            grid.Children.Add(statsPanel);
            grid.Children.Add(brandText);
            card.Child = grid;

            // Measure + arrange + render
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            card.Arrange(new Rect(card.DesiredSize));
            card.UpdateLayout();

            var rtb = new RenderTargetBitmap(500, 220, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(card);
            return rtb;
        }

        // ── Populate stats immediately ────────────────────────────────
        private void PopulateStats(AimTrainerResult r)
        {
            ScoreText.Text        = r.Score.ToString("N0");
            string variantPart = string.IsNullOrEmpty(r.SubVariant) ? "" : $"  •  {r.SubVariant}";
            ScenarioBadgeText.Text = $"{r.Scenario}{variantPart}  •  {r.Difficulty}  •  {r.DurationSeconds}s";

            AccuracyText.Text    = $"{r.Accuracy:F0}%";
            AvgReactionText.Text = $"{r.AvgReactionMs:F0}ms";
            BestReactionText.Text = $"{r.BestReactionMs:F0}ms";
            StreakText.Text      = r.MaxStreak.ToString();

            HitsText.Text   = r.Hits.ToString();
            MissesText.Text = r.Misses.ToString();

            Loaded += (_, _) =>
            {
                double barWidth = (AccuracyBar.Parent as Border)?.ActualWidth ?? 300;
                AccuracyBar.Width = Math.Max(0, barWidth * (r.Accuracy / 100.0));
            };

            AccuracyText.Foreground = r.Accuracy >= 80
                ? System.Windows.Media.Brushes.LightGreen
                : r.Accuracy >= 60
                    ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                    : System.Windows.Media.Brushes.OrangeRed;
        }

        // ── Load AI coaching asynchronously ───────────────────────────
        private async Task LoadCoachingAsync(AimTrainerResult result)
        {
            CoachingLoading.Visibility = Visibility.Visible;
            CoachingContent.Visibility = Visibility.Collapsed;
            CoachingError.Visibility   = Visibility.Collapsed;

            try
            {
                var history       = await Task.Run(() => AimTrainerStorage.LoadAll());
                var recentTracker = await Task.Run(() => SessionStorage.LoadAll()
                                        ?.OrderByDescending(s => s.Timestamp)
                                        .FirstOrDefault(s => s.SessionSeconds >= 45));
                var report        = await Task.Run(() => AiCoachService.Analyze(result, history, recentTracker));
                PopulateCoaching(report);
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to load AI coaching", ex);
                CoachingLoading.Visibility = Visibility.Collapsed;
                CoachingError.Visibility   = Visibility.Visible;
            }
        }

        // ── Populate coaching panel ───────────────────────────────────
        private void PopulateCoaching(AiCoachReport report)
        {
            CoachingLoading.Visibility = Visibility.Collapsed;
            CoachingError.Visibility   = Visibility.Collapsed;
            CoachingContent.Visibility = Visibility.Visible;

            RatingText.Text   = report.OverallRating;
            HeadlineText.Text = report.Headline;

            RatingBadge.Background = report.OverallRating switch
            {
                "Excellent" => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 76, 175, 80)),
                "Great"     => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 0, 212, 255)),
                "Good"      => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 0, 212, 255)),
                "Developing"=> new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 255, 170, 68)),
                _           => new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(40, 211, 47, 47)),
            };

            RatingText.Foreground = report.OverallRating switch
            {
                "Excellent" => System.Windows.Media.Brushes.LightGreen,
                "Great"     => (System.Windows.Media.Brush)FindResource("AccentBrush"),
                "Good"      => (System.Windows.Media.Brush)FindResource("AccentBrush"),
                "Developing"=> System.Windows.Media.Brushes.Orange,
                _           => System.Windows.Media.Brushes.OrangeRed,
            };

            StrengthsList.ItemsSource  = report.Strengths;
            WeaknessesList.ItemsSource = report.Weaknesses;
            AdviceList.ItemsSource     = report.Advice;

            if (report.Prescription != null)
            {
                NextDrillText.Text = $"{report.Prescription.Scenario} • {report.Prescription.Difficulty}" +
                                     (report.Prescription.SubVariant != "Standard"
                                         ? $" • {report.Prescription.SubVariant}" : "");
                NextDrillReasonText.Text   = report.Prescription.Reason;
                NextDrillFocusCueText.Text = $"Focus cue: {report.Prescription.FocusCue}";
                StartPrescribedDrillBtn.Visibility = Visibility.Visible;
                StartPrescribedDrillBtn.Tag = report.Prescription;
            }
            else
            {
                NextDrillText.Text = report.NextDrillSuggestion;
            }
            MotivationalText.Text = report.MotivationalClose;
        }

        // ── Buttons ───────────────────────────────────────────────────
        private void PlayAgain_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is AimTrainerWindow trainer)
            {
                Close();
                trainer.StartNewDrill();
            }
            else
            {
                Close();
            }
        }

        private void StartPrescribedDrill_Click(object sender, RoutedEventArgs e)
        {
            if (StartPrescribedDrillBtn.Tag is Models.DrillPrescription prescription)
            {
                Close();
                if (Application.Current.MainWindow is MainWindow main)
                {
                    var trainer = new AimTrainerWindow { Owner = main };
                    trainer.PreSelectScenario(prescription.Scenario, prescription.Difficulty);
                    trainer.Show();
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Skip prompt when opened as a replay from Last Report
            if (_isReplay) { Close(); return; }

            // Only show the tomorrow prompt once per calendar day
            var settings = SettingsService.Load();
            LogService.Info("TomorrowPrompt: last=" + settings.LastTomorrowPromptDate.Date + " today=" + DateTime.Today);
            if (settings.LastTomorrowPromptDate.Date < DateTime.Today)
            {
                settings.LastTomorrowPromptDate = DateTime.Today;
                SettingsService.Save(settings);

                var response = MessageBox.Show(
                    "Come back tomorrow — 3 sessions builds a reliable trend.\n\nSchedule a reminder?",
                    "See You Tomorrow",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.None);

                if (response == MessageBoxResult.Yes)
                    ToastService.ScheduleTomorrowReminder();
            }

            Close();
        }
    }
}
