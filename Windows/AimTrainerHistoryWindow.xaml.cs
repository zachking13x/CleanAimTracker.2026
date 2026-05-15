using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class AimTrainerHistoryWindow : Window
    {
        private List<AimTrainerResult> _results = new();

        public AimTrainerHistoryWindow()
        {
            InitializeComponent();
            LoadHistory();
        }

        private void LoadHistory()
        {
            _results = AimTrainerStorage.LoadAll();
            _results.Reverse(); // newest first
            HistoryList.ItemsSource = _results;
            SubtitleText.Text = _results.Count == 0
                ? "No sessions recorded yet"
                : $"{_results.Count} session{(_results.Count == 1 ? "" : "s")} recorded";

            LoadPersonalBests();
        }

        private void LoadPersonalBests()
        {
            var scenarios = new[] { "Tracking", "Flicking", "Precision", "Switching" };
            var emojis    = new Dictionary<string, string>
            {
                ["Tracking"]  = "👁️",
                ["Flicking"]  = "⚡",
                ["Precision"] = "🎯",
                ["Switching"] = "🔄",
            };

            var bests = scenarios
                .Select(s =>
                {
                    var group = _results.Where(r => r.Scenario == s).ToList();
                    if (group.Count == 0) return null;
                    var best = group.OrderByDescending(r => r.Score).First();
                    return new PersonalBestRow
                    {
                        Scenario      = s,
                        ScenarioEmoji = emojis.GetValueOrDefault(s, "🎯"),
                        BestScore     = best.Score,
                        BestAccuracy  = group.Max(r => r.Accuracy),
                        BestReaction  = group.Where(r => r.BestReactionMs > 0).Select(r => r.BestReactionMs).DefaultIfEmpty(0).Min(),
                        BestStreak    = group.Max(r => r.MaxStreak),
                        AchievedOn    = best.Timestamp,
                    };
                })
                .Where(b => b != null)
                .ToList();

            PersonalBestList.ItemsSource = bests;
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is AimTrainerResult result)
                new AimTrainerResultWindow(result) { Owner = this }.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ── View model ────────────────────────────────────────────────────
        private class PersonalBestRow
        {
            public string   Scenario      { get; set; } = "";
            public string   ScenarioEmoji { get; set; } = "";
            public int      BestScore     { get; set; }
            public double   BestAccuracy  { get; set; }
            public double   BestReaction  { get; set; }
            public int      BestStreak    { get; set; }
            public DateTime AchievedOn    { get; set; }
        }
    }
}
