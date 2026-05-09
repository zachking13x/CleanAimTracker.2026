using CleanAimTracker.Services;
using System.Windows;

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

            bool ok = await LicenseService.PurchaseAsync(LicenseService.STOREID_PRO);

            if (ok)
            {
                MessageBox.Show(
                    "You're now on Pro! Thank you for subscribing.",
                    "Purchase Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
                Close();
            }
            else
            {
                ProBtn.IsEnabled = true;
                ProBtn.Content   = "Pro  —  $5.99 / mo";
                MessageBox.Show(
                    "Purchase was canceled or could not be completed.",
                    "Purchase Canceled",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
            }
        }

        private async void ProTrainer_Click(object sender, RoutedEventArgs e)
        {
            ProTrainerBtn.IsEnabled = false;
            ProTrainerBtn.Content   = "Processing...";

            bool ok = await LicenseService.PurchaseAsync(LicenseService.STOREID_PRO_TRAINER);

            if (ok)
            {
                MessageBox.Show(
                    "You're now on Pro + Trainer! Thank you for subscribing.",
                    "Purchase Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
                Close();
            }
            else
            {
                ProTrainerBtn.IsEnabled = true;
                ProTrainerBtn.Content   = "Pro + Trainer  —  $8.99 / mo";
                MessageBox.Show(
                    "Purchase was canceled or could not be completed.",
                    "Purchase Canceled",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
            }
        }

        private async void Lifetime_Click(object sender, RoutedEventArgs e)
        {
            LifetimeBtn.IsEnabled = false;
            LifetimeBtn.Content   = "Processing...";

            bool ok = await LicenseService.PurchaseAsync(LicenseService.STOREID_LIFETIME);

            if (ok)
            {
                MessageBox.Show(
                    "You now have Lifetime Pro! Thank you for your support.",
                    "Purchase Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
                Close();
            }
            else
            {
                LifetimeBtn.IsEnabled = true;
                LifetimeBtn.Content   = "Lifetime Pro  —  $39.99";
                MessageBox.Show(
                    "Purchase was canceled or could not be completed.",
                    "Purchase Canceled",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => Close();
    }
}
