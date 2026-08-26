using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WinKey.Services;

namespace WinKey;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly string[] DriverInstallers =
    [
        "HP Privacy Settings.exe", "HP Power Manager.exe", "HP MAC Address Manager.exe", "System Default Settings.exe", "HP Connection Optimizer.exe",
        "Realtek PCIe Media Card Reader Driver.exe", "Intel Bluetooth Driver.exe", "Intel WLAN Driver.exe", "HP Hotkey Support - UWP.exe",
        "Synaptics (Validity) Fingerprint Sensor Driver.exe", "HP Universal Camera Driver.exe", "Synaptics Mouse Driver.exe",
        "Intel Video Driver and Control Panel.exe", "Intel Chipset Installation Utility and Driver.exe", "Intel Management Engine Driver.exe",
        "Intel Serial IO Driver.exe", "Intel Dynamic Platform and Thermal Framework Driver.exe", "Conexant HD Audio Driver - Coffee Lake.exe",
        "HP USB-C Dock G5 - Firmware.exe", "HP USB-C Dock G5 - Audio Driver.exe", "HP Elite USB-C Docking Station Driver.exe",
        "HP USB-C Mini Dock - Driver Pack.exe", "HP USB-C Universal Docking Station Driver.exe", "HP USB 3.0 Port Replicator and USB Travel Dock Driver.exe",
        "Remote HP PC Hardware Diagnostics UEFI.exe", "HP Windows Hardware Diagnostics.exe", "HP PC Hardware Diagnostics UEFI.exe", "HP Firmware Pack (Q85).exe"
    ];

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

    private async void CheckActivationStatus_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CheckActivationStatusButton.IsEnabled = false;
            ActivationStatusText.Text = "Checking detailed activation status...";

            string scriptPath = Path.Combine(AppContext.BaseDirectory, "Check_Activation_Status.cmd");
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException("Check_Activation_Status.cmd was not found next to WinKey.exe.", scriptPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{scriptPath}\"\"",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();
            process.OutputDataReceived += (_, args) => { if (args.Data != null) Dispatcher.Invoke(() => output.AppendLine(args.Data)); };
            process.ErrorDataReceived += (_, args) => { if (args.Data != null) Dispatcher.Invoke(() => output.AppendLine(args.Data)); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            string result = output.ToString().Trim();
            if (string.IsNullOrWhiteSpace(result))
                result = $"The activation status script finished with exit code {process.ExitCode}, but returned no readable output.";

            ActivationInfoBox.Text = result;
            ActivationStatusText.Text = process.ExitCode == 0
                ? "Activation status check finished."
                : $"Activation status check finished with exit code {process.ExitCode}.";
        }
        catch (Exception ex)
        {
            ActivationInfoBox.Text = $"ERROR\r\n\r\n{ex}";
            ActivationStatusText.Text = "Activation status check failed.";
        }
        finally { CheckActivationStatusButton.IsEnabled = true; }
    }

    private void BackupWindowsKey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _report ??= SystemInfoService.GetReport();
            string key = SelectBackupKey(_report);
            if (!IsUsableProductKey(key)) { MessageBox.Show("WinKey could not find a full Windows product key to back up.", "No Product Key Found", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var backup = new WindowsKeyBackup("WinKey Windows Product Key Backup", 1, DateTime.Now.ToString("O"), Environment.MachineName, _report.WindowsEdition, _report.WindowsVersion, _report.WindowsBuild, key, _report.OemKey);
            var dialog = new SaveFileDialog { Filter = "WinKey Backup (*.winkeybackup)|*.winkeybackup|JSON files (*.json)|*.json", FileName = $"WindowsKey-{DateTime.Now:yyyyMMdd-HHmmss}.winkeybackup" };
            if (dialog.ShowDialog() != true) return;
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(backup, JsonOptions), Encoding.UTF8);
            MessageBox.Show("Windows product key backup created successfully. Keep this file somewhere safe.", "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void RestoreWindowsKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "WinKey Backup (*.winkeybackup;*.json)|*.winkeybackup;*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            WindowsKeyBackup? backup = JsonSerializer.Deserialize<WindowsKeyBackup>(File.ReadAllText(dialog.FileName), JsonOptions);
            if (backup == null || backup.Format != "WinKey Windows Product Key Backup" || !IsUsableProductKey(backup.ProductKey)) throw new InvalidDataException("This is not a valid WinKey product key backup.");
            if (MessageBox.Show($"Install this Windows product key?\n\nWindows edition in backup: {backup.WindowsEdition}\nComputer backed up from: {backup.ComputerName}", "Restore Windows Product Key", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            var psi = new ProcessStartInfo { FileName = "cscript.exe", Arguments = $"//NoLogo \"{Path.Combine(Environment.SystemDirectory, "slmgr.vbs")}\" /ipk {backup.ProductKey}", UseShellExecute = true, Verb = "runas" };
            using Process? process = Process.Start(psi);
            if (process == null) throw new InvalidOperationException("Could not start Windows activation.");
            await process.WaitForExitAsync();
            RefreshReport();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { MessageBox.Show("Administrator permission was cancelled.", "Restore Cancelled", MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private static string SelectBackupKey(ComputerReport report) => IsUsableProductKey(report.ProductKey) ? report.ProductKey : report.OemKey;
    private static bool IsUsableProductKey(string? key) => !string.IsNullOrWhiteSpace(key) && key != "Unknown" && key != "Unavailable" && key.Length >= 25;

    private async void InstallDrivers_Click(object sender, RoutedEventArgs e)
    {
        string driverFolder = Path.Combine(AppContext.BaseDirectory, "hp-drivers");
        if (!Directory.Exists(driverFolder)) { MessageBox.Show($"The driver folder was not found:\n\n{driverFolder}", "WinKey - Driver Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        int foundCount = DriverInstallers.Count(name => File.Exists(Path.Combine(driverFolder, name)));
        if (foundCount == 0) { MessageBox.Show("No matching HP driver installers were found in hp-drivers.", "WinKey - No Drivers Found", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (MessageBox.Show($"WinKey found {foundCount} driver installer(s). Each will run one at a time and WinKey will wait for each installer to exit. Begin?", "Install HP Drivers", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        InstallDriversButton.IsEnabled = false;
        var log = new StringBuilder();
        try
        {
            for (int i = 0; i < DriverInstallers.Length; i++)
            {
                string name = DriverInstallers[i]; string path = Path.Combine(driverFolder, name);
                if (!File.Exists(path)) { log.AppendLine($"SKIPPED - Missing: {name}"); continue; }
                DriverInstallStatus.Text = $"Installing {i + 1}/{DriverInstallers.Length}: {name}";
                using Process? process = Process.Start(new ProcessStartInfo { FileName = path, WorkingDirectory = driverFolder, UseShellExecute = true });
                if (process == null) { log.AppendLine($"FAILED - Could not start: {name}"); continue; }
                await process.WaitForExitAsync(); log.AppendLine($"FINISHED - {name} (Exit code: {process.ExitCode})");
            }
            DriversInfoBox.Text = log + Environment.NewLine + DriversInfoBox.Text; DriverInstallStatus.Text = "Driver installation sequence finished.";
        }
        catch (Exception ex) { log.AppendLine($"FAILED - {ex.Message}"); DriversInfoBox.Text = log + Environment.NewLine + DriversInfoBox.Text; }
        finally { InstallDriversButton.IsEnabled = true; }
    }

    private void CopyAll_Click(object sender, RoutedEventArgs e) { if (_report != null) Clipboard.SetText(_report.FullText); }
    private void ExportTxt_Click(object sender, RoutedEventArgs e) { if (_report == null) return; var dialog = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", FileName = $"WinKey-{DateTime.Now:yyyyMMdd-HHmmss}.txt" }; if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, _report.FullText, Encoding.UTF8); }
    private void ExportJson_Click(object sender, RoutedEventArgs e) { if (_report == null) return; var dialog = new SaveFileDialog { Filter = "JSON report (*.json)|*.json", FileName = $"WinKey-{DateTime.Now:yyyyMMdd-HHmmss}.json" }; if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_report, JsonOptions), Encoding.UTF8); }

    private sealed record WindowsKeyBackup(string Format, int FormatVersion, string CreatedAt, string ComputerName, string WindowsEdition, string WindowsVersion, string WindowsBuild, string ProductKey, string OemProductKey);
}
