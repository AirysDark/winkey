using System.Windows;
using WinKey.Services;

namespace WinKey;

public partial class KeyBackupChoiceWindow : Window
{
    public enum KeyChoice { None, Installed, Oem }

    public KeyChoice SelectedChoice { get; private set; } = KeyChoice.Installed;
    public string SelectedKey { get; private set; } = string.Empty;

    public KeyBackupChoiceWindow(ProductKeyDecodeResult installedResult, string oemKey)
    {
        InitializeComponent();

        InstalledKeyText.Text = installedResult.InstalledKeyValid
            ? installedResult.InstalledKey
            : "Not recoverable";

        OemKeyText.Text = ProductKeyDecoder.IsProductKey(oemKey)
            ? oemKey
            : "No embedded OEM/UEFI key found";

        InstalledKeyOption.IsEnabled = installedResult.InstalledKeyValid;
        OemKeyOption.IsEnabled = ProductKeyDecoder.IsProductKey(oemKey);

        if (!installedResult.InstalledKeyValid && OemKeyOption.IsEnabled)
            OemKeyOption.IsChecked = true;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (InstalledKeyOption.IsChecked == true && InstalledKeyOption.IsEnabled)
        {
            SelectedChoice = KeyChoice.Installed;
            SelectedKey = InstalledKeyText.Text.Trim();
        }
        else if (OemKeyOption.IsChecked == true && OemKeyOption.IsEnabled)
        {
            SelectedChoice = KeyChoice.Oem;
            SelectedKey = OemKeyText.Text.Trim();
        }
        else
        {
            SelectedChoice = KeyChoice.None;
            SelectedKey = string.Empty;
        }

        DialogResult = ProductKeyDecoder.IsProductKey(SelectedKey);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedChoice = KeyChoice.None;
        SelectedKey = string.Empty;
        DialogResult = false;
    }
}
