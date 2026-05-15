using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class AchievementsWindow : Window
    {
        public AchievementsWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => LoadAchievements();
        }

        private void LoadAchievements()
        {
            var all      = AchievementService.GetAllWithStatus();
            int unlocked = all.Count(a => a.IsUnlocked);
            int total    = all.Count;

            ProgressText.Text         = $"{unlocked} of {total} unlocked";
            OverallProgressBar.Value  = total > 0 ? (unlocked / (double)total) * 100 : 0;

            // Build view models with display date, grouped by category
            var viewItems = all.Select(a => new AchievementViewModel(a)).ToList();

            var groups = viewItems
                .GroupBy(a => a.Category)
                .OrderBy(g => g.Key)
                .Select(g => new AchievementGroup
                {
                    Category = g.Key,
                    Items    = g.OrderByDescending(a => a.IsUnlocked)
                                .ThenBy(a => a.Name)
                                .ToList(),
                })
                .ToList();

            AchievementGroups.ItemsSource = groups;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ── View models ───────────────────────────────────────────────────
        public class AchievementGroup
        {
            public string                   Category { get; set; } = "";
            public List<AchievementViewModel> Items  { get; set; } = new();
        }

        public class AchievementViewModel
        {
            public string  Id               { get; }
            public string  Name             { get; }
            public string  Description      { get; }
            public string  Emoji            { get; }
            public string  Category         { get; }
            public bool    IsUnlocked       { get; }
            public string  UnlockDateDisplay{ get; }

            public AchievementViewModel(Achievement a)
            {
                Id               = a.Id;
                Name             = a.Name;
                Description      = a.Description;
                Emoji            = a.IsUnlocked ? a.Emoji : "🔒";
                Category         = a.Category;
                IsUnlocked       = a.IsUnlocked;
                UnlockDateDisplay = a.IsUnlocked && a.UnlockedAt.HasValue
                    ? a.UnlockedAt.Value.ToLocalTime().ToString("MMM d")
                    : "";
            }
        }
    }
}
