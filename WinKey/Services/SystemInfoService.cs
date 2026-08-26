using Microsoft.Win32;
using System.Management;
using System.Net.NetworkInformation;
using System.Text;

namespace WinKey.Services;

public sealed record ComputerReport(
    string GeneratedAt,
    string ComputerName,
    string WindowsEdition,
    string WindowsVersion,
    string WindowsBuild,
    string ActivationStatus,
    string ProductKey,
    string OemKey,
    string WindowsSection,
    string HardwareSection,
    string NetworkSection,
    string FullText);

public sealed class SystemInfoService
{
    public ComputerReport GetReport()
    {
        const string currentVersionPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        string computerName = Environment.MachineName;
        string edition = ReadRegistry(currentVersionPath, "ProductName");
        string version = ReadRegistry(currentVersionPath, "DisplayVersion");
        string build = ReadRegistry(currentVersionPath, "CurrentBuild") + "." + ReadRegistry(currentVersionPath, "UBR");
        string productKey = ProductKeyService.GetInstalledProductKey();
        string oemKey = ProductKeyService.GetOemProductKey();
        string activation = GetActivationStatus();

        var windows = new StringBuilder();
        windows.AppendLine($"Computer Name : {computerName}");
        windows.AppendLine($"Windows       : {edition}");
        windows.AppendLine($"Version       : {version}");
        windows.AppendLine($"Build         : {build}");
        windows.AppendLine($"Activation    : {activation}");
        windows.AppendLine($"Installed Key : {productKey}");
        windows.AppendLine($"OEM/UEFI Key  : {oemKey}");
        windows.AppendLine($"Product ID    : {ReadRegistry(currentVersionPath, "ProductId")}");
        windows.AppendLine($"Install Date  : {GetInstallDate()}");

        string hardware = GetHardwareInfo();
        string network = GetNetworkInfo();
        string full = $"WinKey Report\r\nGenerated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n\r\n=== WINDOWS & LICENCE ===\r\n{windows}\r\n=== HARDWARE ===\r\n{hardware}\r\n=== NETWORK ===\r\n{network}";

        return new ComputerReport(
            DateTime.Now.ToString("O"),
            computerName,
            edition,
            version,
            build,
            activation,
            productKey,
            oemKey,
            windows.ToString(),
            hardware,
            network,
            full);
    }

    private static string GetHardwareInfo()
    {
        var sb = new StringBuilder();
        AppendWmi(sb, "Computer System", "SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem", new[] { "Manufacturer", "Model", "TotalPhysicalMemory" });
        AppendWmi(sb, "CPU", "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor", new[] { "Name", "NumberOfCores", "NumberOfLogicalProcessors" });
        AppendWmi(sb, "Motherboard", "SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard", new[] { "Manufacturer", "Product", "SerialNumber" });
        AppendWmi(sb, "BIOS", "SELECT Manufacturer, SMBIOSBIOSVersion, SerialNumber FROM Win32_BIOS", new[] { "Manufacturer", "SMBIOSBIOSVersion", "SerialNumber" });
        AppendWmi(sb, "GPU", "SELECT Name, DriverVersion FROM Win32_VideoController", new[] { "Name", "DriverVersion" });
        AppendWmi(sb, "Disk", "SELECT Model, Size, SerialNumber FROM Win32_DiskDrive", new[] { "Model", "Size", "SerialNumber" });
        return sb.ToString();
    }

    private static string GetNetworkInfo()
    {
        var sb = new StringBuilder();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up))
        {
            sb.AppendLine(adapter.Name);
            sb.AppendLine($"  Type: {adapter.NetworkInterfaceType}");
            sb.AppendLine($"  MAC : {adapter.GetPhysicalAddress()}");

            foreach (var ip in adapter.GetIPProperties().UnicastAddresses)
                sb.AppendLine($"  IP  : {ip.Address}");
        }

        return sb.ToString();
    }

    private static string GetActivationStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL");

            var statuses = searcher.Get()
                .Cast<ManagementObject>()
                .Select(x => Convert.ToInt32(x["LicenseStatus"] ?? 0))
                .ToList();

            return statuses.Contains(1)
                ? "Activated"
                : "Not activated or activation state unavailable";
        }
        catch
        {
            return "Unable to determine";
        }
    }

    private static string GetInstallDate()
    {
        const string currentVersionPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        var raw = ReadRegistry(currentVersionPath, "InstallDate");

        return long.TryParse(raw, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime.ToString()
            : raw;
    }

    private static string ReadRegistry(string path, string name)
    {
        try
        {
            string registryPath = $"HKEY_LOCAL_MACHINE\{path}";
            return Registry.GetValue(registryPath, name, "Unknown")?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unavailable";
        }
    }

    private static void AppendWmi(StringBuilder sb, string title, string query, string[] fields)
    {
        sb.AppendLine($"[{title}]");

        try
        {
            using var searcher = new ManagementObjectSearcher(query);

            foreach (ManagementObject item in searcher.Get())
            {
                foreach (var field in fields)
                {
                    var value = item[field]?.ToString() ?? "Unknown";

                    if (field is "TotalPhysicalMemory" or "Size" &&
                        long.TryParse(value, out var bytes))
                    {
                        value = $"{bytes / 1024d / 1024d / 1024d:N2} GB";
                    }

                    sb.AppendLine($"{field}: {value}");
                }

                sb.AppendLine();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Unavailable: {ex.Message}");
        }
    }
}
