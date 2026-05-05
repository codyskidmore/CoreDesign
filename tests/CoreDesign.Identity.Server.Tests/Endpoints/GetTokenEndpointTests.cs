using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoreDesign.Identity.Server;
using CoreDesign.Identity.Server.Tests.Fakers;
using CoreDesign.Identity.Server.Tests.Fixtures;
using Moq;

namespace CoreDesign.Identity.Server.Tests.Endpoints;

public class GetTokenEndpointTests : IClassFixture<IdentityServerFixture>
{
    private readonly IdentityServerFixture _fixture;

    public GetTokenEndpointTests(IdentityServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.IdentityStoreMock.Reset();
    }

    [Fact]
    public async Task PostGetToken_WithValidCredentials_ReturnsOk()
    {
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);

        var response = await _fixture.Client.PostAsJsonAsync("/get-token",
            new { username = user.Username, password = user.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostGetToken_WithValidCredentials_ReturnsTokenResponse()
    {
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);

        var response = await _fixture.Client.PostAsJsonAsync("/get-token",
            new { username = user.Username, password = user.Password });

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));
        Assert.Equal("Bearer", json.GetProperty("token_type").GetString());
        Assert.Equal("openid profile email", json.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task PostGetToken_WithInvalidCredentials_ReturnsBadRequest()
    {
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((IdentityRecord?)null);

        var response = await _fixture.Client.PostAsJsonAsync("/get-token",
            new { username = "bad", password = "creds" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostGetToken_WithInvalidCredentials_ReturnsOidcError()
    {
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((IdentityRecord?)null);

        var response = await _fixture.Client.PostAsJsonAsync("/get-token",
            new { username = "no", password = "one" });

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostGetToken_ReturnsExpiresIn_MatchingOptions()
    {
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);

        var response = await _fixture.Client.PostAsJsonAsync("/get-token",
            new { username = user.Username, password = user.Password });

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_fixture.Options.TokenLifetimeHours * 3600, json.GetProperty("expires_in").GetInt32());
    }
}
