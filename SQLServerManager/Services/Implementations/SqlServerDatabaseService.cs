using Microsoft.Data.SqlClient; // Thư viện để làm việc với SQL Server
using SQLServerManager.Services.Interfaces; // Thư viện chứa các interface dịch vụ

namespace SQLServerManager.Services.Implementations
{
    // Lớp SqlServerDatabaseService thực hiện giao diện IDatabaseService
    public class SqlServerDatabaseService : IDatabaseService
    {
        private readonly string _connectionString; // Chuỗi kết nối đến cơ sở dữ liệu

        // Constructor nhận IConfiguration để lấy chuỗi kết nối
        public SqlServerDatabaseService(IConfiguration configuration)
        {
            // Lấy chuỗi kết nối từ cấu hình
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // Phương thức bất đồng bộ để lấy danh sách cơ sở dữ liệu
        public async Task<List<string>> GetDatabasesAsync()
        {
            var databases = new List<string>(); // Danh sách để lưu tên cơ sở dữ liệu
            using (var connection = new SqlConnection(_connectionString)) // Tạo kết nối với cơ sở dữ liệu
            {
                await connection.OpenAsync(); // Mở kết nối bất đồng bộ
                // Truy vấn để lấy tên các cơ sở dữ liệu đang trực tuyến
                string query = @"SELECT name FROM sys.databases 
                           WHERE database_id > 4 
                           AND state_desc = 'ONLINE'
                           ORDER BY name";

                using var command = new SqlCommand(query, connection); // Tạo lệnh SQL
                using var reader = await command.ExecuteReaderAsync(); // Thực thi lệnh và lấy dữ liệu

                // Đọc dữ liệu từ SqlDataReader
                while (await reader.ReadAsync())
                {
                    databases.Add(reader.GetString(0)); // Thêm tên cơ sở dữ liệu vào danh sách
                }
            }
            return databases; // Trả về danh sách cơ sở dữ liệu
        }
    }
}
