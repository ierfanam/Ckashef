using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace GovernmentMiningApp.Services;

/// <summary>
/// Production startup guard. Development fixtures are removed and provenance
/// metadata is added to the operational schema. No synthetic operational data
/// is created here.
/// </summary>
public static class ProductionDataSanitizer
{
    public static void Apply(DatabaseService db)
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        Execute(conn, tx, "DELETE FROM DetectedOperations WHERE OperationCode LIKE 'OP-DEMO-%' OR IPAddress LIKE 'SIM-%'");
        Execute(conn, tx, "DELETE FROM ScanHistory WHERE OperationCode LIKE 'OP-DEMO-%'");
        Execute(conn, tx, "DELETE FROM GovernmentOperations WHERE OperationCode LIKE 'OP-DEMO-%'");
        Execute(conn, tx, "DELETE FROM AuthorizedPersonnel WHERE Badge='ADMIN-001' AND Email='admin@gov.local'");

        EnsureColumn(conn, tx, "DetectedOperations", "DataSource", "TEXT NOT NULL DEFAULT 'Unknown'");
        EnsureColumn(conn, tx, "DetectedOperations", "SourceRecordId", "TEXT");
        EnsureColumn(conn, tx, "DetectedOperations", "SourceQueriedAt", "TEXT");
        EnsureColumn(conn, tx, "DetectedOperations", "MatchStatus", "TEXT NOT NULL DEFAULT 'Unresolved'");
        EnsureColumn(conn, tx, "DetectedOperations", "EvidenceType", "TEXT NOT NULL DEFAULT 'NetworkObservation'");
        EnsureColumn(conn, tx, "DetectedOperations", "EvidenceId", "TEXT");
        EnsureColumn(conn, tx, "DetectedOperations", "IsSynthetic", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(conn, tx, "DetectedOperations", "CreatedBy", "TEXT");
        Execute(conn, tx, "CREATE INDEX IF NOT EXISTS idx_detected_source ON DetectedOperations(DataSource, SourceRecordId)");
        Execute(conn, tx, "CREATE INDEX IF NOT EXISTS idx_detected_match ON DetectedOperations(MatchStatus)");

        // First-run provisioning is explicit. There is no hard-coded password.
        var badge = Environment.GetEnvironmentVariable("CKASHEF_BOOTSTRAP_BADGE");
        var password = Environment.GetEnvironmentVariable("CKASHEF_BOOTSTRAP_PASSWORD");
        if (!string.IsNullOrWhiteSpace(badge) && !string.IsNullOrWhiteSpace(password))
        {
            if (password.Length < 14) throw new InvalidOperationException("CKASHEF_BOOTSTRAP_PASSWORD must contain at least 14 characters.");
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT OR IGNORE INTO AuthorizedPersonnel
(PersonnelID, FullName, Department, Role, Badge, PasswordHash, AuthorizationLevel, IsActive)
VALUES (@id,@name,@dept,@role,@badge,@hash,5,1)";
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid().ToString("N"));
            cmd.Parameters.AddWithValue("@name", "نیازمند تکمیل مشخصات پرسنل");
            cmd.Parameters.AddWithValue("@dept", "نیازمند پیکربندی سازمانی");
            cmd.Parameters.AddWithValue("@role", "Administrator");
            cmd.Parameters.AddWithValue("@badge", badge.Trim());
            cmd.Parameters.AddWithValue("@hash", HashPassword(password));
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private static void EnsureColumn(SqliteConnection conn, SqliteTransaction tx, string table, string column, string definition)
    {
        using var check = conn.CreateCommand();
        check.Transaction = tx;
        check.CommandText = $"PRAGMA table_info({table})";
        using var reader = check.ExecuteReader();
        while (reader.Read())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return;
        reader.Close();
        Execute(conn, tx, $"ALTER TABLE {table} ADD COLUMN {column} {definition}");
    }

    private static void Execute(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        using var cmd = conn.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql; cmd.ExecuteNonQuery();
    }

    private static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("GMA|" + password));
        return Convert.ToHexString(bytes);
    }
}
