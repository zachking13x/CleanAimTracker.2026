using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class RecommendationWindow : Window
    {
        public RecommendationWindow()
        {
            InitializeComponent();
        }

        public RecommendationWindow(object recommendation)
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
