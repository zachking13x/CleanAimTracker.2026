using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class WeeklyReportWindow : Window
    {
        private List<SessionSummary> _weekSessions = new();

        public WeeklyReportWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var all = SessionStorage.LoadAll();
                var weekAgo = DateTime.Today.AddDays(-7);
                _weekSessions = all.Where(s => s.Timestamp.Date >= weekAgo).ToList();

                // Summary stats
                TotalSessionsText.Text = _weekSessions.Count.ToString();

                if (_weekSessions.Count > 0)
                {
                    AvgQualityText.Text = $"{_weekSessions.Average(s => s.OverallQualityScore):F0}";
                    AvgVelocityText.Text = $"{_weekSessions.Average(s => s.AverageVelocity):F1}";
                    BestQualityText.Text = $"{_weekSessions.Max(s => s.OverallQualityScore):F0}";
                }

                // Daily breakdown — newest first
                var dailyRows = new List<DailyRow>();
                for (int i = 0; i < 7; i++)
                {
                    var day = DateTime.Today.AddDays(-i);
                    var daySessions = _weekSessions.Where(s => s.Timestamp.Date == day).ToList();

                    string avgQuality = daySessions.Count > 0
                        ? $"{daySessions.Average(s => s.OverallQualityScore):F0}"
                        : "—";
                    string bestVel = daySessions.Count > 0
                        ? $"{daySessions.Max(s => s.PeakVelocity):F1} cm/s"
                        : "—";

                    dailyRows.Add(new DailyRow
                    {
                        DateLabel = day == DateTime.Today ? "Today" :
                                    day == DateTime.Today.AddDays(-1) ? "Yesterday" :
                                    day.ToString("ddd, MMM d"),
                        SessionCount = daySessions.Count == 0 ? "No sessions" : $"{daySessions.Count} session{(daySessions.Count == 1 ? "" : "s")}",
                        AvgQuality = avgQuality,
                        BestVelocity = bestVel
                    });
                }

                DailyList.ItemsSource = dailyRows;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load session data: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            if (_weekSessions.Count == 0)
            {
                MessageBox.Show("No sessions in the last 7 days to export.", "Nothing to Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Weekly Report",
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"WeeklyReport_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                ExportService.ExportAllToCsv(_weekSessions, dialog.FileName);
                MessageBox.Show($"Exported {_weekSessions.Count} sessions successfully!", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        private class DailyRow
        {
            public string DateLabel { get; set; } = "";
            public string SessionCount { get; set; } = "";
            public string AvgQuality { get; set; } = "—";
            public string BestVelocity { get; set; } = "—";
        }
    }
}
