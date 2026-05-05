using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoreDesign.Identity.Server;
using CoreDesign.Identity.Server.Tests.Fakers;
using CoreDesign.Identity.Server.Tests.Fixtures;
using Moq;

namespace CoreDesign.Identity.Server.Tests.Endpoints;

public class TokenEndpointTests : IClassFixture<IdentityServerFixture>
{
    private readonly IdentityServerFixture _fixture;

    public TokenEndpointTests(IdentityServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.IdentityStoreMock.Reset();
    }

    private static FormUrlEncodedContent PasswordGrantForm(string username, string password) =>
        new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password)
        ]);

    [Fact]
    public async Task PostToken_WithValidPasswordGrant_ReturnsOk()
    {
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);

        var response = await _fixture.Client.PostAsync("/connect/token", PasswordGrantForm(user.Username, user.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostToken_WithValidPasswordGrant_ReturnsTokenResponse()
    {
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);

        var response = await _fixture.Client.PostAsync("/connect/token", PasswordGrantForm(user.Username, user.Password));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(json.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", json.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task PostToken_WithWrongContentType_ReturnsBadRequest()
    {
        var content = new StringContent("""{"grant_type":"password"}""", System.Text.Encoding.UTF8, "application/json");

        var response = await _fixture.Client.PostAsync("/connect/token", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostToken_WithUnsupportedGrantType_ReturnsBadRequest()
    {
        var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        ]);

        var response = await _fixture.Client.PostAsync("/connect/token", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unsupported_grant_type", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostToken_WithMissingUsername_ReturnsBadRequest()
    {
        var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("password", "somepass")
        ]);

        var response = await _fixture.Client.PostAsync("/connect/token", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostToken_WithInvalidCredentials_ReturnsBadRequest()
    {
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((IdentityRecord?)null);

        var response = await _fixture.Client.PostAsync("/connect/token", PasswordGrantForm("bad", "creds"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", json.GetProperty("error").GetString());
    }
}
