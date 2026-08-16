-- Production schema. No demo records, default credentials, or synthetic operational data are created here.

CREATE TABLE IF NOT EXISTS GovernmentOperations (
    OperationCode TEXT PRIMARY KEY, Region TEXT NOT NULL, AuthorizedBy TEXT NOT NULL,
    StartTime TEXT NOT NULL, EndTime TEXT, Status TEXT DEFAULT 'InProgress', Notes TEXT,
    CreatedAt TEXT DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS DetectedOperations (
    OperationID TEXT PRIMARY KEY, OperationCode TEXT NOT NULL, IPAddress TEXT NOT NULL, Port INTEGER NOT NULL,
    LocationLatitude REAL, LocationLongitude REAL, LocationAccuracy REAL, Province TEXT, City TEXT, Street TEXT,
    PostalCode TEXT, SubscriberName TEXT, PhoneNumber TEXT, ISP TEXT, OperatorName TEXT, DeviceModel TEXT,
    HashRate TEXT, EstimatedConsumption REAL, DetectionTime TEXT NOT NULL,
    ActionStatus TEXT DEFAULT 'منتظر‌دستورالعمل', ActionType TEXT, ActionTimestamp TEXT,
    ActionEnforcedBy TEXT, ActionNotes TEXT, Confidence REAL,
    DataSource TEXT NOT NULL DEFAULT 'Unknown', SourceRecordId TEXT, SourceQueriedAt TEXT,
    MatchStatus TEXT NOT NULL DEFAULT 'Unresolved', EvidenceType TEXT NOT NULL DEFAULT 'NetworkObservation',
    EvidenceId TEXT, IsSynthetic INTEGER NOT NULL DEFAULT 0, CreatedBy TEXT,
    FOREIGN KEY (OperationCode) REFERENCES GovernmentOperations(OperationCode)
);

CREATE TABLE IF NOT EXISTS ASICDevices (
    DeviceID TEXT PRIMARY KEY, OperationID TEXT NOT NULL, IPAddress TEXT NOT NULL, Port INTEGER NOT NULL,
    DeviceModel TEXT, FirmwareVersion TEXT, PoolAddress TEXT, WalletAddress TEXT,
    PowerConsumption REAL, DetectionConfidence REAL,
    FirstDetected TEXT DEFAULT (datetime('now','localtime')), LastSeen TEXT,
    FOREIGN KEY (OperationID) REFERENCES DetectedOperations(OperationID)
);

CREATE TABLE IF NOT EXISTS ActionLogs (
    LogID TEXT PRIMARY KEY, OperationID TEXT NOT NULL, ActionType TEXT,
    ActionTime TEXT DEFAULT (datetime('now','localtime')), EnforcedBy TEXT, Outcome TEXT, Notes TEXT,
    FOREIGN KEY (OperationID) REFERENCES DetectedOperations(OperationID)
);

CREATE TABLE IF NOT EXISTS AuthorizedPersonnel (
    PersonnelID TEXT PRIMARY KEY, FullName TEXT NOT NULL, Department TEXT, Role TEXT, Badge TEXT UNIQUE,
    PhoneNumber TEXT, Email TEXT, PasswordHash TEXT, AuthorizationLevel INTEGER DEFAULT 1,
    CreatedAt TEXT DEFAULT (datetime('now','localtime')), LastLogin TEXT, IsActive INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS GeneratedReports (
    ReportID TEXT PRIMARY KEY, OperationCode TEXT, ReportType TEXT, GeneratedBy TEXT,
    GenerationTime TEXT DEFAULT (datetime('now','localtime')), FilePath TEXT,
    TotalDetections INTEGER, TotalPowerConsumption REAL, Status TEXT
);

CREATE TABLE IF NOT EXISTS ScanHistory (
    ScanID TEXT PRIMARY KEY, OperationCode TEXT, ScanType TEXT, ScanStartTime TEXT, ScanEndTime TEXT,
    IPsScanned INTEGER, DevicesFound INTEGER, Notes TEXT
);

CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT PRIMARY KEY, Value TEXT);

CREATE INDEX IF NOT EXISTS idx_detected_opcode ON DetectedOperations(OperationCode);
CREATE INDEX IF NOT EXISTS idx_detected_ip ON DetectedOperations(IPAddress);
CREATE INDEX IF NOT EXISTS idx_detected_status ON DetectedOperations(ActionStatus);
CREATE INDEX IF NOT EXISTS idx_detected_source ON DetectedOperations(DataSource, SourceRecordId);
CREATE INDEX IF NOT EXISTS idx_detected_match ON DetectedOperations(MatchStatus);
CREATE INDEX IF NOT EXISTS idx_logs_opid ON ActionLogs(OperationID);