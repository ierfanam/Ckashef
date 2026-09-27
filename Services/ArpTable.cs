using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace GovernmentMiningApp.Services;

public sealed class ArpEntry
{
    public string IpAddress { get; init; } = "";
    public string MacAddress { get; init; } = "";
    public string EntryType { get; init; } = "";
    public bool IsDynamic => EntryType == "پویا";
}

/// <summary>
/// خواندن جدول ARP واقعی سیستم‌عامل (iphlpapi). هیچ داده‌ای ساخته نمی‌شود؛
/// فقط همان چیزی که ویندوز در جدول ARP دارد بازگردانده می‌شود.
/// </summary>
public static class ArpTable
{
    private const int MibIpnetTypeOther = 1;
    private const int MibIpnetTypeInvalid = 2;
    private const int MibIpnetTypeDynamic = 3;
    private const int MibIpnetTypeStatic = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct MibIpNetRow
    {
        public byte PhysicalAddressLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] PhysicalAddress;
        public uint State;
        public uint Type;
        public ulong Index;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern int GetIpNetTable(IntPtr table, ref int size, bool order);

    public static List<ArpEntry> Read()
    {
        var result = new List<ArpEntry>();
        if (!OperatingSystem.IsWindows()) return result;

        try
        {
            int size = 0;
            GetIpNetTable(IntPtr.Zero, ref size, false);
            if (size <= 0) return result;

            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (GetIpNetTable(buffer, ref size, false) != 0) return result;
                var count = Marshal.ReadInt32(buffer);
                int rowSize = Marshal.SizeOf<MibIpNetRow>();
                int offset = sizeof(int);
                offset = (offset + 7) / 8 * 8; // هم‌ترازی مطابق ساختار C

                for (int i = 0; i < count; i++)
                {
                    var row = Marshal.PtrToStructure<MibIpNetRow>(buffer + offset + i * rowSize);
                    if (row.PhysicalAddress == null || row.PhysicalAddressLength < 6) continue;

                    var mac = new StringBuilder();
                    for (int b = 0; b < 6; b++)
                        mac.Append(row.PhysicalAddress[b].ToString("X2")).Append(':');
                    var macText = mac.ToString().TrimEnd(':').ToLowerInvariant();

                    // آدرس IPv4 از چهار بایت آخر پیشوند جدول (هدر MIB_IPNETTABLE) خوانده می‌شود
                    var ip = ReadIPv4(buffer, offset + i * rowSize - 4);
                    if (ip == null) continue;

                    result.Add(new ArpEntry
                    {
                        IpAddress = ip,
                        MacAddress = macText,
                        EntryType = row.Type switch
                        {
                            MibIpnetTypeDynamic => "پویا",
                            MibIpnetTypeStatic => "ایستا",
                            MibIpnetTypeOther => "سایر",
                            MibIpnetTypeInvalid => "نامعتبر",
                            _ => "نامشخص"
                        }
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            // جدول ARP در دسترس نیست (یا سیستم‌عامل پشتیبانی نمی‌کند) — نتیجه خالی برگردانده می‌شود
        }
        return result;
    }

    private static string? ReadIPv4(IntPtr buffer, int addressOffset)
    {
        if (addressOffset < 0) return null;
        var bytes = new byte[4];
        Marshal.Copy(buffer + addressOffset, bytes, 0, 4);
        if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0) return null;
        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
    }

    public static Dictionary<string, ArpEntry> BuildMap()
    {
        var map = new Dictionary<string, ArpEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Read())
            map[entry.IpAddress] = entry;
        return map;
    }

    public static bool IsPrivateIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = address.GetAddressBytes();
        if (b[0] == 10) return true;
        if (b[0] == 127) return true;
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        if (b[0] == 192 && b[1] == 168) return true;
        if (b[0] == 169 && b[1] == 254) return true;
        return false;
    }
}
