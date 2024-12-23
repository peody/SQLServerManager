using Microsoft.Data.SqlClient; // Thư viện để làm việc với SQL Server
using Microsoft.Extensions.Configuration; // Thư viện để quản lý cấu hình

namespace DatabaseSynchronizer.Services // Định nghĩa không gian tên cho dịch vụ
{
    public class DatabaseService // Lớp dịch vụ cơ sở dữ liệu
    {
        private readonly string _connectionString; // Biến lưu trữ chuỗi kết nối

        // Constructor nhận vào IConfiguration để lấy chuỗi kết nối từ cấu hình
        public DatabaseService(IConfiguration configuration)
        {
            // Lấy chuỗi kết nối từ cấu hình với tên "DefaultConnection"
            _connectionString = configuration
                .GetConnectionString("DefaultConnection");
        }

        // Phương thức để lấy danh sách các cơ sở dữ liệu
        public List<string> GetDatabases()
        {
            var databases = new List<string>(); // Danh sách để lưu tên các cơ sở dữ liệu

            try
            {
                // Log chuỗi kết nối để kiểm tra
                Console.WriteLine($"Connection String: {_connectionString}");

                // Tạo kết nối đến cơ sở dữ liệu
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open(); // Mở kết nối

                    // Thực hiện truy vấn để lấy tên các cơ sở dữ liệu
                    using (var command = new SqlCommand(
                        @"SELECT name 
                          FROM sys.databases 
                          WHERE database_id > 4 // Lọc các cơ sở dữ liệu hệ thống
                          AND state = 0 // Chỉ lấy các cơ sở dữ liệu đang hoạt động
                          ORDER BY name", connection))
                    {
                        // Thực hiện truy vấn và đọc kết quả
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read()) // Đọc từng dòng kết quả
                            {
                                string dbName = reader["name"].ToString(); // Lấy tên cơ sở dữ liệu
                                databases.Add(dbName); // Thêm tên vào danh sách
                                Console.WriteLine($"Tìm thấy database: {dbName}"); // Log tên cơ sở dữ liệu
                            }
                        }
                    }
                }
            }
            catch (Exception ex) // Bắt lỗi nếu có
            {
                // Log chi tiết lỗi kết nối cơ sở dữ liệu
                Console.WriteLine($"Lỗi chi tiết kết nối CSDL: {ex}");
                throw; // Ném lại exception để xem chi tiết
            }

            return databases; // Trả về danh sách các cơ sở dữ liệu
        }

        // Phương thức để lấy chuỗi kết nối cho một cơ sở dữ liệu cụ thể
        public string GetConnectionString(string databaseName)
        {
            // Tạo chuỗi kết nối dựa trên tên cơ sở dữ liệu
            return $"Server=.;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;";
        }
    }
}
