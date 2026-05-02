using System.Windows;
using VagImmoEditor.UI.ViewModels;

namespace VagImmoEditor.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
