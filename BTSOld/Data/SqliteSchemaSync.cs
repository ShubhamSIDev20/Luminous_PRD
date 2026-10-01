using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data.Common;

namespace BatteryTestingSystem.Data
{
    public static class SqliteSchemaSync
    {
        public static void SyncModelWithDatabase(DbContext context)
        {
            var conn = context.Database.GetDbConnection();

            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();

            foreach (var entity in context.Model.GetEntityTypes())
            {
                var tableName = entity.GetTableName();

                if (string.IsNullOrWhiteSpace(tableName))
                    continue;

                EnsureTableExists(conn, entity, tableName);
                EnsureColumnsExist(conn, entity, tableName);
            }
        }

        private static void EnsureTableExists(DbConnection conn, IEntityType entity, string tableName)
        {
            var tableExists = false;

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName;";
                var param = cmd.CreateParameter();
                param.ParameterName = "@tableName";
                param.Value = tableName;
                cmd.Parameters.Add(param);

                using var reader = cmd.ExecuteReader();
                tableExists = reader.Read();
            }

            if (tableExists)
                return;

            // Build CREATE TABLE with all columns
            var columns = entity.GetProperties().Select(prop =>
            {
                var colName = prop.GetColumnName();
                var colType = GetSqliteType(prop.ClrType);
                var nullable = prop.IsNullable ? "" : " NOT NULL";
                var pk = prop.IsPrimaryKey() ? " PRIMARY KEY" : "";
                return $"{colName} {colType}{pk}{nullable}";
            });

            using var create = conn.CreateCommand();
            create.CommandText = $"CREATE TABLE IF NOT EXISTS {tableName} ({string.Join(", ", columns)});";
            create.ExecuteNonQuery();
        }

        private static void EnsureColumnsExist(DbConnection conn, IEntityType entity, string tableName)
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({tableName});";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    existingColumns.Add(reader["name"].ToString() ?? string.Empty);
            }

            foreach (var prop in entity.GetProperties())
            {
                var columnName = prop.GetColumnName();

                if (existingColumns.Contains(columnName))
                    continue;

                var columnType = GetSqliteType(prop.ClrType);
                var nullable = prop.IsNullable ? "" : " NOT NULL DEFAULT ''";

                using var alter = conn.CreateCommand();
                alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}{nullable};";
                alter.ExecuteNonQuery();
            }
        }
        private static string GetSqliteType(Type type)
        {
            // unwrap nullable types (float?, int?, etc.)
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type == typeof(int) || type == typeof(long) || type == typeof(bool))
                return "INTEGER";

            if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
                return "REAL";

            if (type == typeof(DateTime))
                return "TEXT";

            return "TEXT";
        }
    }

}
