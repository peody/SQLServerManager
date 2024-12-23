using Microsoft.Data.SqlClient; // Thư viện để làm việc với SQL Server
using System.Collections.Generic; // Thư viện cho danh sách
using System.Threading.Tasks; // Thư viện cho lập trình bất đồng bộ
using Microsoft.Extensions.Configuration; // Thư viện cho cấu hình ứng dụng

namespace SQLServerManager.Services // Không gian tên cho dịch vụ quản lý SQL Server
{
    public class SequenceService // Lớp dịch vụ cho các sequence
    {
        private readonly string _connectionString; // Chuỗi kết nối đến cơ sở dữ liệu

        // Constructor nhận vào cấu hình và khởi tạo chuỗi kết nối
        public SequenceService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // Phương thức bất đồng bộ để lấy danh sách các sequence
        public async Task<List<Sequence>> GetSequencesAsync(string databaseName)
        {
            var sequences = new List<Sequence>(); // Danh sách các sequence

            using (var connection = new SqlConnection(_connectionString)) // Tạo kết nối đến cơ sở dữ liệu
            {
                await connection.OpenAsync(); // Mở kết nối

                // Truy vấn để lấy thông tin về các sequence
                string query = $@"
                SELECT 
                    name, 
                    start_value AS StartValue, 
                    increment AS IncrementValue 
                FROM {databaseName}.sys.sequences";

                using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
                using var reader = await command.ExecuteReaderAsync(); // Thực hiện truy vấn

                // Đọc từng dòng kết quả
                while (await reader.ReadAsync())
                {
                    sequences.Add(new Sequence // Thêm thông tin sequence vào danh sách
                    {
                        Name = reader.GetString(0), // Tên sequence
                        StartValue = reader.GetInt64(1), // Giá trị bắt đầu
                        IncrementValue = reader.GetInt64(2) // Giá trị tăng
                    });
                }
            }

            return sequences; // Trả về danh sách các sequence
        }

        // Phương thức bất đồng bộ để tạo một sequence mới
        public async Task CreateSequenceAsync(string databaseName, string sequenceName, long startValue, long incrementValue)
        {
            var sql = $"CREATE SEQUENCE [{sequenceName}] START WITH {startValue} INCREMENT BY {incrementValue};"; // Lệnh tạo sequence
            var connectionString = $"Server=.\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

            // Kiểm tra xem sequence đã tồn tại chưa
            if (await SequenceExistsAsync(databaseName, sequenceName))
            {
                throw new InvalidOperationException($"Sequence '{sequenceName}' already exists in database '{databaseName}'."); // Ném ngoại lệ nếu đã tồn tại
            }

            using (var connection = new SqlConnection(connectionString)) // Tạo kết nối đến cơ sở dữ liệu
            {
                await connection.OpenAsync(); // Mở kết nối
                using (var command = new SqlCommand(sql, connection)) // Tạo lệnh SQL
                {
                    await command.ExecuteNonQueryAsync(); // Thực hiện lệnh tạo sequence
                }
            }
        }

        // Phương thức bất đồng bộ để xóa một sequence
        public async Task DropSequenceAsync(string databaseName, string sequenceName)
        {
            var connectionString = $"Server=.\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

            using (var connection = new SqlConnection(connectionString)) // Tạo kết nối đến cơ sở dữ liệu
            {
                await connection.OpenAsync(); // Mở kết nối

                // Kiểm tra sự tồn tại của sequence
                var checkCommand = new SqlCommand($"USE {databaseName}; SELECT COUNT(*) FROM sys.sequences WHERE name = '{sequenceName}';", connection);
                var exists = (int)await checkCommand.ExecuteScalarAsync() > 0; // Kiểm tra nếu sequence tồn tại

                if (!exists)
                {
                    throw new Exception($"Sequence '{sequenceName}' does not exist."); // Ném ngoại lệ nếu không tồn tại
                }

                // Thực hiện lệnh xóa
                var dropCommand = new SqlCommand($"USE {databaseName}; DROP SEQUENCE {sequenceName};", connection);
                await dropCommand.ExecuteNonQueryAsync(); // Thực hiện lệnh xóa sequence
            }
        }

