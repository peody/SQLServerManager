using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SQLServerManager.Services
{
    public class SequenceService
    {
        private readonly string _connectionString;

        public SequenceService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        public async Task<List<Sequence>> GetSequencesAsync(string databaseName)
        {
            var sequences = new List<Sequence>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = $@"
                SELECT 
                    name, 
                    start_value AS StartValue, 
                    increment AS IncrementValue 
                FROM {databaseName}.sys.sequences";

                using var command = new SqlCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    sequences.Add(new Sequence
                    {
                        Name = reader.GetString(0),
                        StartValue = reader.GetInt64(1),
                        IncrementValue = reader.GetInt64(2)
                    });
                }
            }

            return sequences;
        }

        public async Task CreateSequenceAsync(string databaseName, string sequenceName, long startValue, long incrementValue)
        {
            var sql = $"CREATE SEQUENCE [{sequenceName}] START WITH {startValue} INCREMENT BY {incrementValue};";
            var connectionString = $"Server=.\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

            // Kiểm tra xem sequence đã tồn tại chưa
            if (await SequenceExistsAsync(databaseName, sequenceName))
            {
                throw new InvalidOperationException($"Sequence '{sequenceName}' already exists in database '{databaseName}'.");
            }

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(sql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DropSequenceAsync(string databaseName, string sequenceName)
        {
            var connectionString = $"Server=.\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Kiểm tra sự tồn tại của sequence
                var checkCommand = new SqlCommand($"USE {databaseName}; SELECT COUNT(*) FROM sys.sequences WHERE name = '{sequenceName}';", connection);
                var exists = (int)await checkCommand.ExecuteScalarAsync() > 0;

                if (!exists)
                {
                    throw new Exception($"Sequence '{sequenceName}' does not exist.");
                }

                // Thực hiện lệnh xóa
                var dropCommand = new SqlCommand($"USE {databaseName}; DROP SEQUENCE {sequenceName};", connection);
                await dropCommand.ExecuteNonQueryAsync();
            }
        }

        public async Task<long> GetCurrentValueAsync(string databaseName, string sequenceName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = $"SELECT CURRENT_VALUE FROM {databaseName}.sys.sequences WHERE name = @sequenceName";
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@sequenceName", sequenceName);

                var result = await command.ExecuteScalarAsync();
                return result != null ? (long)result : 0; // Trả về 0 nếu không tìm thấy
            }
        }

        public async Task AlterSequenceAsync(string databaseName, string sequenceName, long? startValue = null, long? increment = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                var setClauses = new List<string>();

                if (startValue.HasValue)
                {
                    setClauses.Add($"RESTART WITH {startValue.Value}");
                }

                if (increment.HasValue)
                {
                    setClauses.Add($"INCREMENT BY {increment.Value}");
                }

                if (setClauses.Count > 0)
                {
                    string query = $"ALTER SEQUENCE [{sequenceName}] {string.Join(" ", setClauses)}";
                    using var command = new SqlCommand(query, connection);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task GrantAlterPermissionAsync(string sequenceName, string schemaName, string userName)
        {
            var sql = $"GRANT ALTER ON OBJECT::[{schemaName}].[{sequenceName}] TO [{userName}]";

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(sql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<bool> SequenceExistsAsync(string databaseName, string sequenceName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = $"SELECT COUNT(*) FROM {databaseName}.sys.sequences WHERE name = @sequenceName";
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@sequenceName", sequenceName);

                var count = (int)await command.ExecuteScalarAsync();
                return count > 0; // Trả về true nếu sequence tồn tại
            }
        }
    }

    public class Sequence
    {
        public string Name { get; set; }
        public long StartValue { get; set; }
        public long IncrementValue { get; set; }
    }
}
