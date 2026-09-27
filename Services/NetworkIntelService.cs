using System.Net;
using System.Text.Json;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

/// <summary>
/// استعلام واقعی مشخصات شبکه (مالکیت، AS، کشور) از مراجع بیرونی معتبر:
///  • RDAP (افزونه‌ای استاندارد از IANA که به رجیستری مرتبط ارجاع می‌دهد)
///  • RIPE Stat (سرویس عمومی برای اطلاعات پیشوند و AS)
///  • Reverse DNS سیستم‌عامل
/// نتایج در پایگاه داده نهان می‌شوند. اگر مرجع در دسترس نباشد، مقدار خالی ثبت
/// می‌شود و هرگز مقدار ساختگی جایگزین نمی‌گردد.
/// </summary>
public sealed class NetworkIntelService
{
    private static readonly TimeSpan SuccessTtl = TimeSpan.FromDays(30);
    private static readonly TimeSpan FailureTtl = TimeSpan.FromHours(12);

    private readonly DatabaseService _db;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(2, 2);

    public NetworkIntelService(DatabaseService db)
    {
        _db = db;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MinerAudit/2.0 (lawful-enforcement-tool)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<NetworkOwnership> LookupAsync(string ip, CancellationToken ct)
    {
        if (!IPAddress.TryParse(ip, out var address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return new NetworkOwnership
            {
                IPAddress = ip,
                QueryStatus = "آدرس نامعتبر",
                QueriedAt = DateTime.Now
            };
        }

        if (ArpTable.IsPrivateIPv4(address) || IPAddress.IsLoopback(address))
        {
            return new NetworkOwnership
            {
                IPAddress = ip,
                QueryStatus = "آدرس داخلی (RFC1918/Loopback) — استعلام بیرونی لازم نیست",
                Source = "محاسبه محلی",
                QueriedAt = DateTime.Now
            };
        }

        var cached = _db.GetCachedOwnership(ip);
        if (cached != null)
        {
            var age = DateTime.Now - cached.QueriedAt;
            var ttl = cached.QueryStatus == "موفق" ? SuccessTtl : FailureTtl;
            if (age < ttl) return cached;
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var record = new NetworkOwnership
            {
                IPAddress = ip,
                QueriedAt = DateTime.Now,
                Source = "RDAP + RIPE Stat + Reverse DNS"
            };

            record.ReverseDns = await ResolvePtrAsync(address, ct).ConfigureAwait(false);

            var failures = new List<string>();

            var prefixOk = await QueryPrefixAsync(record, failures, ct).ConfigureAwait(false);
            var rdapOk = await QueryRdapAsync(record, failures, ct).ConfigureAwait(false);

            if (!prefixOk && !rdapOk)
                record.QueryStatus = "ناموفق: " + string.Join("؛ ", failures);
            else if (failures.Count > 0)
                record.QueryStatus = "ناقص: " + string.Join("؛ ", failures);
            else
                record.QueryStatus = "موفق";

            record.PayloadHash = MinerFingerprintService.Sha256Hex(
                record.RawResponse + "|" + record.PrefixCidr + "|" + record.AsNumber);

            _db.SaveOwnership(record);
            return record;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<string> ResolvePtrAsync(IPAddress address, CancellationToken ct)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(address.ToString(), ct).ConfigureAwait(false);
            if (entry.HostName.Equals(address.ToString(), StringComparison.OrdinalIgnoreCase))
                return "";
            return entry.HostName;
        }
        catch (Exception)
        {
            return "";
        }
    }

    private async Task<bool> QueryPrefixAsync(
        NetworkOwnership record, List<string> failures, CancellationToken ct)
    {
        var url = $"https://stat.ripe.net/data/prefix-overview/data.json?resource={Uri.EscapeDataString(record.IPAddress)}";
        try
        {
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            record.SourceUrl = url;
            record.RawResponse = Clip(json, 4000);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return false;

            if (data.TryGetProperty("resource", out var res) && res.ValueKind == JsonValueKind.String)
                record.PrefixCidr = res.GetString() ?? "";
            if (data.TryGetProperty("holder", out var holder) && holder.ValueKind == JsonValueKind.String)
                record.AsName = holder.GetString() ?? "";
            if (data.TryGetProperty("asns", out var asns) && asns.ValueKind == JsonValueKind.Array
                && asns.GetArrayLength() > 0 && asns[0].ValueKind == JsonValueKind.String)
                record.AsNumber = asns[0].GetString() ?? "";
            if (data.TryGetProperty("announced", out var announced) && announced.ValueKind is
                JsonValueKind.True or JsonValueKind.False)
            {
                record.NetworkName = announced.GetBoolean()
                    ? record.NetworkName
                    : (record.NetworkName.Length > 0 ? record.NetworkName + " (مسیریابی‌نشده)" : "مسیریابی‌نشده");
            }
            return record.AsNumber != "" || record.PrefixCidr != "";
        }
        catch (Exception ex)
        {
            failures.Add($"RIPE Stat: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> QueryRdapAsync(
        NetworkOwnership record, List<string> failures, CancellationToken ct)
    {
        var url = $"https://rdap.org/ip/{record.IPAddress}";
        try
        {
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            record.SourceUrl = url;
            record.RawResponse = Clip(json, 4000);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var got = false;

            if (root.TryGetProperty("country", out var country) && country.ValueKind == JsonValueKind.String)
            {
                record.Country = country.GetString() ?? "";
                got = true;
            }
            if (root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                && record.AsName == "")
            {
                record.AsName = name.GetString() ?? "";
                got = true;
            }
            if (root.TryGetProperty("startAddress", out var start) &&
                root.TryGetProperty("endAddress", out var end) &&
                start.ValueKind == JsonValueKind.String && end.ValueKind == JsonValueKind.String)
            {
                record.NetworkName = $"{start.GetString()}-{end.GetString()}";
                got = true;
            }
            if (root.TryGetProperty("entities", out var entities) && entities.ValueKind == JsonValueKind.Array)
            {
                foreach (var entity in entities.EnumerateArray())
                {
                    var org = VcardField(entity, "org") ?? VcardField(entity, "fn");
                    if (!string.IsNullOrWhiteSpace(org))
                    {
                        record.Registrant = org;
                        got = true;
                        break;
                    }
                }
            }
            return got;
        }
        catch (Exception ex)
        {
            failures.Add($"RDAP: {ex.Message}");
            return false;
        }
    }

    private static string? VcardField(JsonElement entity, string field)
    {
        if (!entity.TryGetProperty("vcardArray", out var arr)) return null;
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() < 2) return null;
        var props = arr[1];
        if (props.ValueKind != JsonValueKind.Array) return null;

        foreach (var prop in props.EnumerateArray())
        {
            if (prop.ValueKind != JsonValueKind.Array || prop.GetArrayLength() < 4) continue;
            if (prop[0].ValueKind != JsonValueKind.String) continue;
            if (!string.Equals(prop[0].GetString(), field, StringComparison.OrdinalIgnoreCase)) continue;
            if (prop[3].ValueKind == JsonValueKind.String) return prop[3].GetString();
        }
        return null;
    }

    private static string Clip(string text, int max) =>
        text.Length <= max ? text : text[..max];
}
