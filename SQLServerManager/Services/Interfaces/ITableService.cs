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

        Task<bool> AddColumnAsync(string database, string schema, string table, ColumnInfo column);
        Task<bool> AlterColumnAsync(string database, string schema, string table, string originalColumnName, ColumnInfo column);
        Task<bool> DeleteColumnAsync(string database, string schema, string table, string columnName);
        // them phan nay
        Task<bool> InsertRecordAsync(string database, string schema, string table, Dictionary<string, object> record);
        Task<bool> UpdateRecordAsync(string database, string schema, string table,
            Dictionary<string, object> newRecord, Dictionary<string, object> oldRecord);
        Task<bool> DeleteRecordAsync(string database, string schema, string table, Dictionary<string, object> record);

    }
}
