using Microsoft.Data.SqlClient; // Thư viện để làm việc với SQL Server
using SQLServerManager.Models;
using System.Diagnostics; // Thư viện cho xử lý thông tin gỡ lỗi
using System.Text; // Thư viện cho xử lý chuỗi

namespace DatabaseSynchronizer.Services // Không gian tên cho dịch vụ đồng bộ hóa cơ sở dữ liệu
{
    public class ModelGeneratorService // Lớp dịch vụ sinh model
    {
        private readonly ILogger<ModelGeneratorService> _logger; // Logger để ghi lại thông tin
        private readonly IConfiguration _configuration; // Cấu hình ứng dụng

        // Constructor nhận vào logger và cấu hình
        public ModelGeneratorService(
            ILogger<ModelGeneratorService> logger,
            IConfiguration configuration)
        {
            _logger = logger; // Khởi tạo logger
            _configuration = configuration; // Khởi tạo cấu hình
        }

        // Phương thức bất đồng bộ để sinh models
        public async Task<bool> GenerateModelsAsync(
            string databaseName, // Tên cơ sở dữ liệu
            string projectPath, // Đường dẫn dự án
            string modelNamespace, // Không gian tên cho các model
            bool safeMode = false) // Chế độ an toàn
        {
            try
            {
                // Kiểm tra các tham số đầu vào
                if (string.IsNullOrEmpty(databaseName))
                    throw new ArgumentException("Database name cannot be empty");

                if (string.IsNullOrEmpty(projectPath))
                    throw new ArgumentException("Project path cannot be empty");

                if (string.IsNullOrEmpty(modelNamespace))
                    modelNamespace = $"{Path.GetFileName(projectPath)}.Models"; // Thiết lập không gian tên mặc định

                // Chuẩn bị chuỗi kết nối
                string connectionString = GetConnectionString(databaseName);

                // Lấy thông tin các bảng từ cơ sở dữ liệu
                var tables = await GetTablesAsync(connectionString);

                // Tạo thư mục Models nếu chưa tồn tại
                string modelsFolder = Path.Combine(projectPath, "Models");
                Directory.CreateDirectory(modelsFolder);

                // Lấy danh sách các file model hiện tại
                var existingModelFiles = Directory.GetFiles(modelsFolder, "*Model.cs")
                    .Where(f => !f.EndsWith("GlobalUsings.cs"))
                    .ToList();

                // Danh sách tên model từ database
                var databaseTableNames = new HashSet<string>(
                    tables.Select(t => $"{t.Name}Model.cs")
                );

                // Xóa các model files không tồn tại trong database
                foreach (var existingModelFile in existingModelFiles)
                {
                    string modelFileName = Path.GetFileName(existingModelFile);
                    if (!databaseTableNames.Contains(modelFileName))
                    {
                        if (safeMode)
                        {
                            _logger.LogWarning($"Would delete model file {modelFileName} in safe mode");
                            continue; // Bỏ qua việc xóa trong chế độ an toàn
                        }

                        try
                        {
                            File.Delete(existingModelFile); // Xóa file model cũ
                            _logger.LogInformation($"Deleted obsolete model file: {modelFileName}"); // Ghi thông tin xóa file
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Could not delete model file {modelFileName}"); // Ghi lỗi nếu không xóa được
                        }
                    }
                }

                // Sinh models cho từng bảng
                foreach (var table in tables)
                {
                    string modelCode = GenerateModelCode(table, modelNamespace); // Sinh mã model cho bảng
                    string modelFileName = $"{table.Name}Model.cs"; // Tên file model
                    string modelFilePath = Path.Combine(modelsFolder, modelFileName); // Đường dẫn file model

                    await File.WriteAllTextAsync(modelFilePath, modelCode); // Ghi mã vào file
                    _logger.LogInformation($"Generated/Updated model for {table.Name} at {modelFilePath}"); // Ghi thông tin thành công
                }

                // Sinh file index cho models 
                await GenerateModelIndexAsync(modelsFolder, modelNamespace, tables); // Sinh file index

                return true; // Trả về true nếu thành công
            }
            catch (Exception ex) // Bắt lỗi nếu có
            {
                _logger.LogError(ex, $"Model generation failed for database {databaseName}"); // Ghi lỗi
                return false; // Trả về false
            }
        }

