using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CleanAimTracker.Services;


namespace CleanAimTracker.Windows
{
    public partial class ConverterWindow : Window
    {
        public ConverterWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SourceGameCombo.ItemsSource = GameSensitivityTranslators.GetSupportedGames();
            TargetGameCombo.ItemsSource = GameSensitivityTranslators.GetSupportedGames();
        }

        private void OnInputChanged(object sender, EventArgs e)
        {
            StatusText.Text = "";

            if (SourceGameCombo.SelectedItem == null ||
                TargetGameCombo.SelectedItem == null)
                return;

            if (!double.TryParse(DpiInput.Text, out double dpi) || dpi <= 0)
            {
                StatusText.Text = "Invalid DPI.";
                return;
            }

            if (!double.TryParse(SensInput.Text, out double sens) || sens <= 0)
            {
                StatusText.Text = "Invalid sensitivity.";
                return;
            }

            string source = SourceGameCombo.SelectedItem.ToString()!;
            string target = TargetGameCombo.SelectedItem.ToString()!;

            double cm360 = GameSensitivityTranslators.ToCm360(source, dpi, sens);
            SourceCm360Text.Text = $"{cm360:0.00} cm";

            double newSens = GameSensitivityTranslators.FromCm360(target, dpi, cm360);
            ResultSensText.Text = $"{newSens:0.0000}";

            TargetCm360Text.Text = $"{cm360:0.00} cm";

            TargetDpiText.Text = GameSensitivityTranslators.GetRecommendedDpi(target).ToString();
        }

        private void CopyResult_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ResultSensText.Text);
            StatusText.Text = "Copied!";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
