﻿// Services/Implementations/SqlServerTableService.cs


using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SQLServerManager.Models;
using SQLServerManager.Options;
using SQLServerManager.Services.Interfaces;
using System.Data;

namespace SQLServerManager.Services.Implementations
{
    public class SqlServerTableService : ITableService
    {
        private readonly string _connectionString;
        public SqlServerTableService(string connectionString)
        {
            _connectionString = connectionString;
        }
        private readonly List<string> lengthRequiredTypes = new()
    {
        "varchar",
        "nvarchar",
        "char",
        "nchar",
        "binary",
        "varbinary"
    };
        public async Task<List<Dictionary<string, object>>> GetTableDataAsync(string databaseName, string schemaName, string tableName)
        {
            var result = new List<Dictionary<string, object>>();

            var connectionString = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = databaseName
            }.ConnectionString;

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Tạo câu query để lấy dữ liệu từ bảng
                string query = $"SELECT * FROM [{schemaName}].[{tableName}]";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                // Lấy thông tin về các cột
                var columns = Enumerable.Range(0, reader.FieldCount)
                    .Select(reader.GetName)
                    .ToList();

                // Đọc dữ liệu
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    foreach (var column in columns)
                    {
                        var value = reader[column];
                        row[column] = value == DBNull.Value ? null : value;
                    }
                    result.Add(row);
                }
            }

            return result;
        }       
        public async Task<List<ColumnInfo>> GetColumnsAsync(string databaseName, string schema, string tableName)
        {
            var columns = new List<ColumnInfo>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
            USE [" + databaseName + @"];
            SELECT 
                c.name AS ColumnName,
                t.name AS DataType,
                c.max_length AS MaxLength,
                c.is_nullable AS IsNullable,
                ISNULL(i.is_primary_key, 0) AS IsPrimaryKey,
                c.is_identity AS IsIdentity,
                CASE WHEN fk.parent_column_id IS NOT NULL THEN 1 ELSE 0 END AS IsForeignKey,
                OBJECT_NAME(fk.referenced_object_id) AS ForeignKeyTable,
                COL_NAME(fk.referenced_object_id, fk.referenced_column_id) AS ForeignKeyColumn,
                OBJECT_DEFINITION(c.default_object_id) AS DefaultValue
            FROM sys.columns c
            INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
            LEFT JOIN sys.index_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
            LEFT JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            LEFT JOIN sys.foreign_key_columns fk ON fk.parent_object_id = c.object_id AND fk.parent_column_id = c.column_id
            WHERE OBJECT_ID('" + schema + "." + tableName + "') = c.object_id ORDER BY c.column_id";
        
        using (var command = new SqlCommand(query, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var column = new ColumnInfo
                        {
                            Name = reader["ColumnName"]?.ToString(),
                            DataType = reader["DataType"]?.ToString(),
                            MaxLength = reader["MaxLength"] != DBNull.Value ? Convert.ToInt32(reader["MaxLength"]) : 0,
                            IsNullable = reader["IsNullable"] != DBNull.Value && Convert.ToBoolean(reader["IsNullable"]),
                            IsPrimaryKey = reader["IsPrimaryKey"] != DBNull.Value && Convert.ToBoolean(reader["IsPrimaryKey"]),
                            IsIdentity = reader["IsIdentity"] != DBNull.Value && Convert.ToBoolean(reader["IsIdentity"]),
                            IsForeignKey = reader["IsForeignKey"] != DBNull.Value && Convert.ToBoolean(reader["IsForeignKey"]),
                            ForeignKeyTable = reader["ForeignKeyTable"]?.ToString(),
                            ForeignKeyColumn = reader["ForeignKeyColumn"]?.ToString(),
                            DefaultValue = reader["DefaultValue"]?.ToString()
                        };
                        columns.Add(column);
                    }
                }
            }

            return columns;
        }
        public SqlServerTableService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }
        public async Task<List<TableInfo>> GetTablesAsync(string databaseName)
        {
            var tables = new List<TableInfo>();
            var connectionString = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = databaseName
            }.ConnectionString;

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                string query = @"
            SELECT 
                s.name AS SchemaName,
                t.name AS TableName,
                p.rows AS [Rows], 
                SUM(a.total_pages) * 8 AS TotalSpaceKB,
                t.create_date AS CreateDate
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.indexes i ON t.object_id = i.object_id
            INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
            INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
            WHERE i.index_id <= 1  -- This ensures we only count data pages once
            GROUP BY s.name, t.name, p.rows, t.create_date
            ORDER BY s.name, t.name";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    tables.Add(new TableInfo
                    {
                        Schema = reader.GetString(0),
                        Name = reader.GetString(1),
                        RowCount = (int)reader.GetInt64(2),
                        SizeKB = reader.GetInt64(3),
                        CreateDate = reader.GetDateTime(4)
                    });
                }
            }

            return tables;
        }
        public async Task<TableInfo> GetTableDetailsAsync(string databaseName, string schema, string tableName)
        {
            var tableInfo = new TableInfo { Schema = schema, Name = tableName };

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
            SELECT 
                s.name AS SchemaName,
                t.name AS TableName,
                p.rows AS [Rows],  -- Changed from RowCount to [Rows]
                SUM(a.total_pages) * 8 AS TotalSpaceKB,
                t.create_date AS CreateDate
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.indexes i ON t.object_id = i.object_id
            INNER JOIN sys.partitions p ON i.object_id = p.object_id AND i.index_id = p.index_id
            INNER JOIN sys.allocation_units a ON p.partition_id = a.container_id
            WHERE i.index_id <= 1  -- This ensures we only count data pages once
            GROUP BY s.name, t.name, p.rows, t.create_date
            ORDER BY s.name, t.name";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@tableName", $"{schema}.{tableName}");

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tableInfo.Columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetBoolean(2),
                    IsPrimaryKey = reader.GetBoolean(3),
                    IsForeignKey = reader.GetBoolean(4),
                    DefaultValue = reader.IsDBNull(5) ? null : reader.GetString(5),
                    MaxLength = reader.GetInt32(6)
                });
            }

            return tableInfo;
        }
        public async Task<QueryResult> QueryTableDataAsync(string databaseName, string schema,
    string tableName, int page = 1, int pageSize = 50, string orderBy = null)
        {
            var result = new QueryResult();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Get total count
            string countQuery = $"USE [{databaseName}]; SELECT COUNT(*) FROM [{schema}].[{tableName}]";
            using (var countCommand = new SqlCommand(countQuery, connection))
            {
                result.TotalRows = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            }

            // Get data with pagination
            string dataQuery = $@"
        USE [{databaseName}];
        SELECT * FROM [{schema}].[{tableName}]
        ORDER BY {(string.IsNullOrEmpty(orderBy) ? "(SELECT NULL)" : orderBy)}
        OFFSET {(page - 1) * pageSize} ROWS
        FETCH NEXT {pageSize} ROWS ONLY";

            using var command = new SqlCommand(dataQuery, connection);
            using var reader = await command.ExecuteReaderAsync();


            // Get column names
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i));
            }

            // Get data
            while (await reader.ReadAsync())
            {
                var row = new List<object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                }
                result.Rows.Add(row);
            }

            return result;
        }
        public async Task<bool> CreateTableAsync(string databaseName, TableInfo tableInfo)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            var columnDefinitions = tableInfo.Columns
                .Select(c => $"{c.Name} {c.DataType}" +
                           $"{(c.MaxLength > 0 ? $"({c.MaxLength})" : "")}" +
                           $"{(c.IsNullable ? " NULL" : " NOT NULL")}" +
                           $"{(!string.IsNullOrEmpty(c.DefaultValue) ? $" DEFAULT {c.DefaultValue}" : "")}")
                .ToList();

            string createTableQuery = $@"
                USE [{databaseName}];
                CREATE TABLE [{tableInfo.Schema}].[{tableInfo.Name}] (
                    {string.Join(",\n    ", columnDefinitions)}
                )";

            using var command = new SqlCommand(createTableQuery, connection);
            await command.ExecuteNonQueryAsync();

            return true;
        }
        public async Task<bool> DeleteTableAsync(string databaseName, string schema, string tableName)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string dropTableQuery = $@"
                USE [{databaseName}];
                DROP TABLE [{schema}].[{tableName}]";

            using var command = new SqlCommand(dropTableQuery, connection);
            await command.ExecuteNonQueryAsync();

            return true;
        }
        public async Task<bool> AddColumnAsync(string database, string schema, string table, ColumnInfo column)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = $"USE [{database}]; ALTER TABLE [{schema}].[{table}] ADD [{column.Name}] {column.DataType}";

                if (lengthRequiredTypes.Contains(column.DataType.ToLower()))
                {
                    sql += $"({column.MaxLength})";
                }

                sql += column.IsNullable ? " NULL" : " NOT NULL";

                if (!string.IsNullOrEmpty(column.DefaultValue))
                {
                    sql += $" DEFAULT {column.DefaultValue}";
                }

                await using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> AlterColumnAsync(string database, string schema, string table,
            string originalColumnName, ColumnInfo column)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = $"USE [{database}]; ALTER TABLE [{schema}].[{table}] ALTER COLUMN [{originalColumnName}] {column.DataType}";

                if (lengthRequiredTypes.Contains(column.DataType.ToLower()))
                {
                    sql += $"({column.MaxLength})";
                }

                sql += column.IsNullable ? " NULL" : " NOT NULL";

                await using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();

                // If column name changed
                if (originalColumnName != column.Name)
                {
                    sql = $"USE [{database}]; EXEC sp_rename '{schema}.{table}.{originalColumnName}', '{column.Name}', 'COLUMN'";
                    await using var renameCommand = new SqlCommand(sql, connection);
                    await renameCommand.ExecuteNonQueryAsync();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> DeleteColumnAsync(string database, string schema, string table, string columnName)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = $"USE [{database}]; ALTER TABLE [{schema}].[{table}] DROP COLUMN [{columnName}]";

                await using var command = new SqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
        //them phan nay
        public async Task<bool> InsertRecordAsync(string database, string schema, string table, Dictionary<string, object> record)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"USE [{database}]";
                    await command.ExecuteNonQueryAsync();

                    var columns = string.Join(", ", record.Keys);
                    var parameters = string.Join(", ", record.Keys.Select(k => $"@{k}"));

                    command.CommandText = $"INSERT INTO [{schema}].[{table}] ({columns}) VALUES ({parameters})";

                    foreach (var item in record)
                    {
                        command.Parameters.AddWithValue($"@{item.Key}", item.Value ?? DBNull.Value);
                    }

                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
        }
        public async Task<bool> UpdateRecordAsync(string database, string schema, string table,
            Dictionary<string, object> newRecord, Dictionary<string, object> oldRecord)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"USE [{database}]";
                    await command.ExecuteNonQueryAsync();

                    var setClause = string.Join(", ", newRecord.Keys.Select(k => $"[{k}] = @new_{k}"));
                    var whereClause = string.Join(" AND ", oldRecord.Keys.Select(k => $"([{k}] = @old_{k} OR (@old_{k} IS NULL AND [{k}] IS NULL))"));

                    command.CommandText = $"UPDATE [{schema}].[{table}] SET {setClause} WHERE {whereClause}";

                    // Add parameters for new values
                    foreach (var item in newRecord)
                    {
                        command.Parameters.AddWithValue($"@new_{item.Key}", item.Value ?? DBNull.Value);
                    }

                    // Add parameters for old values (WHERE clause)
                    foreach (var item in oldRecord)
                    {
                        command.Parameters.AddWithValue($"@old_{item.Key}", item.Value ?? DBNull.Value);
                    }

                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
        }
        public async Task<bool> DeleteRecordAsync(string database, string schema, string table, Dictionary<string, object> record)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"USE [{database}]";
                    await command.ExecuteNonQueryAsync();

                    var whereClause = string.Join(" AND ", record.Keys.Select(k => $"([{k}] = @{k} OR (@{k} IS NULL AND [{k}] IS NULL))"));

                    command.CommandText = $"DELETE FROM [{schema}].[{table}] WHERE {whereClause}";

                    foreach (var item in record)
                    {
                        command.Parameters.AddWithValue($"@{item.Key}", item.Value ?? DBNull.Value);
                    }

                    await command.ExecuteNonQueryAsync();
                    return true;
                }
            }
        }
    }
}
