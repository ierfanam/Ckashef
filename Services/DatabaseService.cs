using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GovernmentMiningApp.Models;
using Microsoft.Data.Sqlite;

namespace GovernmentMiningApp.Services;

public sealed class DatabaseService
{
    private const int Pbkdf2Iterations = 120_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    private readonly string _dbPath;
    private readonly string _connectionString;

    public DatabaseService(string? dataDirectory = null)
    {
        var dataDir = dataDirectory ?? ResolveWritableDataDirectory();
        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "government_ops.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            Pooling = true
        }.ToString();
    }

    /// <summary>
    /// اگر پوشه اجرای برنامه قابل نوشتن نباشد (مثلاً نصب در Program Files)
    /// داده‌ها در پوشه داده کاربر ذخیره می‌شود تا سامانه از کار نیفتد.
    /// </summary>
    private static string ResolveWritableDataDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var preferred = Path.Combine(baseDir, "Data");
        try
        {
            Directory.CreateDirectory(preferred);
            var probe = Path.Combine(preferred, ".write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return preferred;
        }
        catch (Exception)
        {
            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GovernmentMiningApp");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    public string DatabasePath => _dbPath;

    /// <summary>اولین اجرا: حساب مدیر با رمز تصادفی ساخته می‌شود و رمز یک‌بار نمایش داده می‌شود.</summary>
    public FirstRunInfo EnsureAdminAccount()
    {
        using var conn = Open();
        ApplySchema(conn);
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM AuthorizedPersonnel";
        var count = Convert.ToInt64(check.ExecuteScalar());
        if (count > 0) return new FirstRunInfo();

        var password = GeneratePassword();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO AuthorizedPersonnel
  (PersonnelID, FullName, Department, Role, Badge, PasswordHash, AuthorizationLevel, MustChangePassword, IsActive)
VALUES (@id, @name, @dept, @role, @badge, @pass, 5, 1, 1)";
        P(cmd, "@id", Guid.NewGuid().ToString("N"));
        P(cmd, "@name", "مدیر سیستم");
        P(cmd, "@dept", "مرکز عملیات");
        P(cmd, "@role", "Admin");
        P(cmd, "@badge", "ADMIN-001");
        P(cmd, "@pass", HashPassword(password));
        cmd.ExecuteNonQuery();

        WriteAudit(conn, "System", "-", "InitialAdminCreated", "AuthorizedPersonnel", "ADMIN-001",
            "حساب مدیر در اولین اجرای سامانه ساخته شد و رمز به‌صورت تصادفی تولید گردید.");

        return new FirstRunInfo { Created = true, Badge = "ADMIN-001", Password = password };
    }

    public void Initialize()
    {
        using var conn = Open();
        ApplySchema(conn);
    }

    private void ApplySchema(SqliteConnection conn)
    {
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=10000;";
            pragma.ExecuteNonQuery();
        }

        var schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "schema.sql");
        var sql = File.Exists(schemaPath)
            ? File.ReadAllText(schemaPath, Encoding.UTF8)
            : EmbeddedSchema();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        ApplyMigrations(conn);
    }

    /// <summary>برای پایگاه‌های داده نسخه‌های قبلی، ستون‌های جدید افزوده می‌شود.</summary>
    private static void ApplyMigrations(SqliteConnection conn)
    {
        var additions = new (string Table, string Column, string Definition)[]
        {
            ("GovernmentOperations", "PermitNumber", "TEXT"),
            ("DetectedOperations", "ScanID", "TEXT"),
            ("DetectedOperations", "PortState", "TEXT"),
            ("DetectedOperations", "Protocol", "TEXT"),
            ("DetectedOperations", "DetectionMethod", "TEXT"),
            ("DetectedOperations", "LatencyMs", "INTEGER"),
            ("DetectedOperations", "HttpStatus", "TEXT"),
            ("DetectedOperations", "EvidenceHash", "TEXT"),
            ("DetectedOperations", "EvidenceSummary", "TEXT"),
            ("DetectedOperations", "BannerText", "TEXT"),
            ("DetectedOperations", "SoftwareName", "TEXT"),
            ("DetectedOperations", "FirmwareVersion", "TEXT"),
            ("DetectedOperations", "TemperatureC", "REAL"),
            ("DetectedOperations", "FanPercent", "REAL"),
            ("DetectedOperations", "PowerWatts", "REAL"),
            ("DetectedOperations", "PowerSource", "TEXT"),
            ("DetectedOperations", "UptimeSeconds", "INTEGER"),
            ("DetectedOperations", "PoolAddress", "TEXT"),
            ("DetectedOperations", "WorkerName", "TEXT"),
            ("DetectedOperations", "ReverseDns", "TEXT"),
            ("DetectedOperations", "MacAddress", "TEXT"),
            ("DetectedOperations", "AsNumber", "TEXT"),
            ("DetectedOperations", "AsName", "TEXT"),
            ("DetectedOperations", "NetworkName", "TEXT"),
            ("DetectedOperations", "NetworkCountry", "TEXT"),
            ("DetectedOperations", "NetworkRegistrant", "TEXT"),
            ("DetectedOperations", "PrefixCidr", "TEXT"),
            ("DetectedOperations", "IntelSource", "TEXT"),
            ("DetectedOperations", "IntelQueriedAt", "TEXT"),
            ("DetectedOperations", "OperatorRecordID", "TEXT"),
            ("DetectedOperations", "LastSeen", "TEXT"),
            ("AuthorizedPersonnel", "MustChangePassword", "INTEGER DEFAULT 0"),
            ("GeneratedReports", "MeasuredPowerDevices", "INTEGER"),
            ("GeneratedReports", "ContentHash", "TEXT"),
            ("ScanHistory", "OperatorName", "TEXT"),
            ("ScanHistory", "MachineName", "TEXT"),
            ("ScanHistory", "PortsProbed", "INTEGER DEFAULT 0"),
            ("ScanHistory", "IdentifiedDevices", "INTEGER DEFAULT 0"),
            ("ScanHistory", "ClosedPorts", "INTEGER DEFAULT 0"),
            ("ScanHistory", "FilteredPorts", "INTEGER DEFAULT 0"),
            ("ScanHistory", "ElapsedSeconds", "REAL")
        };

        foreach (var (table, column, definition) in additions)
        {
            if (!ColumnExists(conn, table, column))
                Execute(conn, $"ALTER TABLE {table} ADD COLUMN {column} {definition}");
        }

        if (!ColumnExists(conn, "DetectedOperations", "EstimatedConsumption"))
            Execute(conn, "ALTER TABLE DetectedOperations ADD COLUMN EstimatedConsumption REAL");
    }

    private static bool ColumnExists(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string EmbeddedSchema() => @"
CREATE TABLE IF NOT EXISTS GovernmentOperations (
    OperationCode TEXT PRIMARY KEY, Region TEXT NOT NULL, AuthorizedBy TEXT NOT NULL,
    PermitNumber TEXT, StartTime TEXT NOT NULL, EndTime TEXT, Status TEXT DEFAULT 'InProgress',
    Notes TEXT, CreatedAt TEXT DEFAULT (datetime('now','localtime')));
CREATE TABLE IF NOT EXISTS DetectedOperations (
    OperationID TEXT PRIMARY KEY, OperationCode TEXT NOT NULL, ScanID TEXT, IPAddress TEXT NOT NULL,
    Port INTEGER NOT NULL, PortState TEXT, Protocol TEXT, DetectionMethod TEXT, LatencyMs INTEGER,
    HttpStatus TEXT, EvidenceHash TEXT, EvidenceSummary TEXT, BannerText TEXT, SoftwareName TEXT,
    DeviceModel TEXT, FirmwareVersion TEXT, HashRate TEXT, TemperatureC REAL, FanPercent REAL,
    PowerWatts REAL, PowerSource TEXT, UptimeSeconds INTEGER, PoolAddress TEXT, WorkerName TEXT,
    ReverseDns TEXT, MacAddress TEXT, AsNumber TEXT, AsName TEXT, NetworkName TEXT,
    NetworkCountry TEXT, NetworkRegistrant TEXT, PrefixCidr TEXT, IntelSource TEXT, IntelQueriedAt TEXT,
    OperatorRecordID TEXT, LocationLatitude REAL, LocationLongitude REAL, Province TEXT, City TEXT,
    Street TEXT, PostalCode TEXT, SubscriberName TEXT, PhoneNumber TEXT, ISP TEXT, OperatorName TEXT,
    DetectionTime TEXT NOT NULL, LastSeen TEXT, ActionStatus TEXT DEFAULT 'منتظر دستور',
    ActionType TEXT, ActionTimestamp TEXT, ActionEnforcedBy TEXT, ActionNotes TEXT, Confidence REAL);
CREATE TABLE IF NOT EXISTS OperatorRecords (
    RecordID TEXT PRIMARY KEY, OperationID TEXT NOT NULL, IPAddress TEXT NOT NULL, SubscriberName TEXT,
    PhoneNumber TEXT, Province TEXT, City TEXT, Street TEXT, PostalCode TEXT, RequestReference TEXT,
    SourceAuthority TEXT, RequestedBy TEXT, RequestedAt TEXT, ReceivedAt TEXT, PayloadHash TEXT, Notes TEXT);
CREATE TABLE IF NOT EXISTS NetworkOwnership (
    IPAddress TEXT PRIMARY KEY, PrefixCidr TEXT, AsNumber TEXT, AsName TEXT, NetworkName TEXT,
    Registrant TEXT, Country TEXT, ReverseDns TEXT, Source TEXT, SourceUrl TEXT, RawResponse TEXT,
    PayloadHash TEXT, QueryStatus TEXT, QueriedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS ActionLogs (
    LogID TEXT PRIMARY KEY, OperationID TEXT NOT NULL, ActionType TEXT,
    ActionTime TEXT DEFAULT (datetime('now','localtime')), EnforcedBy TEXT, Outcome TEXT, Notes TEXT);
CREATE TABLE IF NOT EXISTS AuthorizedPersonnel (
    PersonnelID TEXT PRIMARY KEY, FullName TEXT NOT NULL, Department TEXT, Role TEXT, Badge TEXT UNIQUE,
    PhoneNumber TEXT, Email TEXT, PasswordHash TEXT, AuthorizationLevel INTEGER DEFAULT 1,
    MustChangePassword INTEGER DEFAULT 0, CreatedAt TEXT DEFAULT (datetime('now','localtime')),
    LastLogin TEXT, IsActive INTEGER DEFAULT 1);
CREATE TABLE IF NOT EXISTS GeneratedReports (
    ReportID TEXT PRIMARY KEY, OperationCode TEXT, ReportType TEXT, GeneratedBy TEXT,
    GenerationTime TEXT DEFAULT (datetime('now','localtime')), FilePath TEXT, TotalDetections INTEGER,
    TotalPowerConsumption REAL, MeasuredPowerDevices INTEGER, ContentHash TEXT, Status TEXT);
CREATE TABLE IF NOT EXISTS ScanHistory (
    ScanID TEXT PRIMARY KEY, OperationCode TEXT, ScanType TEXT NOT NULL, OperatorName TEXT,
    MachineName TEXT, ScanStartTime TEXT NOT NULL, ScanEndTime TEXT, IPsScanned INTEGER DEFAULT 0,
    PortsProbed INTEGER DEFAULT 0, DevicesFound INTEGER DEFAULT 0, IdentifiedDevices INTEGER DEFAULT 0,
    ClosedPorts INTEGER DEFAULT 0, FilteredPorts INTEGER DEFAULT 0, ElapsedSeconds REAL, Notes TEXT);
CREATE TABLE IF NOT EXISTS AuditLog (
    LogID TEXT PRIMARY KEY, UserName TEXT, Badge TEXT, Action TEXT NOT NULL, Entity TEXT,
    EntityID TEXT, Details TEXT, MachineName TEXT, LoggedAt TEXT NOT NULL, PrevHash TEXT, IntegrityHash TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT PRIMARY KEY, Value TEXT);
";

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static void P(SqliteCommand cmd, string name, object? value) =>
        cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

    public static string NowStamp() => DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string AuditStamp() => DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);

    private static DateTime ParseDate(object? value) => value switch
    {
        null or DBNull => DateTime.MinValue,
        DateTime dt => dt,
        string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
        _ => DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var p) ? p : DateTime.MinValue
    };

    private static DateTime? ParseDateOrNull(object? value)
    {
        if (value is null or DBNull) return null;
        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text)) return null;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    // ---------- رمز عبور ----------

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"pbkdf2$sha256${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    private static string LegacyHash(string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("GMA|" + password)));

    private static bool VerifyHash(string stored, string password)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        if (!stored.StartsWith("pbkdf2$", StringComparison.Ordinal))
            return string.Equals(stored, LegacyHash(password), StringComparison.OrdinalIgnoreCase);

        var parts = stored.Split('$');
        if (parts.Length < 5) return false;
        if (!int.TryParse(parts[2], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789@#%*";
        var chars = new char[16];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(chars);
    }

    public Personnel? Authenticate(string badge, string password)
    {
        if (string.IsNullOrWhiteSpace(badge) || string.IsNullOrEmpty(password)) return null;

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT PersonnelID, FullName, Department, Role, Badge, PhoneNumber, Email,
            AuthorizationLevel, IsActive, LastLogin, MustChangePassword, PasswordHash
            FROM AuthorizedPersonnel WHERE Badge = @badge AND IsActive = 1";
        P(cmd, "@badge", badge.Trim());

        Personnel? user = null;
        string storedHash = "";
        using (var r = cmd.ExecuteReader())
        {
            if (r.Read())
            {
                user = new Personnel
                {
                    PersonnelID = r.GetString(0),
                    FullName = r.GetString(1),
                    Department = Str(r, 2),
                    Role = Str(r, 3),
                    Badge = Str(r, 4),
                    PhoneNumber = Str(r, 5),
                    Email = Str(r, 6),
                    AuthorizationLevel = Int(r, 7, 1),
                    IsActive = Int(r, 8, 0) == 1,
                    LastLogin = ParseDateOrNull(r[9]),
                    MustChangePassword = Int(r, 10, 0) == 1
                };
                storedHash = Str(r, 11);
            }
        }

        if (user == null) return null;
        if (!VerifyHash(storedHash, password))
        {
            WriteAudit(conn, badge.Trim(), badge.Trim(), "LoginFailed", "AuthorizedPersonnel",
                user.PersonnelID, "ورود ناموفق.");
            return null;
        }

        var stamp = NowStamp();
        using (var upd = conn.CreateCommand())
        {
            upd.CommandText = @"UPDATE AuthorizedPersonnel SET LastLogin = @t,
                PasswordHash = CASE WHEN PasswordHash LIKE 'pbkdf2$%' THEN PasswordHash ELSE @pass END
                WHERE PersonnelID = @id";
            P(upd, "@t", stamp);
            P(upd, "@pass", HashPassword(password));
            P(upd, "@id", user.PersonnelID);
            upd.ExecuteNonQuery();
        }

        user.LastLogin = DateTime.Now;
        WriteAudit(conn, user.FullName, user.Badge, "Login", "AuthorizedPersonnel",
            user.PersonnelID, "ورود موفق به سامانه.");
        return user;
    }

    public bool ChangePassword(string personnelId, string oldPassword, string newPassword)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash FROM AuthorizedPersonnel WHERE PersonnelID = @id";
        P(cmd, "@id", personnelId);
        var storedValue = cmd.ExecuteScalar();
        var stored = storedValue == null ? "" : Convert.ToString(storedValue) ?? "";
        if (!VerifyHash(stored, oldPassword)) return false;

        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE AuthorizedPersonnel SET PasswordHash = @p, MustChangePassword = 0 WHERE PersonnelID = @id";
        P(upd, "@p", HashPassword(newPassword));
        P(upd, "@id", personnelId);
        upd.ExecuteNonQuery();
        return true;
    }

    // ---------- حسابرسی ----------

    public void WriteAudit(string action, string entity, string entityId, string details) =>
        WriteAudit(AppSession.CurrentUser?.FullName ?? "ناشناس",
            AppSession.CurrentUser?.Badge ?? "-", action, entity, entityId, details);

    public void WriteAudit(string user, string badge, string action, string entity, string entityId, string details)
    {
        using var conn = Open();
        WriteAudit(conn, user, badge, action, entity, entityId, details);
    }

    private static void WriteAudit(
        SqliteConnection conn, string user, string badge, string action,
        string entity, string entityId, string details)
    {
        string prevHash;
        using (var last = conn.CreateCommand())
        {
            last.CommandText = "SELECT IntegrityHash FROM AuditLog ORDER BY rowid DESC LIMIT 1";
            var lastValue = last.ExecuteScalar();
            prevHash = lastValue == null ? "" : Convert.ToString(lastValue) ?? "";
        }

        var stamp = AuditStamp();
        var payload = $"{prevHash}|{stamp}|{user}|{badge}|{action}|{entity}|{entityId}|{details}|{Environment.MachineName}";
        var hash = MinerFingerprintService.Sha256Hex(payload);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO AuditLog (LogID, UserName, Badge, Action, Entity, EntityID, Details, MachineName, LoggedAt, PrevHash, IntegrityHash)
VALUES (@id,@u,@b,@a,@e,@ei,@d,@m,@t,@p,@h)";
        P(cmd, "@id", Guid.NewGuid().ToString("N"));
        P(cmd, "@u", user);
        P(cmd, "@b", badge);
        P(cmd, "@a", action);
        P(cmd, "@e", entity);
        P(cmd, "@ei", entityId);
        P(cmd, "@d", details);
        P(cmd, "@m", Environment.MachineName);
        P(cmd, "@t", stamp);
        P(cmd, "@p", prevHash);
        P(cmd, "@h", hash);
        cmd.ExecuteNonQuery();
    }

    public List<AuditEntry> GetAuditLog(int limit = 500)
    {
        var list = new List<AuditEntry>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT LogID, UserName, Badge, Action, Entity, EntityID, Details, MachineName, LoggedAt, PrevHash, IntegrityHash FROM AuditLog ORDER BY LoggedAt DESC, rowid DESC LIMIT @l";
        P(cmd, "@l", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new AuditEntry
            {
                LogID = Str(r, 0),
                UserName = Str(r, 1),
                Badge = Str(r, 2),
                Action = Str(r, 3),
                Entity = Str(r, 4),
                EntityID = Str(r, 5),
                Details = Str(r, 6),
                MachineName = Str(r, 7),
                LoggedAt = ParseDate(r[8]),
                PrevHash = Str(r, 9),
                IntegrityHash = Str(r, 10)
            });
        }
        return list;
    }

    /// <summary>بازمحاسبه زنجیره هش برای اثبات عدم دستکاری در سوابق.</summary>
    public (bool Valid, int Checked, string FirstBrokenId) VerifyAuditChain(int limit = 5000)
    {
        var rows = new List<(string Id, string User, string Badge, string Action, string Entity,
            string EntityId, string Details, string Machine, string Stamp, string PrevHash, string Hash)>();

        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT LogID, UserName, Badge, Action, Entity, EntityID, Details, MachineName,
 LoggedAt, PrevHash, IntegrityHash FROM AuditLog ORDER BY rowid ASC LIMIT @l";
        P(cmd, "@l", limit);
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                rows.Add((Str(r, 0), Str(r, 1), Str(r, 2), Str(r, 3), Str(r, 4), Str(r, 5),
                    Str(r, 6), Str(r, 7), Str(r, 8), Str(r, 9), Str(r, 10)));
            }
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var payload = $"{row.PrevHash}|{row.Stamp}|{row.User}|{row.Badge}|{row.Action}|" +
                          $"{row.Entity}|{row.EntityId}|{row.Details}|{row.Machine}";
            if (!string.Equals(MinerFingerprintService.Sha256Hex(payload), row.Hash,
                    StringComparison.OrdinalIgnoreCase))
                return (false, i + 1, row.Id);
        }
        return (true, rows.Count, "");
    }

    // ---------- داشبورد ----------

    public DashboardStats GetDashboardStats()
    {
        using var conn = Open();
        var s = new DashboardStats
        {
            ActiveOperations = ScalarInt(conn, "SELECT COUNT(*) FROM GovernmentOperations WHERE Status = 'InProgress'"),
            TotalDetections = ScalarInt(conn, "SELECT COUNT(*) FROM DetectedOperations"),
            IdentifiedDetections = ScalarInt(conn,
                "SELECT COUNT(*) FROM DetectedOperations WHERE SoftwareName IS NOT NULL AND SoftwareName <> ''"),
            UnidentifiedOpenPorts = ScalarInt(conn,
                "SELECT COUNT(*) FROM DetectedOperations WHERE (SoftwareName IS NULL OR SoftwareName = '') AND (DeviceModel IS NULL OR DeviceModel = '')"),
            PendingActions = ScalarInt(conn, "SELECT COUNT(*) FROM DetectedOperations WHERE ActionStatus IN ('منتظر دستور','منتظر‌دستورالعمل')"),
            SeizedDevices = ScalarInt(conn,
                "SELECT COUNT(*) FROM DetectedOperations WHERE ActionStatus IN ('ضبط‌شده','Seized') OR ActionType = 'ضبط‌شده'"),
            MeasuredPowerKw = ScalarDouble(conn, "SELECT COALESCE(SUM(PowerWatts),0) FROM DetectedOperations") / 1000.0,
            MeasuredPowerDevices = ScalarInt(conn, "SELECT COUNT(*) FROM DetectedOperations WHERE PowerWatts IS NOT NULL"),
            PersonnelCount = ScalarInt(conn, "SELECT COUNT(*) FROM AuthorizedPersonnel WHERE IsActive = 1"),
            TotalScans = ScalarInt(conn, "SELECT COUNT(*) FROM ScanHistory")
        };
        return s;
    }

    private static int ScalarInt(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    private static double ScalarDouble(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToDouble(cmd.ExecuteScalar() ?? 0);
    }

    // ---------- عملیات ----------

    public List<GovernmentOperation> GetOperations()
    {
        var list = new List<GovernmentOperation>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT o.OperationCode, o.Region, o.AuthorizedBy, o.StartTime, o.EndTime, o.Status, o.Notes,
  (SELECT COUNT(*) FROM DetectedOperations d WHERE d.OperationCode = o.OperationCode),
  (SELECT COALESCE(SUM(d.PowerWatts),0) FROM DetectedOperations d WHERE d.OperationCode = o.OperationCode)
FROM GovernmentOperations o ORDER BY o.StartTime DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new GovernmentOperation
            {
                OperationCode = Str(r, 0),
                Region = Str(r, 1),
                AuthorizedBy = Str(r, 2),
                StartTime = ParseDate(r[3]),
                EndTime = ParseDateOrNull(r[4]),
                Status = Str(r, 5),
                Notes = Str(r, 6),
                DetectionCount = Int(r, 7),
                TotalConsumption = Dbl(r, 8)
            });
        }
        return list;
    }

    public bool OperationExists(string code)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM GovernmentOperations WHERE OperationCode = @c";
        P(cmd, "@c", code);
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0) > 0;
    }

    public void CreateOperation(GovernmentOperation op)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO GovernmentOperations
  (OperationCode, Region, AuthorizedBy, PermitNumber, StartTime, Status, Notes)
