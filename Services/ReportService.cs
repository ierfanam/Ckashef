using System.Text;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

public sealed class ReportService
{
    private readonly DatabaseService _db;
    public ReportService(DatabaseService db) => _db = db;

    public string GenerateTextReport(string? operationCode, string generatedBy)
    {
        var reportsDir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Reports"); Directory.CreateDirectory(reportsDir);
        var path=Path.Combine(reportsDir,$"Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        var detections=_db.GetDetections(operationCode); var ops=_db.GetOperations(); if(!string.IsNullOrWhiteSpace(operationCode)) ops=ops.Where(o=>o.OperationCode==operationCode).ToList();
        var sb=new StringBuilder();
        sb.AppendLine("════════════════════════════════════════════════════════════════");
        sb.AppendLine("  گزارش عملیاتی Ckashef");
        sb.AppendLine("  وضعیت داده‌ها: فقط مشاهدات و منابع ثبت‌شده — بدون استنتاج هویتی");
        sb.AppendLine("════════════════════════════════════════════════════════════════");
        sb.AppendLine($"تاریخ تولید: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"); sb.AppendLine($"تهیه‌کننده: {generatedBy}"); sb.AppendLine($"کد عملیات: {(string.IsNullOrWhiteSpace(operationCode)?"همه":operationCode)}"); sb.AppendLine();
        sb.AppendLine("── عملیات‌ها ──"); foreach(var o in ops){sb.AppendLine($"[{o.OperationCode}] {o.Region} | {o.Status} | مجاز: {o.AuthorizedBy}");sb.AppendLine($"شروع: {o.StartTime:yyyy-MM-dd HH:mm} | شناسایی: {o.DetectionCount} | مصرف ثبت‌شده: {o.TotalConsumption:F0} W");} sb.AppendLine();
        sb.AppendLine("── مشاهدات ──"); sb.AppendLine($"تعداد: {detections.Count}"); sb.AppendLine($"توان ثبت‌شده: {detections.Sum(d=>d.EstimatedConsumption):F0} W"); sb.AppendLine();
        foreach(var d in detections)
        {
            sb.AppendLine($"• {d.IPAddress}:{d.Port}"); sb.AppendLine($"  نوع شواهد: {d.EvidenceType}"); sb.AppendLine($"  منبع داده: {d.DataSource}"); sb.AppendLine($"  وضعیت تطبیق هویت: {d.MatchStatus}");
            sb.AppendLine($"  مدل دستگاه: {d.DeviceModel}"); sb.AppendLine($"  HashRate: {d.HashRate}"); sb.AppendLine($"  توان: {d.EstimatedConsumption:F0} W");
            sb.AppendLine($"  استان/شهر/نشانی: {d.Province} / {d.City} / {d.Street}"); sb.AppendLine($"  کدپستی: {d.PostalCode}"); sb.AppendLine($"  مشترک: {d.SubscriberName}"); sb.AppendLine($"  تلفن: {d.PhoneNumber}"); sb.AppendLine($"  اپراتور/ISP: {d.OperatorName} / {d.ISP}"); sb.AppendLine($"  وضعیت اقدام: {d.ActionStatus}"); sb.AppendLine($"  اطمینان مشاهده: {d.Confidence:P0}"); sb.AppendLine($"  زمان: {d.DetectionTime:yyyy-MM-dd HH:mm:ss}"); sb.AppendLine($"  توضیح: {d.ActionNotes}"); sb.AppendLine();
        }
        sb.AppendLine("── هشدار اعتبار ──"); sb.AppendLine("وجود پورت باز یا پاسخ شبکه به‌تنهایی اثبات‌کننده مدل دستگاه، توان مصرفی، هویت مشترک، نشانی یا شماره تلفن نیست. این موارد فقط در صورت ثبت منبع رسمی و مجاز معتبرند.");
        sb.AppendLine("════════════════════════════════════════════════════════════════");
        File.WriteAllText(path,sb.ToString(),new UTF8Encoding(true)); _db.SaveReportMeta(Guid.NewGuid().ToString("N"),operationCode,"Text",generatedBy,path,detections.Count,detections.Sum(d=>d.EstimatedConsumption)); return path;
    }

    public string ExportCsv(string? operationCode,string generatedBy)
    {
        var dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Reports");Directory.CreateDirectory(dir);var path=Path.Combine(dir,$"Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");var ds=_db.GetDetections(operationCode);
        var sb=new StringBuilder();sb.AppendLine("OperationID,OperationCode,IP,Port,EvidenceType,DataSource,MatchStatus,Model,HashRate,PowerW,Province,City,Street,PostalCode,Subscriber,Phone,ISP,Operator,Status,Confidence,DetectionTime");
        foreach(var d in ds) sb.AppendLine(string.Join(",",Csv(d.OperationID),Csv(d.OperationCode),Csv(d.IPAddress),d.Port,Csv(d.EvidenceType),Csv(d.DataSource),Csv(d.MatchStatus),Csv(d.DeviceModel),Csv(d.HashRate),d.EstimatedConsumption.ToString("F0"),Csv(d.Province),Csv(d.City),Csv(d.Street),Csv(d.PostalCode),Csv(d.SubscriberName),Csv(d.PhoneNumber),Csv(d.ISP),Csv(d.OperatorName),Csv(d.ActionStatus),d.Confidence.ToString("F2"),Csv(d.DetectionTime.ToString("yyyy-MM-dd HH:mm:ss"))));
        File.WriteAllText(path,sb.ToString(),new UTF8Encoding(true));_db.SaveReportMeta(Guid.NewGuid().ToString("N"),operationCode,"CSV",generatedBy,path,ds.Count,ds.Sum(d=>d.EstimatedConsumption));return path;
    }
    private static string Csv(string? s){s??="";return s.Contains(',')||s.Contains('"')||s.Contains('\n')||s.Contains('\r')?'"'+s.Replace("\"","\"\"")+'"':s;}
}
