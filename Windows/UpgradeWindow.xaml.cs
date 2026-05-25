using CleanAimTracker.Services;
using System;
using System.Windows;
using System.Windows.Input;

namespace CleanAimTracker.Windows
{
    public partial class UpgradeWindow : Window
    {
        public UpgradeWindow()
        {
            InitializeComponent();
        }

        private async void Pro_Click(object sender, RoutedEventArgs e)
        {
            ProBtn.IsEnabled = false;
            ProBtn.Content   = "Processing...";
            try
            {
                bool ok = await LicenseService.PurchaseAsync(LicenseService.STOREID_PRO);
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
            LifetimeBtn.IsEnabled = false;
            LifetimeBtn.Content   = "Processing...";
            try
            {
                bool ok = await LicenseService.PurchaseAsync(LicenseService.STOREID_LIFETIME);
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
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Pro  — ", VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "$4.99 / month", FontSize = 16, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center });
            ProBtn.Content = stack;
        }

        private void RestoreLifetimeButton()
        {
            LifetimeBtn.IsEnabled = true;
            var stack = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Lifetime  — ", VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
            stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "$24.99", FontSize = 17, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
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
                await LicenseService.RefreshEntitlementsAsync();

                if (LicenseService.HasPro || LicenseService.HasLifetime)
                {
                    RefreshMainAndShowSuccess();
                }
                else
                {
                    MessageBox.Show(
                        "No active subscription was found on this Microsoft account.\n\n" +
                        "If you purchased on a different account, sign in to the Microsoft Store first, then try again.",
                        "Nothing to Restore",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
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
