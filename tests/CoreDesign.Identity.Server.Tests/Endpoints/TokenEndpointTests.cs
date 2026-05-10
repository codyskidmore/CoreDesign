using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using CoreDesign.Identity.Server;
using CoreDesign.Identity.Server.Clients;
using CoreDesign.Identity.Server.Tests.Fakers;
using CoreDesign.Identity.Server.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;
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

    private static FormUrlEncodedContent AuthorizationCodeGrantForm(
        string code,
        string redirectUri,
        string codeVerifier,
        string clientId = TestClientId) =>
        new([
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri),
            new KeyValuePair<string, string>("code_verifier", codeVerifier)
        ]);

    private void SetupAuthorizationCodeClient(string redirectUri, bool requirePkce = true)
    {
        var client = new ClientRecord
        {
            ClientId = TestClientId,
            ClientSecret = null,
            TokenEndpointAuthMethod = "none",
            AllowedGrantTypes = ["authorization_code"],
            AllowedRedirectUris = [redirectUri],
            AllowedScopes = ["openid", "profile", "email"],
            RequirePkce = requirePkce
        };

        _fixture.ClientStoreMock
            .Setup(s => s.FindByClientIdAsync(TestClientId))
            .ReturnsAsync(client);
    }

    private static (string Verifier, string Challenge) BuildPkce()
    {
        var verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~abcd";
        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private async Task<string> CreateAuthorizationCodeAsync(IdentityRecord user, string redirectUri, string codeChallenge, string? nonce = null)
    {
        var nonceParam = nonce is not null ? $"&nonce={Uri.EscapeDataString(nonce)}" : string.Empty;
        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id={TestClientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=openid%20profile%20email&state=test-state&code_challenge={codeChallenge}&code_challenge_method=S256&username={Uri.EscapeDataString(user.Username)}&password={Uri.EscapeDataString(user.Password)}{nonceParam}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);

        var query = HttpUtility.ParseQueryString(location!.Query);
        var code = query["code"];
        Assert.False(string.IsNullOrWhiteSpace(code));
        return code!;
    }

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

    [Fact]
    public async Task PostToken_WithValidAuthorizationCodeGrant_ReturnsOk()
    {
        var redirectUri = "https://localhost:7070/signin-oidc";
        SetupAuthorizationCodeClient(redirectUri);

        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByIdAsync(user.UserId))
            .ReturnsAsync(user);

        var (verifier, challenge) = BuildPkce();
        var code = await CreateAuthorizationCodeAsync(user, redirectUri, challenge);

        var response = await _fixture.Client.PostAsync(
            "/connect/token",
            AuthorizationCodeGrantForm(code, redirectUri, verifier));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("access_token").GetString()));
        Assert.Equal("Bearer", json.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task PostToken_WithInvalidPkceVerifier_ReturnsBadRequest()
    {
        var redirectUri = "https://localhost:7070/signin-oidc";
        SetupAuthorizationCodeClient(redirectUri);

        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);

        var (_, challenge) = BuildPkce();
        var code = await CreateAuthorizationCodeAsync(user, redirectUri, challenge);

        var response = await _fixture.Client.PostAsync(
            "/connect/token",
            AuthorizationCodeGrantForm(code, redirectUri, "invalid-verifier"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("invalid_grant", json.GetProperty("error").GetString());
    }

    // --- Token audience verification ---

    [Fact]
    public async Task PostToken_AuthorizationCodeGrant_AccessTokenAudienceIsApiResource()
    {
        var redirectUri = "https://localhost:7070/signin-oidc";
        SetupAuthorizationCodeClient(redirectUri);

        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByIdAsync(user.UserId))
            .ReturnsAsync(user);

        var (verifier, challenge) = BuildPkce();
        var code = await CreateAuthorizationCodeAsync(user, redirectUri, challenge);

        var response = await _fixture.Client.PostAsync(
            "/connect/token",
            AuthorizationCodeGrantForm(code, redirectUri, verifier));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = json.GetProperty("access_token").GetString()!;

        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(accessToken);

        Assert.Contains(_fixture.Options.Audience, parsed.Audiences);
    }

    [Fact]
    public async Task PostToken_AuthorizationCodeGrant_IdTokenAudienceIsClientId()
    {
        var redirectUri = "https://localhost:7070/signin-oidc";
        SetupAuthorizationCodeClient(redirectUri);

        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByIdAsync(user.UserId))
            .ReturnsAsync(user);

        var (verifier, challenge) = BuildPkce();
        var code = await CreateAuthorizationCodeAsync(user, redirectUri, challenge);

        var response = await _fixture.Client.PostAsync(
            "/connect/token",
            AuthorizationCodeGrantForm(code, redirectUri, verifier));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idToken = json.GetProperty("id_token").GetString()!;

        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(idToken);

        Assert.Contains(TestClientId, parsed.Audiences);
    }

    [Fact]
    public async Task PostToken_AuthorizationCodeGrant_IdTokenContainsNonceFromAuthorizeRequest()
    {
        var redirectUri = "https://localhost:7070/signin-oidc";
        SetupAuthorizationCodeClient(redirectUri);

        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByIdAsync(user.UserId))
            .ReturnsAsync(user);

        var (verifier, challenge) = BuildPkce();
        var expectedNonce = Guid.NewGuid().ToString("N");
        var code = await CreateAuthorizationCodeAsync(user, redirectUri, challenge, nonce: expectedNonce);

        var response = await _fixture.Client.PostAsync(
            "/connect/token",
            AuthorizationCodeGrantForm(code, redirectUri, verifier));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idToken = new JwtSecurityTokenHandler().ReadJwtToken(json.GetProperty("id_token").GetString()!);

        Assert.Equal(expectedNonce, idToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Nonce)?.Value);
    }

    [Fact]
    public async Task PostToken_AuthorizationCodeGrant_AccessTokenDoesNotContainNonce()
    {
        var redirectUri = "https://localhost:7070/signin-oidc";
        SetupAuthorizationCodeClient(redirectUri);

        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByIdAsync(user.UserId))
            .ReturnsAsync(user);

        var (verifier, challenge) = BuildPkce();
        var code = await CreateAuthorizationCodeAsync(user, redirectUri, challenge, nonce: Guid.NewGuid().ToString("N"));

        var response = await _fixture.Client.PostAsync(
            "/connect/token",
            AuthorizationCodeGrantForm(code, redirectUri, verifier));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = new JwtSecurityTokenHandler().ReadJwtToken(json.GetProperty("access_token").GetString()!);

        Assert.Null(accessToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Nonce));
    }

    [Fact]
    public async Task PostToken_AuthorizationCodeGrant_AccessTokenAndIdTokenHaveDifferentAudiences()
    {
        var redirectUri = "https://localhost:7070/signin-oidc";
        SetupAuthorizationCodeClient(redirectUri);

        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByIdAsync(user.UserId))
            .ReturnsAsync(user);

        var (verifier, challenge) = BuildPkce();
        var code = await CreateAuthorizationCodeAsync(user, redirectUri, challenge);

        var response = await _fixture.Client.PostAsync(
            "/connect/token",
            AuthorizationCodeGrantForm(code, redirectUri, verifier));

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var handler = new JwtSecurityTokenHandler();

        var accessToken = handler.ReadJwtToken(json.GetProperty("access_token").GetString()!);
        var idToken = handler.ReadJwtToken(json.GetProperty("id_token").GetString()!);

        Assert.Contains(_fixture.Options.Audience, accessToken.Audiences);
        Assert.Contains(TestClientId, idToken.Audiences);
        Assert.DoesNotContain(TestClientId, accessToken.Audiences);
        Assert.DoesNotContain(_fixture.Options.Audience, idToken.Audiences);
    }
}
