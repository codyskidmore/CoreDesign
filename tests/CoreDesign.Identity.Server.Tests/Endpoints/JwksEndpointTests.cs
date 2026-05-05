using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoreDesign.Identity.Server.Tests.Fixtures;

namespace CoreDesign.Identity.Server.Tests.Endpoints;

public class JwksEndpointTests : IClassFixture<IdentityServerFixture>
{
    private readonly IdentityServerFixture _fixture;

    public JwksEndpointTests(IdentityServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetJwks_ReturnsOk()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/jwks.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetJwks_ReturnsKeysArray()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/jwks.json");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.TryGetProperty("keys", out var keys));
        Assert.Equal(JsonValueKind.Array, keys.ValueKind);
        Assert.Equal(1, keys.GetArrayLength());
    }

    [Fact]
    public async Task GetJwks_ReturnsRsaKeyType()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/jwks.json");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var key = json.GetProperty("keys")[0];

        Assert.Equal("RSA", key.GetProperty("kty").GetString());
    }

    [Fact]
    public async Task GetJwks_ReturnsExpectedKeyId()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/jwks.json");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var key = json.GetProperty("keys")[0];

        Assert.Equal(_fixture.Options.KeyId, key.GetProperty("kid").GetString());
    }

    [Fact]
    public async Task GetJwks_ReturnsSignatureUseAndAlgorithm()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/jwks.json");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var key = json.GetProperty("keys")[0];

        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
    }

    [Fact]
    public async Task GetJwks_ReturnsModulusAndExponent()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/jwks.json");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var key = json.GetProperty("keys")[0];

        Assert.False(string.IsNullOrEmpty(key.GetProperty("n").GetString()));
        Assert.False(string.IsNullOrEmpty(key.GetProperty("e").GetString()));
    }
}
