namespace SQLServerManager.Models
{
    public class TableInfo
    {
        public string Name { get; set; }
        public string Schema { get; set; }
        public int RowCount { get; set; }
        public DateTime CreateDate { get; set; }
        public long SizeKB { get; set; }
        public List<ColumnInfo> Columns { get; set; } = new();
    }
}
