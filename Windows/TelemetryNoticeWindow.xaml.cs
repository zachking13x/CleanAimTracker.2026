using CleanAimTracker.Services;
using System.Windows;

namespace CleanAimTracker.Windows
{
    /// <summary>
    /// CAT_TELEMETRY: the one-time disclosure. Shown before any event is sent —
    /// TelemetryService.IsEnabled is gated on TelemetryNoticeShown, so an install that
    /// never sees this window never transmits anything.
    ///
    /// Both buttons are real. "No thanks" is not a soft dismiss: it routes through
    /// TelemetryService.SetEnabled(false), which shuts the client down and discards the
    /// install id. A disclosure whose decline button did nothing would be worse than no
    /// disclosure at all.
    /// </summary>
    public partial class TelemetryNoticeWindow : Window
    {
        public TelemetryNoticeWindow()
        {
            InitializeComponent();
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            TelemetryService.MarkDisclosureShown();
            Close();
        }

        private void Decline_Click(object sender, RoutedEventArgs e)
        {
            // Order matters: mark the notice shown FIRST so the opt-out isn't undone by a
            // later "we never disclosed, ask again" pass, then actually turn it off.
            TelemetryService.MarkDisclosureShown();
            TelemetryService.SetEnabled(false);
            Close();
        }

        /// <summary>Show once per install, if telemetry is configured and undisclosed.</summary>
        public static void ShowIfNeeded(Window? owner)
        {
            try
            {
                if (!TelemetryService.NeedsDisclosure()) return;
                var w = new TelemetryNoticeWindow();
                if (owner != null && owner.IsLoaded) w.Owner = owner;
                w.ShowDialog();
            }
            catch { /* a disclosure that fails to render must not block the app */ }
        }
    }
}
