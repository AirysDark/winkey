using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WinKey.Services;

namespace WinKey;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private ComputerReport? _report;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshReport();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (e.ClickCount == 2) ToggleMaximizeRestore(); else DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ToggleMaximizeRestore() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshReport();

    private void RefreshReport()
    {
        try
        {
            Cursor = Cursors.Wait;
            _report = SystemInfoService.GetReport();
            WindowsInfoBox.Text = _report.WindowsSection;
            HardwareInfoBox.Text = _report.HardwareSection;
            DriversInfoBox.Text = _report.DriversSection;
            NetworkInfoBox.Text = _report.NetworkSection;
            ReportBox.Text = _report.FullText;
        }
        catch (Exception ex) { MessageBox.Show(ex.ToString(), "WinKey error", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { Cursor = null; }
    }

    private void CheckActivationStatus_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string slmgrPath = Path.Combine(Environment.SystemDirectory, "slmgr.vbs");
            if (!File.Exists(slmgrPath)) throw new FileNotFoundException("Windows activation script slmgr.vbs was not found.", slmgrPath);
            Process.Start(new ProcessStartInfo { FileName = Path.Combine(Environment.SystemDirectory, "wscript.exe"), Arguments = $"\"{slmgrPath}\" /xpr", WorkingDirectory = Environment.SystemDirectory, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            ActivationStatusText.Text = "Activation status opened.";
        }
        catch (Exception ex) { ActivationInfoBox.Text = $"ERROR\r\n\r\n{ex}"; ActivationStatusText.Text = "Could not check activation status."; }
    }

    private void CreateWindowsMedia_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "MediaCreationTool.bat");
            if (!File.Exists(scriptPath)) throw new FileNotFoundException("MediaCreationTool.bat was not found next to WinKey.exe.", scriptPath);
            Process.Start(new ProcessStartInfo { FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"), Arguments = $"/k call \"{scriptPath}\"", WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Media Creation Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void BackupWindowsKey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ProductKeyDecodeResult installedResult = ProductKeyService.DecodeInstalledProductKey();
            string oemKey = ProductKeyService.GetOemProductKey();
            var choiceDialog = new KeyBackupChoiceWindow(installedResult, oemKey) { Owner = this };
            if (choiceDialog.ShowDialog() != true || choiceDialog.SelectedChoice == KeyBackupChoiceWindow.KeyChoice.None) return;

            string key = choiceDialog.SelectedKey.Trim();
            if (!ProductKeyDecoder.IsProductKey(key))
            {
                MessageBox.Show("The selected key is not a valid 25-character product key.", "No Product Key Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string keyType = choiceDialog.SelectedChoice == KeyBackupChoiceWindow.KeyChoice.Installed ? "Installed" : "OEM-UEFI";
            var dialog = new SaveFileDialog
            {
                Title = $"Save {keyType} Windows Product Key Backup",
                Filter = "Windows key backup (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"WindowsKey-{keyType}-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
                DefaultExt = ".txt",
                AddExtension = true
            };
            if (dialog.ShowDialog() != true) return;

            File.WriteAllText(dialog.FileName, key, new UTF8Encoding(false));
            MessageBox.Show($"Backup created successfully.\n\nSelected source: {keyType}\nExact key written to file:\n{key}", "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void RestoreWindowsKey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var choiceDialog = new KeyRestoreChoiceWindow { Owner = this };
            if (choiceDialog.ShowDialog() != true || choiceDialog.SelectedChoice == KeyRestoreChoiceWindow.KeyChoice.None)
            {
                ActivationStatusText.Text = "Restore cancelled.";
                return;
            }

            string keyType = choiceDialog.SelectedChoice == KeyRestoreChoiceWindow.KeyChoice.Installed ? "Installed" : "OEM/UEFI";
            string expectedFileHint = choiceDialog.SelectedChoice == KeyRestoreChoiceWindow.KeyChoice.Installed
                ? "WindowsKey-Installed"
                : "WindowsKey-OEM-UEFI";

            var dialog = new OpenFileDialog
            {
                Title = $"Select {keyType} Windows Product Key Backup",
                Filter = "Windows key backup (*.txt)|*.txt|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                ActivationStatusText.Text = "Restore cancelled.";
                return;
            }

            string productKey = ExtractProductKey(File.ReadAllText(dialog.FileName, Encoding.UTF8).Trim());
            if (!ProductKeyDecoder.IsProductKey(productKey))
                throw new InvalidDataException($"The selected {keyType} backup does not contain a usable Windows product key.");

            string fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
            if (!fileName.StartsWith(expectedFileHint, StringComparison.OrdinalIgnoreCase))
            {
                MessageBoxResult mismatch = MessageBox.Show(
                    $"You selected {keyType}, but this file name does not appear to be a {keyType} backup.\n\nFile: {Path.GetFileName(dialog.FileName)}\n\nRestore it anyway?",
                    "Backup Type Check",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (mismatch != MessageBoxResult.Yes)
                {
                    ActivationStatusText.Text = "Restore cancelled.";
                    return;
                }
            }

            if (MessageBox.Show($"Restore this {keyType} product key?\n\n{productKey}", "Restore & Activate Windows", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            ActivationStatusText.Text = $"Restoring selected {keyType} product key and activating Windows...";
            Cursor = Cursors.Wait;
            int exitCode = await RunRestoreScriptAsync(productKey);
            Cursor = null;
            ActivationStatusText.Text = exitCode == 0 ? $"{keyType} key restored and activation completed." : $"Restore finished with exit code {exitCode}.";
            RefreshReport();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { Cursor = null; ActivationStatusText.Text = "Restore cancelled."; }
        catch (Exception ex) { Cursor = null; ActivationStatusText.Text = "Restore failed."; MessageBox.Show(ex.Message, "Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static string ExtractProductKey(string text) => string.IsNullOrWhiteSpace(text) ? string.Empty : (Regex.Match(text, @"(?i)\b[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}\b") is Match match && match.Success ? match.Value.ToUpperInvariant() : string.Empty);

    private static async Task<int> RunRestoreScriptAsync(string productKey)
    {
        string scriptPath = Path.Combine(AppContext.BaseDirectory, "Restore_Windows_Key.ps1");
        if (!File.Exists(scriptPath)) throw new FileNotFoundException("Restore_Windows_Key.ps1 was not found next to WinKey.exe.", scriptPath);
        string powershellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (!File.Exists(powershellPath)) powershellPath = "powershell.exe";
        var psi = new ProcessStartInfo { FileName = powershellPath, Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\" -ProductKey \"{productKey}\"", WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
        using Process? process = Process.Start(psi);
        if (process == null) throw new InvalidOperationException("Could not start the Windows restore script.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private void InstallDrivers_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Driver installation is unchanged in this update.", "Install HP Drivers", MessageBoxButton.OK, MessageBoxImage.Information);
    private void CopyAll_Click(object sender, RoutedEventArgs e) { if (_report != null) Clipboard.SetText(_report.FullText); }
    private void ExportTxt_Click(object sender, RoutedEventArgs e) { if (_report == null) return; var dialog = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = $"WinKey-{DateTime.Now:yyyyMMdd-HHmmss}.txt" }; if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, _report.FullText, Encoding.UTF8); }
    private void ExportJson_Click(object sender, RoutedEventArgs e) { if (_report == null) return; var dialog = new SaveFileDialog { Filter = "JSON report (*.json)|*.json", FileName = $"WinKey-{DateTime.Now:yyyyMMdd-HHmmss}.json" }; if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_report, JsonOptions), Encoding.UTF8); }
}
