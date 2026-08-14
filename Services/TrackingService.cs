using System.Net.NetworkInformation;
using System.Net.Sockets;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

/// <summary>
/// سرویس ردیابی مجاز: فقط اهداف صریحاً واردشده توسط کاربر مجاز اسکن می‌شوند.
/// اطلاعات هویتی، نشانی و تلفن هرگز از روی IP حدس یا تولید نمی‌شوند و فقط از منبع رسمی متصل‌شده به سامانه قابل ثبت هستند.
/// </summary>
public sealed class TrackingService
{
    private readonly DatabaseService _db;

    public static readonly int[] MinerPorts =
    {
        3333, 4028, 4444, 5555, 7777, 8008, 8080, 8333, 8443, 8545,
        8888, 9332, 9333, 9999, 14433, 14444, 18080, 19332, 19333
    };

    public static readonly MinerProfile[] KnownMiners =
    {
        new() { Key = "S19Pro", Name = "Antminer S19 Pro", HashRate = "110TH/s", PowerWatts = 1450 },
        new() { Key = "S19", Name = "Antminer S19", HashRate = "95TH/s", PowerWatts = 1326 },
        new() { Key = "L7", Name = "Antminer L7", HashRate = "9.5GH/s", PowerWatts = 3425 },
        new() { Key = "M50", Name = "Whatsminer M50", HashRate = "112TH/s", PowerWatts = 3418 },
        new() { Key = "A1366", Name = "Canaan A1366", HashRate = "70TH/s", PowerWatts = 3250 },
        new() { Key = "KAS", Name = "IceRiver KAS", HashRate = "4.7PH/s", PowerWatts = 3600 },
        new() { Key = "E9", Name = "Antminer E9", HashRate = "2.4GH/s", PowerWatts = 2400 },
        new() { Key = "GPU", Name = "GPU Mining Rig", HashRate = "Variable", PowerWatts = 1500 }
    };

    public event EventHandler<ScanProgressEventArgs>? ProgressChanged;

    public TrackingService(DatabaseService db) => _db = db;

    // Compatibility wrapper for the current UI. It deliberately does not fabricate identity data.
    public Task<List<DetectedDevice>> ScanAsync(string operationCode, IReadOnlyList<string> targetIps, CancellationToken ct = default) =>
        RunAuthorizedScanAsync(operationCode, "", targetIps, MinerPorts, false,
            AppSession.CurrentUser?.FullName ?? "سیستم", ct);

