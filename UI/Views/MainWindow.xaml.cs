using System.Windows;
using VagImmoEditor.Pro.UI.ViewModels;

namespace VagImmoEditor.Pro.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "VAG IMMO Editor Pro v2.0\n\n" +
                "Professional EEPROM editor for VAG instrument clusters.\n\n" +
                "Supported chips:\n- 24LC64 (8KB)\n- 24LC32 (4KB)\n- 24LC16 (2KB)\n\n" +
                "Supported IMMO types:\n- IMMO2\n- IMMO3 VDO/Motorola/NEC\n- IMMO4 (partial)\n\n" +
                "Features:\n- PIN Code reading/editing\n- Mileage correction\n- CRC auto-calculation\n- Undo/Redo support\n- JSON export\n- File comparison\n\n" +
                "© 2024 VAG Tools Team",
                "About VAG IMMO Editor Pro",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
