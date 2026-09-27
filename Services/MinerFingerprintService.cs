using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

/// <summary>
/// خروجی یک پروب واقعی روی یک آدرس/پورت.
/// هیچ فیلدی در این کلاس مقدار حدسی یا ساختگی ندارد؛
/// هر چیزی که از دستگاه خوانده نشده باشد null یا رشته تهی است.
/// </summary>
public sealed class ProbeEvidence
{
    public string IpAddress { get; init; } = "";
    public int Port { get; init; }
    public string PortState { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string DetectionMethod { get; set; } = "";
    public int? LatencyMs { get; set; }
    public string HttpStatus { get; set; } = "";
    public string BannerText { get; set; } = "";
    public string EvidenceHash { get; set; } = "";
    public string EvidenceSummary { get; set; } = "";
    public string Error { get; set; } = "";

    public string SoftwareName { get; set; } = "";
    public string DeviceModel { get; set; } = "";
    public string FirmwareVersion { get; set; } = "";
    public string HashRate { get; set; } = "";
    public double? TemperatureC { get; set; }
    public double? FanPercent { get; set; }
    public double? PowerWatts { get; set; }
    public string PowerSource { get; set; } = "";
    public long? UptimeSeconds { get; set; }
    public string PoolAddress { get; set; } = "";
    public string WorkerName { get; set; } = "";
    public double Confidence { get; set; }
}

/// <summary>
/// شناسایی نوع‌شناسانه (fingerprint) دستگاه ماینر از روی پاسخ واقعی خود دستگاه.
/// روش کار: اتصال TCP واقعی، خواندن داده‌های داوطلبانه دستگاه (بنر)،
/// و در صورت نیاز ارسال درخواست HTTP به API مدیریتی همان دستگاه و تحلیل پاسخ.
/// هیچ داده‌ای از پیش ساخته یا شبیه‌سازی نمی‌شود.
/// </summary>
public sealed class MinerFingerprintService
{
    private const int MaxEvidenceChars = 8000;

    // ترتیب این فهرست مهم است: نام‌های نرم‌افزار عمومی پیش از نام سازنده بررسی می‌شوند
    // تا مثلاً پاسخ cgminer که شامل نام مدل «Antminer» است، به اشتباه فریم‌ور آنت‌ماینر گزارش نشود.
    private static readonly (string Signature, string Name)[] SoftwareSignatures =
    {
        ("bosminer", "BraiinsOS (BMiner/Antminer BOS)"),
        ("braiins", "BraiinsOS"),
        ("bmminer", "Bitmain BMminer"),
        ("cgminer", "Cgminer"),
        ("claymore", "Claymore"),
        ("srbminer", "SRBMiner"),
        ("phoenixminer", "PhoenixMiner"),
        ("xmrig", "XMRig"),
        ("cpuminer", "cpuminer"),
        ("bminer", "BMiner"),
        ("antminer", "Antminer firmware"),
        ("bitmain", "Bitmain firmware"),
        ("whatsminer", "Whatsminer firmware"),
        ("stockminer", "Stock Miner firmware"),
        ("epicminer", "ePIC Miner"),
        ("espminer", "ESPMiner"),
        ("innosilicon", "Innosilicon firmware"),
        ("goldshell", "Goldshell firmware"),
        ("avalon", "Avalon firmware")
    };

    /// <summary>فیلدهایی که خودِ نام نرم‌افزار را در آن‌ها اعلام می‌کنند.</summary>
    private static readonly string[] SoftwareFieldKeys =
        { "version", "sw_version", "firmware_version", "web", "api", "software", "app" };

    // فیلدهایی که واحد آن‌ها در نامشان مشخص است (در اولویت اول بررسی می‌شوند)
    private static readonly (string Key, double Factor)[] HashRateKeys =
    {
        ("ph/s", 1_000_000d), ("ph5s", 1_000_000d),
        ("th/s", 1d), ("th5s", 1d),
        ("gh/s", 1d), ("ghs 5s av", 1d), ("g5s", 1d), ("ghs av", 1d),
        ("mhs 5s av", 0.001d), ("m5s", 0.001d), ("mhs av", 0.001d), ("mhs 1m", 0.001d),
        ("khs 5s av", 0.000001d), ("k5s", 0.000001d), ("khs av", 0.000001d)
    };

