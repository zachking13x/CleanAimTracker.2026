using CleanAimTracker.Services;
using System.Diagnostics;
using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class UpgradeDialog : Window
    {
        public UpgradeDialog()
        {
            InitializeComponent();
        }

        private void Upgrade_Click(object sender, RoutedEventArgs e)
        {
            var win = new UpgradeWindow();
            win.Owner = this;
            win.ShowDialog();
        }


        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public static void Show()
        {
            var win = new UpgradeDialog();
            win.Owner = Application.Current.MainWindow;
            win.ShowDialog();
        }
    }
}
