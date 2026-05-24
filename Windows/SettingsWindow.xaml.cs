using CleanAimTracker.Services;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = SettingsService.Load();
            DpiInput.Text          = s.DPI.ToString();
            SensitivityInput.Text  = s.Sensitivity.ToString("F4");
            ThemeSelector.SelectedIndex = s.ThemeMode == "Light" ? 1 : 0;

        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsService.Load();

            if (double.TryParse(DpiInput.Text, out double dpi) && dpi >= 100 && dpi <= 32000)
                s.DPI = (int)dpi;
            else if (double.TryParse(DpiInput.Text, out _))
            {
                MessageBox.Show("DPI must be between 100 and 32000.", "Invalid DPI",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (double.TryParse(SensitivityInput.Text, out double sens) && sens >= 0.001 && sens <= 100.0)
                s.Sensitivity = sens;
            else if (double.TryParse(SensitivityInput.Text, out _))
            {
                MessageBox.Show("Sensitivity must be between 0.001 and 100.", "Invalid Sensitivity",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            s.ThemeMode = ThemeSelector.SelectedIndex == 1 ? "Light" : "Dark";

            SettingsService.Save(s);
            ThemeService.ApplyTheme(s.ThemeMode);

            MessageBox.Show("Settings saved.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.None);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
