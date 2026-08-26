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
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key?.GetValue("DigitalProductId") is not byte[] digitalProductId)
                return "Not recoverable";

            string decodedKey = DecodeProductKey(digitalProductId);
            return IsProductKey(decodedKey) ? decodedKey : "Not recoverable";
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
                var value = item["OA3xOriginalProductKey"]?.ToString()?.Trim();
                if (IsProductKey(value)) return value!;
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
                if (applicationId.Equals(WindowsLicensingApplicationId, StringComparison.OrdinalIgnoreCase) && licenseStatus == 1 && partialKey.Length == 5)
                    return partialKey;
            }
        }
        catch { }
        return string.Empty;
    }

    private static bool IsProductKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        System.Text.RegularExpressions.Regex.IsMatch(key.Trim(), @"(?i)^[A-Z0-9]{5}(?:-[A-Z0-9]{5}){4}$");

    private static string DecodeProductKey(byte[] digitalProductId)
    {
        const string chars = "BCDFGHJKMPQRTVWXY2346789";
        const int keyStart = 52;
        const int keyLength = 15;

        if (digitalProductId.Length < keyStart + keyLength || digitalProductId.Length <= 66)
            return string.Empty;

        // Work on a copy. The Windows 8+ decoding algorithm modifies byte 66.
        byte[] productId = (byte[])digitalProductId.Clone();
        int keyOffset = keyStart;
        int isWin8OrNewer = (productId[66] / 6) & 1;
        productId[66] = (byte)((productId[66] & 0xF7) | ((isWin8OrNewer & 2) * 4));

        var keyBytes = new byte[keyLength];
        Array.Copy(productId, keyOffset, keyBytes, 0, keyLength);

        string decoded = string.Empty;
        int last = 0;

        for (int i = 24; i >= 0; i--)
        {
            int current = 0;
            for (int j = 14; j >= 0; j--)
            {
                current = current * 256 + keyBytes[j];
                keyBytes[j] = (byte)(current / 24);
                current %= 24;
            }

            decoded = chars[current] + decoded;
            last = current;
        }

        // Windows 8 and later encode an N marker into the key. The previous
        // implementation removed the wrong character and produced a different,
        // unusable 25-character key.
        if (isWin8OrNewer == 1)
        {
            string prefix = decoded.Substring(1, Math.Min(last, decoded.Length - 1));
            decoded = decoded.Replace(prefix, prefix + "N", StringComparison.Ordinal);
            if (last == 0)
                decoded = "N" + decoded;
        }

        if (decoded.Length != 25)
            return string.Empty;

        return string.Join("-", Enumerable.Range(0, 5).Select(group => decoded.Substring(group * 5, 5)));
    }
}
