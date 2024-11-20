namespace SQLServerManager.Models
{
    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public int MaxLength { get; set; }
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsIdentity { get; set; }
        public bool IsForeignKey { get; set; }  // Thêm thuộc tính này
        public string? ForeignKeyTable { get; set; }
        public string? ForeignKeyColumn { get; set; }
        public string? DefaultValue { get; set; }
    }


}