VALUES (@c,@r,@a,@pm,@s,@st,@n)";
        P(cmd, "@c", op.OperationCode);
        P(cmd, "@r", op.Region);
        P(cmd, "@a", op.AuthorizedBy);
        P(cmd, "@pm", op.PermitNumber);
        P(cmd, "@s", NowStamp());
        P(cmd, "@st", op.Status);
        P(cmd, "@n", op.Notes);
        cmd.ExecuteNonQuery();
        WriteAudit(conn, AppSession.CurrentUser?.FullName ?? "سیستم",
            AppSession.CurrentUser?.Badge ?? "-", "OperationCreated", "GovernmentOperations",
            op.OperationCode, $"منطقه: {op.Region} | شماره حکم: {op.PermitNumber}");
    }

    public void UpdateOperationStatus(string code, string status, DateTime? end = null)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE GovernmentOperations SET Status = @s, EndTime = @e WHERE OperationCode = @c";
        P(cmd, "@s", status);
        P(cmd, "@e", end.HasValue ? NowStamp() : (object?)DBNull.Value);
        P(cmd, "@c", code);
        cmd.ExecuteNonQuery();
        WriteAudit(conn, AppSession.CurrentUser?.FullName ?? "سیستم",
            AppSession.CurrentUser?.Badge ?? "-", "OperationStatusChanged", "GovernmentOperations",
            code, $"وضعیت جدید: {status}");
    }

    // ---------- شناسایی‌ها ----------

    public List<DetectedDevice> GetDetections(string? operationCode = null, string? statusFilter = null)
    {
        var list = new List<DetectedDevice>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        var sql = new StringBuilder("SELECT * FROM DetectedOperations WHERE 1=1");
        if (!string.IsNullOrWhiteSpace(operationCode))
        {
            sql.Append(" AND OperationCode = @op");
            P(cmd, "@op", operationCode);
        }
        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "همه")
        {
            sql.Append(" AND ActionStatus = @st");
            P(cmd, "@st", statusFilter);
        }
        sql.Append(" ORDER BY DetectionTime DESC");
        cmd.CommandText = sql.ToString();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadDetection(r));
        return list;
    }

    public DetectedDevice? GetDetection(string operationId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM DetectedOperations WHERE OperationID = @id";
        P(cmd, "@id", operationId);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadDetection(r) : null;
    }

    public void SaveDetection(DetectedDevice d)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT OR REPLACE INTO DetectedOperations (
 OperationID, OperationCode, ScanID, IPAddress, Port, PortState, Protocol, DetectionMethod, LatencyMs,
 HttpStatus, EvidenceHash, EvidenceSummary, BannerText, SoftwareName, DeviceModel, FirmwareVersion,
 HashRate, TemperatureC, FanPercent, PowerWatts, PowerSource, UptimeSeconds, PoolAddress, WorkerName,
 ReverseDns, MacAddress, AsNumber, AsName, NetworkName, NetworkCountry, NetworkRegistrant, PrefixCidr,
 IntelSource, IntelQueriedAt, OperatorRecordID, Province, City, Street, PostalCode, SubscriberName,
 PhoneNumber, ISP, OperatorName, DetectionTime, LastSeen, ActionStatus, Confidence)
