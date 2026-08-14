using System.Text;
using GovernmentMiningApp.Models;
using Microsoft.Data.Sqlite;

namespace GovernmentMiningApp.Services;

public sealed class DatabaseService
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public DatabaseService()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(baseDir, "Data");
        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "government_ops.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    public string DatabasePath => _dbPath;

    public void Initialize()
    {
        using var conn = Open();
        var schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "schema.sql");
        string sql;
        if (File.Exists(schemaPath))
            sql = File.ReadAllText(schemaPath, Encoding.UTF8);
        else
            sql = EmbeddedSchema();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();

        EnsureDefaultAdmin(conn);
        EnsureDemoSeedIfEmpty(conn);
    }

    private static string EmbeddedSchema() => @"
CREATE TABLE IF NOT EXISTS GovernmentOperations (
    OperationCode TEXT PRIMARY KEY, Region TEXT NOT NULL, AuthorizedBy TEXT NOT NULL,
    StartTime TEXT NOT NULL, EndTime TEXT, Status TEXT DEFAULT 'InProgress', Notes TEXT,
    CreatedAt TEXT DEFAULT (datetime('now','localtime')));
CREATE TABLE IF NOT EXISTS DetectedOperations (
    OperationID TEXT PRIMARY KEY, OperationCode TEXT NOT NULL, IPAddress TEXT NOT NULL, Port INTEGER NOT NULL,
    LocationLatitude REAL, LocationLongitude REAL, LocationAccuracy REAL, Province TEXT, City TEXT, Street TEXT,
    PostalCode TEXT, SubscriberName TEXT, PhoneNumber TEXT, ISP TEXT, OperatorName TEXT, DeviceModel TEXT,
    HashRate TEXT, EstimatedConsumption REAL, DetectionTime TEXT NOT NULL,
    ActionStatus TEXT DEFAULT 'منتظر‌دستورالعمل', ActionType TEXT, ActionTimestamp TEXT,
    ActionEnforcedBy TEXT, ActionNotes TEXT, Confidence REAL);
CREATE TABLE IF NOT EXISTS ActionLogs (
    LogID TEXT PRIMARY KEY, OperationID TEXT NOT NULL, ActionType TEXT,
    ActionTime TEXT DEFAULT (datetime('now','localtime')), EnforcedBy TEXT, Outcome TEXT, Notes TEXT);
CREATE TABLE IF NOT EXISTS AuthorizedPersonnel (
    PersonnelID TEXT PRIMARY KEY, FullName TEXT NOT NULL, Department TEXT, Role TEXT, Badge TEXT UNIQUE,
    PhoneNumber TEXT, Email TEXT, PasswordHash TEXT, AuthorizationLevel INTEGER DEFAULT 1,
    CreatedAt TEXT DEFAULT (datetime('now','localtime')), LastLogin TEXT, IsActive INTEGER DEFAULT 1);
CREATE TABLE IF NOT EXISTS GeneratedReports (
    ReportID TEXT PRIMARY KEY, OperationCode TEXT, ReportType TEXT, GeneratedBy TEXT,
    GenerationTime TEXT DEFAULT (datetime('now','localtime')), FilePath TEXT,
    TotalDetections INTEGER, TotalPowerConsumption REAL, Status TEXT);
CREATE TABLE IF NOT EXISTS ScanHistory (
    ScanID TEXT PRIMARY KEY, OperationCode TEXT, ScanType TEXT, ScanStartTime TEXT, ScanEndTime TEXT,
    IPsScanned INTEGER, DevicesFound INTEGER, Notes TEXT);
