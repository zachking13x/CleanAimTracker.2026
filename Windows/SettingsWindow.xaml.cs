using CleanAimTracker.Services;
using System.Windows;
using System.Windows.Controls;
using CleanAimTracker.Models;


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

            DpiInput.Text = s.DPI.ToString();
            SensitivityInput.Text = s.Sensitivity.ToString("F4");

            ThemeSelector.SelectedIndex = s.Theme == "Light" ? 1 : 0;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(DpiInput.Text, out int dpi))
            {
                MessageBox.Show("Invalid DPI value.");
                return;
            }

            if (!double.TryParse(SensitivityInput.Text, out double sens))
            {
                MessageBox.Show("Invalid sensitivity value.");
                return;
            }

            string theme = ((ComboBoxItem)ThemeSelector.SelectedItem).Content.ToString();

            SettingsService.Save(new UserSettings

            {
                DPI = dpi,
                Sensitivity = sens,
                Theme = theme
            });

            MessageBox.Show("Settings saved!", "Success");
            Close();
        }
    }
}