        // Phương thức để lấy thông tin các bảng
        private async Task<List<TableInfo>> GetTablesAsync(string connectionString)
        {
            var tables = new List<TableInfo>(); // Danh sách các bảng

            using (var connection = new SqlConnection(connectionString)) // Tạo kết nối đến cơ sở dữ liệu
            {
                await connection.OpenAsync(); // Mở kết nối

                // Truy vấn để lấy thông tin bảng và cột
                string query = @"
            USE [@DatabaseName];
            SELECT 
                t.name AS TableName,
                c.name AS ColumnName,
                TYPE_NAME(c.user_type_id) AS DataType,
                c.is_nullable AS IsNullable
            FROM 
                sys.tables t
            INNER JOIN 
                sys.columns c ON t.object_id = c.object_id
            WHERE 
                t.is_ms_shipped = 0
            ORDER BY 
                t.name, c.column_id";

                // Thay thế @DatabaseName bằng tên database thực tế
                query = query.Replace("@DatabaseName", connection.Database);

                using (var command = new SqlCommand(query, connection)) // Tạo lệnh SQL
                {
                    using (var reader = await command.ExecuteReaderAsync()) // Thực hiện truy vấn
                    {
                        string currentTableName = null; // Biến lưu tên bảng hiện tại
                        TableInfo currentTable = null; // Biến lưu thông tin bảng hiện tại

                        while (await reader.ReadAsync()) // Đọc từng dòng kết quả
                        {
                            string tableName = reader["TableName"].ToString(); // Lấy tên bảng

                            // Nếu là bảng mới
                            if (currentTableName != tableName)
                            {
                                if (currentTable != null)
                                {
                                    tables.Add(currentTable); // Thêm bảng cũ vào danh sách
                                }

                                currentTableName = tableName; // Cập nhật tên bảng hiện tại
                                currentTable = new TableInfo // Khởi tạo bảng mới
                                {
                                    Name = tableName,
                                    Columns = new List<ColumnInfo>() // Danh sách cột
                                };
                            }

                            // Thêm cột vào bảng
                            currentTable.Columns.Add(new ColumnInfo
                            {
                                Name = reader["ColumnName"].ToString(), // Lấy tên cột
                                Type = MapSqlToDotNetType( // Lấy kiểu dữ liệu tương ứng
                                    reader["DataType"].ToString(),
                                    Convert.ToBoolean(reader["IsNullable"])
                                )
                            });
                        }

                        // Thêm bảng cuối cùng vào danh sách
                        if (currentTable != null)
                        {
                            tables.Add(currentTable);
                        }
                    }
                }
            }

            return tables; // Trả về danh sách các bảng
        }

        // Phương thức lấy chuỗi kết nối
        private string GetConnectionString(string databaseName)
        {
            // Lấy chuỗi kết nối gốc
            string baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? "Server=.;Trusted_Connection=True;TrustServerCertificate=True;";

            // Tách và thay thế Database
            var connectionStringBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = databaseName // Cập nhật tên cơ sở dữ liệu
            };

            return connectionStringBuilder.ConnectionString; // Trả về chuỗi kết nối
        }

        // Phương thức sinh mã cho model
        private string GenerateModelCode(TableInfo table, string modelNamespace)
        {
            var sb = new StringBuilder(); // StringBuilder để tạo mã

            // Các using statements
            sb.AppendLine("using System;");
            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
            sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
            sb.AppendLine();

            // Namespace cho model
            sb.AppendLine($"namespace {modelNamespace}");
            sb.AppendLine("{");

            // Khai báo lớp với thuộc tính Table
            sb.AppendLine($"    [Table(\"{table.Name}\")]");
            sb.AppendLine($"    public class {table.Name}Model");
            sb.AppendLine("    {");

            // Properties cho các cột
            foreach (var column in table.Columns)
            {
                // Thêm data annotations cho cột ID
                if (column.Name.ToLower() == "id")
                {
                    sb.AppendLine("        [Key]"); // Đánh dấu cột là khóa chính
                    sb.AppendLine("        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]"); // Tự động sinh giá trị
                }

                sb.AppendLine($"        [Column(\"{column.Name}\")]"); // Đánh dấu cột
                sb.AppendLine($"        public {column.Type} {column.Name} {{ get; set; }}"); // Khai báo thuộc tính
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString(); // Trả về mã model
        }

        // Phương thức sinh file index cho models
        private async Task GenerateModelIndexAsync(
            string modelsFolder,
            string modelNamespace,
            List<TableInfo> tables)
        {
            var sb = new StringBuilder(); // StringBuilder để tạo mã

            // Global using statements
            sb.AppendLine("global using System;");
            sb.AppendLine($"global using {modelNamespace};");
            sb.AppendLine();

            // Khai báo namespace
            sb.AppendLine($"namespace {modelNamespace};");
            sb.AppendLine("{");
            sb.AppendLine("    // Index of generated models");
            sb.AppendLine("}");

            string indexFilePath = Path.Combine(modelsFolder, "GlobalUsings.cs"); // Đường dẫn file index
            await File.WriteAllTextAsync(indexFilePath, sb.ToString()); // Ghi mã vào file
        }

        // Phương thức ánh xạ kiểu SQL sang kiểu .NET
        private string MapSqlToDotNetType(string sqlType, bool isNullable)
        {
            string baseType = sqlType.ToLower() switch
            {
                "int" => "int",
                "bigint" => "long",
                "smallint" => "short",
                "tinyint" => "byte",
                "bit" => "bool",
                "decimal" => "decimal",
                "numeric" => "decimal",
                "float" => "double",
                "real" => "float",
                "datetime" => "DateTime",
                "date" => "DateTime",
                "time" => "TimeSpan",
                "char" => "string",
                "varchar" => "string",
                "nvarchar" => "string",
                "text" => "string",
                "ntext" => "string",
                "uniqueidentifier" => "Guid",
                // Thêm các kiểu còn thiếu
                "money" => "decimal",
                "smallmoney" => "decimal",
                "binary" => "byte[]",
                "varbinary" => "byte[]",
                "image" => "byte[]",
                "xml" => "string",
                "timestamp" => "byte[]",
                "datetimeoffset" => "DateTimeOffset",
                "datetime2" => "DateTime",
                "smalldatetime" => "DateTime",
                "nchar" => "string",

                // Mặc định trả về string nếu không xác định được
                _ => "string"
            };

            // Nếu là kiểu nullable, thêm dấu hỏi
            return isNullable ? $"{baseType}?" : baseType;
        }
    }
    // Các class hỗ trợ 
    public class TableInfo
    {
        public string Name { get; set; }
        public List<ColumnInfo> Columns { get; set; }
    }

    public class ColumnInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
}