CREATE TABLE IF NOT EXISTS AppSettings (Key TEXT PRIMARY KEY, Value TEXT);
";

    private static void EnsureDefaultAdmin(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM AuthorizedPersonnel";
        var count = Convert.ToInt64(check.ExecuteScalar());
        if (count > 0) return;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO AuthorizedPersonnel (PersonnelID, FullName, Department, Role, Badge, PhoneNumber, Email, PasswordHash, AuthorizationLevel, IsActive)
VALUES (@id, @name, @dept, @role, @badge, @phone, @email, @pass, 5, 1)";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("@name", "مدیر سیستم");
        cmd.Parameters.AddWithValue("@dept", "مرکز عملیات");
        cmd.Parameters.AddWithValue("@role", "Admin");
        cmd.Parameters.AddWithValue("@badge", "ADMIN-001");
        cmd.Parameters.AddWithValue("@phone", "021-00000000");
        cmd.Parameters.AddWithValue("@email", "admin@gov.local");
        cmd.Parameters.AddWithValue("@pass", HashPassword("Admin@123"));
        cmd.ExecuteNonQuery();
    }

    private static void EnsureDemoSeedIfEmpty(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM GovernmentOperations";
        if (Convert.ToInt64(check.ExecuteScalar()) > 0) return;

        using var op = conn.CreateCommand();
        op.CommandText = @"
INSERT INTO GovernmentOperations (OperationCode, Region, AuthorizedBy, StartTime, Status, Notes)
VALUES ('OP-DEMO-001', 'تهران', 'مدیر سیستم', @start, 'InProgress', 'عملیات نمونه اولیه')";
        op.Parameters.AddWithValue("@start", DateTime.Now.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss"));
        op.ExecuteNonQuery();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public static string HashPassword(string password)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("GMA|" + password));
        return Convert.ToHexString(bytes);
    }

    public Personnel? Authenticate(string badge, string password)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT PersonnelID, FullName, Department, Role, Badge, PhoneNumber, Email,
            AuthorizationLevel, IsActive, LastLogin
            FROM AuthorizedPersonnel WHERE Badge = @badge AND PasswordHash = @pass AND IsActive = 1";
        cmd.Parameters.AddWithValue("@badge", badge.Trim());
        cmd.Parameters.AddWithValue("@pass", HashPassword(password));
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;

        var p = ReadPersonnel(r);
        r.Close();

        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE AuthorizedPersonnel SET LastLogin = @t WHERE PersonnelID = @id";
        upd.Parameters.AddWithValue("@t", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        upd.Parameters.AddWithValue("@id", p.PersonnelID);
        upd.ExecuteNonQuery();
        p.LastLogin = DateTime.Now;
        return p;
    }

    public DashboardStats GetDashboardStats()
    {
        using var conn = Open();
        var s = new DashboardStats();
        s.ActiveOperations = ScalarInt(conn, "SELECT COUNT(*) FROM GovernmentOperations WHERE Status = 'InProgress'");
        s.TotalDetections = ScalarInt(conn, "SELECT COUNT(*) FROM DetectedOperations");
        s.PendingActions = ScalarInt(conn, "SELECT COUNT(*) FROM DetectedOperations WHERE ActionStatus = 'منتظر‌دستورالعمل'");
        s.SeizedDevices = ScalarInt(conn, "SELECT COUNT(*) FROM DetectedOperations WHERE ActionStatus IN ('ضبط‌شده','Seized') OR ActionType = 'Seized'");
        s.TotalPowerKw = ScalarDouble(conn, "SELECT COALESCE(SUM(EstimatedConsumption),0) FROM DetectedOperations") / 1000.0;
        s.PersonnelCount = ScalarInt(conn, "SELECT COUNT(*) FROM AuthorizedPersonnel WHERE IsActive = 1");
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

    public List<GovernmentOperation> GetOperations()
    {
        var list = new List<GovernmentOperation>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT o.OperationCode, o.Region, o.AuthorizedBy, o.StartTime, o.EndTime, o.Status, o.Notes,
  (SELECT COUNT(*) FROM DetectedOperations d WHERE d.OperationCode = o.OperationCode) AS DetCount,
  (SELECT COALESCE(SUM(EstimatedConsumption),0) FROM DetectedOperations d WHERE d.OperationCode = o.OperationCode) AS TotPow
FROM GovernmentOperations o ORDER BY o.StartTime DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new GovernmentOperation
            {
                OperationCode = r.GetString(0),
                Region = r.GetString(1),
                AuthorizedBy = r.GetString(2),
                StartTime = DateTime.Parse(r.GetString(3)),
                EndTime = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
                Status = r.GetString(5),
                Notes = r.IsDBNull(6) ? "" : r.GetString(6),
                DetectionCount = r.GetInt32(7),
                TotalConsumption = r.GetDouble(8)
            });
        }
        return list;
    }

    public void CreateOperation(GovernmentOperation op)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO GovernmentOperations (OperationCode, Region, AuthorizedBy, StartTime, Status, Notes)
