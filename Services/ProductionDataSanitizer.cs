using Microsoft.Data.Sqlite;

namespace GovernmentMiningApp.Services;

/// <summary>
/// Prevents development/demo fixtures from becoming operational records.
/// Production deployments must provision real personnel and data through an authorized process.
/// </summary>
public static class ProductionDataSanitizer
{
    public static void Apply(DatabaseService db)
    {
        using var conn = db.Open();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "DELETE FROM DetectedOperations WHERE OperationCode LIKE 'OP-DEMO-%' OR IPAddress LIKE 'SIM-%'; DELETE FROM ScanHistory WHERE OperationCode LIKE 'OP-DEMO-%'; DELETE FROM GovernmentOperations WHERE OperationCode LIKE 'OP-DEMO-%'; DELETE FROM AuthorizedPersonnel WHERE Badge='ADMIN-001' AND Email='admin@gov.local';";
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
