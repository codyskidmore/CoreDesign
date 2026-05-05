using System.Text.Json;
using CoreDesign.Identity.Server;
using CoreDesign.Identity.Server.Tests.Fakers;

namespace CoreDesign.Identity.Server.Tests;

public class JsonFileIdentityStoreTests : IDisposable
{
    private readonly string _filePath;
    private readonly List<IdentityRecord> _records;

    public JsonFileIdentityStoreTests()
    {
        _records = IdentityFakers.IdentityRecord().Generate(3);
        _records[0].Username = "Alice";
        _records[0].Password = "secret123";
        _records[1].Username = "Bob";
        _records[1].Password = "bobpass";

        _filePath = Path.GetTempFileName();
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_records,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    public void Dispose() => File.Delete(_filePath);

    [Fact]
    public async Task FindByCredentialsAsync_ReturnsRecord_WhenCredentialsMatch()
    {
        var store = new JsonFileIdentityStore(_filePath);

        var result = await store.FindByCredentialsAsync("Alice", "secret123");

        Assert.NotNull(result);
        Assert.Equal("Alice", result.Username);
    }

    [Fact]
    public async Task FindByCredentialsAsync_ReturnsNull_WhenPasswordWrong()
    {
        var store = new JsonFileIdentityStore(_filePath);

        var result = await store.FindByCredentialsAsync("Alice", "wrongpassword");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByCredentialsAsync_ReturnsNull_WhenUsernameUnknown()
    {
        var store = new JsonFileIdentityStore(_filePath);

        var result = await store.FindByCredentialsAsync("Unknown", "secret123");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByCredentialsAsync_IsCaseInsensitive_ForUsername()
    {
        var store = new JsonFileIdentityStore(_filePath);

        var result = await store.FindByCredentialsAsync("ALICE", "secret123");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task FindByCredentialsAsync_IsCaseSensitive_ForPassword()
    {
        var store = new JsonFileIdentityStore(_filePath);

        var result = await store.FindByCredentialsAsync("Alice", "Secret123");

        Assert.Null(result);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsRecord_WhenIdExists()
    {
        var store = new JsonFileIdentityStore(_filePath);
        var userId = _records[0].UserId;

        var result = await store.FindByIdAsync(userId);

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task FindByIdAsync_ReturnsNull_WhenIdNotFound()
    {
        var store = new JsonFileIdentityStore(_filePath);

        var result = await store.FindByIdAsync(Guid.NewGuid().ToString());

        Assert.Null(result);
    }
}