VALUES (@c, @r, @a, @s, @st, @n)";
        cmd.Parameters.AddWithValue("@c", op.OperationCode);
        cmd.Parameters.AddWithValue("@r", op.Region);
        cmd.Parameters.AddWithValue("@a", op.AuthorizedBy);
        cmd.Parameters.AddWithValue("@s", op.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@st", op.Status);
        cmd.Parameters.AddWithValue("@n", op.Notes ?? "");
        cmd.ExecuteNonQuery();
    }

    public void UpdateOperationStatus(string code, string status, DateTime? end = null)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE GovernmentOperations SET Status = @s, EndTime = @e WHERE OperationCode = @c";
        cmd.Parameters.AddWithValue("@s", status);
        cmd.Parameters.AddWithValue("@e", end?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@c", code);
        cmd.ExecuteNonQuery();
    }

    public List<DetectedDevice> GetDetections(string? operationCode = null, string? statusFilter = null)
    {
        var list = new List<DetectedDevice>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        var sql = new StringBuilder("SELECT * FROM DetectedOperations WHERE 1=1");
        if (!string.IsNullOrWhiteSpace(operationCode))
        {
            sql.Append(" AND OperationCode = @op");
            cmd.Parameters.AddWithValue("@op", operationCode);
        }
        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "همه")
        {
            sql.Append(" AND ActionStatus = @st");
            cmd.Parameters.AddWithValue("@st", statusFilter);
        }
        sql.Append(" ORDER BY DetectionTime DESC");
        cmd.CommandText = sql.ToString();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(ReadDetection(r));
        return list;
    }

    public void SaveDetection(DetectedDevice d)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT OR REPLACE INTO DetectedOperations (
 OperationID, OperationCode, IPAddress, Port, LocationLatitude, LocationLongitude,
 Province, City, Street, PostalCode, SubscriberName, PhoneNumber, ISP, OperatorName,
 DeviceModel, HashRate, EstimatedConsumption, DetectionTime, ActionStatus, Confidence)
