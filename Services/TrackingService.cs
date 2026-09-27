using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

/// <summary>
/// اجرای پروب واقعی روی اهداف صریحاً اعلام‌شده.
/// هیچ حالت شبیه‌سازی در این سرویس وجود ندارد؛ تمام نتایج از اتصال واقعی TCP
/// و از پاسخ واقعی خود دستگاه به دست می‌آید.
/// </summary>
public sealed class TrackingService
{
    /// <summary>پورت‌های متداول سرویس‌های مدیریتی و استخر ماینر.</summary>
    public static readonly int[] DefaultMinerPorts =
    {
        3333, 4028, 4444, 5555, 7777, 8008, 8080, 8333, 8443, 8545,
        8888, 9332, 9333, 9999, 14433, 14444, 18080, 19332, 19333
    };

    public const int MaxTargets = 4096;
    public const int MaxProbes = 120_000;

    private readonly DatabaseService _db;
    private readonly MinerFingerprintService _fingerprint = new();
    private readonly NetworkIntelService _intel;

    public event EventHandler<ScanProgressEventArgs>? ProgressChanged;

    public TrackingService(DatabaseService db)
    {
        _db = db;
        _intel = new NetworkIntelService(db);
    }

    public async Task<ScanOutcome> RunAuthorizedScanAsync(ScanRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.OperationCode))
            throw new ArgumentException("کد عملیات الزامی است.");
        if (request.Targets.Count == 0)
            throw new ArgumentException("هیچ هدفی اعلام نشده است.");

        var startedAt = DateTime.Now;
        var sw = Stopwatch.StartNew();

        var ips = ExpandTargets(request.Targets, out var warnings);
        if (ips.Count == 0)
            throw new ArgumentException("هیچ آدرس IPv4 معتبری در فهرست اهداف یافت نشد.");

        var ports = request.Ports.Count > 0
            ? request.Ports.Distinct().OrderBy(p => p).ToArray()
            : DefaultMinerPorts;

        var totalProbes = (long)ips.Count * ports.Length;
        if (totalProbes > MaxProbes)
            throw new ArgumentException(
                $"حجم پروب {totalProbes} مورد بیش از حد مجاز ({MaxProbes}) است؛ محدوده را محدودتر کنید.");

        var scanId = Guid.NewGuid().ToString("N");
        var machine = Environment.MachineName;
        var authorizedBy = request.AuthorizedBy;

        _db.WriteAudit(authorizedBy, AppSession.CurrentUser?.Badge ?? "-", "ScanStarted", "ScanHistory",
            scanId,
            $"عملیات: {request.OperationCode} | منطقه: {request.Region} | اهداف: {ips.Count} | " +
            $"پورت‌ها: {ports.Length} | حداکثر موازی: {request.MaxParallel} | رایانه: {machine}" +
            (warnings.Count > 0 ? " | هشدار: " + string.Join(" | ", warnings) : ""));

        int probed = 0, open = 0, closed = 0, filtered = 0, identified = 0, errors = 0;
        var evidences = new List<ProbeEvidence>();
        var evidenceGate = new SemaphoreSlim(1);
        int lastReported = -1;

        Report(0, (int)totalProbes, 0, 0, sw.Elapsed.TotalSeconds,
            $"آغاز پروب {ips.Count} هدف روی {ports.Length} پورت");

        var work = new List<(string Ip, int Port)>(checked((int)totalProbes));
        foreach (var ip in ips)
            foreach (var port in ports)
                work.Add((ip, port));

        var arp = request.LookUpLocalMac ? ArpTable.BuildMap() : new Dictionary<string, ArpEntry>();

        await Parallel.ForEachAsync(work,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(request.MaxParallel, 1, 256),
                CancellationToken = ct
            },
            async (item, token) =>
            {
                var ev = await _fingerprint.ProbeAsync(item.Ip, item.Port, request.ConnectTimeoutMs, token)
                    .ConfigureAwait(false);

                int done = Interlocked.Increment(ref probed);
                switch (ev.PortState)
                {
                    case PortStates.Open:
                        Interlocked.Increment(ref open);
                        if (ev.SoftwareName != "" || ev.DeviceModel != "") Interlocked.Increment(ref identified);
                        await evidenceGate.WaitAsync(token).ConfigureAwait(false);
                        try { evidences.Add(ev); }
                        finally { evidenceGate.Release(); }
                        break;
                    case PortStates.Closed:
                        Interlocked.Increment(ref closed);
                        break;
                    case PortStates.Error:
                        Interlocked.Increment(ref errors);
                        break;
                    default:
                        Interlocked.Increment(ref filtered);
                        break;
                }

                int step = (int)Math.Max(1, totalProbes / 100);
                if (done % step == 0 || done == totalProbes)
                {
                    int snapshot = Volatile.Read(ref open);
                    if (done != lastReported)
                    {
                        lastReported = done;
                        Report(done, (int)totalProbes, snapshot, Volatile.Read(ref identified),
                            sw.Elapsed.TotalSeconds, $"پروب {item.Ip}:{item.Port} — یافته: {snapshot}");
                    }
                }
            }).ConfigureAwait(false);

        int intelFailures = 0;
        var devices = new List<DetectedDevice>(evidences.Count);
        int intelDone = 0;
        foreach (var ev in evidences)
        {
            ct.ThrowIfCancellationRequested();
            var device = await BuildDeviceAsync(request, scanId, ev, arp).ConfigureAwait(false);
            if (device == null) continue;
            if (request.ResolveNetworkOwner && device.IntelSource != "موفق") intelFailures++;
            devices.Add(device);
            intelDone++;
            Report(probed, (int)totalProbes, devices.Count, identified, sw.Elapsed.TotalSeconds,
                $"تکمیل استعلام شبکه {intelDone}/{evidences.Count}");
        }

        foreach (var device in devices)
            _db.SaveDetection(device);

        sw.Stop();
        var endedAt = DateTime.Now;

        var entry = new ScanHistoryEntry
        {
            ScanID = scanId,
            OperationCode = request.OperationCode,
            ScanType = "TCP-Probe",
            OperatorName = authorizedBy,
            MachineName = machine,
            StartTime = startedAt,
            EndTime = endedAt,
            IPsScanned = ips.Count,
            PortsProbed = (int)totalProbes,
            DevicesFound = devices.Count,
            IdentifiedDevices = identified,
            ClosedPorts = closed,
            FilteredPorts = filtered,
            ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 2),
            Notes = BuildScanNotes(ips.Count, ports.Length, closed, filtered, errors, intelFailures, warnings)
        };
        _db.SaveScanHistory(entry);

        _db.WriteAudit(authorizedBy, AppSession.CurrentUser?.Badge ?? "-", "ScanCompleted", "ScanHistory",
            scanId,
            $"پورت‌های باز: {devices.Count} | شناسایی‌شده: {identified} | بسته: {closed} | " +
            $"فیلترشده: {filtered} | مدت: {entry.ElapsedSeconds} ثانیه");

        Report((int)totalProbes, (int)totalProbes, devices.Count, identified, sw.Elapsed.TotalSeconds,
            "پایان پروب");

        return new ScanOutcome
        {
            ScanID = scanId,
            TargetsExpanded = ips.Count,
            PortsProbed = (int)totalProbes,
            OpenPorts = devices.Count,
            Identified = identified,
            ClosedPorts = closed,
            FilteredPorts = filtered,
            IntelFailures = intelFailures,
            ElapsedSeconds = entry.ElapsedSeconds,
            StartedAt = startedAt,
            EndedAt = endedAt,
            Devices = devices
        };
    }

    /// <summary>پروب مجدد یک شناسایی ثبت‌شده و به‌روزرسانی وضعیت «آخرین مشاهده».</summary>
    public async Task<DetectedDevice?> RefreshDetectionAsync(string operationId, CancellationToken ct)
    {
        var existing = _db.GetDetection(operationId);
        if (existing == null) return null;

        var ev = await _fingerprint.ProbeAsync(existing.IPAddress, existing.Port, 1500, ct)
            .ConfigureAwait(false);

        if (ev.PortState != PortStates.Open)
        {
            _db.WriteAudit("RefreshProbe", "DetectedOperations", operationId,
                $"پورت دیگر باز نیست: {existing.IPAddress}:{existing.Port} ({ev.PortState}) — {ev.Error}");
            return null;
        }

        var request = new ScanRequest
        {
            OperationCode = existing.OperationCode,
            Region = existing.Province,
            AuthorizedBy = AppSession.CurrentUser?.FullName ?? "سیستم",
            LookUpLocalMac = true
        };
        var arp = ArpTable.BuildMap();
        var device = await BuildDeviceAsync(request, existing.ScanID, ev, arp).ConfigureAwait(false);
        if (device == null) return null;

        device.OperationID = existing.OperationID;
        device.ActionStatus = existing.ActionStatus;
        device.ActionType = existing.ActionType;
        device.ActionEnforcedBy = existing.ActionEnforcedBy;
        device.ActionNotes = existing.ActionNotes;
        device.OperatorRecordID = existing.OperatorRecordID;
        device.Province = existing.Province;
        device.City = existing.City;
        device.Street = existing.Street;
        device.PostalCode = existing.PostalCode;
        device.SubscriberName = existing.SubscriberName;
        device.PhoneNumber = existing.PhoneNumber;
        device.DetectionTime = existing.DetectionTime;
        _db.SaveDetection(device);

        _db.WriteAudit("DetectionRefreshed", "DetectedOperations", operationId,
            $"{existing.IPAddress}:{existing.Port} — وضعیت: {ev.PortState} | اطمینان: {ev.Confidence:F2}");
        return device;
    }

    private async Task<DetectedDevice?> BuildDeviceAsync(
        ScanRequest request, string scanId, ProbeEvidence ev, Dictionary<string, ArpEntry> arp)
    {
        if (ev.PortState != PortStates.Open) return null;

        var device = new DetectedDevice
        {
            OperationID = Guid.NewGuid().ToString("N"),
            OperationCode = request.OperationCode,
            ScanID = scanId,
            IPAddress = ev.IpAddress,
            Port = ev.Port,
            PortState = ev.PortState,
            Protocol = ev.Protocol,
            DetectionMethod = ev.DetectionMethod,
            LatencyMs = ev.LatencyMs,
            HttpStatus = ev.HttpStatus,
            EvidenceHash = ev.EvidenceHash,
            EvidenceSummary = ev.EvidenceSummary,
            BannerText = ev.BannerText,
            SoftwareName = ev.SoftwareName,
            DeviceModel = ev.DeviceModel,
            FirmwareVersion = ev.FirmwareVersion,
            HashRate = ev.HashRate,
            TemperatureC = ev.TemperatureC,
            FanPercent = ev.FanPercent,
            PowerWatts = ev.PowerWatts,
            PowerSource = ev.PowerWatts.HasValue ? ev.PowerSource : "اعلام‌نشده توسط دستگاه",
            UptimeSeconds = ev.UptimeSeconds,
            PoolAddress = ev.PoolAddress,
            WorkerName = ev.WorkerName,
            Confidence = Math.Round(ev.Confidence, 2),
            DetectionTime = DateTime.Now,
            ActionStatus = "منتظر دستور"
        };

        if (arp.TryGetValue(ev.IpAddress, out var arpEntry))
        {
            device.MacAddress = arpEntry.MacAddress;
            device.EvidenceSummary += $" | MAC: {arpEntry.MacAddress} (جدول ARP: {arpEntry.EntryType})";
        }

        if (request.ResolveNetworkOwner)
        {
            var owner = await _intel.LookupAsync(ev.IpAddress, CancellationToken.None).ConfigureAwait(false);
            device.ReverseDns = owner.ReverseDns;
            device.AsNumber = owner.AsNumber;
            device.AsName = owner.AsName;
            device.NetworkName = owner.NetworkName;
            device.NetworkCountry = owner.Country;
            device.NetworkRegistrant = owner.Registrant;
            device.PrefixCidr = owner.PrefixCidr;
            device.IntelSource = owner.QueryStatus;
            device.IntelQueriedAt = owner.QueriedAt.ToString("yyyy-MM-dd HH:mm:ss");
            device.ISP = owner.AsName;
            device.OperatorName = owner.Registrant;
            if (owner.QueryStatus == "موفق" && owner.RawResponse.Length > 0)
                device.EvidenceHash = MinerFingerprintService.Sha256Hex(
                    device.EvidenceHash + "|" + owner.PayloadHash);
        }

        return device;
    }

    /// <summary>گسترش اهداف: آدرس تکی، بازه (a.b.c.d-e.f.g.h)، یا محدوده CIDR.</summary>
    public List<string> ExpandTargets(IEnumerable<string> raw, out List<string> warnings)
    {
        var result = new List<string>();
        warnings = new List<string>();

        foreach (var entry in raw)
        {
            var text = entry.Trim();
            if (text.Length == 0 || text.StartsWith('#')) continue;

            if (text.Contains('/'))
            {
                var parts = text.Split('/', 2);
                if (IPAddress.TryParse(parts[0], out var baseAddr) &&
                    int.TryParse(parts[1], out var prefix) && prefix is >= 0 and <= 32)
                {
                    result.AddRange(ExpandCidr(baseAddr, prefix));
                }
                else warnings.Add($"محدوده نامعتبر نادیده گرفته شد: {text}");
                continue;
            }

            if (text.Contains('-') && text.Count(c => c == '.') == 6)
            {
                var parts = text.Split('-', 2);
                if (IPAddress.TryParse(parts[0].Trim(), out var from) &&
                    IPAddress.TryParse(parts[1].Trim(), out var to))
                {
                    result.AddRange(ExpandRange(from, to));
                }
                else warnings.Add($"بازه نامعتبر نادیده گرفته شد: {text}");
                continue;
            }

            if (IPAddress.TryParse(text, out var single))
                result.Add(single.ToString());
            else
                warnings.Add($"آدرس نامعتبر نادیده گرفته شد: {text}");
        }

        var unique = result.Distinct().ToList();
        if (unique.Count > MaxTargets)
        {
            warnings.Add($"تعداد اهداف از {MaxTargets} بیشتر شد و به همین تعداد محدود گردید.");
            unique = unique.Take(MaxTargets).ToList();
        }
        return unique;
    }

    private static IEnumerable<string> ExpandCidr(IPAddress baseAddress, int prefix)
    {
        var bytes = baseAddress.GetAddressBytes();
        uint value = 0;
        foreach (var b in bytes) value = (value << 8) | b;

        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        var network = value & mask;
        var size = (long)Math.Pow(2, 32 - prefix);
        size = Math.Min(size, MaxTargets);

        for (long i = 0; i < size; i++)
        {
            var current = network + (uint)i;
            yield return $"{(current >> 24) & 0xFF}.{(current >> 16) & 0xFF}.{(current >> 8) & 0xFF}.{current & 0xFF}";
        }
    }

    private static IEnumerable<string> ExpandRange(IPAddress from, IPAddress to)
    {
        var a = from.GetAddressBytes();
        var b = to.GetAddressBytes();
        long start = 0, end = 0;
        for (int i = 0; i < 4; i++)
        {
            start = (start << 8) | a[i];
            end = (end << 8) | b[i];
        }
        if (end < start) (start, end) = (end, start);
        var count = Math.Min(end - start + 1, MaxTargets);
        for (long i = 0; i < count; i++)
        {
            var current = start + i;
            yield return $"{(current >> 24) & 0xFF}.{(current >> 16) & 0xFF}.{(current >> 8) & 0xFF}.{current & 0xFF}";
        }
    }

    private static string BuildScanNotes(
        int ipCount, int portCount, int closed, int filtered, int errors, int intelFailures,
        List<string> warnings)
    {
        var parts = new List<string>
        {
            $"پورت‌های بررسی‌شده: {portCount} برای هر آدرس",
            $"پاسخ‌های بسته: {closed}",
            $"پاسخ‌های فیلترشده/نامشخص: {filtered}",
            $"خطاهای شبکه: {errors}"
        };
        if (intelFailures > 0) parts.Add($"استعلام‌های ناموفق شبکه: {intelFailures}");
        if (warnings.Count > 0) parts.Add("هشدارها: " + string.Join(" | ", warnings));
        return string.Join(" | ", parts);
    }

    /// <summary>آدرس‌ها و محدوده‌های واقعی همین رایانه برای پروب شبکه محلی.</summary>
    public static List<(string Address, string Cidr)> GetLocalSubnets()
    {
        var result = new List<(string, string)>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var mask = ua.IPv4Mask;
                    if (mask == null) continue;
                    result.Add(($"{ua.Address}/{MaskToPrefix(mask)}", ni.Name));
                }
            }
        }
        catch (Exception)
        {
            // خواندن اطلاعات کارت شبکه ممکن است در برخی محیط‌ها ممکن نباشد
        }
        return result;
    }

    private static int MaskToPrefix(IPAddress mask)
    {
        int prefix = 0;
        foreach (var bit in mask.GetAddressBytes())
        {
            for (int i = 7; i >= 0; i--)
            {
                if ((bit & (1 << i)) != 0) prefix++;
            }
        }
        return prefix;
    }

    public static List<string> GetLocalIPv4Addresses()
    {
        var list = new List<string> { "127.0.0.1" };
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var text = ua.Address.ToString();
                    if (!list.Contains(text)) list.Add(text);
                }
            }
        }
        catch (Exception)
        {
            // بی‌اثر
        }
        return list;
    }

    private void Report(int current, int total, int found, int identified, double elapsed, string message)
    {
        var pct = total <= 0 ? 0 : (int)Math.Min(100, current * 100.0 / total);
        ProgressChanged?.Invoke(this, new ScanProgressEventArgs
        {
            Percent = pct,
            Message = message,
            Found = found,
            Probed = current,
            Total = total,
            Identified = identified,
            ElapsedSeconds = Math.Round(elapsed, 1)
        });
    }
}
