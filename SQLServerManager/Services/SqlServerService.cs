using Microsoft.Data.SqlClient;
using SQLServerManager.Models;
using System.Data;

namespace SQLServerManager.Services
{
    public class SqlServerService
    {
        private readonly string _connectionString;

        public SqlServerService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found.");
        }

        public async Task<List<Database>> GetDatabasesAsync()
        {
            var databases = new List<Database>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"
                    SELECT 
                        name,
                        create_date,
                        state_desc,
                        CAST((SELECT SUM(size * 8.0 / 1024) 
                              FROM sys.master_files 
                              WHERE database_id = db.database_id) AS decimal(10,2)) AS size_mb
                    FROM sys.databases db
                    ORDER BY name";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    databases.Add(new Database
                    {
                        Name = reader.GetString(0),
                        CreateDate = reader.GetDateTime(1),
                        State = reader.GetString(2),
                        SizeMB = (long)reader.GetDecimal(3)
                    });
                }
            }

            return databases;
        }

        public async Task CreateDatabaseAsync(string databaseName)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = $"CREATE DATABASE [{databaseName}]";
            using var command = new SqlCommand(query, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteDatabaseAsync(string databaseName)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // First, set database to single user mode
            string singleUserQuery = $@"
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}]";

            using var command = new SqlCommand(singleUserQuery, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> DatabaseExistsAsync(string databaseName)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT COUNT(*) 
                FROM sys.databases 
                WHERE name = @name";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@name", databaseName);

            int count = Convert.ToInt32(await command.ExecuteScalarAsync());
            return count > 0;
        }

        public async Task RenameDatabaseAsync(string oldName, string newName)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = $"ALTER DATABASE [{oldName}] MODIFY NAME = [{newName}]";
            using var command = new SqlCommand(query, connection);
            await command.ExecuteNonQueryAsync();
        }
        public interface ISqlServerService
        {
            Task<bool> DatabaseExistsAsync(string name);
            Task CreateDatabaseAsync(string name);
        }
        public async Task<DatabaseDetails> GetDatabaseDetailsAsync(string databaseName)
        {
            try
            {
                var details = new DatabaseDetails();

                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                // Sửa lại query để đảm bảo kiểu dữ liệu trả về là bigint
                string basicInfoQuery = @"
            SELECT 
                d.name,
                SUSER_SNAME(d.owner_sid) as owner,
                d.create_date,
                d.recovery_model_desc,
                d.state_desc,
                CAST(CAST(SUM(CAST(mf.size AS BIGINT)) * 8.0 / 1024 AS DECIMAL(18,2)) AS BIGINT) as size_mb
            FROM sys.databases d
            JOIN sys.master_files mf ON d.database_id = mf.database_id
            WHERE d.name = @dbName
            GROUP BY d.name, d.owner_sid, d.create_date, d.recovery_model_desc, d.state_desc";

                using (var command = new SqlCommand(basicInfoQuery, connection))
                {
                    command.Parameters.AddWithValue("@dbName", databaseName);
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        details.Name = reader.GetString(0);
                        details.Owner = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
                        details.CreateDate = reader.GetDateTime(2);
                        details.RecoveryModel = reader.GetString(3);
                        details.State = reader.GetString(4);
                        details.SizeInMB = reader.GetInt64(5); // Đổi thành GetInt64
                    }
                }

                // Sửa lại query files để đảm bảo kiểu dữ liệu
                string filesQuery = @"
            SELECT 
                name,
                physical_name,
                type_desc,
                CAST(CAST(size * 8.0 / 1024 AS DECIMAL(18,2)) AS BIGINT) as size_mb,
                CASE max_size 
                    WHEN -1 THEN CAST(268435456 AS BIGINT)
                    ELSE CAST(CAST(max_size * 8.0 / 1024 AS DECIMAL(18,2)) AS BIGINT)
                END as max_size_mb,
                CAST(growth AS BIGINT),
                is_percent_growth
            FROM sys.master_files
            WHERE database_id = DB_ID(@dbName)";

                using (var command = new SqlCommand(filesQuery, connection))
                {
                    command.Parameters.AddWithValue("@dbName", databaseName);
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        details.Files.Add(new DatabaseFileInfo
                        {
                            Name = reader.GetString(0),
                            PhysicalName = reader.GetString(1),
                            Type = reader.GetString(2),
                            SizeInMB = reader.GetInt64(3),
                            MaxSizeInMB = reader.GetInt64(4),
                            Growth = reader.GetInt64(5),
                            IsPercentGrowth = reader.GetBoolean(6)
                        });
                    }
                }

                return details;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting database details: {ex.Message}", ex);
            }
        }
    }
}
