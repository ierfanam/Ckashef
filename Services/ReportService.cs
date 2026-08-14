using System.Text;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

public sealed class ReportService
{
    private readonly DatabaseService _db;

    public ReportService(DatabaseService db) => _db = db;

    public string GenerateTextReport(string? operationCode, string generatedBy)
    {
        var reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
        Directory.CreateDirectory(reportsDir);
        var fileName = $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var path = Path.Combine(reportsDir, fileName);

        var detections = _db.GetDetections(operationCode);
        var ops = _db.GetOperations();
        if (!string.IsNullOrWhiteSpace(operationCode))
            ops = ops.Where(o => o.OperationCode == operationCode).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("════════════════════════════════════════════════════════════════");
        sb.AppendLine("  گزارش رسمی — برنامه قانونی و مجوزدار سفارشی دولت");
        sb.AppendLine("  سیستم مدیریت عملیات ردیابی دستگاه‌های ماینر");
        sb.AppendLine("════════════════════════════════════════════════════════════════");
        sb.AppendLine($"تاریخ تولید: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"تهیه‌کننده: {generatedBy}");
        sb.AppendLine($"کد عملیاتی فیلتر: {(string.IsNullOrWhiteSpace(operationCode) ? "همه" : operationCode)}");
        sb.AppendLine();

        sb.AppendLine("── عملیات‌ها ──");
        foreach (var o in ops)
        {
            sb.AppendLine($"  [{o.OperationCode}] {o.Region} | {o.Status} | مجاز: {o.AuthorizedBy}");
            sb.AppendLine($"     شروع: {o.StartTime:yyyy-MM-dd HH:mm} | شناسایی: {o.DetectionCount} | مصرف: {o.TotalConsumption:F0} W");
        }
        sb.AppendLine();

        sb.AppendLine("── دستگاه‌های شناسایی‌شده ──");
        sb.AppendLine($"تعداد کل: {detections.Count}");
        sb.AppendLine($"مصرف تخمینی کل: {detections.Sum(d => d.EstimatedConsumption):F0} وات ({detections.Sum(d => d.EstimatedConsumption) / 1000.0:F2} کیلووات)");
        sb.AppendLine();

        foreach (var d in detections)
        {
            sb.AppendLine($"• {d.IPAddress}:{d.Port} | {d.DeviceModel} | {d.EstimatedConsumption:F0}W");
            sb.AppendLine($"  استان/شهر: {d.Province} / {d.City}");
            sb.AppendLine($"  مشترک: {d.SubscriberName} | تلفن: {d.PhoneNumber} | ISP: {d.ISP}");
            sb.AppendLine($"  وضعیت اقدام: {d.ActionStatus} | اطمینان: {d.Confidence:P0}");
            sb.AppendLine($"  زمان شناسایی: {d.DetectionTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
        }

        sb.AppendLine("── آمار منطقه‌ای ──");
        foreach (var (prov, count, power) in _db.GetRegionalStats())
            sb.AppendLine($"  {prov}: {count} دستگاه — {power:F0} وات");

        sb.AppendLine();
        sb.AppendLine("════════════════════════════════════════════════════════════════");
        sb.AppendLine("پایان گزارش — سند داخلی مجاز");
        sb.AppendLine("════════════════════════════════════════════════════════════════");

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var id = Guid.NewGuid().ToString("N");
        _db.SaveReportMeta(id, operationCode, "Text", generatedBy, path, detections.Count,
            detections.Sum(d => d.EstimatedConsumption));

        return path;
    }

    public string ExportCsv(string? operationCode, string generatedBy)
    {
        var reportsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
        Directory.CreateDirectory(reportsDir);
        var path = Path.Combine(reportsDir, $"Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        var detections = _db.GetDetections(operationCode);

        var sb = new StringBuilder();
        sb.AppendLine("OperationID,OperationCode,IP,Port,Model,PowerW,Province,City,Subscriber,Phone,ISP,Status,Confidence,DetectionTime");
        foreach (var d in detections)
        {
            sb.AppendLine(string.Join(",",
                Csv(d.OperationID), Csv(d.OperationCode), Csv(d.IPAddress), d.Port,
                Csv(d.DeviceModel), d.EstimatedConsumption.ToString("F0"),
                Csv(d.Province), Csv(d.City), Csv(d.SubscriberName), Csv(d.PhoneNumber),
                Csv(d.ISP), Csv(d.ActionStatus), d.Confidence.ToString("F2"),
                Csv(d.DetectionTime.ToString("yyyy-MM-dd HH:mm:ss"))));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        _db.SaveReportMeta(Guid.NewGuid().ToString("N"), operationCode, "CSV", generatedBy, path,
            detections.Count, detections.Sum(d => d.EstimatedConsumption));
        return path;
    }

    private static string Csv(string? s)
    {
        s ??= "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
