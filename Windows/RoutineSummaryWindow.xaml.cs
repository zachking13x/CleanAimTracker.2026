using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// CAT_ROUTINE: one report for the whole session.
    ///
    /// WHY THIS EXISTS RATHER THAN N RESULT WINDOWS: real telemetry showed a player run
    /// eight drills in seven minutes and close every coach report in 0-14 seconds — the
    /// per-drill report is a speed bump between reps, not a moment anyone reads. A
    /// routine therefore reports ONCE, over every drill together, at the point the
    /// player has actually stopped training and might look up.
    ///
    /// The full per-session coach analysis is still one click away for anyone who wants
    /// it — the depth isn't removed, it's moved to where attention exists.
    /// </summary>
    public partial class RoutineSummaryWindow : Window
    {
        private readonly List<AimTrainerResult> _results;

        public RoutineSummaryWindow(List<AimTrainerResult> results, string headline)
        {
            InitializeComponent();
            _results = results ?? new List<AimTrainerResult>();
            Populate(headline);
        }

        private void Populate(string headline)
        {
            if (_results.Count == 0) { SubtitleText.Text = "No drills were completed."; return; }

            HeadlineText.Text = string.IsNullOrWhiteSpace(headline) ? "Session complete" : $"{headline} — done";

            double avgAcc = _results.Average(r => r.Accuracy);
            int totalScore = _results.Sum(r => r.Score);
            int seconds = _results.Sum(r => r.DurationSeconds);

            DrillsText.Text   = _results.Count.ToString();
            AccuracyText.Text = $"{avgAcc:F0}%";
            ScoreText.Text    = totalScore.ToString("N0");
            TimeText.Text     = seconds >= 60 ? $"{seconds / 60}m" : $"{seconds}s";

            SubtitleText.Text = $"{_results.Count} drills back to back. Here's how each one went.";

            var accent = new SolidColorBrush(Color.FromRgb(0x00, 0xD4, 0xFF));
            var muted  = (Brush)FindResource("MutedText");
            var body   = (Brush)FindResource("PrimaryText");

            DrillList.Children.Clear();
            int n = 1;
            foreach (var r in _results)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var left = new StackPanel();
                left.Children.Add(new TextBlock
                {
                    Text = $"{n}. {DisplayName(r.Scenario)} · {r.Difficulty}",
                    FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = body,
                });
                left.Children.Add(new TextBlock
                {
                    Text = $"{r.Hits} hit · {r.Misses} missed · best streak {r.MaxStreak}",
                    FontSize = 11, Foreground = muted, Margin = new Thickness(0, 2, 0, 0),
                });
                Grid.SetColumn(left, 0);
                grid.Children.Add(left);

                var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                right.Children.Add(new TextBlock
                {
                    Text = $"{r.Accuracy:F0}%", FontSize = 20, FontWeight = FontWeights.Black,
                    Foreground = accent, TextAlignment = TextAlignment.Right,
                });
                Grid.SetColumn(right, 1);
                grid.Children.Add(right);

                DrillList.Children.Add(new Border
                {
                    Background = (Brush)FindResource("CardBackground"),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 0, 4, 8),
                    Child = grid,
                });
                n++;
            }

            // The one coaching line worth reading here — the rest is behind the button.
            CoachNoteText.Text = BuildNote(avgAcc);
        }

        /// <summary>
        /// A single honest line about the session. Deliberately narrow: this compares
        /// the session's own drills to each other, which needs no benchmark table and
        /// can't overclaim. Anything deeper is in the full report.
        /// </summary>
        private string BuildNote(double avgAcc)
        {
            if (_results.Count < 2)
                return $"One drill logged at {avgAcc:F0}% — open the full report for the breakdown.";

            var best  = _results.OrderByDescending(r => r.Accuracy).First();
            var worst = _results.OrderBy(r => r.Accuracy).First();

            if (best.Accuracy - worst.Accuracy < 8)
                return $"You held {avgAcc:F0}% across all {_results.Count} drills — that's a steady session.";

            return $"Your strongest drill was {DisplayName(best.Scenario)} at {best.Accuracy:F0}%, " +
                   $"your toughest was {DisplayName(worst.Scenario)} at {worst.Accuracy:F0}%.";
        }

        private static string DisplayName(string scenario) => scenario switch
        {
            "StaticClicking"  => "Static Clicking",
            "DynamicClicking" => "Dynamic Clicking",
            "SpeedSwitching"  => "Speed Switching",
            "AirTracking"     => "Air Tracking",
            "PeekTraining"    => "Peek Training",
            "HeadshotStrafes" => "Headshot Strafes",
            "PeekClick"       => "Peek & Click",
            "HeadTrack"       => "Track the Head",
            "SmgAr"           => "SMG / AR",
            _                 => scenario,
        };

        /// <summary>Opens the existing coach report for the LAST drill of the session.</summary>
        private void FullReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var last = _results.LastOrDefault();
                if (last == null) return;
                new AimTrainerResultWindow(last) { Owner = this }.ShowDialog();
            }
            catch (Exception ex) { LogService.Error("Open full report from routine summary failed", ex); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
