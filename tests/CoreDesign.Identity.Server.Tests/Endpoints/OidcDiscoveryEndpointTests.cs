using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoreDesign.Identity.Server.Tests.Fixtures;

namespace CoreDesign.Identity.Server.Tests.Endpoints;

public class OidcDiscoveryEndpointTests : IClassFixture<IdentityServerFixture>
{
    private readonly IdentityServerFixture _fixture;

    public OidcDiscoveryEndpointTests(IdentityServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetOidcDiscovery_ReturnsOk()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetOidcDiscovery_ReturnsConfiguredIssuer()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/openid-configuration");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(_fixture.Options.Issuer, json.GetProperty("issuer").GetString());
    }

    [Fact]
    public async Task GetOidcDiscovery_ReturnsTokenEndpoint()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/openid-configuration");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var tokenEndpoint = json.GetProperty("token_endpoint").GetString();

        Assert.NotNull(tokenEndpoint);
        Assert.EndsWith("/connect/token", tokenEndpoint);
    }

    [Fact]
    public async Task GetOidcDiscovery_ReturnsJwksUri()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/openid-configuration");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jwksUri = json.GetProperty("jwks_uri").GetString();

        Assert.NotNull(jwksUri);
        Assert.EndsWith("/.well-known/jwks.json", jwksUri);
    }

    [Fact]
    public async Task GetOidcDiscovery_ReturnsSupportedResponseTypes()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/openid-configuration");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var types = json.GetProperty("response_types_supported")
            .EnumerateArray().Select(e => e.GetString()).ToArray();

        Assert.Contains("code", types);
        Assert.Contains("token", types);
    }

    [Fact]
    public async Task GetOidcDiscovery_ReturnsSupportedScopes()
    {
        var response = await _fixture.Client.GetAsync("/.well-known/openid-configuration");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var scopes = json.GetProperty("scopes_supported")
            .EnumerateArray().Select(e => e.GetString()).ToArray();

        Assert.Contains("openid", scopes);
        Assert.Contains("profile", scopes);
        Assert.Contains("email", scopes);
    }
}
