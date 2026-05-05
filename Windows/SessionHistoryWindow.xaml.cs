using CleanAimTracker.Models;
using CleanAimTracker.Services;
using CleanAimTracker.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CleanAimTracker
{
    public partial class SessionHistoryWindow : Window
    {
        private List<SessionSummary> _sessions = new();

        public SessionHistoryWindow()
        {
            InitializeComponent();
            LoadSessions();
        }

        // ── Load & bind ──────────────────────────────────────────────
        private void LoadSessions()
        {
            _sessions = SessionStorage.LoadAll() ?? new List<SessionSummary>();
            _sessions = _sessions.OrderByDescending(s => s.Timestamp).ToList();
            SessionList.ItemsSource = _sessions;

            UpdateSummaryHeader();
            DrawTrendChart();
        }

        // ── Summary header stats ─────────────────────────────────────
        private void UpdateSummaryHeader()
        {
            TotalSessionsText.Text = $"{_sessions.Count} session{(_sessions.Count == 1 ? "" : "s")} recorded";

            if (_sessions.Count == 0)
            {
                AvgQualityText.Text = "Avg quality: --";
                TrendDirectionText.Text = "";
                return;
            }

            double avg = _sessions.Average(s => s.OverallQualityScore);
            AvgQualityText.Text = $"Avg quality: {avg:F0}/100";

            if (_sessions.Count >= 3)
            {
                // Compare newest 3 vs previous 3
                var newer = _sessions.Take(3).Average(s => s.OverallQualityScore);
                var older = _sessions.Skip(3).Take(3);
                if (older.Any())
                {
                    double olderAvg = older.Average(s => s.OverallQualityScore);
                    if (newer > olderAvg + 3)
                        TrendDirectionText.Text = "↑ Improving";
                    else if (newer < olderAvg - 3)
                        TrendDirectionText.Text = "↓ Declining";
                    else
                        TrendDirectionText.Text = "→ Stable";
                }
            }
        }

        // ── Canvas line chart ────────────────────────────────────────
        private void DrawTrendChart()
        {
            TrendChart.Children.Clear();

            if (_sessions.Count < 2) return;

            // Use chronological order for the chart (oldest → newest)
            var ordered = _sessions
                .OrderBy(s => s.Timestamp)
                .Take(20) // cap at 20 points for clarity
                .ToList();

            double w = TrendChart.ActualWidth;
            double h = TrendChart.ActualHeight;

            if (w <= 0 || h <= 0) return;

            double minScore = Math.Max(0, ordered.Min(s => s.OverallQualityScore) - 10);
            double maxScore = Math.Min(100, ordered.Max(s => s.OverallQualityScore) + 10);
            double range = maxScore - minScore;
            if (range <= 0) range = 10;

            double xStep = w / (ordered.Count - 1);
            double padding = 8;

            // Draw horizontal guide lines at 25, 50, 75, 100
            foreach (int guide in new[] { 25, 50, 75, 100 })
            {
                if (guide < minScore || guide > maxScore) continue;
                double gy = h - ((guide - minScore) / range * (h - padding * 2)) - padding;
                var guideLine = new Line
                {
                    X1 = 0,
                    Y1 = gy,
                    X2 = w,
                    Y2 = gy,
                    Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 4 }
                };
                TrendChart.Children.Add(guideLine);

                var label = new TextBlock
                {
                    Text = guide.ToString(),
                    Foreground = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    FontSize = 9
                };
                Canvas.SetLeft(label, 2);
                Canvas.SetTop(label, gy - 9);
                TrendChart.Children.Add(label);
            }

            // Build polyline points
            var points = new PointCollection();
            var dotPositions = new List<(double x, double y, double score)>();

            for (int i = 0; i < ordered.Count; i++)
            {
                double x = i * xStep;
                double y = h - ((ordered[i].OverallQualityScore - minScore) / range * (h - padding * 2)) - padding;
                points.Add(new Point(x, y));
                dotPositions.Add((x, y, ordered[i].OverallQualityScore));
            }

            // Shaded area under the line
            var areaPoints = new PointCollection(points);
            areaPoints.Add(new Point(w, h));
            areaPoints.Add(new Point(0, h));
            var area = new Polygon
            {
                Points = areaPoints,
                Fill = new LinearGradientBrush(
                    Color.FromArgb(60, 0, 229, 255),
                    Color.FromArgb(5, 0, 229, 255),
                    new Point(0, 0), new Point(0, 1)),
                Stroke = Brushes.Transparent
            };
            TrendChart.Children.Add(area);

            // Main line
            var polyline = new Polyline
            {
                Points = points,
                Stroke = new SolidColorBrush(Color.FromRgb(0, 229, 255)),
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round
            };
            TrendChart.Children.Add(polyline);

            // Dots at each data point
            foreach (var (x, y, score) in dotPositions)
            {
                var dot = new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = score >= 75 ? Brushes.LightGreen
                         : score >= 50 ? new SolidColorBrush(Color.FromRgb(0, 229, 255))
                         : Brushes.OrangeRed,
                    Stroke = new SolidColorBrush(Color.FromRgb(17, 17, 17)),
                    StrokeThickness = 1.5
                };
                Canvas.SetLeft(dot, x - 3.5);
                Canvas.SetTop(dot, y - 3.5);
                TrendChart.Children.Add(dot);
            }
        }

        // Redraw chart when canvas is resized
        private void TrendChart_SizeChanged(object sender, SizeChangedEventArgs e)
            => DrawTrendChart();

        // ── List events ──────────────────────────────────────────────
        private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void ViewSession_Click(object sender, RoutedEventArgs e)
        {
            if (SessionList.SelectedItem is not SessionSummary selected)
            {
                MessageBox.Show("Select a session first.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var customProfiles = ProfileStorage.LoadProfiles();
            var allProfiles = GameProfile.GetAllProfiles(customProfiles);
            var profile = allProfiles.Find(p => p.Name == selected.ProfileName)
                          ?? (allProfiles.Count > 0 ? allProfiles[0] : null);

            if (profile == null)
            {
                MessageBox.Show("No game profiles available.", "No Profiles",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var rec = RecommendationEngine.Analyze(selected, profile);
            new SummaryWindow(selected, rec).Show();
        }

        private void DeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (SessionList.SelectedItem is not SessionSummary selected)
            {
                MessageBox.Show("Select a session to delete.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Delete session from {selected.Timestamp:MMM dd yyyy h:mm tt}?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            _sessions.Remove(selected);
            var toSave = _sessions.OrderBy(s => s.Timestamp).ToList();

            SessionStorage.ClearAll();
            foreach (var s in toSave)
                SessionStorage.SaveSession(s);

            LoadSessions();
        }

        private void ExportSelected_Click(object sender, RoutedEventArgs e)
        {
            if (SessionList.SelectedItem is not SessionSummary selected)
            {
                MessageBox.Show("Select a session to export.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string folder = ExportService.GetDefaultExportFolder();
                string path = System.IO.Path.Combine(folder,
                    $"session_{selected.Timestamp:yyyy-MM-dd_HHmmss}.csv");
                ExportService.ExportToCsv(selected, path);
                MessageBox.Show($"Exported to:\n{path}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogService.Error("Export selected failed", ex);
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            if (_sessions.Count == 0)
            {
                MessageBox.Show("No sessions to export.", "Empty",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string folder = ExportService.GetDefaultExportFolder();
                string path = System.IO.Path.Combine(folder,
                    $"all_sessions_{DateTime.Now:yyyy-MM-dd_HHmmss}.csv");
                ExportService.ExportAllToCsv(_sessions, path);
                MessageBox.Show($"All {_sessions.Count} sessions exported to:\n{path}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogService.Error("Export all failed", ex);
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
