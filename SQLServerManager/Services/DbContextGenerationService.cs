using DatabaseSynchronizer.Services; // Thư viện chứa dịch vụ đồng bộ hóa cơ sở dữ liệu
using Microsoft.CodeAnalysis; // Thư viện cho phân tích mã nguồn
using Microsoft.CodeAnalysis.CSharp; // Thư viện cho phân tích mã C#
using System.Reflection; // Thư viện cho phản chiếu
using System.Text; // Thư viện cho xử lý chuỗi
using System.Text.RegularExpressions; // Thư viện cho xử lý biểu thức chính quy

namespace SQLServerManager.Services // Không gian tên cho dịch vụ quản lý SQL Server
{
    public class DbContextGenerationService // Lớp dịch vụ sinh DbContext
    {
        private readonly ILogger<DbContextGenerationService> _logger; // Logger để ghi lại thông tin
        private readonly IConfiguration _configuration; // Cấu hình ứng dụng
        private readonly ModelGeneratorService _modelGeneratorService; // Dịch vụ sinh model

        // Constructor nhận vào các dịch vụ cần thiết
        public DbContextGenerationService(
            ILogger<DbContextGenerationService> logger,
            IConfiguration configuration,
            ModelGeneratorService modelGeneratorService)
        {
            _logger = logger; // Khởi tạo logger
            _configuration = configuration; // Khởi tạo cấu hình
            _modelGeneratorService = modelGeneratorService; // Khởi tạo dịch vụ sinh model
        }

        // Phương thức bất đồng bộ để sinh DbContext
        public async Task<bool> GenerateDbContextAsync(
            string databaseName, // Tên cơ sở dữ liệu
            string projectPath, // Đường dẫn dự án
            string contextNamespace, // Không gian tên cho DbContext
            bool safeMode = false) // Chế độ an toàn
        {
            try
            {
                // Bước 1: Sinh Models
                bool modelsGenerated = await _modelGeneratorService.GenerateModelsAsync(
                    databaseName,
                    projectPath,
                    contextNamespace + ".Models", // Không gian tên cho models
                    safeMode
                );

                if (!modelsGenerated) // Kiểm tra nếu không sinh được models
                {
                    _logger.LogError("Không thể sinh Models"); // Ghi lỗi
                    return false; // Trả về false
                }

                // Bước 2: Sinh DbContext
                string dbContextCode = GenerateDbContextCode(databaseName, contextNamespace, projectPath); // Sinh mã DbContext
                string dataFolder = Path.Combine(projectPath, "Data"); // Thư mục Data
                Directory.CreateDirectory(dataFolder); // Tạo thư mục nếu chưa tồn tại

                string dbContextFilePath = Path.Combine(dataFolder, $"{databaseName}DbContext.cs"); // Đường dẫn file DbContext
                await File.WriteAllTextAsync(dbContextFilePath, dbContextCode); // Ghi mã vào file

                // Bước 3: Sinh Dependency Injection Extensions
                string diExtensionsCode = GenerateDiExtensionsCode(databaseName, contextNamespace); // Sinh mã DI Extensions
                string diFilePath = Path.Combine(dataFolder, "DataServiceExtensions.cs"); // Đường dẫn file DI Extensions
                await File.WriteAllTextAsync(diFilePath, diExtensionsCode); // Ghi mã vào file

                _logger.LogInformation($"Đã sinh DbContext cho database {databaseName}"); // Ghi thông tin thành công
                return true; // Trả về true
            }
            catch (Exception ex) // Bắt lỗi nếu có
            {
                _logger.LogError(ex, $"Lỗi sinh DbContext cho {databaseName}"); // Ghi lỗi
                return false; // Trả về false
            }
        }

