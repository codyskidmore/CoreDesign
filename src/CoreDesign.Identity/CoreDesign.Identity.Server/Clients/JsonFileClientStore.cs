using System.Text.Json;

namespace CoreDesign.Identity.Server.Clients;

public class JsonFileClientStore : IClientStore
{
    private readonly List<ClientRecord> _records;

    public JsonFileClientStore(string filePath)
    {
        var path = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(AppContext.BaseDirectory, filePath);
        _records = JsonSerializer.Deserialize<List<ClientRecord>>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public Task<ClientRecord?> FindByClientIdAsync(string clientId)
    {
        var result = _records.FirstOrDefault(c =>
            string.Equals(c.ClientId, clientId, StringComparison.Ordinal));
        return Task.FromResult(result);
    }
}
