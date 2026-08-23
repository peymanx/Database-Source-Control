using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Barsa.EndUserGitUI.Git
{
    public static class DatabaseDiff
    {
        public static List<DatabaseObjectInfo> Scan(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("ConnectionString تنظیم نشده است.");

            List<DatabaseObjectInfo> result = new List<DatabaseObjectInfo>();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                ProgressManager.ReportStage("مرحله 1 از 5", "اتصال به Database و آماده‌سازی Metadata...", 5);
                connection.Open();

                ScanTables(connection, result);
                ScanProgrammableObjects(connection, result);
                ScanTriggers(connection, result);
            }

            ProgressManager.ReportStage("مرحله 5 از 5", "Snapshot متادیتای Database آماده شد؛ هیچ رکوردی از جداول خوانده نشد.", 100);
            return result;
        }

        private static void ScanTables(SqlConnection connection, List<DatabaseObjectInfo> result)
        {
            const string sql = @"
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_computed
FROM sys.tables t
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.columns c ON c.object_id = t.object_id
LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name, c.column_id;";

            Dictionary<string, DatabaseObjectInfo> tables = new Dictionary<string, DatabaseObjectInfo>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand command = new SqlCommand(sql, connection))
            using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                while (reader.Read())
                {
                    string schema = reader.GetString(0);
                    string name = reader.GetString(1);
                    string key = schema + "." + name;
                    DatabaseObjectInfo table;
                    if (!tables.TryGetValue(key, out table))
                    {
                        table = new DatabaseObjectInfo
                        {
                            Type = DatabaseObjectType.Table,
                            Schema = schema,
                            Name = name
                        };
                        tables.Add(key, table);
                        result.Add(table);
                    }

                    if (!reader.IsDBNull(2))
                    {
                        table.Columns.Add(new DatabaseColumnInfo
                        {
                            Ordinal = reader.GetInt32(2),
                            ColumnName = reader.GetString(3),
                            DataType = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            MaxLength = reader.IsDBNull(5) ? 0 : reader.GetInt16(5),
                            Precision = reader.IsDBNull(6) ? (byte)0 : reader.GetByte(6),
                            Scale = reader.IsDBNull(7) ? (byte)0 : reader.GetByte(7),
                            IsNullable = !reader.IsDBNull(8) && reader.GetBoolean(8),
                            IsComputed = !reader.IsDBNull(9) && reader.GetBoolean(9)
                        });
                    }
                }
            }

            ProgressManager.ReportStage("مرحله 2 از 5", "خواندن ساختار Tableها و Columnها...", 30);
            ProgressManager.Log("تعداد Tableهای خوانده‌شده: " + tables.Count);
        }

        private static void ScanProgrammableObjects(SqlConnection connection, List<DatabaseObjectInfo> result)
        {
            const string sql = @"
SELECT
    CASE o.type
        WHEN 'V' THEN 1
        WHEN 'P' THEN 2
        WHEN 'FN' THEN 3
        WHEN 'IF' THEN 3
        WHEN 'TF' THEN 3
        ELSE 0
    END AS ObjectType,
    s.name AS SchemaName,
    o.name AS ObjectName,
    ISNULL(sm.definition, '') AS Definition
FROM sys.objects o
INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
LEFT JOIN sys.sql_modules sm ON sm.object_id = o.object_id
WHERE o.is_ms_shipped = 0
  AND o.type IN ('V', 'P', 'FN', 'IF', 'TF')
ORDER BY s.name, o.name;";

            int count = 0;
            using (SqlCommand command = new SqlCommand(sql, connection))
            using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                while (reader.Read())
                {
                    int type = reader.GetInt32(0);
                    DatabaseObjectInfo obj = new DatabaseObjectInfo
                    {
                        Type = type == 1 ? DatabaseObjectType.View :
                               type == 2 ? DatabaseObjectType.Procedure : DatabaseObjectType.Function,
                        Schema = reader.GetString(1),
                        Name = reader.GetString(2),
                        Definition = reader.IsDBNull(3) ? "" : reader.GetString(3)
                    };
                    result.Add(obj);
                    count++;
                }
            }

            ProgressManager.ReportStage("مرحله 3 از 5", "خواندن View / Procedure / Functionها...", 55);
            ProgressManager.Log("تعداد Objectهای برنامه‌ای: " + count);
        }

        private static void ScanTriggers(SqlConnection connection, List<DatabaseObjectInfo> result)
        {
            const string sql = @"
SELECT
    s.name AS SchemaName,
    tr.name AS TriggerName,
    ISNULL(sm.definition, '') AS Definition
FROM sys.triggers tr
INNER JOIN sys.tables t ON t.object_id = tr.parent_id
INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.sql_modules sm ON sm.object_id = tr.object_id
WHERE tr.is_ms_shipped = 0
ORDER BY s.name, tr.name;";

            int count = 0;
            using (SqlCommand command = new SqlCommand(sql, connection))
            using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                while (reader.Read())
                {
                    result.Add(new DatabaseObjectInfo
                    {
                        Type = DatabaseObjectType.Trigger,
                        Schema = reader.GetString(0),
                        Name = reader.GetString(1),
                        Definition = reader.IsDBNull(2) ? "" : reader.GetString(2)
                    });
                    count++;
                }
            }

            ProgressManager.ReportStage("مرحله 4 از 5", "خواندن Triggerها و نهایی‌سازی Snapshot...", 80);
            ProgressManager.Log("تعداد Triggerهای خوانده‌شده: " + count);
        }

        public static List<DatabaseChange> Compare(List<DatabaseObjectInfo> oldSnapshot, List<DatabaseObjectInfo> current)
        {
            List<DatabaseChange> changes = new List<DatabaseChange>();
            oldSnapshot = oldSnapshot ?? new List<DatabaseObjectInfo>();
            current = current ?? new List<DatabaseObjectInfo>();

            Dictionary<string, DatabaseObjectInfo> oldMap = oldSnapshot.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, DatabaseObjectInfo> newMap = current.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);

            foreach (DatabaseObjectInfo obj in current.OrderBy(x => Key(x), StringComparer.OrdinalIgnoreCase))
            {
                string key = Key(obj);
                DatabaseObjectInfo oldObj;
                if (!oldMap.TryGetValue(key, out oldObj))
                {
                    changes.Add(new DatabaseChange { Status = "A", ObjectType = obj.Type, Schema = obj.Schema, Name = obj.Name });
                    continue;
                }

                DatabaseChange modified = null;
                if (obj.Type == DatabaseObjectType.Table)
                {
                    List<DatabaseColumnChange> columnChanges = CompareColumns(oldObj.Columns, obj.Columns);
                    if (columnChanges.Count > 0)
                    {
                        modified = new DatabaseChange
                        {
                            Status = "M",
                            ObjectType = obj.Type,
                            Schema = obj.Schema,
                            Name = obj.Name,
                            ColumnChanges = columnChanges
                        };
                    }
                }
                else if (!NormalizeSql(oldObj.Definition).Equals(NormalizeSql(obj.Definition), StringComparison.Ordinal))
                {
                    modified = new DatabaseChange { Status = "M", ObjectType = obj.Type, Schema = obj.Schema, Name = obj.Name };
                }

                if (modified != null) changes.Add(modified);
            }

            foreach (DatabaseObjectInfo oldObj in oldSnapshot.OrderBy(x => Key(x), StringComparer.OrdinalIgnoreCase))
            {
                if (!newMap.ContainsKey(Key(oldObj)))
                    changes.Add(new DatabaseChange { Status = "D", ObjectType = oldObj.Type, Schema = oldObj.Schema, Name = oldObj.Name });
            }

            return changes;
        }

        private static List<DatabaseColumnChange> CompareColumns(List<DatabaseColumnInfo> oldColumns, List<DatabaseColumnInfo> newColumns)
        {
            List<DatabaseColumnChange> result = new List<DatabaseColumnChange>();
            Dictionary<string, DatabaseColumnInfo> oldMap = oldColumns.ToDictionary(x => x.ColumnName, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, DatabaseColumnInfo> newMap = newColumns.ToDictionary(x => x.ColumnName, StringComparer.OrdinalIgnoreCase);

            HashSet<string> matchedOld = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> matchedNew = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // First match columns by name. This is the safest match and preserves the
            // existing Added/Deleted/Modified behaviour.
            foreach (DatabaseColumnInfo n in newColumns)
            {
                DatabaseColumnInfo o;
                if (!oldMap.TryGetValue(n.ColumnName, out o))
                    continue;

                matchedOld.Add(o.ColumnName);
                matchedNew.Add(n.ColumnName);

                if (ColumnModified(o, n))
                {
                    result.Add(new DatabaseColumnChange
                    {
                        ChangeType = ColumnChangeType.Modified,
                        OldName = o.ColumnName,
                        NewName = n.ColumnName,
                        OldDataType = o.DataType,
                        NewDataType = n.DataType,
                        OldMaxLength = o.MaxLength,
                        NewMaxLength = n.MaxLength,
                        OldNullable = o.IsNullable,
                        NewNullable = n.IsNullable,
                        OldOrdinal = o.Ordinal,
                        NewOrdinal = n.Ordinal
                    });
                }
            }

            // Columns that disappeared and appeared again may actually be a rename.
            // Match only when the candidate is unique and structurally very similar.
            // This prevents unrelated Drop/Add operations from being reported as Rename.
            List<DatabaseColumnInfo> deleted = oldColumns
                .Where(x => !matchedOld.Contains(x.ColumnName))
                .ToList();
            List<DatabaseColumnInfo> added = newColumns
                .Where(x => !matchedNew.Contains(x.ColumnName))
                .ToList();

            foreach (DatabaseColumnInfo n in added)
            {
                List<ColumnRenameCandidate> candidates = deleted
                    .Select(o2 => new ColumnRenameCandidate
                    {
                        OldColumn = o2,
                        Score = RenameScore(o2, n)
                    })
                    .Where(x => x.Score >= 70)
                    .OrderByDescending(x => x.Score)
                    .ToList();

                if (candidates.Count == 0)
                    continue;

                ColumnRenameCandidate best = candidates[0];
                bool ambiguous = candidates.Count > 1 && candidates[1].Score == best.Score;
                if (ambiguous)
                    continue;

                // A rename should be a strong match. Ordinal + type is enough for a
                // typical SQL rename, while the additional attributes raise confidence.
                DatabaseColumnInfo o = best.OldColumn;
                matchedOld.Add(o.ColumnName);
                matchedNew.Add(n.ColumnName);

                result.Add(new DatabaseColumnChange
                {
                    ChangeType = ColumnChangeType.Renamed,
                    OldName = o.ColumnName,
                    NewName = n.ColumnName,
                    OldDataType = o.DataType,
                    NewDataType = n.DataType,
                    OldMaxLength = o.MaxLength,
                    NewMaxLength = n.MaxLength,
                    OldNullable = o.IsNullable,
                    NewNullable = n.IsNullable,
                    OldOrdinal = o.Ordinal,
                    NewOrdinal = n.Ordinal
                });
            }

            // Remaining unmatched columns are real additions/deletions.
            foreach (DatabaseColumnInfo n in newColumns)
            {
                if (matchedNew.Contains(n.ColumnName))
                    continue;

                result.Add(new DatabaseColumnChange
                {
                    ChangeType = ColumnChangeType.Added,
                    NewName = n.ColumnName,
                    NewDataType = n.DataType,
                    NewMaxLength = n.MaxLength,
                    NewNullable = n.IsNullable,
                    NewOrdinal = n.Ordinal
                });
            }

            foreach (DatabaseColumnInfo o in oldColumns)
            {
                if (matchedOld.Contains(o.ColumnName))
                    continue;

                result.Add(new DatabaseColumnChange
                {
                    ChangeType = ColumnChangeType.Deleted,
                    OldName = o.ColumnName,
                    OldDataType = o.DataType,
                    OldMaxLength = o.MaxLength,
                    OldNullable = o.IsNullable,
                    OldOrdinal = o.Ordinal
                });
            }

            return result;
        }

        private sealed class ColumnRenameCandidate
        {
            public DatabaseColumnInfo OldColumn { get; set; }
            public int Score { get; set; }
        }

        private static int RenameScore(DatabaseColumnInfo oldColumn, DatabaseColumnInfo newColumn)
        {
            int score = 0;

            if (string.Equals(oldColumn.DataType, newColumn.DataType, StringComparison.OrdinalIgnoreCase))
                score += 35;
            if (oldColumn.MaxLength == newColumn.MaxLength)
                score += 15;
            if (oldColumn.Precision == newColumn.Precision)
                score += 5;
            if (oldColumn.Scale == newColumn.Scale)
                score += 5;
            if (oldColumn.IsNullable == newColumn.IsNullable)
                score += 10;
            if (oldColumn.IsComputed == newColumn.IsComputed)
                score += 5;
            if (oldColumn.Ordinal == newColumn.Ordinal)
                score += 25;

            return score;
        }

        private static bool ColumnModified(DatabaseColumnInfo a, DatabaseColumnInfo b)
        {
            return !a.DataType.Equals(b.DataType, StringComparison.OrdinalIgnoreCase) ||
                   a.MaxLength != b.MaxLength ||
                   a.Precision != b.Precision ||
                   a.Scale != b.Scale ||
                   a.IsNullable != b.IsNullable ||
                   a.IsComputed != b.IsComputed ||
                   a.Ordinal != b.Ordinal;
        }

        private static string Key(DatabaseObjectInfo obj)
        {
            return obj.Type + "|" + obj.Schema + "|" + obj.Name;
        }

        private static string NormalizeSql(string value)
        {
            return (value ?? "").Replace("\r", "").Replace("\n", "").Replace("\t", "").Trim();
        }
    }
}
