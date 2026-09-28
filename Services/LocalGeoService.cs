using System.Net.Http;
using System.Text.Json;
using System.Threading;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

/// <summary>
/// استعلام جغرافیای عمومی از IP (بدون کلید) از ip-api.com.
/// بدون هزینه، بدون نیاز به کلید API، با نرخ محدود 45 درخواست در دقیقه.
/// هر مقداری که واقعاً از سرویس خوانده نشده باشد، null باقی می‌ماند.
/// </summary>
public sealed class LocalGeoService
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(2, 2);

    public LocalGeoService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(6),
            MaxResponseContentBufferSize = 1024 * 1024 // 1 MB
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MinerAudit/2.0 (lawful-enforcement-tool)");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<LocalGeoRecord?> LookupAsync(string ip, CancellationToken ct)
    {
        if (!IPAddress.TryParse(ip, out var addr) || addr.AddressFamily != AddressFamily.InterNetwork)
            return null;

        // بررسی سابقه محلی (NetworkOwnership)
        if (ArpTable.IsPrivateIPv4(addr) || IPAddress.IsLoopback(addr))
            return null; // آدرس‌های خصوصی نیاز به استعلام ندارند

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var url = $"http://ip-api.com/json/{ip}?fields=status,country,countryCode,region,regionName,city,lat,lon,timezone,isp,org,query";
            var json = await _http.GetStringAsync(url, ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("status", out var status) || status.GetString() != "success")
                return null;

            return new LocalGeoRecord
            {
                IPAddress = ip,
                Country = root.GetProperty("country").GetString() ?? "",
                CountryCode = root.GetProperty("countryCode").GetString() ?? "",
                Region = root.GetProperty("region").GetString() ?? "",
                RegionName = root.GetProperty("regionName").GetString() ?? "",
                City = root.GetProperty("city").GetString() ?? "",
                Lat = root.TryGetProperty("lat", out var lat) && lat.ValueKind == JsonValueKind.Number ? lat.GetDouble() : null,
                Lon = root.TryGetProperty("lon", out var lon) && lon.ValueKind == JsonValueKind.Number ? lon.GetDouble() : null,
                Timezone = root.GetProperty("timezone").GetString() ?? "",
                ISP = root.GetProperty("isp").GetString() ?? "",
                Org = root.GetProperty("org").GetString() ?? "",
                Query = root.GetProperty("query").GetString() ?? "",
                QueriedAt = DateTime.Now
            };
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(DatabaseService db, LocalGeoRecord record, CancellationToken ct)
    {
        var ownership = new NetworkOwnership
        {
            IPAddress = record.IPAddress,
            QueriedAt = DateTime.Now,
            QueryStatus = "موفق",
            GeoLat = record.Lat,
            GeoLon = record.Lon,
            GeoCity = record.City,
            GeoRegion = record.RegionName,
            GeoCountry = record.Country,
            GeoISP = record.ISP,
            GeoOrg = record.Org,
            GeoTimezone = record.Timezone,
            GeoQueryStatus = "موفق",
            GeoQueriedAt = DateTime.Now
        };
        db.SaveOwnership(ownership);
    }
}

public sealed class LocalGeoRecord
{
    public string IPAddress { get; init; } = "";
    public string Country { get; init; } = "";
    public string CountryCode { get; init; } = "";
    public string Region { get; init; } = "";
    public string RegionName { get; init; } = "";
    public string City { get; init; } = "";
    public double? Lat { get; init; }
    public double? Lon { get; init; }
    public string Timezone { get; init; } = "";
    public string ISP { get; init; } = "";
    public string Org { get; init; } = "";
    public string Query { get; init; } = "";
    public DateTime QueriedAt { get; init; }
}