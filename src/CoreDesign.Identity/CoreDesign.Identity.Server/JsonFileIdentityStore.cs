using System.Text.Json;

namespace CoreDesign.Identity.Server;

public class JsonFileIdentityStore : IIdentityStore
{
    private readonly List<IdentityRecord> _records;

    public JsonFileIdentityStore(string filePath)
    {
        var path = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(AppContext.BaseDirectory, filePath);
        _records = JsonSerializer.Deserialize<List<IdentityRecord>>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public Task<IdentityRecord?> FindByCredentialsAsync(string username, string password)
    {
        var result = _records.FirstOrDefault(i =>
            string.Equals(i.Username, username, StringComparison.OrdinalIgnoreCase) &&
            i.Password == password);
        return Task.FromResult(result);
    }

    public Task<IdentityRecord?> FindByIdAsync(string userId)
    {
        var result = _records.FirstOrDefault(i => i.UserId == userId);
        return Task.FromResult(result);
    }
}
