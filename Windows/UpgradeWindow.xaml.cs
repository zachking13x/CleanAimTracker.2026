using CleanAimTracker.Services;
using System;
using System.Windows;
using System.Windows.Input;

namespace CleanAimTracker.Windows
{
    public partial class UpgradeWindow : Window
    {
        // CAT_TELEMETRY: which surface opened this window. Every entry point names
        // itself so the funnel can answer "which prompt actually sells" rather than
        // just "the paywall was seen N times".
        private readonly string _trigger;

        public UpgradeWindow(string trigger = "unknown")
        {
            InitializeComponent();
            _trigger = trigger;

            // Prices come from the single source of truth (Services/Pricing.cs), which
            // must be kept in sync with Partner Center by hand.
            LifetimePriceText.Text   = Pricing.Lifetime;
            LifetimeBadgeText.Text   = Pricing.LifetimeLabel.ToUpperInvariant();
            LifetimeCaptionText.Text = Pricing.LifetimeCaption;
            MonthlyPriceText.Text    = Pricing.MonthlyPer;

            // A trial user buys the app, not an add-on. The monthly add-on is retired.
            if (LicenseService.IsAppTrial)
            {
                LifetimeCaptionText.Text = "One-time · the full app · no subscription";
                ProBtn.Visibility        = Visibility.Collapsed;
            }

            TelemetryService.TrackPaywallShown(_trigger);
        }

        private async void Pro_Click(object sender, RoutedEventArgs e)
        {
            // Purchase INTENT, not a completed sale — Partner Center owns revenue truth.
            // What this measures is how far down the funnel people get before dropping.
            TelemetryService.TrackPurchaseStarted("monthly", _trigger);
            ProBtn.IsEnabled = false;
            ProBtn.Content   = "Processing...";
            try
            {
                bool ok = await LicenseService.PurchaseAsync(LicenseService.STOREID_PRO);
                TelemetryService.TrackPurchaseCompleted(
                    "monthly", ok ? "succeeded" : "canceled", !LicenseService.IsFree);

                if (ok)
                {
                    RefreshMainAndShowSuccess();
                }
                else
                {
                    RestoreProButton();
                    MessageBox.Show(
                        "Purchase was canceled or could not be completed. You were not charged.\n\n" +
                        "If you already purchased, use \"Already purchased? Restore\" below.",
                        "Purchase Canceled",
                        MessageBoxButton.OK,
                        MessageBoxImage.None);
                }
            }
            catch (Exception ex)
            {
                TelemetryService.TrackPurchaseCompleted("monthly", "failed", !LicenseService.IsFree);
                LogService.Error("Pro_Click purchase failed", ex);
                RestoreProButton();
                MessageBox.Show(
                    "Something went wrong during the purchase. You were not charged — the transaction was not completed.\n\n" +
                    "Please try again, or use \"Already purchased? Restore\" to recover access.",
                    "Purchase Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void Lifetime_Click(object sender, RoutedEventArgs e)
        {
            TelemetryService.TrackPurchaseStarted("lifetime", _trigger);
            LifetimeBtn.IsEnabled = false;
            LifetimeBtn.Content   = "Processing...";
            try
            {
                bool ok = await LicenseService.PurchaseAsync(LicenseService.OneTimeStoreId);
                TelemetryService.TrackPurchaseCompleted(
                    "lifetime", ok ? "succeeded" : "canceled", TrialService.IsFullVersion());

                if (ok)
                {
                    RefreshMainAndShowSuccess();
                }
                else
                {
                    RestoreLifetimeButton();
                    MessageBox.Show(
                        "Purchase was canceled or could not be completed. You were not charged.\n\n" +
                        "If you already purchased, use \"Already purchased? Restore\" below.",
                        "Purchase Canceled",
                        MessageBoxButton.OK,
                        MessageBoxImage.None);
                }
            }
            catch (Exception ex)
            {
                TelemetryService.TrackPurchaseCompleted("lifetime", "failed", !LicenseService.IsFree);
                LogService.Error("Lifetime_Click purchase failed", ex);
                RestoreLifetimeButton();
                MessageBox.Show(
                    "Something went wrong during the purchase. You were not charged — the transaction was not completed.\n\n" +
                    "Please try again, or use \"Already purchased? Restore\" to recover access.",
                    "Purchase Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void RestoreProButton()
        {
            ProBtn.IsEnabled = true;
            var stack = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Or try monthly — ", VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = Pricing.MonthlyPer, FontSize = 14, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center });
            ProBtn.Content = stack;
        }

        private void RestoreLifetimeButton()
        {
            LifetimeBtn.IsEnabled = true;
            var stack = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "⭐  Unlock forever — ", VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = Pricing.Lifetime, FontSize = 18, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            LifetimeBtn.Content = stack;
        }

        // ── Shared post-purchase flow ─────────────────────────────────
        private void RefreshMainAndShowSuccess()
        {
            // Refresh the trial banner on the main window
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.UpdateTrialBanner();
                main.RefreshAfterPurchase();
            }

            // Show the celebration screen, then close this window
            new PostUpgradeWindow { Owner = this }.ShowDialog();
            Close();
        }

        // ── Restore purchases ─────────────────────────────────────────
        private async void RestorePurchases_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // AUDIT A6: this used to branch purely on the entitlement booleans, so a
                // Store timeout looked identical to "you own nothing" and the user was told
                // their purchase did not exist. Restore is exactly when someone is already
                // anxious about a purchase, so the two cases now read differently.
                var result = await LicenseService.RefreshEntitlementsAsync();

                switch (result)
                {
                    case EntitlementRefreshResult.Entitled:
                        RefreshMainAndShowSuccess();
                        break;

                    case EntitlementRefreshResult.NotEntitled:
                        MessageBox.Show(
                            "No active purchase was found on this Microsoft account.\n\n" +
                            "If you purchased on a different account, sign in to the Microsoft Store with that account first, then try again.",
                            "Nothing to Restore",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        break;

                    case EntitlementRefreshResult.StoreUnavailable:
                        MessageBox.Show(
                            "Could not reach the Microsoft Store, so your purchases could not be checked.\n\n" +
                            "This does not mean anything is missing. Check your connection, make sure you are signed in to the Store, and try again.",
                            "Store Unavailable",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogService.Error("RestorePurchases_Click failed", ex);
                MessageBox.Show(
                    "Could not check your purchase history. Please check your connection and try again.",
                    "Restore Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void RemindLater_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.Load();
            settings.PendingUpgradeReminder = true;
            SettingsService.Save(settings);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
