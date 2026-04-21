using System.Windows;
using System.Windows.Controls;

namespace CleanAimTracker.Windows
{
    public partial class ConverterWindow : Window
    {
        public ConverterWindow()
        {
            InitializeComponent();
        }

        private void CopyResult_Click(object sender, RoutedEventArgs e)
        {
            // Temporary logic so the app compiles
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OnInputChanged(object sender, TextChangedEventArgs e)
        {
            // Temporary logic so the app compiles
        }
    }
}
