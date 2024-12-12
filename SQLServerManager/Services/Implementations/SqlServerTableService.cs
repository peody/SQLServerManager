// Services/Implementations/SqlServerTableService.cs

using Microsoft.Data.SqlClient; // Thư viện để làm việc với SQL Server
using Microsoft.Extensions.Options; // Thư viện cho tùy chọn cấu hình
using SQLServerManager.Models; // Thư viện chứa các mô hình dữ liệu

using SQLServerManager.Services.Interfaces; // Thư viện chứa các interface dịch vụ
using System.Data; // Thư viện cho các kiểu dữ liệu

namespace SQLServerManager.Services.Implementations
{
    // Lớp SqlServerTableService thực hiện giao diện ITableService
    public class SqlServerTableService : ITableService
    {
        private readonly string _connectionString; // Chuỗi kết nối đến cơ sở dữ liệu

        // Constructor nhận chuỗi kết nối
        public SqlServerTableService(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Danh sách các kiểu dữ liệu yêu cầu chiều dài
        private readonly List<string> lengthRequiredTypes = new()
        {
            "varchar",
            "nvarchar",
            "char",
            "nchar",
            "binary",
            "varbinary"
        };

        // Phương thức bất đồng bộ để lấy dữ liệu từ bảng
        public async Task<List<Dictionary<string, object>>> GetTableDataAsync(string databaseName, string schemaName, string tableName)
        {
            var result = new List<Dictionary<string, object>>(); // Danh sách để lưu dữ liệu bảng

            // Tạo chuỗi kết nối với cơ sở dữ liệu cụ thể
            var connectionString = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = databaseName
            }.ConnectionString;

            using (var connection = new SqlConnection(connectionString)) // Tạo kết nối với cơ sở dữ liệu
            {
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ

                // Tạo câu truy vấn để lấy dữ liệu từ bảng
                string query = $"SELECT * FROM [{schemaName}].[{tableName}] WHERE table_id > 3" ;

                using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
                using var reader = await command.ExecuteReaderAsync(); // Thực thi lệnh và lấy dữ liệu

                // Lấy thông tin về các cột
                var columns = Enumerable.Range(0, reader.FieldCount)
                    .Select(reader.GetName)
                    .ToList();

                // Đọc dữ liệu
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>(); // Tạo từ điển cho hàng dữ liệu
                    foreach (var column in columns) // Lặp qua các cột
                    {
                        var value = reader[column]; // Lấy giá trị của cột
                        row[column] = value == DBNull.Value ? null : value; // Thêm giá trị vào từ điển
                    }
                    result.Add(row); // Thêm hàng vào danh sách kết quả
                }
            }

            return result; // Trả về danh sách dữ liệu
        }

        // Phương thức bất đồng bộ để lấy thông tin về các cột của bảng
        public async Task<List<ColumnInfo>> GetColumnsAsync(string databaseName, string schema, string tableName)
        {
            var columns = new List<ColumnInfo>(); // Danh sách để lưu thông tin cột

            using (var connection = new SqlConnection(_connectionString)) // Tạo kết nối
            {
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ

                // Truy vấn để lấy thông tin cột
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

                using (var command = new SqlCommand(query, connection)) // Tạo lệnh SQL
                using (var reader = await command.ExecuteReaderAsync()) // Thực thi lệnh và lấy dữ liệu
                {
                    while (await reader.ReadAsync()) // Đọc từng hàng dữ liệu
                    {
                        var column = new ColumnInfo // Tạo đối tượng ColumnInfo
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
                        columns.Add(column); // Thêm thông tin cột vào danh sách
                    }
                }
            }

            return columns; // Trả về danh sách cột
        }

