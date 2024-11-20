namespace SQLServerManager.Models
{
    // Models/DatabaseDetails.cs
    public class DatabaseDetails
    {
        public string Name { get; set; }
        public string Owner { get; set; }
        public DateTime CreateDate { get; set; }
        public string RecoveryModel { get; set; }
        public string State { get; set; }
        public long SizeInMB { get; set; }
        public List<DatabaseFileInfo> Files { get; set; } = new();
    }

    public class DatabaseFileInfo
    {
        public string Name { get; set; }
        public string PhysicalName { get; set; }
        public string Type { get; set; }
        public long SizeInMB { get; set; }
        public long MaxSizeInMB { get; set; }
        public long Growth { get; set; }
        public bool IsPercentGrowth { get; set; }
    }

}
