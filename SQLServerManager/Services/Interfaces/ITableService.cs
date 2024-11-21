// ITableService.cs
using SQLServerManager.Models; // Thư viện chứa các mô hình dữ liệu
namespace SQLServerManager.Services.Interfaces
{
    // Interface định nghĩa các phương thức cho dịch vụ bảng
    public interface ITableService
    {
        // Phương thức bất đồng bộ để lấy danh sách các bảng trong cơ sở dữ liệu
        Task<List<TableInfo>> GetTablesAsync(string databaseName);

        // Phương thức bất đồng bộ để lấy thông tin chi tiết về một bảng
        Task<TableInfo> GetTableDetailsAsync(string databaseName, string schema, string tableName);

        // Phương thức bất đồng bộ để xóa một bảng
        Task<bool> DeleteTableAsync(string databaseName, string schema, string tableName);

        // Phương thức bất đồng bộ để truy vấn dữ liệu bảng với phân trang
        Task<QueryResult> QueryTableDataAsync(string databaseName, string schema, string tableName,
            int page = 1, int pageSize = 50, string orderBy = null);

        // Phương thức bất đồng bộ để tạo một bảng mới
        Task<bool> CreateTableAsync(string databaseName, TableInfo tableInfo);

        // Phương thức bất đồng bộ để lấy thông tin về các cột của bảng
        Task<List<ColumnInfo>> GetColumnsAsync(string databaseName, string schemaName, string tableName);

        // Phương thức bất đồng bộ để lấy dữ liệu từ bảng
        Task<List<Dictionary<string, object>>> GetTableDataAsync(string databaseName, string schemaName, string tableName);

        // Phương thức bất đồng bộ để thêm cột vào bảng
        Task<bool> AddColumnAsync(string database, string schema, string table, ColumnInfo column);

        // Phương thức bất đồng bộ để thay đổi cột trong bảng
        Task<bool> AlterColumnAsync(string database, string schema, string table, string originalColumnName, ColumnInfo column);

        // Phương thức bất đồng bộ để xóa cột trong bảng
        Task<bool> DeleteColumnAsync(string database, string schema, string table, string columnName);

        // Phương thức bất đồng bộ để chèn một bản ghi mới vào bảng
        Task<bool> InsertRecordAsync(string database, string schema, string table, Dictionary<string, object> record);

        // Phương thức bất đồng bộ để cập nhật một bản ghi trong bảng
        Task<bool> UpdateRecordAsync(string database, string schema, string table,
            Dictionary<string, object> newRecord, Dictionary<string, object> oldRecord);

        // Phương thức bất đồng bộ để xóa một bản ghi trong bảng
        Task<bool> DeleteRecordAsync(string database, string schema, string table, Dictionary<string, object> record);
    }
}
