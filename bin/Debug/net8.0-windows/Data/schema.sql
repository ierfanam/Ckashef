-- پایگاه داده سامانه ردیابی دستگاه‌های ماینر
-- اصل حاکم: تنها داده‌های واقعی اندازه‌گیری‌شده ذخیره می‌شوند.
-- هر مقداری که واقعاً از دستگاه یا از مرجع معتبر خوانده نشده باشد، NULL/تهی ذخیره می‌شود.

CREATE TABLE IF NOT EXISTS GovernmentOperations (
    OperationCode TEXT PRIMARY KEY,
    Region TEXT NOT NULL,
    AuthorizedBy TEXT NOT NULL,
    PermitNumber TEXT,
    StartTime TEXT NOT NULL,
    EndTime TEXT,
    Status TEXT DEFAULT 'InProgress',
    Notes TEXT,
    CreatedAt TEXT DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS DetectedOperations (
    OperationID TEXT PRIMARY KEY,
    OperationCode TEXT NOT NULL,
    ScanID TEXT,
    IPAddress TEXT NOT NULL,
    Port INTEGER NOT NULL,

    -- وضعیت واقعی پورت و نحوه شناسایی
    PortState TEXT,
    Protocol TEXT,
    DetectionMethod TEXT,
    LatencyMs INTEGER,
    HttpStatus TEXT,
    EvidenceHash TEXT,
    EvidenceSummary TEXT,
    BannerText TEXT,

    -- داده‌های خوانده‌شده از خود دستگاه
    SoftwareName TEXT,
    DeviceModel TEXT,
    FirmwareVersion TEXT,
    HashRate TEXT,
    TemperatureC REAL,
    FanPercent REAL,
    PowerWatts REAL,
    PowerSource TEXT,
    UptimeSeconds INTEGER,
    PoolAddress TEXT,
    WorkerName TEXT,

    -- استعلام شبکه از مراجع واقعی (RDAP / RIPE Stat / rDNS / ARP)
    ReverseDns TEXT,
    MacAddress TEXT,
    AsNumber TEXT,
    AsName TEXT,
    NetworkName TEXT,
    NetworkCountry TEXT,
    NetworkRegistrant TEXT,
    PrefixCidr TEXT,
    IntelSource TEXT,
    IntelQueriedAt TEXT,

    -- اطلاعات مشترک: فقط از پاسخ رسمی اپراتور (جدول OperatorRecords)
    OperatorRecordID TEXT,
    LocationLatitude REAL,
    LocationLongitude REAL,
    Province TEXT,
    City TEXT,
    Street TEXT,
    PostalCode TEXT,
    SubscriberName TEXT,
    PhoneNumber TEXT,

    ISP TEXT,
    OperatorName TEXT,
    DetectionTime TEXT NOT NULL,
    LastSeen TEXT,
    ActionStatus TEXT DEFAULT 'منتظر‌دستورالعمل',
    ActionType TEXT,
    ActionTimestamp TEXT,
    ActionEnforcedBy TEXT,
    ActionNotes TEXT,
    Confidence REAL,
    FOREIGN KEY (OperationCode) REFERENCES GovernmentOperations(OperationCode)
);

-- سوابق اقدامات اجرایی روی دستگاه‌های شناسایی‌شده
CREATE TABLE IF NOT EXISTS ActionLogs (
    LogID TEXT PRIMARY KEY,
    OperationID TEXT NOT NULL,
    ActionType TEXT,
    ActionTime TEXT DEFAULT (datetime('now','localtime')),
    EnforcedBy TEXT,
    Outcome TEXT,
    Notes TEXT,
    FOREIGN KEY (OperationID) REFERENCES DetectedOperations(OperationID)
);

-- رکورد رسمی استعلام اطلاعات مشترک از اپراتور/مراجع ذی‌صلاح
CREATE TABLE IF NOT EXISTS OperatorRecords (
    RecordID TEXT PRIMARY KEY,
    OperationID TEXT NOT NULL,
    IPAddress TEXT NOT NULL,
    SubscriberName TEXT,
    PhoneNumber TEXT,
    Province TEXT,
    City TEXT,
    Street TEXT,
    PostalCode TEXT,
    RequestReference TEXT,       -- شماره نامه/استعلام رسمی
    SourceAuthority TEXT,        -- مرجع پاسخ‌دهنده
    RequestedBy TEXT,
    RequestedAt TEXT,
    ReceivedAt TEXT,
    PayloadHash TEXT,            -- SHA-256 متن پاسخ جهت اصالت‌سنجی
    Notes TEXT,
    FOREIGN KEY (OperationID) REFERENCES DetectedOperations(OperationID)
);

-- مالکیت شبکه: نتیجه واقعی استعلام از RDAP و RIPE Stat (با حافظه نهان)
CREATE TABLE IF NOT EXISTS NetworkOwnership (
    IPAddress TEXT PRIMARY KEY,
    PrefixCidr TEXT,
    AsNumber TEXT,
    AsName TEXT,
    NetworkName TEXT,
    Registrant TEXT,
    Country TEXT,
    ReverseDns TEXT,
    Source TEXT,
    SourceUrl TEXT,
    RawResponse TEXT,
    PayloadHash TEXT,
    QueryStatus TEXT,
    QueriedAt TEXT NOT NULL
);

-- زنجیره حسابرسی تغییرناپذیر (هش زنجیره‌ای)
CREATE TABLE IF NOT EXISTS AuditLog (
    LogID TEXT PRIMARY KEY,
    UserName TEXT,
    Badge TEXT,
    Action TEXT NOT NULL,
    Entity TEXT,
    EntityID TEXT,
    Details TEXT,
    MachineName TEXT,
    LoggedAt TEXT NOT NULL,
    PrevHash TEXT,
    IntegrityHash TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS AuthorizedPersonnel (
    PersonnelID TEXT PRIMARY KEY,
    FullName TEXT NOT NULL,
    Department TEXT,
    Role TEXT,
    Badge TEXT UNIQUE,
    PhoneNumber TEXT,
    Email TEXT,
    PasswordHash TEXT,
    AuthorizationLevel INTEGER DEFAULT 1,
    MustChangePassword INTEGER DEFAULT 0,
    CreatedAt TEXT DEFAULT (datetime('now','localtime')),
    LastLogin TEXT,
    IsActive INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS GeneratedReports (
    ReportID TEXT PRIMARY KEY,
    OperationCode TEXT,
    ReportType TEXT,
    GeneratedBy TEXT,
    GenerationTime TEXT DEFAULT (datetime('now','localtime')),
    FilePath TEXT,
    TotalDetections INTEGER,
    TotalPowerConsumption REAL,
    MeasuredPowerDevices INTEGER,
    ContentHash TEXT,
    Status TEXT
);

CREATE TABLE IF NOT EXISTS ScanHistory (
    ScanID TEXT PRIMARY KEY,
    OperationCode TEXT,
    ScanType TEXT NOT NULL,      -- PortProbe
    OperatorName TEXT,
    MachineName TEXT,
    ScanStartTime TEXT NOT NULL,
    ScanEndTime TEXT,
    IPsScanned INTEGER DEFAULT 0,
    PortsProbed INTEGER DEFAULT 0,
    DevicesFound INTEGER DEFAULT 0,
    IdentifiedDevices INTEGER DEFAULT 0,
    ClosedPorts INTEGER DEFAULT 0,
    FilteredPorts INTEGER DEFAULT 0,
    ElapsedSeconds REAL,
    Notes TEXT
);

CREATE TABLE IF NOT EXISTS AppSettings (
    Key TEXT PRIMARY KEY,
    Value TEXT
);

CREATE INDEX IF NOT EXISTS idx_detected_opcode ON DetectedOperations(OperationCode);
CREATE INDEX IF NOT EXISTS idx_detected_ip ON DetectedOperations(IPAddress);
CREATE INDEX IF NOT EXISTS idx_detected_status ON DetectedOperations(ActionStatus);
CREATE INDEX IF NOT EXISTS idx_detected_scan ON DetectedOperations(ScanID);
CREATE INDEX IF NOT EXISTS idx_logs_opid ON ActionLogs(OperationID);
CREATE INDEX IF NOT EXISTS idx_audit_time ON AuditLog(LoggedAt);
CREATE INDEX IF NOT EXISTS idx_operatorrec_opid ON OperatorRecords(OperationID);
