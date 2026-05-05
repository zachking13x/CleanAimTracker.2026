using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System.Collections.Generic;
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
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is AimTrainerResult result)
            {
                new AimTrainerResultWindow(result) { Owner = this }.ShowDialog();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

    }
}
