using System.Net;
using System.Security.Cryptography;
using System.Web;
using CoreDesign.Identity.Server.Clients;
using CoreDesign.Identity.Server.Tests.Fakers;
using CoreDesign.Identity.Server.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace CoreDesign.Identity.Server.Tests.Endpoints;

public class AuthorizeEndpointTests : IClassFixture<IdentityServerFixture>
{
    private readonly IdentityServerFixture _fixture;

    public AuthorizeEndpointTests(IdentityServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.IdentityStoreMock.Reset();
        _fixture.ClientStoreMock.Reset();
    }

    // --- Login page rendering ---

    [Fact]
    public async Task GetAuthorize_WithoutCredentials_ReturnsLoginPage()
    {
        SetupAuthCodeClient();
        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&scope=openid%20profile%20email&state=abc&code_challenge={challenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<form", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("class=\"id-error\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAuthorize_LoginPage_ContainsHiddenOidcParameters()
    {
        SetupAuthCodeClient();
        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&scope=openid%20profile%20email&state=test-state&code_challenge={challenge}&code_challenge_method=S256");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name=\"response_type\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"client_id\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"redirect_uri\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"scope\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"state\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"code_challenge\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"code_challenge_method\"", html, StringComparison.OrdinalIgnoreCase);
    }

    // --- Successful redirects ---

    [Fact]
    public async Task GetAuthorize_WithValidCredentials_RedirectsWithCodeAndState()
    {
        SetupAuthCodeClient();
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);

        var (_, challenge) = BuildPkce();
        var redirectUri = "https://localhost:7070/signin-oidc";

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=openid%20profile%20email&state=test-state&code_challenge={challenge}&code_challenge_method=S256&username={Uri.EscapeDataString(user.Username)}&password={Uri.EscapeDataString(user.Password)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var query = HttpUtility.ParseQueryString(response.Headers.Location!.Query);
        Assert.False(string.IsNullOrWhiteSpace(query["code"]));
        Assert.Equal("test-state", query["state"]);
    }

    [Fact]
    public async Task PostAuthorize_WithValidCredentials_RedirectsWithCode()
    {
        SetupAuthCodeClient();
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(user.Username, user.Password))
            .ReturnsAsync(user);

        var (_, challenge) = BuildPkce();

        var form = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("response_type", "code"),
            new KeyValuePair<string, string>("client_id", "sample-blazor"),
            new KeyValuePair<string, string>("redirect_uri", "https://localhost:7070/signin-oidc"),
            new KeyValuePair<string, string>("scope", "openid profile email"),
            new KeyValuePair<string, string>("state", "test-state"),
            new KeyValuePair<string, string>("code_challenge", challenge),
            new KeyValuePair<string, string>("code_challenge_method", "S256"),
            new KeyValuePair<string, string>("username", user.Username),
            new KeyValuePair<string, string>("password", user.Password)
        ]);

        var response = await _fixture.Client.PostAsync("/connect/authorize", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var query = HttpUtility.ParseQueryString(response.Headers.Location!.Query);
        Assert.False(string.IsNullOrWhiteSpace(query["code"]));
        Assert.Equal("test-state", query["state"]);
    }

    // --- Invalid credentials ---

    [Fact]
    public async Task PostAuthorize_WithInvalidCredentials_Returns401WithErrorMessage()
    {
        SetupAuthCodeClient();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByCredentialsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((IdentityRecord?)null);

        var (_, challenge) = BuildPkce();

        var form = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("response_type", "code"),
            new KeyValuePair<string, string>("client_id", "sample-blazor"),
            new KeyValuePair<string, string>("redirect_uri", "https://localhost:7070/signin-oidc"),
            new KeyValuePair<string, string>("scope", "openid profile email"),
            new KeyValuePair<string, string>("state", "test-state"),
            new KeyValuePair<string, string>("code_challenge", challenge),
            new KeyValuePair<string, string>("code_challenge_method", "S256"),
            new KeyValuePair<string, string>("username", "wrong@example.com"),
            new KeyValuePair<string, string>("password", "BadPassword")
        ]);

        var response = await _fixture.Client.PostAsync("/connect/authorize", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("class=\"id-error\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Invalid username or password", html, StringComparison.OrdinalIgnoreCase);
    }

    // --- Request validation ---

    [Fact]
    public async Task GetAuthorize_WithWrongResponseType_ReturnsBadRequest()
    {
        SetupAuthCodeClient();
        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=token&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&scope=openid&code_challenge={challenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WithMissingClientId_ReturnsBadRequest()
    {
        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&scope=openid&code_challenge={challenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WithUnknownClientId_ReturnsBadRequest()
    {
        _fixture.ClientStoreMock
            .Setup(s => s.FindByClientIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ClientRecord?)null);

        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=no-such-client&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&scope=openid&code_challenge={challenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WithMissingRedirectUri_ReturnsBadRequest()
    {
        SetupAuthCodeClient();
        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=sample-blazor&scope=openid&code_challenge={challenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WithUnregisteredRedirectUri_ReturnsBadRequest()
    {
        SetupAuthCodeClient();
        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString("https://evil.example.com/callback")}&scope=openid&code_challenge={challenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WithMissingScope_ReturnsBadRequest()
    {
        SetupAuthCodeClient();
        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&code_challenge={challenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WithoutOpenIdScope_ReturnsBadRequest()
    {
        SetupAuthCodeClient();
        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&scope=profile%20email&code_challenge={challenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WhenPkceRequiredAndCodeChallengeMissing_ReturnsBadRequest()
    {
        SetupAuthCodeClient(); // RequirePkce = true

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&scope=openid%20profile%20email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthorize_WithUnsupportedCodeChallengeMethod_ReturnsBadRequest()
    {
        SetupAuthCodeClient();
        var challenge = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("some-plain-challenge"));

        var response = await _fixture.Client.GetAsync(
            $"/connect/authorize?response_type=code&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&scope=openid%20profile%20email&code_challenge={challenge}&code_challenge_method=plain");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private void SetupAuthCodeClient()
    {
        var client = new ClientRecord
        {
            ClientId = "sample-blazor",
            TokenEndpointAuthMethod = "none",
            AllowedGrantTypes = ["authorization_code"],
            AllowedRedirectUris = ["https://localhost:7070/signin-oidc"],
            AllowedScopes = ["openid", "profile", "email"],
            RequirePkce = true
        };

        _fixture.ClientStoreMock
            .Setup(s => s.FindByClientIdAsync(client.ClientId))
            .ReturnsAsync(client);
    }

    private static (string Verifier, string Challenge) BuildPkce()
    {
        var verifier = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~abcd";
        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }
}
