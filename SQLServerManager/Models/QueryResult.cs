namespace SQLServerManager.Models
{
    public class QueryResult
    {
        public List<string> Columns { get; set; } = new();
        public List<List<object>> Rows { get; set; } = new();
        public int TotalRows { get; set; }
    }
}
