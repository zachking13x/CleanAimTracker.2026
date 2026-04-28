using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CleanAimTracker.Windows;

namespace CleanAimTracker
{
    public partial class SessionHistoryWindow : Window
    {
        private List<SessionSummary> _sessions;

        public SessionHistoryWindow()
        {
            InitializeComponent();
            LoadSessions();
        }

        private void LoadSessions()
        {
            _sessions = SessionStorage.LoadAll();
            _sessions.Reverse(); // newest first
            SessionList.ItemsSource = _sessions;
        }

        private void SessionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // No auto-action on selection
        }

        private void ViewSession_Click(object sender, RoutedEventArgs e)
        {
            if (SessionList.SelectedItem is not SessionSummary selected)
            {
                MessageBox.Show("Select a session first.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Load all profiles (built-in + custom)
            var customProfiles = ProfileStorage.LoadProfiles();
            var allProfiles = GameProfile.GetAllProfiles(customProfiles);

            // Match profile by name saved in the session
            var profile = allProfiles.Find(p => p.Name == selected.ProfileName)
                          ?? (allProfiles.Count > 0 ? allProfiles[0] : null);

            if (profile == null)
            {
                MessageBox.Show("No game profiles available to analyze this session.",
                    "No Profiles", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build recommendation for this historical session
            var rec = RecommendationEngine.Analyze(selected, profile);

            // Open summary window with both objects
            var win = new SummaryWindow(selected, rec);
            win.Show();
        }

        private void DeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (SessionList.SelectedItem is not SessionSummary selected)
            {
                MessageBox.Show("Select a session to delete.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Delete session from {selected.Timestamp:MMM dd yyyy h:mm tt}?",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _sessions.Remove(selected);

                // Re-reverse so the save order is chronological
                var toSave = new List<SessionSummary>(_sessions);
                toSave.Reverse();

                SessionStorage.ClearAll();
                foreach (var s in toSave)
                    SessionStorage.SaveSession(s);

                LoadSessions();
            }
        }

        private void ExportSelected_Click(object sender, RoutedEventArgs e)
        {
            if (SessionList.SelectedItem is not SessionSummary selected)
            {
                MessageBox.Show("Select a session to export.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string folder = ExportService.GetDefaultExportFolder();
                string timestamp = selected.Timestamp.ToString("yyyy-MM-dd_HHmmss");
                string csvPath = Path.Combine(folder, $"session_{timestamp}.csv");

                ExportService.ExportToCsv(selected, csvPath);

                MessageBox.Show($"Session exported to:\n{csvPath}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogService.Error("Export selected failed", ex);
                MessageBox.Show($"Export failed: {ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            if (_sessions == null || _sessions.Count == 0)
            {
                MessageBox.Show("No sessions to export.",
                    "Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string folder = ExportService.GetDefaultExportFolder();
                string csvPath = Path.Combine(folder,
                    $"all_sessions_{DateTime.Now:yyyy-MM-dd_HHmmss}.csv");

                ExportService.ExportAllToCsv(_sessions, csvPath);

                MessageBox.Show($"All {_sessions.Count} sessions exported to:\n{csvPath}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            }
            catch (Exception ex)
            {
                LogService.Error("Export all failed", ex);
                MessageBox.Show($"Export failed: {ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
