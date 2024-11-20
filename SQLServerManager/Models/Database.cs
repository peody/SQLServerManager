namespace SQLServerManager.Models
{
    public class Database
    {
        public string Name { get; set; }
        public string Owner { get; set; }
        public DateTime CreateDate { get; set; }
        public long SizeMB { get; set; }
        public string RecoveryModel { get; set; }
        public string State { get; set; }
    }
}