VALUES (@id,@op,@ip,@port,@lat,@lon,@prov,@city,@st,@pc,@sub,@ph,@isp,@oper,@model,@hash,@pow,@dt,@as,@cf)";
        cmd.Parameters.AddWithValue("@id", d.OperationID);
        cmd.Parameters.AddWithValue("@op", d.OperationCode);
        cmd.Parameters.AddWithValue("@ip", d.IPAddress);
        cmd.Parameters.AddWithValue("@port", d.Port);
        cmd.Parameters.AddWithValue("@lat", (object?)d.LocationLatitude ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lon", (object?)d.LocationLongitude ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@prov", d.Province ?? "");
        cmd.Parameters.AddWithValue("@city", d.City ?? "");
        cmd.Parameters.AddWithValue("@st", d.Street ?? "");
        cmd.Parameters.AddWithValue("@pc", d.PostalCode ?? "");
        cmd.Parameters.AddWithValue("@sub", d.SubscriberName ?? "");
        cmd.Parameters.AddWithValue("@ph", d.PhoneNumber ?? "");
        cmd.Parameters.AddWithValue("@isp", d.ISP ?? "");
        cmd.Parameters.AddWithValue("@oper", d.OperatorName ?? "");
        cmd.Parameters.AddWithValue("@model", d.DeviceModel ?? "");
        cmd.Parameters.AddWithValue("@hash", d.HashRate ?? "");
        cmd.Parameters.AddWithValue("@pow", d.EstimatedConsumption);
        cmd.Parameters.AddWithValue("@dt", d.DetectionTime.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@as", d.ActionStatus);
        cmd.Parameters.AddWithValue("@cf", d.Confidence);
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
            cmd.Parameters.AddWithValue("@st", actionType);
            cmd.Parameters.AddWithValue("@at", actionType);
            cmd.Parameters.AddWithValue("@ts", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@by", enforcedBy);
            cmd.Parameters.AddWithValue("@n", notes ?? "");
            cmd.Parameters.AddWithValue("@id", operationId);
            cmd.ExecuteNonQuery();
        }
        using (var log = conn.CreateCommand())
        {
            log.Transaction = tx;
            log.CommandText = @"INSERT INTO ActionLogs (LogID, OperationID, ActionType, EnforcedBy, Outcome, Notes)
VALUES (@lid, @oid, @at, @by, @out, @n)";
            log.Parameters.AddWithValue("@lid", Guid.NewGuid().ToString("N"));
            log.Parameters.AddWithValue("@oid", operationId);
            log.Parameters.AddWithValue("@at", actionType);
            log.Parameters.AddWithValue("@by", enforcedBy);
            log.Parameters.AddWithValue("@out", "ثبت‌شد");
            log.Parameters.AddWithValue("@n", notes ?? "");
            log.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void SaveScanHistory(string opCode, string scanType, DateTime start, DateTime end, int scanned, int found, string notes)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO ScanHistory (ScanID, OperationCode, ScanType, ScanStartTime, ScanEndTime, IPsScanned, DevicesFound, Notes)
VALUES (@id,@op,@t,@s,@e,@sc,@f,@n)";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
        cmd.Parameters.AddWithValue("@op", opCode);
        cmd.Parameters.AddWithValue("@t", scanType);
        cmd.Parameters.AddWithValue("@s", start.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@e", end.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@sc", scanned);
        cmd.Parameters.AddWithValue("@f", found);
        cmd.Parameters.AddWithValue("@n", notes ?? "");
        cmd.ExecuteNonQuery();
    }

    public void SaveReportMeta(string reportId, string? opCode, string type, string by, string path, int dets, double power)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO GeneratedReports (ReportID, OperationCode, ReportType, GeneratedBy, FilePath, TotalDetections, TotalPowerConsumption, Status)
VALUES (@id,@op,@t,@by,@p,@d,@pow,'Generated')";
        cmd.Parameters.AddWithValue("@id", reportId);
        cmd.Parameters.AddWithValue("@op", (object?)opCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@t", type);
        cmd.Parameters.AddWithValue("@by", by);
        cmd.Parameters.AddWithValue("@p", path);
        cmd.Parameters.AddWithValue("@d", dets);
        cmd.Parameters.AddWithValue("@pow", power);
        cmd.ExecuteNonQuery();
    }

    public List<Personnel> GetPersonnel()
    {
        var list = new List<Personnel>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT PersonnelID, FullName, Department, Role, Badge, PhoneNumber, Email,
 AuthorizationLevel, IsActive, LastLogin FROM AuthorizedPersonnel ORDER BY FullName";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(ReadPersonnel(r));
        return list;
    }

    public void UpsertPersonnel(Personnel p, string? newPassword = null)
    {
        using var conn = Open();
        if (string.IsNullOrEmpty(p.PersonnelID))
            p.PersonnelID = Guid.NewGuid().ToString("N");

        using var exists = conn.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM AuthorizedPersonnel WHERE PersonnelID = @id";
        exists.Parameters.AddWithValue("@id", p.PersonnelID);
        var isNew = Convert.ToInt64(exists.ExecuteScalar()) == 0;

        using var cmd = conn.CreateCommand();
        if (isNew)
        {
            cmd.CommandText = @"INSERT INTO AuthorizedPersonnel
 (PersonnelID, FullName, Department, Role, Badge, PhoneNumber, Email, PasswordHash, AuthorizationLevel, IsActive)
 VALUES (@id,@n,@d,@r,@b,@p,@e,@pass,@lvl,@act)";
            cmd.Parameters.AddWithValue("@pass", HashPassword(string.IsNullOrWhiteSpace(newPassword) ? "ChangeMe@1" : newPassword));
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                cmd.CommandText = @"UPDATE AuthorizedPersonnel SET FullName=@n, Department=@d, Role=@r, Badge=@b,
 PhoneNumber=@p, Email=@e, AuthorizationLevel=@lvl, IsActive=@act, PasswordHash=@pass WHERE PersonnelID=@id";
                cmd.Parameters.AddWithValue("@pass", HashPassword(newPassword));
            }
            else
            {
                cmd.CommandText = @"UPDATE AuthorizedPersonnel SET FullName=@n, Department=@d, Role=@r, Badge=@b,
 PhoneNumber=@p, Email=@e, AuthorizationLevel=@lvl, IsActive=@act WHERE PersonnelID=@id";
            }
        }
        cmd.Parameters.AddWithValue("@id", p.PersonnelID);
        cmd.Parameters.AddWithValue("@n", p.FullName);
        cmd.Parameters.AddWithValue("@d", p.Department ?? "");
        cmd.Parameters.AddWithValue("@r", p.Role ?? "");
        cmd.Parameters.AddWithValue("@b", p.Badge);
        cmd.Parameters.AddWithValue("@p", p.PhoneNumber ?? "");
        cmd.Parameters.AddWithValue("@e", p.Email ?? "");
        cmd.Parameters.AddWithValue("@lvl", p.AuthorizationLevel);
        cmd.Parameters.AddWithValue("@act", p.IsActive ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public List<(string Province, int Count, double Power)> GetRegionalStats()
    {
        var list = new List<(string, int, double)>();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT COALESCE(NULLIF(Province,''),'نامشخص') AS Prov, COUNT(*), COALESCE(SUM(EstimatedConsumption),0)
FROM DetectedOperations GROUP BY Prov ORDER BY COUNT(*) DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add((r.GetString(0), r.GetInt32(1), r.GetDouble(2)));
        return list;
    }

    private static Personnel ReadPersonnel(SqliteDataReader r) => new()
    {
        PersonnelID = r.GetString(0),
        FullName = r.GetString(1),
        Department = r.IsDBNull(2) ? "" : r.GetString(2),
        Role = r.IsDBNull(3) ? "" : r.GetString(3),
        Badge = r.IsDBNull(4) ? "" : r.GetString(4),
        PhoneNumber = r.IsDBNull(5) ? "" : r.GetString(5),
        Email = r.IsDBNull(6) ? "" : r.GetString(6),
        AuthorizationLevel = r.IsDBNull(7) ? 1 : r.GetInt32(7),
        IsActive = !r.IsDBNull(8) && r.GetInt32(8) == 1,
        LastLogin = r.IsDBNull(9) ? null : DateTime.Parse(r.GetString(9))
    };

    private static DetectedDevice ReadDetection(SqliteDataReader r)
    {
        string S(string col) => r[col] is DBNull or null ? "" : Convert.ToString(r[col]) ?? "";
        double? D(string col) => r[col] is DBNull or null ? null : Convert.ToDouble(r[col]);
        return new DetectedDevice
        {
            OperationID = S("OperationID"),
            OperationCode = S("OperationCode"),
            IPAddress = S("IPAddress"),
            Port = Convert.ToInt32(r["Port"]),
            LocationLatitude = D("LocationLatitude"),
            LocationLongitude = D("LocationLongitude"),
            Province = S("Province"),
            City = S("City"),
            Street = S("Street"),
            PostalCode = S("PostalCode"),
            SubscriberName = S("SubscriberName"),
            PhoneNumber = S("PhoneNumber"),
            ISP = S("ISP"),
            OperatorName = S("OperatorName"),
            DeviceModel = S("DeviceModel"),
            HashRate = S("HashRate"),
            EstimatedConsumption = D("EstimatedConsumption") ?? 0,
            DetectionTime = DateTime.Parse(S("DetectionTime")),
            ActionStatus = S("ActionStatus"),
            ActionType = S("ActionType"),
            ActionEnforcedBy = S("ActionEnforcedBy"),
            ActionNotes = S("ActionNotes"),
            Confidence = D("Confidence") ?? 0
        };
    }
}