    // فیلدهای بدون واحد صریح — تنها اگر هیچ فیلد واحدداری وجود نداشته باشد استفاده می‌شوند
    private static readonly (string Key, double Factor, string Unit)[] HashRateFallbackKeys =
    {
        ("hs_total", 1d, "TH/s"),
        ("th_av", 1d, "TH/s"),
        ("hs_av", 1d, "TH/s"),
        ("hashrate", 1d, "TH/s"),
        ("hash_rate", 1d, "TH/s"),
        ("mhs", 0.001d, "TH/s"),
        ("ghs", 1d, "TH/s"),
        ("khs", 0.000001d, "TH/s")
    };

    private static readonly string[] TemperatureKeys =
        { "temp", "temperature", "temp_avg", "chip_temp", "temperatures", "temp_max", "board_temp" };

    private static readonly string[] FanKeys =
        { "fan", "fans", "fan_avg", "fan_speed", "fan_speed_avg", "fan_pct" };

    private static readonly string[] PowerKeys =
    {
        "power", "power_w", "powerwatts", "power_watts", "poweravg", "power_w_avg",
        "power_real", "power_consumption", "watt", "watts"
    };

    private static readonly string[] UptimeKeys =
        { "elapsed", "uptime", "uptime_s", "uptime_secs", "elapsed_secs", "runtime", "elapsed_s" };

    private static readonly string[] ModelKeys =
    {
        "model", "minermodel", "dev_name", "devname", "devmodel", "device_model",
        "board", "chain", "product", "hardware", "miner_type", "minertype"
    };

    private static readonly string[] FirmwareKeys =
    {
        "firmware", "firmware_version", "firmware_ver", "fw_version", "fwversion",
        "version", "build", "sw_version"
    };

    private static readonly string[] PoolKeys =
    {
        "url", "pool", "pool_address", "current_server", "server", "stratum",
        "active_pool", "url_stratum", "server_stratum"
    };

    private static readonly string[] WorkerKeys =
    {
        "user", "username", "user_name", "worker", "worker_name", "account"
    };

    public async Task<ProbeEvidence> ProbeAsync(
        string ip, int port, int connectTimeoutMs, CancellationToken ct)
    {
        var ev = new ProbeEvidence { IpAddress = ip, Port = port };
        var raw = new StringBuilder();

        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(connectTimeoutMs);
            await client.ConnectAsync(ip, port, connectCts.Token).ConfigureAwait(false);
            sw.Stop();

            ev.PortState = PortStates.Open;
            ev.LatencyMs = (int)sw.ElapsedMilliseconds;

            var passive = await ReadAvailableAsync(client, TimeSpan.FromMilliseconds(700), ct)
                .ConfigureAwait(false);
            raw.Append(passive);

            if (LooksLikeJson(passive))
            {
                ev.Protocol = "Stratum/JSON";
                ev.DetectionMethod = "خواندن بنر JSON ارسالی از سوی دستگاه";
                AnalyzeJson(ev, passive);
            }
            else if (LooksLikeHttp(passive))
            {
                ev.Protocol = "HTTP";
                ev.DetectionMethod = "پاسخ HTTP ارسالی از سوی دستگاه";
                AnalyzeHttpResponse(ev, passive);
            }
            else if (passive.Trim().Length > 0)
            {
                ev.Protocol = "بنر متنی";
                ev.DetectionMethod = "بنر متنی داوطلبانه دستگاه";
                ev.BannerText = Clip(passive);
                ev.EvidenceSummary = $"بنر دریافتی: {Clip(passive, 160)}";
                ev.Confidence = 0.25;
            }
            else
            {
                ev.Protocol = "TCP";
                ev.DetectionMethod = "پورت باز بدون پاسخ داوطلبانه — سرویس HTTP/API بررسی شد";
            }

            if (ev.Confidence < 0.6)
            {
                var http = await HttpProbeAsync(ip, port, ct).ConfigureAwait(false);
                if (http != null)
                {
                    raw.Append(http);
                    if (ev.Protocol is "" or "TCP")
                        ev.Protocol = "HTTP";
                    ev.DetectionMethod = http.Length > 0
                        ? "پاسخ واقعی API مدیریتی دستگاه روی HTTP"
                        : ev.DetectionMethod;
                    AnalyzeHttpResponse(ev, http);
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            ev.PortState = PortStates.Filtered;
            ev.Error = "اتصال در مهلت تعیین‌شده برقرار نشد (احتمال فیلتر یا خاموشی)";
        }
        catch (SocketException ex)
        {
            ev.PortState = ex.SocketErrorCode == SocketError.ConnectionRefused
                ? PortStates.Closed
                : PortStates.Filtered;
            ev.Error = $"خطای شبکه: {ex.SocketErrorCode}";
        }
        catch (Exception ex)
        {
            ev.PortState = PortStates.Error;
            ev.Error = ex.Message;
        }

        if (ev.PortState == PortStates.Open)
        {
            var evidence = raw.ToString();
            if (evidence.Trim().Length == 0)
            {
                ev.DetectionMethod = "پورت باز — دستگاه هیچ داده‌ای ارسال نکرد (ناشناخته)";
                ev.EvidenceSummary = "هیچ پاسخی دریافت نشد؛ نوع سرویس قابل تشخیص نبود.";
                ev.Confidence = 0.10;
            }
            if (ev.EvidenceSummary.Length == 0)
                ev.EvidenceSummary = BuildSummary(ev);
            ev.BannerText = Clip(evidence, MaxEvidenceChars);
            ev.EvidenceHash = Sha256Hex(evidence);
        }

        return ev;
    }

    private static async Task<string> ReadAvailableAsync(
        TcpClient client, TimeSpan window, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        try
        {
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            readCts.CancelAfter(window);
            var stream = client.GetStream();
            var deadline = DateTime.UtcNow + window;
            while (DateTime.UtcNow < deadline && sb.Length < MaxEvidenceChars)
            {
                int read;
                try
                {
                    read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), readCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                if (read <= 0) break;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                if (sb.ToString().Contains("\r\n\r\n") || LooksLikeJson(sb.ToString())) break;
            }
        }
        catch (IOException) { /* اتصال بسته شد */ }
        catch (ObjectDisposedException) { /* اتصال بسته شد */ }
        return sb.ToString();
    }

    private static async Task<string?> HttpProbeAsync(string ip, int port, CancellationToken ct)
    {
        string[] paths = { PreferredPath(port), "/summary", "/" };
        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (!tried.Add(path)) continue;
            var response = await SendHttpAsync(ip, port, path, ct).ConfigureAwait(false);
            if (response != null && response.Trim().Length > 0)
                return response;
        }
        return null;
    }

