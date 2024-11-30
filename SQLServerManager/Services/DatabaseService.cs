using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
namespace DatabaseSynchronizer.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            _connectionString = configuration
                .GetConnectionString("DefaultConnection");
        }

        public List<string> GetDatabases()
        {
            var databases = new List<string>();

            try
            {
                // Log connection string để kiểm tra
                Console.WriteLine($"Connection String: {_connectionString}");

                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();

                    // Query chi tiết hơn
                    using (var command = new SqlCommand(
                        @"SELECT name 
                  FROM sys.databases 
                  WHERE database_id > 4 
                  AND state = 0 
                  ORDER BY name", connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string dbName = reader["name"].ToString();
                                databases.Add(dbName);
                                Console.WriteLine($"Tìm thấy database: {dbName}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log chi tiết lỗi
                Console.WriteLine($"Lỗi chi tiết kết nối CSDL: {ex}");
                throw; // Ném lại exception để xem chi tiết
            }

            return databases;
        }
        // Thêm phương thức lấy connection string cho database cụ thể
        public string GetConnectionString(string databaseName)
        {
            // Logic tạo connection string dựa trên database name
            return $"Server=.;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;";
        }

    }
}

