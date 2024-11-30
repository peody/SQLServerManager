using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Text;

namespace DatabaseSynchronizer.Services
{
    public class ModelGeneratorService
    {
        private readonly ILogger<ModelGeneratorService> _logger;
        private readonly IConfiguration _configuration;

        public ModelGeneratorService(
            ILogger<ModelGeneratorService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> GenerateModelsAsync(
            string databaseName,
            string projectPath,
            string modelNamespace,
            bool safeMode = false)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(databaseName))
                    throw new ArgumentException("Database name cannot be empty");

                if (string.IsNullOrEmpty(projectPath))
                    throw new ArgumentException("Project path cannot be empty");

                if (string.IsNullOrEmpty(modelNamespace))
                    modelNamespace = $"{Path.GetFileName(projectPath)}.Models";

                // Chuẩn bị connection string
                string connectionString = GetConnectionString(databaseName);

                // Lấy thông tin các bảng
                var tables = await GetTablesAsync(connectionString);

                // Tạo thư mục Models nếu chưa tồn tại
                string modelsFolder = Path.Combine(projectPath, "Models");
                Directory.CreateDirectory(modelsFolder);

                // Lấy danh sách các file model hiện tại
                var existingModelFiles = Directory.GetFiles(modelsFolder, "*Model.cs")
                    .Where(f => !f.EndsWith("GlobalUsings.cs"))
                    .ToList();

                // Danh sách model names từ database
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
                            File.Delete(existingModelFile);
                            _logger.LogInformation($"Deleted obsolete model file: {modelFileName}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Could not delete model file {modelFileName}");
                        }
                    }
                }

                // Sinh models cho từng bảng
                foreach (var table in tables)
                {
                    string modelCode = GenerateModelCode(table, modelNamespace);
                    string modelFileName = $"{table.Name}Model.cs";
                    string modelFilePath = Path.Combine(modelsFolder, modelFileName);

                    await File.WriteAllTextAsync(modelFilePath, modelCode);
                    _logger.LogInformation($"Generated/Updated model for {table.Name} at {modelFilePath}");
                }

                // Sinh file index cho models 
                await GenerateModelIndexAsync(modelsFolder, modelNamespace, tables);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Model generation failed for database {databaseName}");
                return false;
            }
        }

        private async Task<List<TableInfo>> GetTablesAsync(string connectionString)
        {
            var tables = new List<TableInfo>();

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // Thay đổi query để lấy thông tin từ database được chọn
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

                using (var command = new SqlCommand(query, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        string currentTableName = null;
                        TableInfo currentTable = null;

                        while (await reader.ReadAsync())
                        {
                            string tableName = reader["TableName"].ToString();

                            // Nếu là bảng mới
                            if (currentTableName != tableName)
                            {
                                if (currentTable != null)
                                {
                                    tables.Add(currentTable);
                                }

                                currentTableName = tableName;
                                currentTable = new TableInfo
                                {
                                    Name = tableName,
                                    Columns = new List<ColumnInfo>()
                                };
                            }

                            // Thêm cột
                            currentTable.Columns.Add(new ColumnInfo
                            {
                                Name = reader["ColumnName"].ToString(),
                                Type = MapSqlToDotNetType(
                                    reader["DataType"].ToString(),
                                    Convert.ToBoolean(reader["IsNullable"])
                                )
                            });
                        }

                        // Thêm bảng cuối cùng
                        if (currentTable != null)
                        {
                            tables.Add(currentTable);
                        }
                    }
                }
            }

            return tables;
        }

        // Cập nhật phương thức GetConnectionString
        private string GetConnectionString(string databaseName)
        {
            // Lấy connection string gốc
            string baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? "Server=.;Trusted_Connection=True;TrustServerCertificate=True;";

            // Tách và thay thế Database
            var connectionStringBuilder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = databaseName
            };

            return connectionStringBuilder.ConnectionString;
        }

        private string GenerateModelCode(TableInfo table, string modelNamespace)
        {
            var sb = new StringBuilder();

            // Using statements
            sb.AppendLine("using System;");
            sb.AppendLine("using System.ComponentModel.DataAnnotations;");
            sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
            sb.AppendLine();

            // Namespace
            sb.AppendLine($"namespace {modelNamespace}");
            sb.AppendLine("{");

            // Class declaration with table attribute
            sb.AppendLine($"    [Table(\"{table.Name}\")]");
            sb.AppendLine($"    public class {table.Name}Model");
            sb.AppendLine("    {");

            // Properties
            foreach (var column in table.Columns)
            {
                // Thêm data annotations
                if (column.Name.ToLower() == "id")
                {
                    sb.AppendLine("        [Key]");
                    sb.AppendLine("        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]");
                }

                sb.AppendLine($"        [Column(\"{column.Name}\")]");
                sb.AppendLine($"        public {column.Type} {column.Name} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private async Task GenerateModelIndexAsync(
            string modelsFolder,
            string modelNamespace,
            List<TableInfo> tables)
        {
            var sb = new StringBuilder();

            // Global using statements
            sb.AppendLine("global using System;");
            sb.AppendLine($"global using {modelNamespace};");
            sb.AppendLine();

            // Namespace declaration
            sb.AppendLine($"namespace {modelNamespace};");
            sb.AppendLine("{");
            sb.AppendLine("    // Index of generated models");
            sb.AppendLine("}");

            string indexFilePath = Path.Combine(modelsFolder, "GlobalUsings.cs");
            await File.WriteAllTextAsync(indexFilePath, sb.ToString());
        }

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
                // Thêm các type còn thiếu
                
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

            // Nếu là kiểu nullable và không phải string
            return isNullable && baseType != "string"
                ? $"{baseType}?"
                : baseType;
        }
    }

    // Các class hỗ trợ giữ nguyên như ở phiên bản trước
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
