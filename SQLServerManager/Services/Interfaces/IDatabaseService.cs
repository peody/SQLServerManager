namespace SQLServerManager.Services.Interfaces
{
    // Services/Interfaces/IDatabaseService.cs
    public interface IDatabaseService
    {
        Task<List<string>> GetDatabasesAsync();
    }

}
