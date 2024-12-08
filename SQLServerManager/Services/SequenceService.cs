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
        public async Task ReplaceSequenceAsync(string databaseName, string oldSequenceName, string newSequenceName, long startValue, long incrementValue)
        {
            var connectionString = $"Server=.\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Bắt đầu giao dịch
                using var transaction = connection.BeginTransaction();
                try
                {
                    // Kiểm tra xem sequence cũ có tồn tại không
                    string checkOldSql = $"IF EXISTS (SELECT * FROM sys.sequences WHERE name = '{oldSequenceName}') " +
                                         $"BEGIN " +
                                         $"ALTER SEQUENCE [{oldSequenceName}] " +
                                         $"RESTART WITH {startValue}; " + // Chỉ đặt lại giá trị bắt đầu
                                         $"ALTER SEQUENCE [{oldSequenceName}] " +
                                         $"INCREMENT BY {incrementValue}; " + // Cập nhật giá trị tăng
                                         $"END;";
                    using (var alterCommand = new SqlCommand(checkOldSql, connection, transaction))
                    {
                        await alterCommand.ExecuteNonQueryAsync();
                        Console.WriteLine($"Đã cập nhật sequence: {oldSequenceName} với Bắt đầu: {startValue}, Tăng: {incrementValue}");
                    }

                    // Tạo sequence mới nếu cần
                    if (!string.Equals(oldSequenceName, newSequenceName, StringComparison.OrdinalIgnoreCase))
                    {
                        string checkNewSql = $"IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name = '{newSequenceName}') " +
                                             $"BEGIN " +
                                             $"CREATE SEQUENCE [{newSequenceName}] START WITH {startValue} INCREMENT BY {incrementValue}; " +
                                             $"END;";
                        using (var createCommand = new SqlCommand(checkNewSql, connection, transaction))
                        {
                            await createCommand.ExecuteNonQueryAsync();
                            Console.WriteLine($"Đã tạo sequence mới: {newSequenceName}");
                        }

                        // Xóa sequence cũ
                        string dropOldSql = $"DROP SEQUENCE IF EXISTS [{oldSequenceName}];";
                        using (var dropCommand = new SqlCommand(dropOldSql, connection, transaction))
                        {
                            await dropCommand.ExecuteNonQueryAsync();
                            Console.WriteLine($"Đã xóa sequence cũ: {oldSequenceName}");
                        }
                    }

                    // Kiểm tra giá trị hiện tại của sequence mới
                    string checkUpdatedSql = $"SELECT current_value FROM sys.sequences WHERE name = '{newSequenceName}';";
                    using (var checkCommand = new SqlCommand(checkUpdatedSql, connection, transaction))
                    {
                        var currentValue = await checkCommand.ExecuteScalarAsync();
                        if (currentValue != null)
                        {
                            Console.WriteLine($"Giá trị hiện tại của {newSequenceName}: {currentValue}");
                        }
                        else
                        {
                            Console.WriteLine($"Không tìm thấy giá trị cho sequence: {newSequenceName}");
                        }
                    }

                    // Cam kết giao dịch
                    transaction.Commit();
                    Console.WriteLine("Giao dịch đã được cam kết thành công.");
                }
                catch (Exception ex)
                {
                    // Nếu có lỗi, hoàn tác giao dịch
                    transaction.Rollback();
                    Console.WriteLine($"Lỗi khi thay thế sequence: {ex.Message}");
                    throw;
                }
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
