using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using CleanAimTracker.Models;
using CleanAimTracker.Services;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// AUDIT A4: this window used to contain "TODO: Add your real save logic here later"
    /// and set ProfileSaved = true without reading a single field — a user could fill it
    /// in, get no error, and lose everything they typed. It now validates and persists.
    ///
    /// NOTE: nothing in the app currently opens this window (the "REQUIRED by MainWindow"
    /// comment was stale — MainWindow never referenced it). It is kept working rather than
    /// silently lying, so that wiring up an entry point is a one-line change instead of a
    /// trap. Its styling is still plain WPF and does not match the app's dark theme; that
    /// needs doing before it is ever shown to a user.
    /// </summary>
    public partial class AddProfileWindow : Window
    {
        public bool ProfileSaved { get; private set; }

        /// <summary>The profile that was saved, or null if the user cancelled.</summary>
        public AimProfile? SavedProfile { get; private set; }

        public AddProfileWindow()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = (NameBox.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Give the profile a name.");
                return;
            }

            if (!TryParsePositive(DpiBox.Text, out double dpi) || dpi < 100 || dpi > 32000)
            {
                ShowError("DPI must be a number between 100 and 32000.");
                return;
            }

            if (!TryParsePositive(SensBox.Text, out double sens) || sens > 100)
            {
                ShowError("Sensitivity must be a positive number (100 or less).");
                return;
            }

            if (!TryParsePositive(Cm360Box.Text, out double cm360) || cm360 < 1 || cm360 > 300)
            {
                ShowError("cm/360 must be a number between 1 and 300.");
                return;
            }

            var profiles = ProfileStorage.LoadProfiles();

            if (profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                ShowError($"You already have a profile called \"{name}\".");
                return;
            }

            var profile = new AimProfile
            {
                Name        = name,
                DPI         = (int)Math.Round(dpi),
                Sensitivity = sens,
                CmPer360    = cm360,
            };

            profiles.Add(profile);
            ProfileStorage.SaveProfiles(profiles);

            // SaveProfiles swallows its own exceptions, so confirm the write landed rather
            // than reporting success on faith.
            bool persisted = ProfileStorage.LoadProfiles()
                .Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            if (!persisted)
            {
                ShowError("Could not save the profile. Check that CleanAimTracker can write to your AppData folder, then try again.");
                LogService.Error($"AddProfileWindow: profile \"{name}\" did not persist", null);
                return;
            }

            SavedProfile = profile;
            ProfileSaved = true;
            Close();
        }

        private static bool TryParsePositive(string? text, out double value)
        {
            // InvariantCulture so "1.5" works on comma-decimal locales too — most of the
            // current user base is outside the US.
            bool ok = double.TryParse(
                (text ?? "").Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);

            return ok && value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void ShowError(string message)
        {
            ErrorText.Text       = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