    private static string PreferredPath(int port) => port switch
    {
        3333 or 14433 or 14444 => "/",
        4028 or 8333 or 9333 or 9332 or 8080 or 8545 or 18080 => "/summary",
        _ => "/summary"
    };

    private static async Task<string?> SendHttpAsync(
        string ip, int port, string path, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(2500);
            await client.ConnectAsync(ip, port, connectCts.Token).ConfigureAwait(false);

            var request = $"GET {path} HTTP/1.1\r\nHost: {ip}:{port}\r\n" +
                          "User-Agent: MinerAudit/2.0\r\nAccept: */*\r\nConnection: close\r\n\r\n";
            var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes(request), connectCts.Token)
                .ConfigureAwait(false);
            await stream.FlushAsync(connectCts.Token).ConfigureAwait(false);

            var buffer = new byte[16384];
            var sb = new StringBuilder();
            using (var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                readCts.CancelAfter(3000);
                while (sb.Length < 16384)
                {
                    int read;
                    try
                    {
                        read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), readCts.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { break; }
                    if (read <= 0) break;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                }
            }
            return sb.ToString();
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception) { return null; }
    }

    private static void AnalyzeHttpResponse(ProbeEvidence ev, string response)
    {
        var crlf = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var lf = response.IndexOf("\n\n", StringComparison.Ordinal);
        int split = -1, skip = 0;
        if (crlf >= 0 && (lf < 0 || crlf <= lf)) { split = crlf; skip = 4; }
        else if (lf >= 0) { split = lf; skip = 2; }

        var head = split > 0 ? response[..split] : response;
        var body = split > 0 ? response[(split + skip)..] : "";

        var statusLine = head.Split('\n')[0].Trim();
        if (statusLine.Length > 0)
        {
            ev.HttpStatus = statusLine;
            if (statusLine.Contains(" 200", StringComparison.Ordinal))
            {
                ev.DetectionMethod = "پاسخ 200 از API مدیریتی خود دستگاه (HTTP)";
                ev.Confidence = Math.Max(ev.Confidence, 0.40);
            }
        }

        if (body.Trim().Length > 0)
        {
            ev.BannerText = Clip(body, 2000);
            if (LooksLikeJson(body))
                AnalyzeJson(ev, body);
            else
            {
                ev.DetectionMethod = "وب‌رابط مدیریتی HTML دستگاه";
                ev.Confidence = Math.Max(ev.Confidence, 0.35);
                ev.EvidenceSummary = $"محتوای وب‌رابط دریافت شد ({body.Length} کاراکتر)";
            }
        }
    }

    private static void AnalyzeJson(ProbeEvidence ev, string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            ev.Confidence = Math.Max(ev.Confidence, 0.20);
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            var rawText = json;

            // ۱) نام نرم‌افزار از فیلدهایی که صریحاً نسخه/نام را اعلام می‌کنند
            var declared = FindString(root, SoftwareFieldKeys);
            var software = DetectSoftware(declared) ?? DetectSoftware(rawText);
            if (software != null) ev.SoftwareName = software;

            // ۲) هش‌ریت: ابتدا فیلدهایی که واحدشان در نامشان مشخص است
            if (TryFindHashRate(root, HashRateKeys, out var thPerSec, out var hashKey))
                ev.HashRate = FormatHashRate(thPerSec) + $" (فیلد «{hashKey}» در پاسخ دستگاه)";
            else if (TryFindHashRateFallback(root, out var fbTh, out var fbKey, out var fbUnit))
                ev.HashRate = FormatHashRate(fbTh) + $" (فیلد «{fbKey}» — واحد {fbUnit} در پاسخ دستگاه)";

            if (TryFindNumber(root, TemperatureKeys, out var temp) && temp is >= -20 and <= 150)
                ev.TemperatureC = Math.Round(temp, 1);

            if (TryFindNumber(root, FanKeys, out var fan) && fan is >= 0 and <= 100)
                ev.FanPercent = Math.Round(fan, 0);

            if (TryFindNumber(root, PowerKeys, out var power) && power is > 0 and <= 50000)
            {
                ev.PowerWatts = Math.Round(power, 1);
                ev.PowerSource = "اندازه‌گیری‌شده توسط API دستگاه";
            }

            if (TryFindNumber(root, UptimeKeys, out var uptime) && uptime is > 0 and < 100_000_000)
                ev.UptimeSeconds = (long)uptime;

            ev.DeviceModel = FindString(root, ModelKeys);
            ev.FirmwareVersion = FindString(root, FirmwareKeys);
            ev.PoolAddress = FindString(root, PoolKeys);
            ev.WorkerName = FindString(root, WorkerKeys);

            if (ev.PowerWatts == null)
                ev.PowerSource = "اعلام‌نشده توسط دستگاه";

            ev.Confidence = ScoreConfidence(ev, root);
        }
    }

    private static double ScoreConfidence(ProbeEvidence ev, JsonElement root)
    {
        // اطمینان فقط و فقط بر پایه شواهد موجود در پاسخ واقعی دستگاه محاسبه می‌شود.
        var hasStatusArray = HasArrayKey(root, new[] { "status", "result", "miners", "chains" });
        var identified = !string.IsNullOrEmpty(ev.SoftwareName)
                         || !string.IsNullOrEmpty(ev.DeviceModel);

        if (identified && ev.HashRate != "" && (ev.TemperatureC != null || ev.UptimeSeconds != null))
            return 1.00;
        if (identified && ev.HashRate != "") return 0.90;
        if (identified && (ev.PoolAddress != "" || ev.DeviceModel != "")) return 0.75;
        if (identified) return 0.65;
        if (ev.HashRate != "") return 0.45;
        if (hasStatusArray) return 0.35;
        return Math.Max(ev.Confidence, 0.20);
    }

    private static string BuildSummary(ProbeEvidence ev)
    {
        var parts = new List<string>();
        if (ev.SoftwareName != "") parts.Add($"نرم‌افزار: {ev.SoftwareName}");
        if (ev.DeviceModel != "") parts.Add($"مدل: {ev.DeviceModel}");
        if (ev.HashRate != "") parts.Add($"هش‌ریت: {ev.HashRate}");
        if (ev.TemperatureC != null) parts.Add($"دما: {ev.TemperatureC}°C");
        if (ev.FanPercent != null) parts.Add($"فن: {ev.FanPercent}%");
        if (ev.PowerWatts != null) parts.Add($"توان: {ev.PowerWatts} وات");
        if (ev.FirmwareVersion != "") parts.Add($"فریم‌ور: {ev.FirmwareVersion}");
        if (ev.UptimeSeconds != null) parts.Add($"آپ‌تایم: {FormatUptime(ev.UptimeSeconds.Value)}");
        if (ev.PoolAddress != "") parts.Add($"استخر: {ev.PoolAddress}");
        if (ev.HttpStatus != "") parts.Add($"وضعیت HTTP: {ev.HttpStatus}");
        if (ev.LatencyMs != null) parts.Add($"تأخیر اتصال: {ev.LatencyMs} میلی‌ثانیه");
        return parts.Count == 0 ? "شواهدی استخراج نشد" : string.Join(" | ", parts);
    }

    private static string? DetectSoftware(string rawText)
    {
        foreach (var (signature, name) in SoftwareSignatures)
        {
            if (rawText.Contains(signature, StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return null;
    }

    private static bool TryFindHashRate(
        JsonElement root, (string Key, double Factor)[] keys, out double thPerSec, out string keyUsed)
    {
        thPerSec = 0;
        keyUsed = "";
        foreach (var element in Descendants(root))
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            foreach (var prop in element.EnumerateObject())
            {
                foreach (var (key, factor) in keys)
                {
                    if (!string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase)) continue;
                    if (prop.Value.ValueKind == JsonValueKind.Number &&
                        prop.Value.TryGetDouble(out var value) && value > 0)
                    {
                        thPerSec = value * factor;
                        keyUsed = prop.Name;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool TryFindHashRateFallback(
        JsonElement root, out double thPerSec, out string keyUsed, out string unit)
    {
        thPerSec = 0;
        keyUsed = "";
        unit = "";
        foreach (var element in Descendants(root))
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            foreach (var prop in element.EnumerateObject())
            {
                foreach (var (key, factor, fallbackUnit) in HashRateFallbackKeys)
                {
                    if (!string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase)) continue;
                    if (prop.Value.ValueKind == JsonValueKind.Number &&
                        prop.Value.TryGetDouble(out var value) && value > 0)
                    {
                        thPerSec = value * factor;
                        keyUsed = prop.Name;
                        unit = fallbackUnit;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool TryFindNumber(JsonElement root, IEnumerable<string> keys, out double value)
    {
        var set = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        foreach (var element in Descendants(root))
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            foreach (var prop in element.EnumerateObject())
            {
                if (!set.Contains(prop.Name)) continue;
                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var v))
                {
                    value = v;
                    return true;
                }
                if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                {
                    var sum = 0d;
                    var count = 0;
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var iv))
                        {
                            sum += iv;
                            count++;
                        }
                    }
                    if (count > 0)
                    {
                        value = sum / count;
                        return true;
                    }
                }
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var text = prop.Value.GetString();
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var pv))
                    {
                        value = pv;
                        return true;
                    }
                }
            }
        }
        value = 0;
        return false;
    }

    private static string FindString(JsonElement root, IEnumerable<string> keys)
    {
        var set = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        foreach (var element in Descendants(root))
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            foreach (var prop in element.EnumerateObject())
            {
                if (!set.Contains(prop.Name)) continue;
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var text = prop.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
                }
                if (prop.Value.ValueKind == JsonValueKind.Number)
                    return prop.Value.GetRawText();
            }
        }
        return "";
    }

    private static bool HasArrayKey(JsonElement root, IEnumerable<string> keys)
    {
        var set = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        foreach (var element in Descendants(root))
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            foreach (var prop in element.EnumerateObject())
            {
                if (set.Contains(prop.Name) && prop.Value.ValueKind == JsonValueKind.Array)
                    return true;
            }
        }
        return false;
    }

    /// <summary>پیمایش عمق‌اول به همان ترتیب ظاهر شدن فیلدها در سند JSON.</summary>
    private static IEnumerable<JsonElement> Descendants(JsonElement root)
    {
        yield return root;
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
            {
                foreach (var child in Descendants(prop.Value))
                    yield return child;
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                foreach (var child in Descendants(item))
                    yield return child;
            }
        }
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal)
               || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static bool LooksLikeHttp(string text) =>
        text.TrimStart().StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase);

    private static string FormatHashRate(double thPerSec)
    {
        if (thPerSec >= 1_000_000) return (thPerSec / 1_000_000).ToString("F2", CultureInfo.InvariantCulture) + " PH/s";
        if (thPerSec >= 1000) return (thPerSec / 1000).ToString("F2", CultureInfo.InvariantCulture) + " EH/s";
        if (thPerSec >= 1) return thPerSec.ToString("F2", CultureInfo.InvariantCulture) + " TH/s";
        if (thPerSec >= 0.001) return (thPerSec * 1000).ToString("F2", CultureInfo.InvariantCulture) + " GH/s";
        if (thPerSec >= 0.000001) return (thPerSec * 1_000_000).ToString("F2", CultureInfo.InvariantCulture) + " MH/s";
        return (thPerSec * 1_000_000_000).ToString("F2", CultureInfo.InvariantCulture) + " KH/s";
    }

    private static string FormatUptime(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return $"{(int)span.TotalDays} روز و {span.Hours}:{span.Minutes:00}:{span.Seconds:00}";
    }

    private static string Clip(string text, int max = 400) =>
        text.Length <= max ? text : text[..max] + $"… ({text.Length} کاراکتر)";

    public static string Sha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