        // Constructor nhận IConfiguration để lấy chuỗi kết nối
        public SqlServerTableService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }

        // Phương thức bất đồng bộ để lấy thông tin về các bảng trong cơ sở dữ liệu
        public async Task<List<TableInfo>> GetTablesAsync(string databaseName)
        {
            var tables = new List<TableInfo>(); // Danh sách để lưu thông tin bảng
            var connectionString = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = databaseName // Thiết lập cơ sở dữ liệu
            }.ConnectionString;

            using (var connection = new SqlConnection(connectionString)) // Tạo kết nối
            {
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ
                // Truy vấn để lấy thông tin về các bảng
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

                using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
                using var reader = await command.ExecuteReaderAsync(); // Thực thi lệnh và lấy dữ liệu

                while (await reader.ReadAsync()) // Đọc từng hàng dữ liệu
                {
                    tables.Add(new TableInfo // Tạo đối tượng TableInfo
                    {
                        Schema = reader.GetString(0),
                        Name = reader.GetString(1),
                        RowCount = (int)reader.GetInt64(2),
                        SizeKB = reader.GetInt64(3),
                        CreateDate = reader.GetDateTime(4)
                    });
                }
            }

            return tables; // Trả về danh sách bảng
        }

        // Phương thức bất đồng bộ để lấy thông tin chi tiết về một bảng
        public async Task<TableInfo> GetTableDetailsAsync(string databaseName, string schema, string tableName)
        {
            var tableInfo = new TableInfo { Schema = schema, Name = tableName }; // Tạo đối tượng TableInfo

            using var connection = new SqlConnection(_connectionString); // Tạo kết nối
            await connection.OpenAsync(); // Mở kết nối bất đồng bộ

            // Truy vấn để lấy thông tin chi tiết về bảng
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
                WHERE i.index_id <= 1  
                AND t.is_ms_shipped = 0  -- Loại trừ tất cả các bảng hệ thống
                GROUP BY s.name, t.name, p.rows, t.create_date
                ORDER BY s.name, t.name";

            using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
            command.Parameters.AddWithValue("@tableName", $"{schema}.{tableName}"); // Thêm tham số

            using var reader = await command.ExecuteReaderAsync(); // Thực thi lệnh và lấy dữ liệu

            while (await reader.ReadAsync()) // Đọc từng hàng dữ liệu
            {
                tableInfo.Columns.Add(new ColumnInfo // Tạo thông tin cột
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

            return tableInfo; // Trả về thông tin bảng
        }

        // Phương thức bất đồng bộ để truy vấn dữ liệu bảng với phân trang
        public async Task<QueryResult> QueryTableDataAsync(string databaseName, string schema,
            string tableName, int page = 1, int pageSize = 50, string orderBy = null)
        {
            var result = new QueryResult(); // Tạo đối tượng QueryResult

            using var connection = new SqlConnection(_connectionString); // Tạo kết nối
            await connection.OpenAsync(); // Mở kết nối bất đồng bộ

            // Lấy tổng số hàng
            string countQuery = $"USE [{databaseName}]; SELECT COUNT(*) FROM [{schema}].[{tableName}]";
            using (var countCommand = new SqlCommand(countQuery, connection)) // Tạo lệnh đếm
            {
                result.TotalRows = Convert.ToInt32(await countCommand.ExecuteScalarAsync()); // Lấy tổng số hàng
            }

            // Lấy dữ liệu với phân trang
            string dataQuery = $@"
            USE [{databaseName}];
            SELECT * FROM [{schema}].[{tableName}]
            ORDER BY {(string.IsNullOrEmpty(orderBy) ? "(SELECT NULL)" : orderBy)}
            OFFSET {(page - 1) * pageSize} ROWS
            FETCH NEXT {pageSize} ROWS ONLY";

            using var command = new SqlCommand(dataQuery, connection); // Tạo lệnh SQL
            using var reader = await command.ExecuteReaderAsync(); // Thực thi lệnh và lấy dữ liệu

            // Lấy tên cột
            for (int i = 0; i < reader.FieldCount; i++)
            {
                result.Columns.Add(reader.GetName(i)); // Thêm tên cột vào kết quả
            }

            // Lấy dữ liệu
            while (await reader.ReadAsync()) // Đọc từng hàng dữ liệu
            {
                var row = new List<object>(); // Tạo danh sách cho hàng dữ liệu
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i)); // Thêm giá trị vào hàng
                }
                result.Rows.Add(row); // Thêm hàng vào kết quả
            }

            return result; // Trả về kết quả truy vấn
        }

        // Phương thức bất đồng bộ để tạo bảng mới
        public async Task<bool> CreateTableAsync(string databaseName, TableInfo tableInfo)
        {
            using var connection = new SqlConnection(_connectionString); // Tạo kết nối
            await connection.OpenAsync(); // Mở kết nối bất đồng bộ

            // Tạo định nghĩa cột cho câu lệnh tạo bảng
            var columnDefinitions = tableInfo.Columns
                .Select(c => $"{c.Name} {c.DataType}" +
                           $"{(c.MaxLength > 0 ? $"({c.MaxLength})" : "")}" +
                           $"{(c.IsNullable ? " NULL" : " NOT NULL")}" +
                           $"{(!string.IsNullOrEmpty(c.DefaultValue) ? $" DEFAULT {c.DefaultValue}" : "")}")
                .ToList();

            // Câu lệnh tạo bảng
            string createTableQuery = $@"
                USE [{databaseName}];
                CREATE TABLE [{tableInfo.Schema}].[{tableInfo.Name}] (
                    {string.Join(",\n    ", columnDefinitions)}
                )";

            using var command = new SqlCommand(createTableQuery, connection); // Tạo lệnh SQL
            await command.ExecuteNonQueryAsync(); // Thực thi lệnh tạo bảng

            return true; // Trả về true nếu thành công
        }

        // Phương thức bất đồng bộ để xóa bảng
        public async Task<bool> DeleteTableAsync(string databaseName, string schema, string tableName)
        {
            using var connection = new SqlConnection(_connectionString); // Tạo kết nối
            await connection.OpenAsync(); // Mở kết nối bất đồng bộ

            // Câu lệnh xóa bảng
            string dropTableQuery = $@"
                USE [{databaseName}];
                DROP TABLE [{schema}].[{tableName}]";

            using var command = new SqlCommand(dropTableQuery, connection); // Tạo lệnh SQL
            await command.ExecuteNonQueryAsync(); // Thực thi lệnh xóa bảng

            return true; // Trả về true nếu thành công
        }

        // Phương thức bất đồng bộ để thêm cột vào bảng
        public async Task<bool> AddColumnAsync(string database, string schema, string table, ColumnInfo column)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString); // Tạo kết nối
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ

                // Câu lệnh thêm cột
                var sql = $"USE [{database}]; ALTER TABLE [{schema}].[{table}] ADD [{column.Name}] {column.DataType}";

                if (lengthRequiredTypes.Contains(column.DataType.ToLower())) // Kiểm tra kiểu dữ liệu yêu cầu chiều dài
                {
                    sql += $"({column.MaxLength})"; // Thêm chiều dài nếu cần
                }

                sql += column.IsNullable ? " NULL" : " NOT NULL"; // Thêm thông tin nullable

                if (!string.IsNullOrEmpty(column.DefaultValue)) // Thêm giá trị mặc định nếu có
                {
                    sql += $" DEFAULT {column.DefaultValue}";
                }

                await using var command = new SqlCommand(sql, connection); // Tạo lệnh SQL
                await command.ExecuteNonQueryAsync(); // Thực thi lệnh thêm cột
                return true; // Trả về true nếu thành công
            }
            catch
            {
                return false; // Trả về false nếu có lỗi
            }
        }

        // Phương thức bất đồng bộ để thay đổi cột trong bảng
        public async Task<bool> AlterColumnAsync(string database, string schema, string table,
            string originalColumnName, ColumnInfo column)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString); // Tạo kết nối
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ

                // Câu lệnh thay đổi cột
                var sql = $"USE [{database}]; ALTER TABLE [{schema}].[{table}] ALTER COLUMN [{originalColumnName}] {column.DataType}";

                if (lengthRequiredTypes.Contains(column.DataType.ToLower())) // Kiểm tra kiểu dữ liệu yêu cầu chiều dài
                {
                    sql += $"({column.MaxLength})"; // Thêm chiều dài nếu cần
                }

                sql += column.IsNullable ? " NULL" : " NOT NULL"; // Thêm thông tin nullable

                await using var command = new SqlCommand(sql, connection); // Tạo lệnh SQL
                await command.ExecuteNonQueryAsync(); // Thực thi lệnh thay đổi cột

                // Nếu tên cột đã thay đổi
                if (originalColumnName != column.Name)
                {
                    sql = $"USE [{database}]; EXEC sp_rename '{schema}.{table}.{originalColumnName}', '{column.Name}', 'COLUMN'";
                    await using var renameCommand = new SqlCommand(sql, connection); // Tạo lệnh đổi tên cột
                    await renameCommand.ExecuteNonQueryAsync(); // Thực thi lệnh đổi tên
                }
                return true; // Trả về true nếu thành công
            }
            catch
            {
                return false; // Trả về false nếu có lỗi
            }
        }

        // Phương thức bất đồng bộ để xóa cột trong bảng
        public async Task<bool> DeleteColumnAsync(string database, string schema, string table, string columnName)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString); // Tạo kết nối
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ

                // Câu lệnh xóa cột
                var sql = $"USE [{database}]; ALTER TABLE [{schema}].[{table}] DROP COLUMN [{columnName}]";

                await using var command = new SqlCommand(sql, connection); // Tạo lệnh SQL
                await command.ExecuteNonQueryAsync(); // Thực thi lệnh xóa cột
                return true; // Trả về true nếu thành công
            }
            catch
            {
                return false; // Trả về false nếu có lỗi
            }
        }

        // Phương thức bất đồng bộ để chèn một bản ghi mới vào bảng
        public async Task<bool> InsertRecordAsync(string database, string schema, string table, Dictionary<string, object> record)
        {
            // Kiểm tra và loại bỏ cột ID nếu có (giả sử tên cột là "ID")
            if (record.ContainsKey("ID"))
            {
                record.Remove("ID");
            }

            using (var connection = new SqlConnection(_connectionString)) // Tạo kết nối
            {
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ
                using (var command = connection.CreateCommand()) // Tạo lệnh SQL
                {
                    command.CommandText = $"USE [{database}]"; // Chọn cơ sở dữ liệu
                    await command.ExecuteNonQueryAsync(); // Thực thi lệnh chọn cơ sở dữ liệu

                    // Tạo danh sách tên cột và tham số
                    var columns = string.Join(", ", record.Keys);
                    var parameters = string.Join(", ", record.Keys.Select(k => $"@{k}"));

                    // Câu lệnh chèn bản ghi
                    command.CommandText = $"INSERT INTO [{schema}].[{table}] ({columns}) VALUES ({parameters})";

                    // Thêm tham số cho lệnh chèn
                    foreach (var item in record)
                    {
                        command.Parameters.AddWithValue($"@{item.Key}", item.Value ?? DBNull.Value); // Thêm giá trị vào tham số
                    }

                    await command.ExecuteNonQueryAsync(); // Thực thi lệnh chèn
                    return true; // Trả về true nếu thành công
                }
            }
        }

        // Phương thức bất đồng bộ để cập nhật một bản ghi trong bảng
        public async Task<bool> UpdateRecordAsync(string database, string schema, string table,
            Dictionary<string, object> newRecord, Dictionary<string, object> oldRecord)
        {
            using (var connection = new SqlConnection(_connectionString)) // Tạo kết nối
            {
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ
                using (var command = connection.CreateCommand()) // Tạo lệnh SQL
                {
                    command.CommandText = $"USE [{database}]"; // Chọn cơ sở dữ liệu
                    await command.ExecuteNonQueryAsync(); // Thực thi lệnh chọn cơ sở dữ liệu

                    // Tạo câu lệnh SET cho việc cập nhật
                    var setClause = string.Join(", ", newRecord.Keys.Select(k => $"[{k}] = @new_{k}"));
                    // Tạo câu lệnh WHERE cho việc xác định bản ghi cần cập nhật
                    var whereClause = string.Join(" AND ", oldRecord.Keys.Select(k => $"([{k}] = @old_{k} OR (@old_{k} IS NULL AND [{k}] IS NULL))"));

                    command.CommandText = $"UPDATE [{schema}].[{table}] SET {setClause} WHERE {whereClause}";

                    // Thêm tham số cho giá trị mới
                    foreach (var item in newRecord)
                    {
                        command.Parameters.AddWithValue($"@new_{item.Key}", item.Value ?? DBNull.Value);
                    }

                    // Thêm tham số cho giá trị cũ (WHERE clause)
                    foreach (var item in oldRecord)
                    {
                        command.Parameters.AddWithValue($"@old_{item.Key}", item.Value ?? DBNull.Value);
                    }

                    await command.ExecuteNonQueryAsync(); // Thực thi lệnh cập nhật
                    return true; // Trả về true nếu thành công
                }
            }
        }

        // Phương thức bất đồng bộ để xóa một bản ghi trong bảng
        public async Task<bool> DeleteRecordAsync(string database, string schema, string table, Dictionary<string, object> record)
        {
            using (var connection = new SqlConnection(_connectionString)) // Tạo kết nối
            {
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ
                using (var command = connection.CreateCommand()) // Tạo lệnh SQL
                {
                    command.CommandText = $"USE [{database}]"; // Chọn cơ sở dữ liệu
                    await command.ExecuteNonQueryAsync(); // Thực thi lệnh chọn cơ sở dữ liệu

                    // Tạo câu lệnh WHERE cho việc xác định bản ghi cần xóa
                    var whereClause = string.Join(" AND ", record.Keys.Select(k => $"([{k}] = @{k} OR (@{k} IS NULL AND [{k}] IS NULL))"));

                    command.CommandText = $"DELETE FROM [{schema}].[{table}] WHERE {whereClause}"; // Câu lệnh xóa bản ghi

                    // Thêm tham số cho lệnh xóa
                    foreach (var item in record)
                    {
                        command.Parameters.AddWithValue($"@{item.Key}", item.Value ?? DBNull.Value);
                    }

                    await command.ExecuteNonQueryAsync(); // Thực thi lệnh xóa
                    return true; // Trả về true nếu thành công
                }
            }
        }
    }
}
