using CleanAimTracker.Models;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class AimTrainerResultWindow : Window
    {
        public AimTrainerResultWindow(AimTrainerResult result)
        {
            InitializeComponent();
            DataContext = result;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
