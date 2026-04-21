using System.Windows;

namespace CleanAimTracker.Windows
{
    public partial class AddProfileWindow : Window
    {
        public AddProfileWindow()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Temporary logic so the app compiles
            this.Close();
        }
    }
}
