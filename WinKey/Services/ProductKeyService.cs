using Microsoft.Win32;
using System.Management;

namespace WinKey.Services;

public static class ProductKeyService
{
    public static string GetInstalledProductKey()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key?.GetValue("DigitalProductId") is not byte[] digitalProductId)
                return "Not found";

            return DecodeProductKey(digitalProductId);
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
                var value = item["OA3xOriginalProductKey"]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }
        catch
        {
        }

        return "No embedded OEM/UEFI key found";
    }

    private static string DecodeProductKey(byte[] digitalProductId)
    {
        const string chars = "BCDFGHJKMPQRTVWXY2346789";
        const int keyStart = 52;
        const int keyLength = 15;

        if (digitalProductId.Length < keyStart + keyLength || digitalProductId.Length <= 66)
            return "Not found";

        var keyBytes = digitalProductId.Skip(keyStart).Take(keyLength).ToArray();
        bool isWin8OrNewer = ((digitalProductId[66] / 6) & 1) != 0;

        if (isWin8OrNewer)
            digitalProductId[66] = (byte)((digitalProductId[66] & 0xF7) | ((2 & 4) * 4));

        var decodedCharacters = new char[25];
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

            decodedCharacters[i] = chars[current];
            last = current;
        }

        string decoded = new(decodedCharacters);

        // Windows 8 and newer use the special "N" insertion algorithm.
        // Do this before adding hyphens; manipulating an already formatted
        // 29-character key can corrupt the five-character groups.
        if (isWin8OrNewer)
        {
            decoded = decoded.Remove(0, 1).Insert(Math.Clamp(last, 0, 24), "N");
        }

        if (decoded.Length != 25)
            return "Not found";

        return string.Join("-", Enumerable.Range(0, 5)
            .Select(group => decoded.Substring(group * 5, 5)));
    }
}
