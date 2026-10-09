using CleanAimTracker.Models;
using CleanAimTracker.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

            SoundToggle.IsChecked = s.SoundEnabled;
            ReportEveryDrillToggle.IsChecked = s.ReportAfterEveryDrill;
            NotificationsToggle.IsChecked = s.NotificationsEnabled;
            // Honest signal: if Windows itself has CAT toasts off, say so — a checked
            // toggle wouldn't deliver anything.
            NotificationsOsWarning.Visibility =
                ToastService.OsAllowsToasts() ? Visibility.Collapsed : Visibility.Visible;

            // CAT_TELEMETRY: only offer the control when the build can actually send
            // anything. Showing a privacy toggle on an inert build would imply data is
            // leaving the machine when none is.
            bool configured = !string.IsNullOrWhiteSpace(TelemetryConfig.ConnectionString);
            PrivacySection.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
            TelemetryToggle.IsChecked = s.TelemetryEnabled;

            LoadCrosshair(s.Crosshair ?? new());
        }

        // ── CAT_CROSSHAIR editor ─────────────────────────────────────
        // Controls fire change events while InitializeComponent/LoadCrosshair set them;
        // the flag keeps those from rendering a half-populated crosshair.
        private bool   _crosshairReady;
        private string _crosshairColor = new CrosshairSettings().Color;

        private void LoadCrosshair(CrosshairSettings x)
        {
            _crosshairReady = false;
            CrosshairStyleSelector.SelectedIndex = (int)x.Style;
            _crosshairColor                     = x.Color;
            CrosshairColorInput.Text            = x.Color;
            CrosshairLengthSlider.Value         = x.Length;
            CrosshairThicknessSlider.Value      = x.Thickness;
            CrosshairGapSlider.Value            = x.Gap;
            CrosshairOpacitySlider.Value        = x.Opacity;
            CrosshairOutlineToggle.IsChecked    = x.Outline;
            _crosshairReady = true;
            UpdateCrosshairPreview();
        }

        private CrosshairSettings ReadCrosshair() => new()
        {
            Style     = (CrosshairStyle)Math.Max(0, CrosshairStyleSelector.SelectedIndex),
            Color     = _crosshairColor,
            Length    = (int)CrosshairLengthSlider.Value,
            Thickness = (int)CrosshairThicknessSlider.Value,
            Gap       = (int)CrosshairGapSlider.Value,
            Opacity   = (int)CrosshairOpacitySlider.Value,
            Outline   = CrosshairOutlineToggle.IsChecked == true,
        };

        private void Crosshair_Changed(object sender, RoutedEventArgs e)
        {
            if (!_crosshairReady) return;
            // A half-typed hex keeps the last valid colour rather than flashing a fallback.
            if (CrosshairRenderer.TryParseColor(CrosshairColorInput.Text, out byte r, out byte g, out byte b))
                _crosshairColor = $"#{r:X2}{g:X2}{b:X2}";
            UpdateCrosshairPreview();
        }

        private void CrosshairSwatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string hex }) CrosshairColorInput.Text = hex;   // TextChanged re-renders
        }

        private void CrosshairReset_Click(object sender, RoutedEventArgs e) => LoadCrosshair(new());

        private void UpdateCrosshairPreview()
        {
            var x = ReadCrosshair();
            CrosshairLengthValue.Text    = x.Length.ToString();
            CrosshairThicknessValue.Text = x.Thickness.ToString();
            CrosshairGapValue.Text       = x.Gap.ToString();
            CrosshairOpacityValue.Text   = $"{x.Opacity}%";

            // Same renderer and DPI as the trainer cursor, shown 1:1 in device pixels.
            double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var img = CrosshairRenderer.Render(x, dpi);
            var bmp = new WriteableBitmap(img.Size, img.Size, 96 * dpi, 96 * dpi, PixelFormats.Bgra32, null);
            bmp.WritePixels(new Int32Rect(0, 0, img.Size, img.Size), img.Bgra, img.Size * 4, 0);
            CrosshairPreview.Source = bmp;
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

            s.SoundEnabled = SoundToggle.IsChecked == true;
            s.Crosshair    = ReadCrosshair();
            s.ReportAfterEveryDrill = ReportEveryDrillToggle.IsChecked == true;
            SoundService.SetEnabled(s.SoundEnabled);   // apply immediately

            s.NotificationsEnabled   = NotificationsToggle.IsChecked == true;
            s.NotificationPermissionAsked = true;   // the user has now made an explicit choice

            SettingsService.Save(s);
            ThemeService.ApplyTheme(s.ThemeMode);

            // CAT_TELEMETRY: applied through the service, not written directly — it owns
            // sending the final opt-out event and discarding the install id, and it
            // re-reads settings itself so it can't be clobbered by the save above.
            if (PrivacySection.Visibility == Visibility.Visible)
                TelemetryService.SetEnabled(TelemetryToggle.IsChecked == true);

            // Apply the notification choice immediately: schedule fresh nudges if enabled,
            // or clear any pending ones if the user just turned them off.
            ToastService.RescheduleNudges();

            MessageBox.Show("Settings saved.", "Saved",
                MessageBoxButton.OK, MessageBoxImage.None);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close();

        // CAT_FIRST_FUN_AND_FUNNEL T1.2: hidden dev/tester view — tap the "Settings" header
        // 5× to read this install's LOCAL onboarding funnel (never transmitted anywhere).
        private int _devTapCount;
        private void DevFunnel_Tap(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (++_devTapCount < 5) return;
            _devTapCount = 0;
            // The preview-as-free toggle went with the free tier (CAT_PAID_APP), since
            // there is no paywall left to preview.
            try
            {
                MessageBox.Show(OnboardingFunnelService.FormatReport(),
                    "Dev tools (local only)", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { /* dev view only — never disrupt settings */ }
        }

        // CAT_REVIEW_PROMPT Gate 2 / T2.1: minimal feedback — opens a prefilled mail to the
        // dev. Pull, never push: only ever reached from Settings, never an unprompted popup.
        // Doubles as a private valve so a frustrated user vents here, not via a 1-star review.
        private void Feedback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = "mailto:zachking13@outlook.com?subject=" + Uri.EscapeDataString("CAT feedback");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex) { LogService.Error("Open feedback mail failed", ex); }
        }
    }
}
