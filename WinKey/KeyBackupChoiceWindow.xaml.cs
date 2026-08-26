using System.Windows;
using WinKey.Services;

namespace WinKey;

public partial class KeyBackupChoiceWindow : Window
{
    public enum KeyChoice { None, Modern, Legacy, Oem }
    public KeyChoice SelectedChoice { get; private set; } = KeyChoice.Modern;
    public string SelectedKey { get; private set; } = string.Empty;

    public KeyBackupChoiceWindow(ProductKeyDecodeResult results, string oemKey)
    {
        InitializeComponent();
        ModernKeyText.Text = results.ModernKeyValid ? results.ModernKey : "Not recoverable";
        LegacyKeyText.Text = results.LegacyKeyValid ? results.LegacyKey : "Not recoverable";
        OemKeyText.Text = ProductKeyDecoder.IsProductKey(oemKey) ? oemKey : "No embedded OEM/UEFI key found";
        ModernKeyOption.IsEnabled = results.ModernKeyValid;
        LegacyKeyOption.IsEnabled = results.LegacyKeyValid;
        OemKeyOption.IsEnabled = ProductKeyDecoder.IsProductKey(oemKey);
        if (!results.ModernKeyValid && results.LegacyKeyValid) LegacyKeyOption.IsChecked = true;
        else if (!results.ModernKeyValid && !results.LegacyKeyValid && OemKeyOption.IsEnabled) OemKeyOption.IsChecked = true;
    }

    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        if (ModernKeyOption.IsChecked == true) { SelectedChoice = KeyChoice.Modern; SelectedKey = ModernKeyText.Text.Trim(); }
        else if (LegacyKeyOption.IsChecked == true) { SelectedChoice = KeyChoice.Legacy; SelectedKey = LegacyKeyText.Text.Trim(); }
        else if (OemKeyOption.IsChecked == true) { SelectedChoice = KeyChoice.Oem; SelectedKey = OemKeyText.Text.Trim(); }
        else { SelectedChoice = KeyChoice.None; SelectedKey = string.Empty; }
        DialogResult = ProductKeyDecoder.IsProductKey(SelectedKey);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        SelectedChoice = KeyChoice.None;
        SelectedKey = string.Empty;
        DialogResult = false;
    }
}
