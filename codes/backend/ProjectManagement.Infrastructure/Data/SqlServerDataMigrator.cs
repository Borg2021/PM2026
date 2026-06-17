using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ProjectManagement.Infrastructure.Data;

/// <summary>
/// 从 SQLite 数据库迁移全部数据到 SQL Server。
/// 迁移成功后 SQLite 文件会被重命名为 .migrated 防止重复迁移。
/// </summary>
public class SqlServerDataMigrator
{
    private readonly AppDbContext _target;
    private readonly string _sqliteFilePath;

    public SqlServerDataMigrator(AppDbContext target, string sqliteFilePath)
    {
        _target = target;
        _sqliteFilePath = sqliteFilePath;
    }

    public async Task<bool> MigrateIfNeededAsync()
    {
        if (!File.Exists(_sqliteFilePath))
        {
            Console.WriteLine($"[Migration] SQLite file not found at {_sqliteFilePath}, skipping.");
            return false;
        }

        // 检测目标库是否已有迁移数据（防止重复迁移因文件重命名失败导致）
        var existingUserCount = await _target.Users.CountAsync();
        if (existingUserCount > 1)
        {
            Console.WriteLine($"[Migration] Target already has {existingUserCount} users, data appears migrated. Skipping.");
            return false;
        }

        var sqliteConnStr = $"Data Source={_sqliteFilePath}";
        var tablesData = ReadAllTables(sqliteConnStr);
        if (tablesData.Count == 0)
        {
            Console.WriteLine("[Migration] SQLite database has no tables, skipping.");
            return false;
        }

        Console.WriteLine($"[Migration] Read {tablesData.Sum(kv => kv.Value.Count)} rows from {tablesData.Count} tables. Migrating...");

        // 禁用外键约束
        await _target.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");

        try
        {
            // 清除 EnsureCreated 写入的种子数据（迁移数据会重新写入）
            await ClearSeedDataAsync();

            // 按 FK 依赖顺序逐表插入
            foreach (var tableName in GetTableOrder())
            {
                if (tablesData.TryGetValue(tableName, out var rows) && rows.Count > 0)
                {
                    await InsertTableAsync(tableName, rows);
                }
            }
        }
        finally
        {
            // 重新启用外键约束
            await _target.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'");
        }

        // 迁移成功，重命名 SQLite 文件防止重复执行
        var backupPath = _sqliteFilePath + ".migrated";
        try
        {
            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Move(_sqliteFilePath, backupPath);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[Migration] Warning: Could not rename SQLite file: {ex.Message}");
            Console.WriteLine("[Migration] Please manually rename or delete it to prevent re-migration.");
        }
        return true;
    }

