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
            bool ok = await LicenseService.PurchaseAsync(LicenseService.PRODUCT_PRO);

            if (ok)
            {
                MessageBox.Show("You now have Pro!", "Success");
                Close();
            }
            else
            {
                MessageBox.Show("Purchase failed or canceled.", "Error");
            }
        }

        private async void ProTrainer_Click(object sender, RoutedEventArgs e)
        {
            bool ok = await LicenseService.PurchaseAsync(LicenseService.PRODUCT_PRO_TRAINER);

            if (ok)
            {
                MessageBox.Show("You now have Pro + Trainer!", "Success");
                Close();
            }
            else
            {
                MessageBox.Show("Purchase failed or canceled.", "Error");
            }
        }

        private async void Lifetime_Click(object sender, RoutedEventArgs e)
        {
            bool ok = await LicenseService.PurchaseAsync(LicenseService.PRODUCT_LIFETIME);

            if (ok)
            {
                MessageBox.Show("You now own Pro for life!", "Success");
                Close();
            }
            else
            {
                MessageBox.Show("Purchase failed or canceled.", "Error");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
