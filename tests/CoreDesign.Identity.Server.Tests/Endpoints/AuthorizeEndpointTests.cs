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

    [Fact]
    public async Task GetAuthorize_WithoutCredentials_ReturnsLoginPage()
    {
        SetupAuthCodeClient();
        var (_, challenge) = BuildPkce();

        var response = await _fixture.Client.GetAsync($"/connect/authorize?response_type=code&client_id=sample-blazor&redirect_uri={Uri.EscapeDataString("https://localhost:7070/signin-oidc")}&scope=openid%20profile%20email&state=abc&code_challenge={challenge}&code_challenge_method=S256");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("text/html", contentType);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<form", html, StringComparison.OrdinalIgnoreCase);
    }

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

        var uri = response.Headers.Location!;
        var query = HttpUtility.ParseQueryString(uri.Query);
        Assert.False(string.IsNullOrWhiteSpace(query["code"]));
        Assert.Equal("test-state", query["state"]);
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




