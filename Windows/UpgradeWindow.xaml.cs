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
                // Restore the button content
                var stack = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Pro  — ", VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
                stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "$4.99 / month", FontSize = 16, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center });
                ProBtn.Content = stack;

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
                    "You now have Lifetime Pro! Thank you — enjoy it forever.",
                    "Purchase Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.None);
                Close();
            }
            else
            {
                LifetimeBtn.IsEnabled = true;
                // Restore the button content
                var stack = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "Lifetime  — ", VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
                stack.Children.Add(new System.Windows.Controls.TextBlock { Text = "$24.99", FontSize = 17, FontWeight = FontWeights.Black, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                LifetimeBtn.Content = stack;

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