        // Phương thức sinh mã DbContext
        private string GenerateDbContextCode(string databaseName, string contextNamespace, string projectPath)
        {
            var sb = new StringBuilder(); // StringBuilder để tạo mã

            // Thêm các using statements
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
            sb.AppendLine($"using {contextNamespace}.Models;");
            sb.AppendLine();

            // Namespace cho DbContext
            sb.AppendLine($"namespace {contextNamespace}.Data");
            sb.AppendLine("{");

            // Định nghĩa lớp DbContext
            sb.AppendLine($"    public class {databaseName}DbContext : DbContext");
            sb.AppendLine("    {");

            // Constructor cho DbContext
            sb.AppendLine($"        public {databaseName}DbContext(DbContextOptions<{databaseName}DbContext> options)");
            sb.AppendLine("            : base(options)"); // Gọi constructor của lớp cha
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Lấy danh sách tên model
            var modelNames = FindModelNames(projectPath, contextNamespace);

            foreach (var modelName in modelNames) // Duyệt qua từng tên model
            {
                // Tạo tên DbSet thông minh hơn
                string dbSetName = GetDbSetName(modelName);
                sb.AppendLine($"        public DbSet<{modelName}> {dbSetName} {{ get; set; }}"); // Định nghĩa DbSet
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString(); // Trả về mã DbContext
        }

        // Hàm hỗ trợ tạo tên DbSet
        private string GetDbSetName(string modelName)
        {
            // Loại bỏ hậu tố Model nếu có
            if (modelName.EndsWith("Model"))
            {
                modelName = modelName.Substring(0, modelName.Length - 5);
            }

            // Chuyển sang dạng số nhiều
            return modelName.EndsWith("y")
                ? modelName.Substring(0, modelName.Length - 1) + "ies"  // Ví dụ: Category -> Categories
                : modelName + "s";  // Ví dụ: User -> Users
        }

        // Tìm kiếm tên model trong thư mục Models
        private List<string> FindModelNames(string projectPath, string contextNamespace)
        {
            var modelNames = new List<string>(); // Danh sách tên model
            string modelDirectory = Path.Combine(projectPath, "Models"); // Đường dẫn thư mục Models

            if (!Directory.Exists(modelDirectory)) // Kiểm tra nếu thư mục không tồn tại
            {
                _logger.LogWarning($"Không tìm thấy thư mục Models tại: {modelDirectory}"); // Ghi cảnh báo
                return modelNames; // Trả về danh sách rỗng
            }

            // Tìm tất cả file .cs trong thư mục Models
            var modelFiles = Directory.GetFiles(modelDirectory, "*.cs", SearchOption.AllDirectories);

            foreach (var filePath in modelFiles) // Duyệt qua từng file
            {
                try
                {
                    string fileContent = File.ReadAllText(filePath); // Đọc nội dung file
                    string fileName = Path.GetFileNameWithoutExtension(filePath); // Lấy tên file

                    // Sử dụng regex để parse class name
                    var classNameMatch = Regex.Match(fileContent,
                        @$"namespace\s+{Regex.Escape(contextNamespace)}\.Models\s*{{[^}}]*class\s+({fileName})\s*(?::\s*\w+)?{{",
                        RegexOptions.Singleline);

                    if (classNameMatch.Success) // Kiểm tra nếu tìm thấy tên class
                    {
                        string className = classNameMatch.Groups[1].Value; // Lấy tên class

                        // Loại trừ abstract class và interface
                        if (!fileContent.Contains("abstract") && !fileContent.Contains("interface"))
                        {
                            modelNames.Add(className); // Thêm tên class vào danh sách
                            _logger.LogInformation($"Tìm thấy model: {className}"); // Ghi thông tin tìm thấy model
                        }
                    }
                }
                catch (Exception ex) // Bắt lỗi nếu có
                {
                    _logger.LogError(ex, $"Lỗi xử lý file: {filePath}"); // Ghi lỗi
                }
            }

            _logger.LogInformation($"Tổng số model tìm được: {modelNames.Count}"); // Ghi tổng số model tìm được
            return modelNames; // Trả về danh sách tên model
        }

        // Phương thức sinh mã DI Extensions
        private string GenerateDiExtensionsCode(string databaseName, string contextNamespace)
        {
            var sb = new StringBuilder(); // StringBuilder để tạo mã

            // Thêm các using statements
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine($"using {contextNamespace}.Data;");
            sb.AppendLine();

            // Namespace cho DI Extensions
            sb.AppendLine($"namespace {contextNamespace}.Data");
            sb.AppendLine("{");

            // Định nghĩa lớp mở rộng
            sb.AppendLine("    public static class DataServiceExtensions");
            sb.AppendLine("    {");

            // Phương thức mở rộng để thêm DbContext vào DI container
            sb.AppendLine($"        public static IServiceCollection Add{databaseName}Context(");
            sb.AppendLine("            this IServiceCollection services,");
            sb.AppendLine("            string connectionString)"); // Tham số là chuỗi kết nối
            sb.AppendLine("        {");
            sb.AppendLine($"            services.AddDbContext<{databaseName}DbContext>(options =>"); // Thêm DbContext
            sb.AppendLine("                options.UseSqlServer(connectionString));"); // Sử dụng SQL Server
            sb.AppendLine("            return services;"); // Trả về services
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString(); // Trả về mã DI Extensions
        }
    }
}
