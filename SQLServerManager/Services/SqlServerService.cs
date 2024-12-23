using Microsoft.Data.SqlClient; // Thư viện để làm việc với SQL Server
using SQLServerManager.Models; // Thư viện chứa các mô hình dữ liệu
using System.Data; // Thư viện cho các loại dữ liệu
using System.Collections.Generic; // Thư viện cho danh sách
using System.Threading.Tasks; // Thư viện cho lập trình bất đồng bộ

namespace SQLServerManager.Services // Không gian tên cho dịch vụ quản lý SQL Server
{
    // Lớp SqlServerService thực hiện các thao tác với SQL Server
    public class SqlServerService
    {
        private readonly string _connectionString; // Chuỗi kết nối đến cơ sở dữ liệu

        // Constructor nhận vào IConfiguration để lấy chuỗi kết nối
        public SqlServerService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentNullException("Connection string 'DefaultConnection' not found.");
        }

        // Phương thức bất đồng bộ để lấy danh sách cơ sở dữ liệu
        public async Task<List<Database>> GetDatabasesAsync()
        {
            var databases = new List<Database>(); // Danh sách lưu trữ cơ sở dữ liệu

            using (var connection = new SqlConnection(_connectionString)) // Tạo kết nối
            {
                await connection.OpenAsync(); // Mở kết nối

                // Câu truy vấn lấy thông tin cơ sở dữ liệu
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

                using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
                using var reader = await command.ExecuteReaderAsync(); // Thực thi lệnh và đọc dữ liệu
                while (await reader.ReadAsync()) // Đọc từng dòng kết quả
                {
                    databases.Add(new Database
                    {
                        Name = reader.GetString(0), // Tên cơ sở dữ liệu
                        CreateDate = reader.GetDateTime(1), // Ngày tạo
                        State = reader.GetString(2), // Trạng thái
                        SizeMB = (long)reader.GetDecimal(3) // Kích thước (MB)
                    });
                }
            }

            return databases; // Trả về danh sách cơ sở dữ liệu
        }

        // Phương thức bất đồng bộ để tạo một cơ sở dữ liệu mới
        public async Task CreateDatabaseAsync(string databaseName)
        {
            using var connection = new SqlConnection(_connectionString); // Tạo kết nối
            await connection.OpenAsync(); // Mở kết nối

            string query = $"CREATE DATABASE [{databaseName}]"; // Câu lệnh tạo cơ sở dữ liệu
            using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
            await command.ExecuteNonQueryAsync(); // Thực thi lệnh
        }

        // Phương thức bất đồng bộ để xóa một cơ sở dữ liệu
        public async Task DeleteDatabaseAsync(string databaseName)
        {
            using var connection = new SqlConnection(_connectionString); // Tạo kết nối
            await connection.OpenAsync(); // Mở kết nối

            // Đưa cơ sở dữ liệu vào chế độ đơn người dùng trước khi xóa
            string singleUserQuery = $@"
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}]";

            using var command = new SqlCommand(singleUserQuery, connection); // Tạo lệnh SQL
            await command.ExecuteNonQueryAsync(); // Thực thi lệnh
        }

        // Phương thức bất đồng bộ để kiểm tra xem cơ sở dữ liệu có tồn tại không
        public async Task<bool> DatabaseExistsAsync(string databaseName)
        {
            using var connection = new SqlConnection(_connectionString); // Tạo kết nối
            await connection.OpenAsync(); // Mở kết nối

            string query = @"
                SELECT COUNT(*) 
                FROM sys.databases 
                WHERE name = @name"; // Câu lệnh kiểm tra

            using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
            command.Parameters.AddWithValue("@name", databaseName); // Thêm tham số

            int count = Convert.ToInt32(await command.ExecuteScalarAsync()); // Đếm số lượng cơ sở dữ liệu
            return count > 0; // Trả về true nếu tồn tại
        }

        // Phương thức bất đồng bộ để đổi tên cơ sở dữ liệu
        public async Task RenameDatabaseAsync(string oldName, string newName)
        {
            using var connection = new SqlConnection(_connectionString); // Tạo kết nối
            await connection.OpenAsync(); // Mở kết nối

            string query = $"ALTER DATABASE [{oldName}] MODIFY NAME = [{newName}]"; // Câu lệnh đổi tên
            using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
            await command.ExecuteNonQueryAsync(); // Thực thi lệnh
        }

        // Interface định nghĩa các phương thức cho SqlServerService
        public interface ISqlServerService
        {
            Task<bool> DatabaseExistsAsync(string name); // Kiểm tra sự tồn tại của cơ sở dữ liệu
            Task CreateDatabaseAsync(string name); // Tạo cơ sở dữ liệu
        }

        // Phương thức bất đồng bộ để lấy thông tin chi tiết về cơ sở dữ liệu
        public async Task<DatabaseDetails> GetDatabaseDetailsAsync(string databaseName)
        {
            try
            {
                var details = new DatabaseDetails(); // Khởi tạo đối tượng lưu trữ thông tin chi tiết

                using var connection = new SqlConnection(_connectionString); // Tạo kết nối
                await connection.OpenAsync(); // Mở kết nối

                // Câu truy vấn lấy thông tin cơ bản của cơ sở dữ liệu
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

                using (var command = new SqlCommand(basicInfoQuery, connection)) // Tạo lệnh SQL
                {
                    command.Parameters.AddWithValue("@dbName", databaseName); // Thêm tham số
                    using var reader = await command.ExecuteReaderAsync(); // Thực thi lệnh và đọc dữ liệu
                    if (await reader.ReadAsync()) // Đọc kết quả
                    {
                        details.Name = reader.GetString(0); // Tên cơ sở dữ liệu
                        details.Owner = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1); // Chủ sở hữu
                        details.CreateDate = reader.GetDateTime(2); // Ngày tạo
                        details.RecoveryModel = reader.GetString(3); // Mô hình phục hồi
                        details.State = reader.GetString(4); // Trạng thái
                        details.SizeInMB = reader.GetInt64(5); // Kích thước (MB)
                    }
                }

                // Câu truy vấn lấy thông tin các file của cơ sở dữ liệu
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

                using (var command = new SqlCommand(filesQuery, connection)) // Tạo lệnh SQL
                {
                    command.Parameters.AddWithValue("@dbName", databaseName); // Thêm tham số
                    using var reader = await command.ExecuteReaderAsync(); // Thực thi lệnh và đọc dữ liệu
                    while (await reader.ReadAsync()) // Đọc từng dòng kết quả
                    {
                        details.Files.Add(new DatabaseFileInfo
                        {
                            Name = reader.GetString(0), // Tên file
                            PhysicalName = reader.GetString(1), // Tên vật lý
                            Type = reader.GetString(2), // Loại file
                            SizeInMB = reader.GetInt64(3), // Kích thước (MB)
                            MaxSizeInMB = reader.GetInt64(4), // Kích thước tối đa (MB)
                            Growth = reader.GetInt64(5), // Tăng trưởng
                            IsPercentGrowth = reader.GetBoolean(6) // Tăng trưởng theo phần trăm
                        });
                    }
                }

                return details; // Trả về thông tin chi tiết của cơ sở dữ liệu
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting database details: {ex.Message}", ex); // Bắt lỗi và ném ra ngoại lệ mới
            }
        }
    }
}
