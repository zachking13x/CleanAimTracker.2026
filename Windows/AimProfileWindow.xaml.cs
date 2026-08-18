using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// CAT_AIM_RADAR detail view. The dashboard card answers "what shape am I?"; this
    /// answers "why is each spoke where it is, and what moves it?".
    ///
    /// HONESTY RULES carried over from the card, and made louder here because a detail
    /// view invites more trust than a glance:
    ///   • An axis with no sessions says NO DATA and names the drills that would fill it.
    ///     It never shows a zero.
    ///   • Every axis states how it was scored, including that Consistency is measured
    ///     against the player themselves rather than any published standard.
    ///   • Thin axes are labelled provisional with their session count visible, so a
    ///     reading built on two sessions can't be mistaken for a settled one.
    /// </summary>
    public partial class AimProfileWindow : Window
    {
        private AimRadarService.AimRadar? _radar;

        public AimProfileWindow(AimRadarService.AimRadar radar)
        {
            InitializeComponent();
            _radar = radar;
            Populate();
        }

        /// <summary>Build from storage — used by the dashboard so the caller needn't hold state.</summary>
        public static void Open(Window? owner)
        {
            try
            {
                var radar = AimRadarService.Build(AimTrainerStorage.LoadAll());
                if (!radar.HasEnoughData)
                {
                    MessageBox.Show(
                        "Your aim profile needs a few more drills before it can say anything useful.\n\n" +
                        "Run a few sessions across different scenarios and it'll fill in.",
                        "Not enough data yet", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var w = new AimProfileWindow(radar);
                if (owner != null && owner.IsLoaded) w.Owner = owner;
                w.ShowDialog();
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to open aim profile window", ex);
            }
        }

        private void Populate()
        {
            if (_radar == null) return;

            OverallText.Text = $"{_radar.AverageScore:F0}";
            SummaryText.Text = AimRadarService.Summarize(_radar);

            AxisList.Children.Clear();
            foreach (var axis in _radar.Axes)
                AxisList.Children.Add(BuildAxisCard(axis));

            var missing = _radar.Axes.Where(a => !a.HasData).Select(a => a.Name).ToList();
            var notes = new List<string>
            {
                $"Built from your {_radar.TotalSessions} logged sessions, weighted to the most recent " +
                $"{AimRadarService.RecentSessionsPerAxis} on each axis — this is current form, not a lifetime average."
            };
            if (missing.Count > 0)
                notes.Add($"{string.Join(" and ", missing)} {(missing.Count == 1 ? "has" : "have")} no sessions yet and " +
                          $"{(missing.Count == 1 ? "is" : "are")} shown empty rather than scored zero.");
            notes.Add("The overall number and the strongest/weakest call use only the benchmark-scored axes, " +
                      "because Consistency is measured on a different ruler.");
            FootnoteText.Text = string.Join(" ", notes);

            AimRadarRenderer.Draw(RadarCanvas, _radar, this, labelPadding: 46, labelFontSize: 12);
        }

        private Border BuildAxisCard(AimRadarService.RadarAxis a)
        {
            var accent = new SolidColorBrush(AimRadarRenderer.Accent);
            var muted  = (Brush)FindResource("MutedText");
            var body   = (Brush)FindResource("PrimaryText");

            var stack = new StackPanel();

            // Title row: name + score (or "NO DATA").
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBlock = new TextBlock
            {
                Text = a.Name, FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = a.HasData ? body : muted,
                Opacity = a.HasData ? 1.0 : 0.6,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(nameBlock, 0);
            head.Children.Add(nameBlock);

            var scoreBlock = new TextBlock
            {
                Text = a.HasData ? $"{a.Score:F0}" : "NO DATA",
                FontSize = a.HasData ? 20 : 11,
                FontWeight = FontWeights.Black,
                Foreground = a.HasData ? accent : muted,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(scoreBlock, 1);
            head.Children.Add(scoreBlock);
            stack.Children.Add(head);

            stack.Children.Add(new TextBlock
            {
                Text = a.Blurb, FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Foreground = muted, Margin = new Thickness(0, 2, 0, 8),
            });

            // Bar — only when there's something to draw.
            if (a.HasData)
            {
                var track = new Border
                {
                    Height = 7, CornerRadius = new CornerRadius(4),
                    Background = (Brush)FindResource("SeparatorBrush"),
                };
                var fill = new Border
                {
                    Height = 7, CornerRadius = new CornerRadius(4),
                    Background = accent,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Opacity = a.IsProvisional ? 0.55 : 1.0,
                };
                var barGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                barGrid.Children.Add(track);
                barGrid.Children.Add(fill);
                barGrid.SizeChanged += (_, e) =>
                    fill.Width = Math.Max(0, e.NewSize.Width * Math.Clamp(a.Score, 0, 100) / 100.0);
                stack.Children.Add(barGrid);
            }

            // Provenance: how many sessions, and how it was scored.
            string sessions = a.HasData
                ? $"From {a.SessionCount} session{(a.SessionCount == 1 ? "" : "s")}." +
                  (a.IsProvisional ? " Still settling — treat this one as provisional." : "")
                : "No sessions on this axis yet.";

            stack.Children.Add(new TextBlock
            {
                Text = sessions, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Foreground = muted, Margin = new Thickness(0, 0, 0, 4),
            });

            string basis = AimRadarService.ScoringBasis(a.Name);
            if (!string.IsNullOrEmpty(basis))
                stack.Children.Add(new TextBlock
                {
                    Text = basis, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                    Foreground = muted, Opacity = 0.85, Margin = new Thickness(0, 0, 0, 4),
                });

            if (!a.IsBenchmarkRelative)
                stack.Children.Add(new TextBlock
                {
                    Text = "Self-relative — not comparable to other players.",
                    FontSize = 11, FontStyle = FontStyles.Italic,
                    Foreground = muted, Opacity = 0.85,
                });

            // The route: which drills move this axis.
            var drills = AimRadarService.ScenariosFor(a.Name);
            if (drills.Count > 0)
                stack.Children.Add(new TextBlock
                {
                    Text = (a.HasData ? "Fed by: " : "Play to unlock: ") +
                           string.Join(", ", drills.Select(GetDisplayScenario)),
                    FontSize = 11, TextWrapping = TextWrapping.Wrap,
                    Foreground = a.HasData ? muted : accent,
                    Margin = new Thickness(0, 4, 0, 0),
                });

            bool isWeak   = a.Name == _radar!.WeakestAxis && a.HasData;
            bool isStrong = a.Name == _radar.StrongestAxis && a.HasData;

            return new Border
            {
                Background = (Brush)FindResource("CardBackground"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 13, 16, 13),
                Margin = new Thickness(0, 0, 4, 10),
                BorderThickness = new Thickness(isWeak || isStrong ? 1 : 0),
                BorderBrush = isWeak ? new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x47))
                            : isStrong ? accent
                            : Brushes.Transparent,
                Opacity = a.HasData ? 1.0 : 0.75,
                Child = stack,
            };
        }

        /// <summary>Trainer-facing names for scenario keys ("HeadshotStrafes" → "Headshot Strafes").</summary>
        private static string GetDisplayScenario(string scenario) => scenario switch
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

        private void RadarCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_radar != null)
                AimRadarRenderer.Draw(RadarCanvas, _radar, this, labelPadding: 46, labelFontSize: 12);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (_radar == null) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"My Clean Aim Tracker profile — {_radar.AverageScore:F0}/100 overall");
                foreach (var a in _radar.Axes)
                    sb.AppendLine(a.HasData
                        ? $"  {a.Name,-12} {a.Score,5:F0}{(a.IsProvisional ? "  (provisional)" : "")}"
                        : $"  {a.Name,-12}     —  (no sessions yet)");

                string summary = AimRadarService.Summarize(_radar);
                if (!string.IsNullOrEmpty(summary)) sb.AppendLine().AppendLine(summary);

                Clipboard.SetText(sb.ToString());
                CopyBtn.Content = "Copied";
            }
            catch (Exception ex) { LogService.Error("Copy aim profile failed", ex); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
