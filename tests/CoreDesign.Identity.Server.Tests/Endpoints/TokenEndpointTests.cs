using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoreDesign.Identity.Server;
using CoreDesign.Identity.Server.Clients;
using CoreDesign.Identity.Server.Tests.Fakers;
using CoreDesign.Identity.Server.Tests.Fixtures;
using Moq;

namespace CoreDesign.Identity.Server.Tests.Endpoints;

public class TokenEndpointTests : IClassFixture<IdentityServerFixture>
{
    private const string TestClientId = "test-client";

    private readonly IdentityServerFixture _fixture;

    public TokenEndpointTests(IdentityServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.IdentityStoreMock.Reset();
        _fixture.ClientStoreMock.Reset();
    }

    private void SetupValidClient(string clientId = TestClientId, string[]? grantTypes = null)
    {
        var client = ClientFakers.ClientRecord(grantTypes).Generate();
        _fixture.ClientStoreMock
            .Setup(s => s.FindByClientIdAsync(clientId))
            .ReturnsAsync(client with { ClientId = clientId });
    }

    private static FormUrlEncodedContent PasswordGrantForm(
        string username, string password, string? clientId = TestClientId) =>
        new([
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
            .. clientId is not null
                ? new[] { new KeyValuePair<string, string>("client_id", clientId) }
                : Array.Empty<KeyValuePair<string, string>>()
        ]);

    // --- Happy path ---

    [Fact]
    public async Task PostToken_WithValidPasswordGrant_ReturnsOk()
    {
        SetupValidClient();
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
        SetupValidClient();
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);

        var response = await _fixture.Client.PostAsync("/connect/token", PasswordGrantForm(user.Username, user.Password));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(json.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", json.GetProperty("token_type").GetString());
    }

    // --- Content type ---

    [Fact]
    public async Task PostToken_WithWrongContentType_ReturnsBadRequest()
    {
        var content = new StringContent("""{"grant_type":"password"}""", System.Text.Encoding.UTF8, "application/json");

        var response = await _fixture.Client.PostAsync("/connect/token", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_request", json.GetProperty("error").GetString());
    }

    // --- Client validation ---

    [Fact]
    public async Task PostToken_WithMissingClientId_Returns401()
    {
        var user = IdentityFakers.IdentityRecord().Generate();
        var response = await _fixture.Client.PostAsync("/connect/token",
            PasswordGrantForm(user.Username, user.Password, clientId: null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_client", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostToken_WithUnknownClientId_Returns401()
    {
        _fixture.ClientStoreMock
            .Setup(s => s.FindByClientIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ClientRecord?)null);

        var user = IdentityFakers.IdentityRecord().Generate();
        var response = await _fixture.Client.PostAsync("/connect/token",
            PasswordGrantForm(user.Username, user.Password, clientId: "no-such-client"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_client", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostToken_WithClientNotAuthorizedForGrant_ReturnsBadRequest()
    {
        SetupValidClient(grantTypes: ["authorization_code"]);

        var form = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", TestClientId),
            new KeyValuePair<string, string>("username", "user"),
            new KeyValuePair<string, string>("password", "pass")
        ]);

        var response = await _fixture.Client.PostAsync("/connect/token", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unauthorized_client", json.GetProperty("error").GetString());
    }

    // --- Grant type ---

    [Fact]
    public async Task PostToken_WithUnsupportedGrantType_ReturnsBadRequest()
    {
        SetupValidClient();

        var form = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", TestClientId)
        ]);

        var response = await _fixture.Client.PostAsync("/connect/token", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("unsupported_grant_type", json.GetProperty("error").GetString());
    }

    // --- Password grant field validation ---

    [Fact]
    public async Task PostToken_WithMissingUsername_ReturnsBadRequest()
    {
        SetupValidClient();

        var form = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", TestClientId),
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
        SetupValidClient();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((IdentityRecord?)null);

        var response = await _fixture.Client.PostAsync("/connect/token", PasswordGrantForm("bad", "creds"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", json.GetProperty("error").GetString());
    }
}
