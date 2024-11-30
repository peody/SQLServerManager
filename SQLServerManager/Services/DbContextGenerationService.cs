using DatabaseSynchronizer.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SQLServerManager.Services
{
    public class DbContextGenerationService
    {
        private readonly ILogger<DbContextGenerationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ModelGeneratorService _modelGeneratorService;

        public DbContextGenerationService(
            ILogger<DbContextGenerationService> logger,
            IConfiguration configuration,
            ModelGeneratorService modelGeneratorService)
        {
            _logger = logger;
            _configuration = configuration;
            _modelGeneratorService = modelGeneratorService;
        }

        public async Task<bool> GenerateDbContextAsync(
            string databaseName,
            string projectPath,
            string contextNamespace,
            bool safeMode = false)
        {
            try
            {
                // Bước 1: Sinh Models
                bool modelsGenerated = await _modelGeneratorService.GenerateModelsAsync(
                    databaseName,
                    projectPath,
                    contextNamespace + ".Models",
                    safeMode
                );

                if (!modelsGenerated)
                {
                    _logger.LogError("Không thể sinh Models");
                    return false;
                }

                // Bước 2: Sinh DbContext
                string dbContextCode = GenerateDbContextCode(databaseName, contextNamespace, projectPath);
                string dataFolder = Path.Combine(projectPath, "Data");
                Directory.CreateDirectory(dataFolder);

                string dbContextFilePath = Path.Combine(dataFolder, $"{databaseName}DbContext.cs");
                await File.WriteAllTextAsync(dbContextFilePath, dbContextCode);

                // Bước 3: Sinh Dependency Injection Extensions
                string diExtensionsCode = GenerateDiExtensionsCode(databaseName, contextNamespace);
                string diFilePath = Path.Combine(dataFolder, "DataServiceExtensions.cs");
                await File.WriteAllTextAsync(diFilePath, diExtensionsCode);

                _logger.LogInformation($"Đã sinh DbContext cho database {databaseName}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi sinh DbContext cho {databaseName}");
                return false;
            }
        }

        // Phương thức sinh DbContext sử dụng tên model
        private string GenerateDbContextCode(string databaseName, string contextNamespace, string projectPath)
        {
            var sb = new StringBuilder();

            // Using statements
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
            sb.AppendLine($"using {contextNamespace}.Models;");
            sb.AppendLine();

            // Namespace
            sb.AppendLine($"namespace {contextNamespace}.Data");
            sb.AppendLine("{");

            // DbContext class
            sb.AppendLine($"    public class {databaseName}DbContext : DbContext");
            sb.AppendLine("    {");

            // Constructor
            sb.AppendLine($"        public {databaseName}DbContext(DbContextOptions<{databaseName}DbContext> options)");
            sb.AppendLine("            : base(options)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Lấy danh sách tên model
            var modelNames = FindModelNames(projectPath, contextNamespace);

            foreach (var modelName in modelNames)
            {
                // Tạo tên DbSet thông minh hơn
                string dbSetName = GetDbSetName(modelName);
                sb.AppendLine($"        public DbSet<{modelName}> {dbSetName} {{ get; set; }}");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
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
                ? modelName.Substring(0, modelName.Length - 1) + "ies"  // Category -> Categories
                : modelName + "s";  // User -> Users
        }

        private List<string> FindModelNames(string projectPath, string contextNamespace)
        {
            var modelNames = new List<string>();
            string modelDirectory = Path.Combine(projectPath, "Models");

            if (!Directory.Exists(modelDirectory))
            {
                _logger.LogWarning($"Không tìm thấy thư mục Models tại: {modelDirectory}");
                return modelNames;
            }

            // Tìm tất cả file .cs trong thư mục Models
            var modelFiles = Directory.GetFiles(modelDirectory, "*.cs", SearchOption.AllDirectories);

            foreach (var filePath in modelFiles)
            {
                try
                {
                    string fileContent = File.ReadAllText(filePath);
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // Sử dụng regex để parse class name
                    var classNameMatch = Regex.Match(fileContent,
                        @$"namespace\s+{Regex.Escape(contextNamespace)}\.Models\s*{{[^}}]*class\s+({fileName})\s*(?::\s*\w+)?{{",
                        RegexOptions.Singleline);

                    if (classNameMatch.Success)
                    {
                        string className = classNameMatch.Groups[1].Value;

                        // Loại trừ abstract class và interface
                        if (!fileContent.Contains("abstract") &&
                            !fileContent.Contains("interface"))
                        {
                            modelNames.Add(className);
                            _logger.LogInformation($"Tìm thấy model: {className}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi xử lý file: {filePath}");
                }
            }

            _logger.LogInformation($"Tổng số model tìm được: {modelNames.Count}");
            return modelNames;
        }

        private string GenerateDiExtensionsCode(string databaseName, string contextNamespace)
        {
            var sb = new StringBuilder();

            // Using statements
            sb.AppendLine("using Microsoft.EntityFrameworkCore;");
            sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
            sb.AppendLine($"using {contextNamespace}.Data;");
            sb.AppendLine();

            // Namespace
            sb.AppendLine($"namespace {contextNamespace}.Data");
            sb.AppendLine("{");

            // Extension class
            sb.AppendLine("    public static class DataServiceExtensions");
            sb.AppendLine("    {");

            // Extension method
            sb.AppendLine($"        public static IServiceCollection Add{databaseName}Context(");
            sb.AppendLine("            this IServiceCollection services,");
            sb.AppendLine("            string connectionString)");
            sb.AppendLine("        {");
            sb.AppendLine("            services.AddDbContext<" +
                $"{databaseName}DbContext>(options =>");
            sb.AppendLine("                options.UseSqlServer(connectionString));");
            sb.AppendLine();
            sb.AppendLine("            return services;");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }

}
