using Microsoft.Data.SqlClient;
using static Microsoft.CodeAnalysis.CSharp.SyntaxTokenParser;
using static SQLServerManager.Components.Pages.SequenceManager;

namespace SQLServerManager.Services
{
    public class SequenceService
    {
        private readonly string _connectionString;

        public SequenceService(IConfiguration configuration)
        {
            _connectionString = configuration
                .GetConnectionString("DefaultConnection");
        }

        public async Task<List<Sequence>> GetSequencesAsync(string databaseName)
        {
            var sequences = new List<Sequence>();

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Thực hiện truy vấn để lấy danh sách sequences từ database
                string query = $"SELECT name, start_value, increment_value FROM sys.sequences WHERE [database_name] = @databaseName";
                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@databaseName", databaseName);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sequences.Add(new Sequence
                    {
                        Name = reader.GetString(0),
                        StartValue = reader.GetInt32(1),
                        IncrementValue = reader.GetInt32(2)
                    });
                }
            }

            return sequences;
        }



        public async Task CreateSequenceAsync(string sequenceName, int startValue, int increment)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = $"CREATE SEQUENCE [{sequenceName}] START WITH {startValue} INCREMENT BY {increment}";
                using var command = new SqlCommand(query, connection);
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task DropSequenceAsync(string sequenceName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = $"DROP SEQUENCE [{sequenceName}]";
                using var command = new SqlCommand(query, connection);
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<int> GetCurrentValueAsync(string sequenceName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                string query = $"SELECT CURRENT_VALUE FROM sys.sequences WHERE name = '{sequenceName}'";
                using var command = new SqlCommand(query, connection);
                return (int)await command.ExecuteScalarAsync();
            }
        }
    }
}
