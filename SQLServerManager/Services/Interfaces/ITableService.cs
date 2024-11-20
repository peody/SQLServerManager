// ITableService.cs
using SQLServerManager.Models;
namespace SQLServerManager.Services.Interfaces
{
    public interface ITableService
    {
        Task<List<TableInfo>> GetTablesAsync(string databaseName);
        Task<TableInfo> GetTableDetailsAsync(string databaseName, string schema, string tableName);
        Task<bool> DeleteTableAsync(string databaseName, string schema, string tableName);
        Task<QueryResult> QueryTableDataAsync(string databaseName, string schema, string tableName,
            int page = 1, int pageSize = 50, string orderBy = null);
        Task<bool> CreateTableAsync(string databaseName, TableInfo tableInfo);
        
            Task<List<ColumnInfo>> GetColumnsAsync(string databaseName, string schemaName, string tableName);
            Task<List<Dictionary<string, object>>> GetTableDataAsync(string databaseName, string schemaName, string tableName);
        

        Task<bool> AddColumnAsync(string databaseName, string schema, string tableName, ColumnInfo column);
        Task<bool> DeleteColumnAsync(string databaseName, string schema, string tableName, string columnName);
    }
}
