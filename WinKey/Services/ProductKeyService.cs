using Microsoft.Win32;
using System.Management;

namespace WinKey.Services;

public static class ProductKeyService
{
    private const string WindowsLicensingApplicationId = "55c92734-d682-4d71-983e-d6ec3f16059f";

    public static string GetInstalledProductKey()
    {
        try
        {
            ProductKeyDecodeResult result = DecodeInstalledProductKey();
            return result.InstalledKeyValid ? result.InstalledKey : "Not recoverable";
        }
        catch
        {
            return "Unavailable";
        }
    }

    // Reads the installed Windows DigitalProductId and returns the single
    // canonical Windows 8/10/11 decoded product key. No modern/legacy choice,
    // no licence-based substitution and no UI-derived value is used here.
    public static ProductKeyDecodeResult DecodeInstalledProductKey()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (key?.GetValue("DigitalProductId") is not byte[] digitalProductId)
            return new ProductKeyDecodeResult(string.Empty, false);

        return ProductKeyDecoder.Decode(digitalProductId);
    }

    // Backup uses this exact decoder output. It does not re-select, compare or
    // replace the decoded key with the displayed report key.
    public static string GetDecodedInstalledProductKey()
    {
        try
        {
            ProductKeyDecodeResult result = DecodeInstalledProductKey();
            return result.InstalledKeyValid ? result.InstalledKey : "Not recoverable";
        }
        catch
        {
            return "Unavailable";
        }
    }

    public static string GetOemProductKey()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT OA3xOriginalProductKey FROM SoftwareLicensingService");
            foreach (ManagementObject item in searcher.Get())
            {
                string? value = item["OA3xOriginalProductKey"]?.ToString()?.Trim();
                if (ProductKeyDecoder.IsProductKey(value))
                    return value!;
            }
        }
        catch { }

        return "No embedded OEM/UEFI key found";
    }

    public static string GetActiveWindowsPartialProductKey()
    {
        try
        {
            const string query = "SELECT ApplicationID, LicenseStatus, PartialProductKey FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL";
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject item in searcher.Get())
            {
                string applicationId = item["ApplicationID"]?.ToString() ?? string.Empty;
                int licenseStatus = item["LicenseStatus"] is null ? 0 : Convert.ToInt32(item["LicenseStatus"]);
                string partialKey = item["PartialProductKey"]?.ToString()?.Trim() ?? string.Empty;

                if (applicationId.Equals(WindowsLicensingApplicationId, StringComparison.OrdinalIgnoreCase) &&
                    licenseStatus == 1 && partialKey.Length == 5)
                    return partialKey;
            }
        }
        catch { }

        return string.Empty;
    }
}