VALUES (@id,@op,@scan,@ip,@port,@pstate,@proto,@method,@lat,@http,@ehash,@esum,@banner,@sw,@model,@fw,
 @hash,@temp,@fan,@pow,@psrc,@uptime,@pool,@worker,@rdns,@mac,@asn,@asname,@netname,@country,@registrant,
 @prefix,@isrc,@iquery,@oprec,@prov,@city,@street,@postal,@sub,@phone,@isp,@opname,@dt,@ls,@astatus,@conf)";
        P(cmd, "@id", d.OperationID);
        P(cmd, "@op", d.OperationCode);
        P(cmd, "@scan", d.ScanID);
        P(cmd, "@ip", d.IPAddress);
        P(cmd, "@port", d.Port);
        P(cmd, "@pstate", d.PortState);
        P(cmd, "@proto", d.Protocol);
        P(cmd, "@method", d.DetectionMethod);
        P(cmd, "@lat", d.LatencyMs);
        P(cmd, "@http", d.HttpStatus);
        P(cmd, "@ehash", d.EvidenceHash);
        P(cmd, "@esum", d.EvidenceSummary);
        P(cmd, "@banner", d.BannerText);
        P(cmd, "@sw", d.SoftwareName);
        P(cmd, "@model", d.DeviceModel);
        P(cmd, "@fw", d.FirmwareVersion);
        P(cmd, "@hash", d.HashRate);
        P(cmd, "@temp", d.TemperatureC);
        P(cmd, "@fan", d.FanPercent);
        P(cmd, "@pow", d.PowerWatts);
        P(cmd, "@psrc", d.PowerSource);
        P(cmd, "@uptime", d.UptimeSeconds);
        P(cmd, "@pool", d.PoolAddress);
        P(cmd, "@worker", d.WorkerName);
        P(cmd, "@rdns", d.ReverseDns);
        P(cmd, "@mac", d.MacAddress);
        P(cmd, "@asn", d.AsNumber);
        P(cmd, "@asname", d.AsName);
        P(cmd, "@netname", d.NetworkName);
        P(cmd, "@country", d.NetworkCountry);
        P(cmd, "@registrant", d.NetworkRegistrant);
        P(cmd, "@prefix", d.PrefixCidr);
        P(cmd, "@isrc", d.IntelSource);
        P(cmd, "@iquery", d.IntelQueriedAt);
        P(cmd, "@oprec", d.OperatorRecordID);
        P(cmd, "@prov", d.Province);
        P(cmd, "@city", d.City);
        P(cmd, "@street", d.Street);
        P(cmd, "@postal", d.PostalCode);
        P(cmd, "@sub", d.SubscriberName);
        P(cmd, "@phone", d.PhoneNumber);
        P(cmd, "@isp", d.ISP);
        P(cmd, "@opname", d.OperatorName);
        P(cmd, "@dt", d.DetectionTime == default ? NowStamp() : d.DetectionTime.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
        P(cmd, "@ls", NowStamp());
        P(cmd, "@astatus", d.ActionStatus);
        P(cmd, "@conf", d.Confidence);
        cmd.ExecuteNonQuery();
    }

    public void RecordEnforcement(string operationId, string actionType, string enforcedBy, string notes)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"UPDATE DetectedOperations SET ActionStatus = @st, ActionType = @at,
 ActionTimestamp = @ts, ActionEnforcedBy = @by, ActionNotes = @n WHERE OperationID = @id";
            P(cmd, "@st", actionType);
            P(cmd, "@at", actionType);
            P(cmd, "@ts", NowStamp());
            P(cmd, "@by", enforcedBy);
            P(cmd, "@n", notes);
            P(cmd, "@id", operationId);
            cmd.ExecuteNonQuery();
        }
        using (var log = conn.CreateCommand())
        {
            log.Transaction = tx;
            log.CommandText = @"INSERT INTO ActionLogs (LogID, OperationID, ActionType, ActionTime, EnforcedBy, Outcome, Notes)
VALUES (@lid,@oid,@at,@t,@by,@out,@n)";
            P(log, "@lid", Guid.NewGuid().ToString("N"));
            P(log, "@oid", operationId);
            P(log, "@at", actionType);
            P(log, "@t", NowStamp());
            P(log, "@by", enforcedBy);
            P(log, "@out", "ثبت‌شد");
            P(log, "@n", notes);
            log.ExecuteNonQuery();
        }
        tx.Commit();

        WriteAudit(conn, enforcedBy, AppSession.CurrentUser?.Badge ?? "-", "EnforcementAction",
            "DetectedOperations", operationId, $"اقدام: {actionType} | یادداشت: {notes}");
    }

    // ---------- تاریخچه اسکن ----------

    public void SaveScanHistory(ScanHistoryEntry entry)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO ScanHistory
 (ScanID, OperationCode, ScanType, OperatorName, MachineName, ScanStartTime, ScanEndTime,
  IPsScanned, PortsProbed, DevicesFound, IdentifiedDevices, ClosedPorts, FilteredPorts, ElapsedSeconds, Notes)