    /// <summary>
    /// 用 Microsoft.Data.Sqlite 读取旧库全部表数据到内存。
    /// </summary>
    private static Dictionary<string, List<Dictionary<string, object?>>> ReadAllTables(string connStr)
    {
        var result = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase);
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        // 获取所有表名
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        var tableNames = new List<string>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read()) tableNames.Add(reader.GetString(0));
        }

        foreach (var tableName in tableNames)
        {
            var rows = new List<Dictionary<string, object?>>();
            using var tCmd = conn.CreateCommand();
            tCmd.CommandText = $"SELECT * FROM \"{tableName}\"";
            using var reader = tCmd.ExecuteReader();
            while (reader.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    var colName = reader.GetName(i);
                    row[colName] = value;
                }
                rows.Add(row);
            }
            result[tableName] = rows;
        }

        return result;
    }

    /// <summary>
    /// 清除 EnsureCreated() + HasData() 写入的种子行。
    /// </summary>
    private async Task ClearSeedDataAsync()
    {
        // 先删外键叶子表，再删根表
        await _target.Database.ExecuteSqlRawAsync("DELETE FROM [RolePermissions]");
        await _target.Database.ExecuteSqlRawAsync("DELETE FROM [UserRoles]");
        await _target.Database.ExecuteSqlRawAsync("DELETE FROM [Roles]");
        await _target.Database.ExecuteSqlRawAsync("DELETE FROM [Permissions]");
        await _target.Database.ExecuteSqlRawAsync("DELETE FROM [DictItems]");
        await _target.Database.ExecuteSqlRawAsync("DELETE FROM [DictTypes]");
        await _target.Database.ExecuteSqlRawAsync("DELETE FROM [RoleDicts]");
        await _target.Database.ExecuteSqlRawAsync("DELETE FROM [Departments]");
    }

    /// <summary>
    /// 按 FK 依赖顺序排列的表名（父表在前，子表在后）。
    /// </summary>
    private static string[] GetTableOrder() => new[]
    {
        "Departments",
        "RoleDicts",
        "DictTypes",
        "DictItems",
        "SysParams",
        "Functions",
        "Permissions",
        "Roles",
        "Users",
        "UserRoles",
        "UserFunctions",
        "RolePermissions",
        "Templates",
        "PlanNodes",
        "Milestones",
        "TemplateMembers",
        "FileTemplateItems",
        "PlanBundles",
        "PlanBundleItems",
        "PlanNodeDependencies",
        "Projects",
        "ProjectProducts",
        "ProjectMembers",
        "ProjectMilestones",
        "ProjectTasks",
        "ProjectChanges",
        "ProjectFinances",
        "ProjectPlanReceipts",
        "ProjectReceipts",
        "ProjectInvoices",
        "ProjectFiles",
        "ProjectFileItems",
        "ProjectFileVersions",
        "OperationLogs",
    };

    // 缓存目标表列名，避免重复查询
    private readonly Dictionary<string, HashSet<string>> _targetColumns = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 查询目标 SQL Server 表中实际存在的列名。
    /// </summary>
    private async Task<HashSet<string>> GetTargetColumnsAsync(string tableName)
    {
        if (_targetColumns.TryGetValue(tableName, out var cached))
            return cached;

        var conn = _target.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}'";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));
            _targetColumns[tableName] = columns;
            return columns;
        }
        finally
        {
            if (!wasOpen) conn.Close();
        }
    }

    /// <summary>
    /// 批量插入一个表的所有行（带 IDENTITY_INSERT），自动过滤目标表中不存在的列。
    /// </summary>
    private async Task InsertTableAsync(string tableName, List<Dictionary<string, object?>> rows)
    {
        var targetCols = await GetTargetColumnsAsync(tableName);
        // 过滤：只保留目标表中存在的列
        var srcColumns = rows[0].Keys.Where(c => targetCols.Contains(c)).ToList();
        if (srcColumns.Count == 0)
        {
            Console.WriteLine($"[Migration]   {tableName}: skipped ({rows.Count} rows, no matching columns)");
            return;
        }
        var colList = string.Join(", ", srcColumns.Select(c => $"[{c}]"));

        // 分批插入，每批 50 行（避免超 SQL Server 2100 参数限制）
        var batchSize = 50;
        for (var start = 0; start < rows.Count; start += batchSize)
        {
            var batch = rows.GetRange(start, Math.Min(batchSize, rows.Count - start));
            var sqlList = new List<string>();
            var allParams = new List<SqlParameter>();

            for (var r = 0; r < batch.Count; r++)
            {
                var paramNames = new List<string>();
                for (var c = 0; c < srcColumns.Count; c++)
                {
                    var paramName = $"@p{start + r}_{c}";
                    paramNames.Add(paramName);
                    var val = batch[r][srcColumns[c]];
                    if (val is long longVal)
                        allParams.Add(new SqlParameter(paramName, longVal));
                    else if (val is double doubleVal)
                        allParams.Add(new SqlParameter(paramName, doubleVal));
                    else if (val is string strVal)
                        allParams.Add(new SqlParameter(paramName, strVal));
                    else if (val is byte[] blobVal)
                        allParams.Add(new SqlParameter(paramName, blobVal));
                    else
                        allParams.Add(new SqlParameter(paramName, val ?? DBNull.Value));
                }
                sqlList.Add($"({string.Join(", ", paramNames)})");
            }

            var sql = $"SET IDENTITY_INSERT [{tableName}] ON;\n"
                    + $"INSERT INTO [{tableName}] ({colList}) VALUES\n"
                    + string.Join(",\n", sqlList) + ";\n"
                    + $"SET IDENTITY_INSERT [{tableName}] OFF;";

            await _target.Database.ExecuteSqlRawAsync(sql, allParams);
        }

        var skippedCols = rows[0].Keys.Count - srcColumns.Count;
        Console.WriteLine($"[Migration]   {tableName}: {rows.Count} rows{(skippedCols > 0 ? $" (skipped {skippedCols} legacy columns)" : "")}");
    }
}