        // Phương thức bất đồng bộ để lấy giá trị hiện tại của một sequence
        public async Task<long> GetCurrentValueAsync(string databaseName, string sequenceName)
        {
            using (var connection = new SqlConnection(_connectionString)) // Tạo kết nối đến cơ sở dữ liệu
            {
                await connection.OpenAsync(); // Mở kết nối
                string query = $"SELECT CURRENT_VALUE FROM {databaseName}.sys.sequences WHERE name = @sequenceName"; // Truy vấn lấy giá trị hiện tại
                using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
                command.Parameters.AddWithValue("@sequenceName", sequenceName); // Thêm tham số

                var result = await command.ExecuteScalarAsync(); // Thực hiện truy vấn
                return result != null ? (long)result : 0; // Trả về giá trị hiện tại hoặc 0 nếu không tìm thấy
            }
        }

        // Phương thức kiểm tra sự tồn tại của một sequence
        private async Task<bool> SequenceExistsAsync(string databaseName, string sequenceName)
        {
            using (var connection = new SqlConnection(_connectionString)) // Tạo kết nối đến cơ sở dữ liệu
            {
                await connection.OpenAsync(); // Mở kết nối
                string query = $"SELECT COUNT(*) FROM {databaseName}.sys.sequences WHERE name = @sequenceName"; // Truy vấn kiểm tra
                using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
                command.Parameters.AddWithValue("@sequenceName", sequenceName); // Thêm tham số

                var count = (int)await command.ExecuteScalarAsync(); // Thực hiện truy vấn
                return count > 0; // Trả về true nếu sequence tồn tại
            }
        }

        // Phương thức bất đồng bộ để thay thế một sequence
        public async Task ReplaceSequenceAsync(string databaseName, string oldSequenceName, string newSequenceName, long startValue, long incrementValue)
        {
            var connectionString = $"Server=.\\SQLEXPRESS;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

            using (var connection = new SqlConnection(connectionString)) // Tạo kết nối đến cơ sở dữ liệu
            {
                await connection.OpenAsync(); // Mở kết nối

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
                        await alterCommand.ExecuteNonQueryAsync(); // Thực hiện lệnh cập nhật sequence
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
                            await createCommand.ExecuteNonQueryAsync(); // Thực hiện lệnh tạo sequence
                            Console.WriteLine($"Đã tạo sequence mới: {newSequenceName}");
                        }

                        // Xóa sequence cũ
                        string dropOldSql = $"DROP SEQUENCE IF EXISTS [{oldSequenceName}];";
                        using (var dropCommand = new SqlCommand(dropOldSql, connection, transaction))
                        {
                            await dropCommand.ExecuteNonQueryAsync(); // Thực hiện lệnh xóa sequence cũ
                            Console.WriteLine($"Đã xóa sequence cũ: {oldSequenceName}");
                        }
                    }

                    // Kiểm tra giá trị hiện tại của sequence mới
                    string checkUpdatedSql = $"SELECT current_value FROM sys.sequences WHERE name = '{newSequenceName}';";
                    using (var checkCommand = new SqlCommand(checkUpdatedSql, connection, transaction))
                    {
                        var currentValue = await checkCommand.ExecuteScalarAsync(); // Lấy giá trị hiện tại
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
                    transaction.Rollback(); // Hoàn tác giao dịch
                    Console.WriteLine($"Lỗi trong quá trình thay thế sequence: {ex.Message}"); // Ghi lỗi
                }
            }
        }
    }

    // Định nghĩa lớp Sequence để lưu trữ thông tin về sequence
    public class Sequence
    {
        public string Name { get; set; } // Tên sequence
        public long StartValue { get; set; } // Giá trị bắt đầu
        public long IncrementValue { get; set; } // Giá trị tăng
    }
}