    public async Task<List<DetectedDevice>> RunAuthorizedScanAsync(
        string operationCode,
        string region,
        IReadOnlyList<string> targetIps,
        IReadOnlyList<int> ports,
        bool simulationMode,
        string authorizedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operationCode)) throw new ArgumentException("کد عملیاتی الزامی است.");
        if (targetIps.Count == 0 && !simulationMode) throw new ArgumentException("لیست IP هدف خالی است.");

        var start = DateTime.Now;
        var found = new List<DetectedDevice>();
        var portsToUse = ports.Count > 0 ? ports.ToArray() : MinerPorts;
        var rnd = new Random();

        if (simulationMode)
        {
            var simCount = rnd.Next(3, 12);
            for (int i = 0; i < simCount; i++)
            {
                ct.ThrowIfCancellationRequested(); await Task.Delay(180, ct);
                var miner = KnownMiners[rnd.Next(KnownMiners.Length)];
                var device = BuildDevice(operationCode, region, $"SIM-{i + 1:000}", portsToUse[rnd.Next(portsToUse.Length)], miner, rnd, 0.72 + rnd.NextDouble() * 0.25, true);
                found.Add(device); _db.SaveDetection(device); Report(i + 1, simCount, found.Count, $"شبیه‌سازی شناسایی {device.IPAddress}");
            }
        }
        else
        {
            int total = targetIps.Count * portsToUse.Length, scanned = 0;
            foreach (var ip in targetIps)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsValidIp(ip)) { scanned += portsToUse.Length; continue; }
                foreach (var port in portsToUse)
                {
                    ct.ThrowIfCancellationRequested(); scanned++;
                    if (await IsPortOpenAsync(ip, port, 600, ct))
                    {
                        var miner = GuessMiner(port, rnd);
                        var device = BuildDevice(operationCode, region, ip, port, miner, rnd, 0.55 + rnd.NextDouble() * 0.35, false);
                        found.Add(device); _db.SaveDetection(device);
                    }
                    if (scanned % 3 == 0 || scanned == total) Report(scanned, total, found.Count, $"بررسی {ip}:{port}");
                }
            }
        }

        var end = DateTime.Now;
        _db.SaveScanHistory(operationCode, simulationMode ? "Simulation" : "AuthorizedScan", start, end,
            simulationMode ? found.Count : targetIps.Count * portsToUse.Length, found.Count,
            $"مجازکننده: {authorizedBy} | منطقه: {region}");
        Report(100, 100, found.Count, "اسکن تکمیل شد");
        return found;
    }

    public async Task<List<DetectedDevice>> QuickLocalProbeAsync(string operationCode, string region, CancellationToken ct = default)
    {
        var localIps = GetLocalIpv4Addresses();
        var ports = new[] { 3333, 4028, 8333, 9332, 8080 };
        return await RunAuthorizedScanAsync(operationCode, region, localIps, ports, false,
            AppSession.CurrentUser?.FullName ?? "سیستم", ct);
    }

    private DetectedDevice BuildDevice(string opCode, string region, string ip, int port, MinerProfile miner, Random rnd, double conf, bool simulation)
    {
        return new DetectedDevice
        {
            OperationID = Guid.NewGuid().ToString("N"), OperationCode = opCode, IPAddress = ip, Port = port,
            // Location and subscriber identity are intentionally left empty until an authorized official source supplies them.
            Province = region ?? "", City = "", Street = "", PostalCode = "", SubscriberName = "", PhoneNumber = "",
            ISP = "", OperatorName = "", DeviceModel = miner.Name, HashRate = miner.HashRate,
            EstimatedConsumption = miner.PowerWatts, DetectionTime = DateTime.Now,
            ActionStatus = "منتظر‌دستورالعمل", Confidence = Math.Round(conf, 2),
            ActionNotes = simulation ? "داده آزمایشی؛ فاقد اطلاعات هویتی واقعی" : "اطلاعات هویتی از منبع رسمی ثبت نشده است"
        };
    }

    private static MinerProfile GuessMiner(int port, Random rnd)
    {
        if (port is 3333 or 14433) return KnownMiners[0];
        if (port is 4028) return KnownMiners[1];
        if (port is 8545) return KnownMiners[6];
        return KnownMiners[rnd.Next(KnownMiners.Length)];
    }

    private static bool IsValidIp(string ip) => System.Net.IPAddress.TryParse(ip.Trim(), out var addr) && addr.AddressFamily == AddressFamily.InterNetwork;

    private static async Task<bool> IsPortOpenAsync(string ip, int port, int timeoutMs, CancellationToken ct)
    {
        try { using var client = new TcpClient(); using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct); linked.CancelAfter(timeoutMs); await client.ConnectAsync(ip, port, linked.Token); return client.Connected; }
        catch { return false; }
    }

    private static List<string> GetLocalIpv4Addresses()
    {
        var list = new List<string> { "127.0.0.1" };
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !list.Contains(ua.Address.ToString())) list.Add(ua.Address.ToString());
            }
        }
        catch { }
        return list;
    }

    private void Report(int current, int total, int found, string message)
    {
        var pct = total <= 0 ? 0 : (int)Math.Min(100, current * 100.0 / total);
        ProgressChanged?.Invoke(this, new ScanProgressEventArgs { Percent = pct, Message = message, Found = found, Scanned = current });
    }
}
