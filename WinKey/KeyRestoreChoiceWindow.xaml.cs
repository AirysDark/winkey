using System.Windows;

namespace WinKey;

public partial class KeyRestoreChoiceWindow : Window
{
    public enum KeyChoice { None, Installed, Oem }

    public KeyChoice SelectedChoice { get; private set; } = KeyChoice.None;

    public KeyRestoreChoiceWindow()
    {
        InitializeComponent();
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        SelectedChoice = InstalledKeyOption.IsChecked == true
            ? KeyChoice.Installed
            : OemKeyOption.IsChecked == true
                ? KeyChoice.Oem
                : KeyChoice.None;

        DialogResult = SelectedChoice != KeyChoice.None;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedChoice = KeyChoice.None;
        DialogResult = false;
    }
}
