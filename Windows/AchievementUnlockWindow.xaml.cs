using CleanAimTracker.Models;
using System.Collections.Generic;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class AchievementUnlockWindow : Window
    {
        public AchievementUnlockWindow(List<Achievement> newlyUnlocked)
        {
            InitializeComponent();

            AchievementList.ItemsSource = newlyUnlocked;

            SubtitleText.Text = newlyUnlocked.Count == 1
                ? "You unlocked a new achievement!"
                : $"You unlocked {newlyUnlocked.Count} achievements!";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
