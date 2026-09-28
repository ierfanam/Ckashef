namespace GovernmentMiningApp.Models;

public class GovernmentOperation
{
    public string OperationCode { get; set; } = "";
    public string Region { get; set; } = "";
    public string AuthorizedBy { get; set; } = "";
    public string PermitNumber { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = "InProgress";
    public string Notes { get; set; } = "";
    public int DetectionCount { get; set; }
    public double TotalConsumption { get; set; }
}

/// <summary>
/// رکورد یک دستگاه شناسایی‌شده. تمام مقادیر این رکورد از پروب واقعی شبکه
/// یا از استعلام رسمی مراجع بیرونی به دست آمده‌اند؛ هیچ مقدار ساختگی تولید نمی‌شود.
/// هر مقدار نامعلوم به صورت null یا رشته تهی باقی می‌ماند.
/// </summary>
public class DetectedDevice
{
    public string OperationID { get; set; } = "";
    public string OperationCode { get; set; } = "";
    public string ScanID { get; set; } = "";

    public string IPAddress { get; set; } = "";
    public int Port { get; set; }
    public string PortState { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string DetectionMethod { get; set; } = "";
    public int? LatencyMs { get; set; }
    public string HttpStatus { get; set; } = "";
    public string EvidenceHash { get; set; } = "";
    public string EvidenceSummary { get; set; } = "";
    public string BannerText { get; set; } = "";

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

    public string ReverseDns { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public string AsNumber { get; set; } = "";
    public string AsName { get; set; } = "";
    public string NetworkName { get; set; } = "";
    public string NetworkCountry { get; set; } = "";
    public string NetworkRegistrant { get; set; } = "";
    public string PrefixCidr { get; set; } = "";
    public string IntelSource { get; set; } = "";
    public string IntelQueriedAt { get; set; } = "";

    public string OperatorRecordID { get; set; } = "";
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    public string Province { get; set; } = "";
    public string City { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string SubscriberName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    public string ISP { get; set; } = "";
    public string OperatorName { get; set; } = "";
    public DateTime DetectionTime { get; set; }
    public DateTime? LastSeen { get; set; }
    public string ActionStatus { get; set; } = "منتظر دستور";
    public string ActionType { get; set; } = "";
    public string ActionEnforcedBy { get; set; } = "";
    public string ActionNotes { get; set; } = "";
    public double Confidence { get; set; }
}

public class Personnel
{
    public string PersonnelID { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Department { get; set; } = "";
    public string Role { get; set; } = "";
    public string Badge { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public int AuthorizationLevel { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime? LastLogin { get; set; }
}

public class DashboardStats
{
    public int ActiveOperations { get; set; }
    public int TotalDetections { get; set; }
    public int IdentifiedDetections { get; set; }
    public int UnidentifiedOpenPorts { get; set; }
    public int PendingActions { get; set; }
    public int SeizedDevices { get; set; }
    public double MeasuredPowerKw { get; set; }
    public int MeasuredPowerDevices { get; set; }
    public int PersonnelCount { get; set; }
    public int TotalScans { get; set; }
}

public class OperatorRecord
{
    public string RecordID { get; set; } = "";
    public string OperationID { get; set; } = "";
    public string IPAddress { get; set; } = "";
    public string SubscriberName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Province { get; set; } = "";
    public string City { get; set; } = "";
    public string Street { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string RequestReference { get; set; } = "";
    public string SourceAuthority { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string RequestedAt { get; set; } = "";
    public string ReceivedAt { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class NetworkOwnership
{
    public string IPAddress { get; set; } = "";
    public string PrefixCidr { get; set; } = "";
    public string AsNumber { get; set; } = "";
    public string AsName { get; set; } = "";
    public string NetworkName { get; set; } = "";
    public string Registrant { get; set; } = "";
    public string Country { get; set; } = "";
    public string ReverseDns { get; set; } = "";
    public string Source { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string RawResponse { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string QueryStatus { get; set; } = "";
    public DateTime QueriedAt { get; set; }

    // جغرافیای واقعی IP از سرویس عمومی بدون کلید
    public double? GeoLat { get; set; }
    public double? GeoLon { get; set; }
    public string GeoCity { get; set; } = "";
    public string GeoRegion { get; set; } = "";
    public string GeoCountry { get; set; } = "";
    public string GeoISP { get; set; } = "";
    public string GeoOrg { get; set; } = "";
    public string GeoTimezone { get; set; } = "";
    public string GeoQueryStatus { get; set; } = "";
    public DateTime? GeoQueriedAt { get; set; }
}

public class ScanHistoryEntry
{
    public string ScanID { get; set; } = "";
    public string OperationCode { get; set; } = "";
    public string ScanType { get; set; } = "";
    public string OperatorName { get; set; } = "";
    public string MachineName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int IPsScanned { get; set; }
    public int PortsProbed { get; set; }
    public int DevicesFound { get; set; }
    public int IdentifiedDevices { get; set; }
    public int ClosedPorts { get; set; }
    public int FilteredPorts { get; set; }
    public double ElapsedSeconds { get; set; }
    public string Notes { get; set; } = "";
}

public class AuditEntry
{
    public string LogID { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Badge { get; set; } = "";
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public string EntityID { get; set; } = "";
    public string Details { get; set; } = "";
    public string MachineName { get; set; } = "";
    public DateTime LoggedAt { get; set; }
    public string IntegrityHash { get; set; } = "";
    public string PrevHash { get; set; } = "";
}

public static class PortStates
{
    public const string Open = "باز";
    public const string Closed = "بسته";
    public const string Filtered = "فیلترشده";
    public const string Error = "خطا";
}

public sealed class ScanProgressEventArgs : EventArgs
{
    public int Percent { get; set; }
    public string Message { get; set; } = "";
    public int Found { get; set; }
    public int Probed { get; set; }
    public int Total { get; set; }
    public int Identified { get; set; }
    public double ElapsedSeconds { get; set; }
}

/// <summary>پارامترهای اجرای یک پروب مجاز واقعی روی اهداف اعلام‌شده.</summary>
public sealed class ScanRequest
{
    public string OperationCode { get; set; } = "";
    public string Region { get; set; } = "";
    public IReadOnlyList<string> Targets { get; set; } = Array.Empty<string>();
    public IReadOnlyList<int> Ports { get; set; } = Array.Empty<int>();
    public string AuthorizedBy { get; set; } = "";
    public int MaxParallel { get; set; } = 48;
    public int ConnectTimeoutMs { get; set; } = 800;
    public bool ResolveNetworkOwner { get; set; } = true;
    public bool LookUpLocalMac { get; set; } = true;
}

public sealed class ScanOutcome
{
    public string ScanID { get; set; } = "";
    public int TargetsExpanded { get; set; }
    public int PortsProbed { get; set; }
    public int OpenPorts { get; set; }
    public int Identified { get; set; }
    public int ClosedPorts { get; set; }
    public int FilteredPorts { get; set; }
    public int IntelFailures { get; set; }
    public double ElapsedSeconds { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public List<DetectedDevice> Devices { get; set; } = new();
}

public static class AppSession
{
    public static Personnel? CurrentUser { get; set; }
    public static bool IsAuthenticated => CurrentUser != null;
}
