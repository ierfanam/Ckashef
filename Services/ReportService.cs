using System.Globalization;
using System.Text;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

/// <summary>
/// تولید گزارش رسمی. گزارش صرفاً بازتاب داده‌های واقعی ثبت‌شده در پایگاه داده است.
/// هر مقداری که واقعاً اندازه‌گیری نشده باشد با «ثبت نشده» مشخص می‌شود
/// و هرگز مقدار فرضی یا تقریبی جایگزین نمی‌گردد.
/// </summary>
public sealed class ReportService
{
    private const string Unknown = "ثبت نشده";
    private const string UnknownNum = "نامشخص";

    private readonly DatabaseService _db;

    public ReportService(DatabaseService db) => _db = db;

    private static string V(string? value) => string.IsNullOrWhiteSpace(value) ? Unknown : value;
    private static string N(double? value) => value.HasValue
        ? value.Value.ToString("F1", CultureInfo.InvariantCulture)
        : UnknownNum;

    private static string ReportsDirectory()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public string GenerateTextReport(string? operationCode, string generatedBy)
    {
        var path = Path.Combine(ReportsDirectory(), $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        var detections = _db.GetDetections(operationCode);
        var ops = _db.GetOperations();
        if (!string.IsNullOrWhiteSpace(operationCode))
            ops = ops.Where(o => o.OperationCode == operationCode).ToList();

        var stats = _db.GetDashboardStats();
        var scans = _db.GetScanHistory(operationCode);
        var measured = detections.Count(d => d.PowerWatts != null);
        var measuredSum = detections.Where(d => d.PowerWatts != null).Sum(d => d.PowerWatts!.Value);

        var sb = new StringBuilder();
        sb.AppendLine("════════════════════════════════════════════════════════════════════");
        sb.AppendLine("  گزارش رسمی — سامانه ردیابی دستگاه‌های ماینر");
        sb.AppendLine("  صرفاً بر پایه داده‌های واقعی اندازه‌گیری‌شده و استعلام‌شده");
        sb.AppendLine("════════════════════════════════════════════════════════════════════");
        sb.AppendLine($"تاریخ تولید گزارش : {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"تهیه‌کننده         : {generatedBy}");
        sb.AppendLine($"فیلتر عملیات      : {(string.IsNullOrWhiteSpace(operationCode) ? "همه عملیات" : operationCode)}");
        sb.AppendLine($"هش این گزارش      : (در انتهای سند درج می‌شود)");
        sb.AppendLine();

        sb.AppendLine("── خلاصه آماری کل سامانه ──");
        sb.AppendLine($"  کل دستگاه‌های ثبت‌شده      : {stats.TotalDetections}");
        sb.AppendLine($"  شناسایی‌شده (نرم‌افزار/مدل)  : {stats.IdentifiedDetections}");
        sb.AppendLine($"  پورت باز بدون شناسایی      : {stats.UnidentifiedOpenPorts}");
        sb.AppendLine($"  دستگاه‌های دارای توان اندازه‌گیری‌شده: {measured}");
        sb.AppendLine($"  مجموع توان اندازه‌گیری‌شده  : {measuredSum:F1} وات");
        sb.AppendLine();

        sb.AppendLine("── عملیات ──");
        if (ops.Count == 0) sb.AppendLine("  عملیاتی ثبت نشده است.");
        foreach (var o in ops)
        {
            sb.AppendLine($"  [{o.OperationCode}] منطقه: {V(o.Region)} | وضعیت: {V(o.Status)} | مجازکننده: {V(o.AuthorizedBy)}");
            sb.AppendLine($"      شماره حکم/مجوز: {V(o.PermitNumber)}");
            sb.AppendLine($"      شروع: {o.StartTime:yyyy-MM-dd HH:mm} | پایان: {(o.EndTime.HasValue ? o.EndTime.Value.ToString("yyyy-MM-dd HH:mm") : Unknown)}");
            sb.AppendLine($"      شناسایی‌ها: {o.DetectionCount} | توان اندازه‌گیری‌شده: {o.TotalConsumption:F1} وات");
        }
        sb.AppendLine();

        sb.AppendLine("── تاریخچه پروب‌ها ──");
        if (scans.Count == 0) sb.AppendLine("  پروبی ثبت نشده است.");
        foreach (var s in scans)
        {
            sb.AppendLine($"  شناسه پروب: {s.ScanID}");
            sb.AppendLine($"     شروع: {s.StartTime:yyyy-MM-dd HH:mm:ss} | پایان: {(s.EndTime.HasValue ? s.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : Unknown)} | مدت: {s.ElapsedSeconds:F1} ثانیه");
            sb.AppendLine($"     آدرس‌ها: {s.IPsScanned} | پورت‌های بررسی‌شده: {s.PortsProbed} | باز: {s.DevicesFound} | شناسایی‌شده: {s.IdentifiedDevices}");
            sb.AppendLine($"     بسته: {s.ClosedPorts} | فیلترشده: {s.FilteredPorts} | اپراتور: {V(s.OperatorName)} | رایانه: {V(s.MachineName)}");
        }
        sb.AppendLine();

        sb.AppendLine("── فهرست دستگاه‌های شناسایی‌شده ──");
        sb.AppendLine($"تعداد کل: {detections.Count}");
        if (detections.Count == 0)
            sb.AppendLine("هیچ دستگاهی ثبت نشده است.");
        foreach (var d in detections)
        {
            sb.AppendLine($"• {d.IPAddress}:{d.Port}  |  وضعیت پورت: {V(d.PortState)}  |  تأخیر: {(d.LatencyMs.HasValue ? d.LatencyMs + " ms" : UnknownNum)}");
            sb.AppendLine($"  نرم‌افزار: {V(d.SoftwareName)} | مدل: {V(d.DeviceModel)} | فریم‌ور: {V(d.FirmwareVersion)}");
            sb.AppendLine($"  هش‌ریت: {V(d.HashRate)} | دما: {N(d.TemperatureC)} °C | فن: {N(d.FanPercent)}٪");
            sb.AppendLine($"  توان: {N(d.PowerWatts)} وات (منبع: {V(d.PowerSource)})");
            sb.AppendLine($"  استخر: {V(d.PoolAddress)} | نام کارگر: {V(d.WorkerName)}");
            sb.AppendLine($"  روش شناسایی: {V(d.DetectionMethod)} | پروتکل: {V(d.Protocol)} | وضعیت HTTP: {V(d.HttpStatus)}");
            sb.AppendLine($"  اطمینان: {d.Confidence:P0} (بر پایه شواهد پاسخ واقعی دستگاه)");
            sb.AppendLine($"  شبکه: AS {V(d.AsNumber)} | مالک: {V(d.AsName)} | ثبت‌کننده: {V(d.NetworkRegistrant)} | کشور: {V(d.NetworkCountry)}");
            sb.AppendLine($"  محدوده: {V(d.PrefixCidr)} | PTR: {V(d.ReverseDns)} | MAC: {V(d.MacAddress)}");
            sb.AppendLine($"  وضعیت استعلام شبکه: {V(d.IntelSource)} | زمان استعلام: {V(d.IntelQueriedAt)}");
            sb.AppendLine($"  اطلاعات مشترک (فقط از پاسخ رسمی اپراتور): استان {V(d.Province)} | شهر {V(d.City)} | " +
                          $"نشانی {V(d.Street)} | کدپستی {V(d.PostalCode)} | نام {V(d.SubscriberName)} | تلفن {V(d.PhoneNumber)}");
            sb.AppendLine($"  اقدام: {V(d.ActionStatus)} | توسط: {V(d.ActionEnforcedBy)} | یادداشت: {V(d.ActionNotes)}");
            sb.AppendLine($"  زمان شناسایی: {d.DetectionTime:yyyy-MM-dd HH:mm:ss} | آخرین مشاهده: {(d.LastSeen.HasValue ? d.LastSeen.Value.ToString("yyyy-MM-dd HH:mm:ss") : Unknown)}");
            sb.AppendLine($"  هش شواهد (SHA-256): {V(d.EvidenceHash)}");
            sb.AppendLine($"  خلاصه شواهد: {V(d.EvidenceSummary)}");
            sb.AppendLine();
        }

        sb.AppendLine("── آمار استانی (بر پایه پاسخ رسمی اپراتور) ──");
        var regional = _db.GetRegionalStats();
        if (regional.Count == 0) sb.AppendLine("  داده استانی ثبت نشده است.");
        foreach (var (prov, count, power) in regional)
            sb.AppendLine($"  {prov}: {count} دستگاه — {power:F1} وات اندازه‌گیری‌شده");
        sb.AppendLine();

        var verification = _db.VerifyAuditChain();
        sb.AppendLine("── اصالت سوابق ──");
        sb.AppendLine($"  زنجیره حسابرسی: {(verification.Valid ? "معتبر" : "دستکاری‌شده")} " +
                      $"({verification.Checked} رکورد بررسی شد)");
        if (!verification.Valid) sb.AppendLine($"  نخستین رکورد نامعتبر: {verification.FirstBrokenId}");

        var body = sb.ToString();
        var hash = MinerFingerprintService.Sha256Hex(body);
        var footer = $"\n\n════════════════════════════════════════════════════════════════════\n" +
                     $"SHA-256 سند (بدون این بخش): {hash}\n" +
                     $"════════════════════════════════════════════════════════════════════\n";
        var content = body + footer;
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        _db.SaveReportMeta(Guid.NewGuid().ToString("N"), operationCode, "Text", generatedBy, path,
            detections.Count, measuredSum, measured, hash);
        return path;
    }

    public string ExportCsv(string? operationCode, string generatedBy)
    {
        var path = Path.Combine(ReportsDirectory(), $"Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var detections = _db.GetDetections(operationCode);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", new[]
        {
            "OperationID", "OperationCode", "ScanID", "IP", "Port", "PortState", "Protocol",
            "SoftwareName", "DeviceModel", "FirmwareVersion", "HashRate", "TemperatureC",
            "PowerWatts", "PowerSource", "UptimeSeconds", "PoolAddress", "WorkerName",
            "AsNumber", "AsName", "NetworkRegistrant", "NetworkCountry", "PrefixCidr",
            "ReverseDns", "MacAddress", "LatencyMs", "Confidence", "DetectionMethod",
            "EvidenceHash", "EvidenceSummary", "HttpStatus",
            "Province", "City", "Street", "PostalCode", "SubscriberName", "PhoneNumber",
            "ActionStatus", "ActionEnforcedBy", "ActionNotes", "DetectionTime", "LastSeen"
        }));

        foreach (var d in detections)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                Csv(d.OperationID), Csv(d.OperationCode), Csv(d.ScanID), Csv(d.IPAddress),
                d.Port.ToString(CultureInfo.InvariantCulture), Csv(d.PortState), Csv(d.Protocol),
                Csv(d.SoftwareName), Csv(d.DeviceModel), Csv(d.FirmwareVersion), Csv(d.HashRate),
                N(d.TemperatureC), N(d.PowerWatts), Csv(d.PowerSource),
                (d.UptimeSeconds?.ToString(CultureInfo.InvariantCulture) ?? ""), Csv(d.PoolAddress),
                Csv(d.WorkerName), Csv(d.AsNumber), Csv(d.AsName), Csv(d.NetworkRegistrant),
                Csv(d.NetworkCountry), Csv(d.PrefixCidr), Csv(d.ReverseDns), Csv(d.MacAddress),
                (d.LatencyMs?.ToString(CultureInfo.InvariantCulture) ?? ""),
                d.Confidence.ToString("F2", CultureInfo.InvariantCulture), Csv(d.DetectionMethod),
                Csv(d.EvidenceHash), Csv(d.EvidenceSummary), Csv(d.HttpStatus),
                Csv(d.Province), Csv(d.City), Csv(d.Street), Csv(d.PostalCode),
                Csv(d.SubscriberName), Csv(d.PhoneNumber),
                Csv(d.ActionStatus), Csv(d.ActionEnforcedBy), Csv(d.ActionNotes),
                Csv(d.DetectionTime.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)),
                Csv(d.LastSeen?.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture) ?? "")
            }));
        }

        var content = sb.ToString();
        File.WriteAllText(path, content, new UTF8Encoding(true));
        var hash = MinerFingerprintService.Sha256Hex(content);
        _db.SaveReportMeta(Guid.NewGuid().ToString("N"), operationCode, "CSV", generatedBy, path,
            detections.Count, detections.Where(d => d.PowerWatts != null).Sum(d => d.PowerWatts!.Value),
            detections.Count(d => d.PowerWatts != null), hash);
        return path;
    }

    /// <summary>خروجی JSON شامل متن خام پاسخ دستگاه برای بایگانی و بازبینی کارشناسی.</summary>
    public string ExportEvidenceJson(string? operationCode, string generatedBy)
    {
        var path = Path.Combine(ReportsDirectory(), $"Evidence_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        var detections = _db.GetDetections(operationCode);

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"generatedAt\": \"{DateTime.Now:yyyy-MM-ddTHH:mm:sszzz}\",");
        sb.AppendLine($"  \"generatedBy\": {Quote(generatedBy)},");
        sb.AppendLine($"  \"operationCode\": {Quote(operationCode ?? "همه")},");
        sb.AppendLine($"  \"deviceCount\": {detections.Count},");
        sb.AppendLine("  \"devices\": [");
        for (int i = 0; i < detections.Count; i++)
        {
            var d = detections[i];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"operationId\": {Quote(d.OperationID)},");
            sb.AppendLine($"      \"ip\": {Quote(d.IPAddress)},");
            sb.AppendLine($"      \"port\": {d.Port},");
            sb.AppendLine($"      \"portState\": {Quote(d.PortState)},");
            sb.AppendLine($"      \"protocol\": {Quote(d.Protocol)},");
            sb.AppendLine($"      \"detectionMethod\": {Quote(d.DetectionMethod)},");
            sb.AppendLine($"      \"confidence\": {d.Confidence.ToString("F2", CultureInfo.InvariantCulture)},");
            sb.AppendLine($"      \"software\": {Quote(d.SoftwareName)},");
            sb.AppendLine($"      \"model\": {Quote(d.DeviceModel)},");
            sb.AppendLine($"      \"firmware\": {Quote(d.FirmwareVersion)},");
            sb.AppendLine($"      \"hashRate\": {Quote(d.HashRate)},");
            sb.AppendLine($"      \"temperatureC\": {(d.TemperatureC?.ToString("F1", CultureInfo.InvariantCulture) ?? "null")},");
            sb.AppendLine($"      \"powerWatts\": {(d.PowerWatts?.ToString("F1", CultureInfo.InvariantCulture) ?? "null")},");
            sb.AppendLine($"      \"powerSource\": {Quote(d.PowerSource)},");
            sb.AppendLine($"      \"asNumber\": {Quote(d.AsNumber)},");
            sb.AppendLine($"      \"asName\": {Quote(d.AsName)},");
            sb.AppendLine($"      \"registrant\": {Quote(d.NetworkRegistrant)},");
            sb.AppendLine($"      \"evidenceHash\": {Quote(d.EvidenceHash)},");
            sb.AppendLine($"      \"evidenceSummary\": {Quote(d.EvidenceSummary)},");
            sb.AppendLine($"      \"rawResponse\": {Quote(d.BannerText)}");
            sb.AppendLine(i == detections.Count - 1 ? "    }" : "    },");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        var content = sb.ToString();
        File.WriteAllText(path, content, new UTF8Encoding(false));
        var hash = MinerFingerprintService.Sha256Hex(content);
        _db.SaveReportMeta(Guid.NewGuid().ToString("N"), operationCode, "EvidenceJSON", generatedBy, path,
            detections.Count, 0, 0, hash);
        return path;
    }

    private static string Quote(string? value)
    {
        var text = value ?? "";
        return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
    }

    private static string Csv(string? s)
    {
        s ??= "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
