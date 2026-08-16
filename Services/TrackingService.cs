using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

/// <summary>Production network observation service. It records only facts directly observed on authorized targets.</summary>
public sealed class TrackingService
{
    private readonly DatabaseService _db;
    private readonly IOfficialIdentitySource _identitySource;

    public static readonly int[] MinerPorts = { 3333, 4028, 4444, 5555, 7777, 8008, 8080, 8333, 8443, 8545, 8888, 9332, 9333, 9999, 14433, 14444, 18080, 19332, 19333 };
    public event EventHandler<ScanProgressEventArgs>? ProgressChanged;

    public TrackingService(DatabaseService db, IOfficialIdentitySource? identitySource = null)
    {
        _db = db;
        _identitySource = identitySource ?? new UnconfiguredOfficialIdentitySource();
    }

    public Task<List<DetectedDevice>> ScanAsync(string operationCode, IReadOnlyList<string> targetIps, CancellationToken ct = default)
        => RunAuthorizedScanAsync(operationCode, "", targetIps, MinerPorts, AppSession.CurrentUser?.FullName ?? "سیستم", ct);

    /// <summary>
    /// Scans only explicitly supplied targets. No simulation, IP-to-person inference,
    /// device guessing, fabricated hash-rate, or fabricated power values are permitted.
    /// </summary>
    public async Task<List<DetectedDevice>> RunAuthorizedScanAsync(
        string operationCode,
        string region,
        IReadOnlyList<string> targetIps,
        IReadOnlyList<int> ports,
        string authorizedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(operationCode)) throw new ArgumentException("کد عملیاتی الزامی است.");
        if (!AppSession.IsAuthenticated) throw new UnauthorizedAccessException("اسکن فقط برای کاربر احراز‌شده مجاز است.");
        if (targetIps.Count == 0) throw new ArgumentException("لیست IP هدف خالی است.");

        var portsToUse = ports.Count == 0 ? MinerPorts : ports.ToArray();
        var start = DateTime.Now;
        var found = new List<DetectedDevice>();
        var total = targetIps.Count * portsToUse.Length;
        var scanned = 0;

        foreach (var raw in targetIps)
        {
            ct.ThrowIfCancellationRequested();
            var ip = raw.Trim();
            if (!IPAddress.TryParse(ip, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork)
            {
                scanned += portsToUse.Length;
                Report(scanned, total, found.Count, $"نشانی نامعتبر: {ip}");
                continue;
            }

            foreach (var port in portsToUse)
            {
                ct.ThrowIfCancellationRequested();
                scanned++;
                if (!await IsPortOpenAsync(parsed, port, 600, ct))
                {
                    Report(scanned, total, found.Count, $"بررسی {ip}:{port}");
                    continue;
                }

                var observedAt = DateTime.UtcNow;
                var identity = await _identitySource.ResolveAsync(ip, observedAt, ct);
                var detection = new DetectedDevice
                {
                    OperationID = Guid.NewGuid().ToString("N"),
                    OperationCode = operationCode,
                    IPAddress = ip,
                    Port = port,
                    Province = identity.Province,
                    City = identity.City,
                    Street = identity.Street,
                    PostalCode = identity.PostalCode,
                    SubscriberName = identity.SubscriberName,
                    PhoneNumber = identity.PhoneNumber,
                    DeviceModel = "تأیید نشده",
                    HashRate = "تأیید نشده",
                    EstimatedConsumption = 0,
                    DetectionTime = DateTime.Now,
                    ActionStatus = "منتظر‌دستورالعمل",
                    Confidence = identity.MatchStatus == "Matched" ? 1.0 : 0,
                    EvidenceType = "NetworkObservation",
                    DataSource = identity.DataSource,
                    SourceRecordId = identity.SourceRecordId,
                    SourceQueriedAt = identity.SourceQueriedAt,
                    MatchStatus = identity.MatchStatus,
                    ActionNotes = identity.Notes
                };
                found.Add(detection);
                _db.SaveDetection(detection);
                Report(scanned, total, found.Count, $"مشاهده واقعی {ip}:{port}");
            }
        }

        _db.SaveScanHistory(operationCode, "AuthorizedScan", start, DateTime.Now, scanned, found.Count,
            $"مجازکننده: {authorizedBy} | منبع هویت: {_identitySource.SourceName}");
        Report(100, 100, found.Count, "اسکن تکمیل شد");
        return found;
    }

    public async Task<List<DetectedDevice>> QuickLocalProbeAsync(string operationCode, string region, CancellationToken ct = default)
    {
        var ips = GetLocalIpv4Addresses();
        return await RunAuthorizedScanAsync(operationCode, region, ips, new[] { 3333, 4028, 8333, 9332, 8080 }, AppSession.CurrentUser?.FullName ?? "سیستم", ct);
    }

    private static async Task<bool> IsPortOpenAsync(IPAddress address, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(timeoutMs);
            await client.ConnectAsync(address, port, timeout.Token);
            return client.Connected;
        }
        catch { return false; }
    }

    private static List<string> GetLocalIpv4Addresses()
    {
        var list = new List<string>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !list.Contains(ua.Address.ToString()))
                    list.Add(ua.Address.ToString());
        }
        return list;
    }

    private void Report(int current, int total, int found, string message)
        => ProgressChanged?.Invoke(this, new ScanProgressEventArgs
        {
            Percent = total <= 0 ? 0 : (int)Math.Min(100, current * 100.0 / total),
            Message = message,
            Found = found,
            Scanned = current
        });
}