VALUES (@id,@op,@t,@o,@m,@s,@e,@ips,@pp,@df,@idn,@cl,@fl,@el,@n)";
        P(cmd, "@id", entry.ScanID);
        P(cmd, "@op", entry.OperationCode);
        P(cmd, "@t", entry.ScanType);
        P(cmd, "@o", entry.OperatorName);
        P(cmd, "@m", entry.MachineName);
        P(cmd, "@s", entry.StartTime.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
        P(cmd, "@e", entry.EndTime?.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
        P(cmd, "@ips", entry.IPsScanned);
        P(cmd, "@pp", entry.PortsProbed);
        P(cmd, "@df", entry.DevicesFound);
        P(cmd, "@idn", entry.IdentifiedDevices);
        P(cmd, "@cl", entry.ClosedPorts);
        P(cmd, "@fl", entry.FilteredPorts);
        P(cmd, "@el", entry.ElapsedSeconds);
        P(cmd, "@n", entry.Notes);
        cmd.ExecuteNonQuery();
    }

    public List<ScanHistoryEntry> GetScanHistory(string? operationCode = null, int limit = 200)
    {
        var list = new List<ScanHistoryEntry>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        var sql = @"SELECT ScanID, OperationCode, ScanType, OperatorName, MachineName, ScanStartTime,
 ScanEndTime, IPsScanned, PortsProbed, DevicesFound, IdentifiedDevices, ClosedPorts, FilteredPorts,
 ElapsedSeconds, Notes FROM ScanHistory WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(operationCode))
        {
            sql += " AND OperationCode = @op";
            P(cmd, "@op", operationCode);
        }
        sql += " ORDER BY ScanStartTime DESC LIMIT @l";
        P(cmd, "@l", limit);
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new ScanHistoryEntry
            {
                ScanID = Str(r, 0),
                OperationCode = Str(r, 1),
                ScanType = Str(r, 2),
                OperatorName = Str(r, 3),
                MachineName = Str(r, 4),
                StartTime = ParseDate(r[5]),
                EndTime = ParseDateOrNull(r[6]),
                IPsScanned = Int(r, 7),
                PortsProbed = Int(r, 8),
                DevicesFound = Int(r, 9),
                IdentifiedDevices = Int(r, 10),
                ClosedPorts = Int(r, 11),
                FilteredPorts = Int(r, 12),
                ElapsedSeconds = Dbl(r, 13),
                Notes = Str(r, 14)
            });
        }
        return list;
    }

    // ---------- مالکیت شبکه ----------

    public NetworkOwnership? GetCachedOwnership(string ip)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT IPAddress, PrefixCidr, AsNumber, AsName, NetworkName, Registrant, Country,
 ReverseDns, Source, SourceUrl, RawResponse, PayloadHash, QueryStatus, QueriedAt
 FROM NetworkOwnership WHERE IPAddress = @ip";
        P(cmd, "@ip", ip);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new NetworkOwnership
        {
            IPAddress = Str(r, 0),
            PrefixCidr = Str(r, 1),
            AsNumber = Str(r, 2),
            AsName = Str(r, 3),
            NetworkName = Str(r, 4),
            Registrant = Str(r, 5),
            Country = Str(r, 6),
            ReverseDns = Str(r, 7),
            Source = Str(r, 8),
            SourceUrl = Str(r, 9),
            RawResponse = Str(r, 10),
            PayloadHash = Str(r, 11),
            QueryStatus = Str(r, 12),
            QueriedAt = ParseDate(r[13])
        };
    }

    public void SaveOwnership(NetworkOwnership o)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT OR REPLACE INTO NetworkOwnership
 (IPAddress, PrefixCidr, AsNumber, AsName, NetworkName, Registrant, Country, ReverseDns, Source, SourceUrl,
  RawResponse, PayloadHash, QueryStatus, QueriedAt)
