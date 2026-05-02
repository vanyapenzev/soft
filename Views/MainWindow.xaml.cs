using System.Windows;
using VagImmoEditor.ViewModels;

namespace VagImmoEditor.Views;

/// <summary>
/// Логика взаимодействия для MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void About_Click(object sender, RoutedEventArgs e)
    {
        var message = @"VAG IMMO Editor v1.0

Программа для чтения и редактирования прошивок иммобилайзеров VAG Group.

Поддерживаемые чипы:
- 24LC64E (8KB)
- SN1413 1J0

Функции:
• Чтение PIN-кода иммобилайзера
• Просмотр пробега (одометр)
• Редактирование опций и настроек
• Поддержка различных типов IMMO (IMMO2, IMMO3, VDO, Motorola, NEC)

Стек технологий:
- C# 12
- WPF
- .NET 8
- BouncyCastle.Cryptography

© 2024 VAG Tools";

        MessageBox.Show(message, "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
