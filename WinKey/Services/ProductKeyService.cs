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
            if (key?.GetValue("DigitalProductId") is not byte[] digitalProductId) return "Not found";
            return DecodeProductKey(digitalProductId);
        }
        catch { return "Unavailable"; }
    }

    public static string GetOemProductKey()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT OA3xOriginalProductKey FROM SoftwareLicensingService");
            foreach (ManagementObject item in searcher.Get())
            {
                var value = item["OA3xOriginalProductKey"]?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }
        catch { }
        return "No embedded OEM/UEFI key found";
    }

    private static string DecodeProductKey(byte[] digitalProductId)
    {
        const string chars = "BCDFGHJKMPQRTVWXY2346789";
        const int keyStart = 52;
        var key = digitalProductId.Skip(keyStart).Take(15).ToArray();
        bool isWin8OrNewer = ((digitalProductId[66] / 6) & 1) != 0;
        if (isWin8OrNewer) digitalProductId[66] = (byte)((digitalProductId[66] & 0xF7) | ((2 & 4) * 4));
        int last = 0;
        var result = new char[29];
        for (int i = 28; i >= 0; i--)
        {
            if ((i + 1) % 6 == 0) { result[i] = '-'; continue; }
            int current = 0;
            for (int j = 14; j >= 0; j--)
            {
                current = current * 256 ^ key[j];
                key[j] = (byte)(current / 24);
                current %= 24;
                last = current;
            }
            result[i] = chars[current];
        }
        var decoded = new string(result);
        if (isWin8OrNewer)
        {
            int insert = last;
            decoded = decoded.Remove(0, 1).Insert(insert, "N");
        }
        return decoded;
    }
}
