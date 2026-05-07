using System.Text.Json;
using CoreDesign.Identity.Server.Clients;
using CoreDesign.Identity.Server.Tests.Fakers;

namespace CoreDesign.Identity.Server.Tests;

public class JsonFileClientStoreTests : IDisposable
{
    private readonly string _filePath;
    private readonly List<ClientRecord> _records;

    public JsonFileClientStoreTests()
    {
        _records = ClientFakers.ClientRecord().Generate(3);

        _filePath = Path.GetTempFileName();
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_records,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    public void Dispose() => File.Delete(_filePath);

    [Fact]
    public async Task FindByClientIdAsync_ReturnsRecord_WhenClientIdMatches()
    {
        var store = new JsonFileClientStore(_filePath);
        var expected = _records[0];

        var result = await store.FindByClientIdAsync(expected.ClientId);

        Assert.NotNull(result);
        Assert.Equal(expected.ClientId, result.ClientId);
    }

    [Fact]
    public async Task FindByClientIdAsync_ReturnsNull_WhenClientIdNotFound()
    {
        var store = new JsonFileClientStore(_filePath);

        var result = await store.FindByClientIdAsync("no-such-client");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByClientIdAsync_IsCaseSensitive()
    {
        var store = new JsonFileClientStore(_filePath);
        var expected = _records[0];

        var result = await store.FindByClientIdAsync(expected.ClientId.ToUpperInvariant());

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByClientIdAsync_ReturnsCorrectAllowedGrantTypes()
    {
        var store = new JsonFileClientStore(_filePath);
        var expected = _records[0];

        var result = await store.FindByClientIdAsync(expected.ClientId);

        Assert.NotNull(result);
        Assert.Equal(expected.AllowedGrantTypes, result.AllowedGrantTypes);
    }

    [Fact]
    public async Task FindByClientIdAsync_ReturnsCorrectAllowedScopes()
    {
        var store = new JsonFileClientStore(_filePath);
        var expected = _records[0];

        var result = await store.FindByClientIdAsync(expected.ClientId);

        Assert.NotNull(result);
        Assert.Equal(expected.AllowedScopes, result.AllowedScopes);
    }
}
