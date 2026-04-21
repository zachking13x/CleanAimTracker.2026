using System.Windows;
using System.Windows.Controls;

namespace CleanAimTracker.Windows
{
    public partial class ConverterWindow : Window
    {
        public ConverterWindow(string profileName, double dpi, double sensitivity)
        {
            InitializeComponent();

            // These must match your XAML element names
            ProfileNameText.Text = profileName;
            DpiInput.Text = dpi.ToString("F0");
            SensInput.Text = sensitivity.ToString("F4");
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