VALUES (@ip,@p,@asn,@asname,@net,@reg,@country,@rdns,@src,@url,@raw,@hash,@st,@at)";
        P(cmd, "@ip", o.IPAddress);
        P(cmd, "@p", o.PrefixCidr);
        P(cmd, "@asn", o.AsNumber);
        P(cmd, "@asname", o.AsName);
        P(cmd, "@net", o.NetworkName);
        P(cmd, "@reg", o.Registrant);
        P(cmd, "@country", o.Country);
        P(cmd, "@rdns", o.ReverseDns);
        P(cmd, "@src", o.Source);
        P(cmd, "@url", o.SourceUrl);
        P(cmd, "@raw", o.RawResponse);
        P(cmd, "@hash", o.PayloadHash);
        P(cmd, "@st", o.QueryStatus);
        P(cmd, "@at", o.QueriedAt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
    }

    // ---------- اطلاعات مشترک (استعلام رسمی) ----------

    public List<OperatorRecord> GetOperatorRecords(string? operationId = null)
    {
        var list = new List<OperatorRecord>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        var sql = "SELECT * FROM OperatorRecords WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(operationId))
        {
            sql += " AND OperationID = @id";
            P(cmd, "@id", operationId);
        }
        sql += " ORDER BY RequestedAt DESC";
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new OperatorRecord
            {
                RecordID = Str(r, "RecordID"),
                OperationID = Str(r, "OperationID"),
                IPAddress = Str(r, "IPAddress"),
                SubscriberName = Str(r, "SubscriberName"),
                PhoneNumber = Str(r, "PhoneNumber"),
                Province = Str(r, "Province"),
                City = Str(r, "City"),
                Street = Str(r, "Street"),
                PostalCode = Str(r, "PostalCode"),
                RequestReference = Str(r, "RequestReference"),
                SourceAuthority = Str(r, "SourceAuthority"),
                RequestedBy = Str(r, "RequestedBy"),
                RequestedAt = Str(r, "RequestedAt"),
                ReceivedAt = Str(r, "ReceivedAt"),
                PayloadHash = Str(r, "PayloadHash"),
                Notes = Str(r, "Notes")
            });
        }
        return list;
    }

    public void SaveOperatorRecord(OperatorRecord rec, bool applyToDetection)
    {
        using var conn = Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"INSERT OR REPLACE INTO OperatorRecords
 (RecordID, OperationID, IPAddress, SubscriberName, PhoneNumber, Province, City, Street, PostalCode,
  RequestReference, SourceAuthority, RequestedBy, RequestedAt, ReceivedAt, PayloadHash, Notes)
VALUES (@id,@oid,@ip,@sub,@ph,@prov,@city,@street,@postal,@ref,@auth,@req,@rat,@rec,@hash,@notes)";
            P(cmd, "@id", rec.RecordID);
            P(cmd, "@oid", rec.OperationID);
            P(cmd, "@ip", rec.IPAddress);
            P(cmd, "@sub", rec.SubscriberName);
            P(cmd, "@ph", rec.PhoneNumber);
            P(cmd, "@prov", rec.Province);
            P(cmd, "@city", rec.City);
            P(cmd, "@street", rec.Street);
            P(cmd, "@postal", rec.PostalCode);
            P(cmd, "@ref", rec.RequestReference);
            P(cmd, "@auth", rec.SourceAuthority);
            P(cmd, "@req", rec.RequestedBy);
            P(cmd, "@rat", rec.RequestedAt);
            P(cmd, "@rec", rec.ReceivedAt);
            P(cmd, "@hash", rec.PayloadHash);
            P(cmd, "@notes", rec.Notes);
            cmd.ExecuteNonQuery();
        }

        if (applyToDetection)
        {
            using var upd = conn.CreateCommand();
            upd.CommandText = @"UPDATE DetectedOperations SET OperatorRecordID = @rid, SubscriberName = @sub,
 PhoneNumber = @ph, Province = @prov, City = @city, Street = @st, PostalCode = @postal
 WHERE OperationID = @oid";
            P(upd, "@rid", rec.RecordID);
            P(upd, "@sub", rec.SubscriberName);
            P(upd, "@ph", rec.PhoneNumber);
            P(upd, "@prov", rec.Province);
            P(upd, "@city", rec.City);
            P(upd, "@st", rec.Street);
            P(upd, "@postal", rec.PostalCode);
            P(upd, "@oid", rec.OperationID);
            upd.ExecuteNonQuery();
        }

        WriteAudit(conn, AppSession.CurrentUser?.FullName ?? "سیستم",
            AppSession.CurrentUser?.Badge ?? "-", "OperatorRecordSaved", "OperatorRecords",
            rec.RecordID, $"IP: {rec.IPAddress} | مرجع: {rec.SourceAuthority} | شماره نامه: {rec.RequestReference}");
    }

    // ---------- پرسنل ----------

    public List<Personnel> GetPersonnel()
    {
        var list = new List<Personnel>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT PersonnelID, FullName, Department, Role, Badge, PhoneNumber, Email,
 AuthorizationLevel, IsActive, LastLogin, MustChangePassword FROM AuthorizedPersonnel ORDER BY FullName";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadPersonnel(r));
        return list;
    }

    public void UpsertPersonnel(Personnel p, string? newPassword = null)
    {
        using var conn = Open();
        if (string.IsNullOrEmpty(p.PersonnelID)) p.PersonnelID = Guid.NewGuid().ToString("N");

        using var exists = conn.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM AuthorizedPersonnel WHERE PersonnelID = @id";
        P(exists, "@id", p.PersonnelID);
        var isNew = Convert.ToInt64(exists.ExecuteScalar() ?? 0) == 0;

        using var cmd = conn.CreateCommand();
        if (isNew)
        {
            if (string.IsNullOrWhiteSpace(newPassword))
                throw new ArgumentException("برای کاربر جدید رمز عبور الزامی است.");
            cmd.CommandText = @"INSERT INTO AuthorizedPersonnel
 (PersonnelID, FullName, Department, Role, Badge, PhoneNumber, Email, PasswordHash,
  AuthorizationLevel, MustChangePassword, IsActive)
VALUES (@id,@n,@d,@r,@b,@p,@e,@pass,@lvl,1,@act)";
            P(cmd, "@pass", HashPassword(newPassword));
        }
        else if (!string.IsNullOrWhiteSpace(newPassword))
        {
            cmd.CommandText = @"UPDATE AuthorizedPersonnel SET FullName=@n, Department=@d, Role=@r, Badge=@b,
 PhoneNumber=@p, Email=@e, AuthorizationLevel=@lvl, IsActive=@act, PasswordHash=@pass, MustChangePassword=0
 WHERE PersonnelID=@id";
            P(cmd, "@pass", HashPassword(newPassword));
        }
        else
        {
            cmd.CommandText = @"UPDATE AuthorizedPersonnel SET FullName=@n, Department=@d, Role=@r, Badge=@b,
 PhoneNumber=@p, Email=@e, AuthorizationLevel=@lvl, IsActive=@act WHERE PersonnelID=@id";
        }

        P(cmd, "@id", p.PersonnelID);
        P(cmd, "@n", p.FullName);
        P(cmd, "@d", p.Department);
        P(cmd, "@r", p.Role);
        P(cmd, "@b", p.Badge);
        P(cmd, "@p", p.PhoneNumber);
        P(cmd, "@e", p.Email);
        P(cmd, "@lvl", p.AuthorizationLevel);
        P(cmd, "@act", p.IsActive ? 1 : 0);
        cmd.ExecuteNonQuery();

        WriteAudit(conn, AppSession.CurrentUser?.FullName ?? "سیستم",
            AppSession.CurrentUser?.Badge ?? "-", isNew ? "PersonnelCreated" : "PersonnelUpdated",
            "AuthorizedPersonnel", p.PersonnelID, $"Badge: {p.Badge} | سطح: {p.AuthorizationLevel}");
    }

    public List<(string Province, int Count, double Power)> GetRegionalStats()
    {
        var list = new List<(string, int, double)>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COALESCE(NULLIF(Province,''),'نامشخص') AS Prov, COUNT(*),
 COALESCE(SUM(PowerWatts),0) FROM DetectedOperations GROUP BY Prov ORDER BY COUNT(*) DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add((r.GetString(0), r.GetInt32(1), r.GetDouble(2)));
        return list;
    }

    // ---------- گزارش‌ها و تنظیمات ----------

    public void SaveReportMeta(
        string reportId, string? opCode, string type, string by, string path,
        int detections, double power, int measuredDevices, string contentHash)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO GeneratedReports
 (ReportID, OperationCode, ReportType, GeneratedBy, FilePath, TotalDetections, TotalPowerConsumption,
  MeasuredPowerDevices, ContentHash, Status)
