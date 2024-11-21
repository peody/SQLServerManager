namespace SQLServerManager.Models
{
    public class TableRelationship
    {
        public string TableName { get; set; }
        public string ReferencedTableName { get; set; }
        public string ForeignKeyName { get; set; }
        public List<string> Columns { get; set; }
        public List<string> ReferencedColumns { get; set; }
    }
}
