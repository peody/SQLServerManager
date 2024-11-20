using Microsoft.Data.SqlClient;
using SQLServerManager.Services.Interfaces;

namespace SQLServerManager.Services.Implementations
{
    // Services/Implementations/SqlServerDatabaseService.cs
    public class SqlServerDatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public SqlServerDatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<List<string>> GetDatabasesAsync()
        {
            var databases = new List<string>();
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = @"SELECT name FROM sys.databases 
                           WHERE database_id > 4 
                           AND state_desc = 'ONLINE'
                           ORDER BY name";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0));
                }
            }
            return databases;
        }
    }

}