VALUES (@id,@op,@t,@by,@p,@d,@pow,@m,@h,'Generated')";
        P(cmd, "@id", reportId);
        P(cmd, "@op", opCode);
        P(cmd, "@t", type);
        P(cmd, "@by", by);
        P(cmd, "@p", path);
        P(cmd, "@d", detections);
        P(cmd, "@pow", power);
        P(cmd, "@m", measuredDevices);
        P(cmd, "@h", contentHash);
        cmd.ExecuteNonQuery();

        WriteAudit(conn, by, AppSession.CurrentUser?.Badge ?? "-", "ReportGenerated", "GeneratedReports",
            reportId, $"نوع: {type} | فایل: {path}");
    }

    public string GetSetting(string key, string fallback = "")
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = @k";
        P(cmd, "@k", key);
        var value = Convert.ToString(cmd.ExecuteScalar());
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    public void SetSetting(string key, string value)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO AppSettings (Key, Value) VALUES (@k, @v)";
        P(cmd, "@k", key);
        P(cmd, "@v", value);
        cmd.ExecuteNonQuery();
    }

    // ---------- کمکی خواندن داده ----------

    private static string Str(SqliteDataReader r, int ordinal) =>
        r.IsDBNull(ordinal) ? "" : Convert.ToString(r.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";

    private static string Str(SqliteDataReader r, string column)
    {
        var ordinal = r.GetOrdinal(column);
        return r.IsDBNull(ordinal) ? "" : Convert.ToString(r.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";
    }

    private static int Int(SqliteDataReader r, int ordinal, int fallback = 0)
    {
        if (r.IsDBNull(ordinal)) return fallback;
        return Convert.ToInt32(r.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static double Dbl(SqliteDataReader r, int ordinal, double fallback = 0)
    {
        if (r.IsDBNull(ordinal)) return fallback;
        return Convert.ToDouble(r.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static double? DblOrNull(SqliteDataReader r, string column)
    {
        var ordinal = r.GetOrdinal(column);
        if (r.IsDBNull(ordinal)) return null;
        return Convert.ToDouble(r.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static long? LongOrNull(SqliteDataReader r, string column)
    {
        var ordinal = r.GetOrdinal(column);
        if (r.IsDBNull(ordinal)) return null;
        return Convert.ToInt64(r.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static int? IntOrNull(SqliteDataReader r, string column)
    {
        var ordinal = r.GetOrdinal(column);
        if (r.IsDBNull(ordinal)) return null;
        return Convert.ToInt32(r.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static Personnel ReadPersonnel(SqliteDataReader r) => new()
    {
        PersonnelID = Str(r, 0),
        FullName = Str(r, 1),
        Department = Str(r, 2),
        Role = Str(r, 3),
        Badge = Str(r, 4),
        PhoneNumber = Str(r, 5),
        Email = Str(r, 6),
        AuthorizationLevel = Int(r, 7, 1),
        IsActive = Int(r, 8, 0) == 1,
        LastLogin = ParseDateOrNull(r[9]),
        MustChangePassword = Str(r, "MustChangePassword") == "1"
    };

    private static DetectedDevice ReadDetection(SqliteDataReader r)
    {
        bool Has(string col) => r.GetOrdinal(col) >= 0;
        string S(string col) => Has(col) ? Str(r, col) : "";

        return new DetectedDevice
        {
            OperationID = S("OperationID"),
            OperationCode = S("OperationCode"),
            ScanID = S("ScanID"),
            IPAddress = S("IPAddress"),
            Port = Int(r, r.GetOrdinal("Port")),
            PortState = S("PortState"),
            Protocol = S("Protocol"),
            DetectionMethod = S("DetectionMethod"),
            LatencyMs = IntOrNull(r, "LatencyMs"),
            HttpStatus = S("HttpStatus"),
            EvidenceHash = S("EvidenceHash"),
            EvidenceSummary = S("EvidenceSummary"),
            BannerText = S("BannerText"),
            SoftwareName = S("SoftwareName"),
            DeviceModel = S("DeviceModel"),
            FirmwareVersion = S("FirmwareVersion"),
            HashRate = S("HashRate"),
            TemperatureC = DblOrNull(r, "TemperatureC"),
            FanPercent = DblOrNull(r, "FanPercent"),
            PowerWatts = DblOrNull(r, "PowerWatts"),
            PowerSource = S("PowerSource"),
            UptimeSeconds = LongOrNull(r, "UptimeSeconds"),
            PoolAddress = S("PoolAddress"),
            WorkerName = S("WorkerName"),
            ReverseDns = S("ReverseDns"),
            MacAddress = S("MacAddress"),
            AsNumber = S("AsNumber"),
            AsName = S("AsName"),
            NetworkName = S("NetworkName"),
            NetworkCountry = S("NetworkCountry"),
            NetworkRegistrant = S("NetworkRegistrant"),
            PrefixCidr = S("PrefixCidr"),
            IntelSource = S("IntelSource"),
            IntelQueriedAt = S("IntelQueriedAt"),
            OperatorRecordID = S("OperatorRecordID"),
            LocationLatitude = DblOrNull(r, "LocationLatitude"),
            LocationLongitude = DblOrNull(r, "LocationLongitude"),
            Province = S("Province"),
            City = S("City"),
            Street = S("Street"),
            PostalCode = S("PostalCode"),
            SubscriberName = S("SubscriberName"),
            PhoneNumber = S("PhoneNumber"),
            ISP = S("ISP"),
            OperatorName = S("OperatorName"),
            DetectionTime = ParseDate(r["DetectionTime"]),
            LastSeen = ParseDateOrNull(r["LastSeen"]),
            ActionStatus = S("ActionStatus"),
            ActionType = S("ActionType"),
            ActionEnforcedBy = S("ActionEnforcedBy"),
            ActionNotes = S("ActionNotes"),
            Confidence = Has("Confidence") ? Dbl(r, r.GetOrdinal("Confidence")) : 0
        };
    }
}

public sealed class FirstRunInfo
{
    public bool Created { get; init; }
    public string Badge { get; init; } = "";
    public string Password { get; init; } = "";
}
