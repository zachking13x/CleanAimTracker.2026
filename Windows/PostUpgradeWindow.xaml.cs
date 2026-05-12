using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class PostUpgradeWindow : Window
    {
        public PostUpgradeWindow()
        {
            InitializeComponent();
        }

        private void LetsGo_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
