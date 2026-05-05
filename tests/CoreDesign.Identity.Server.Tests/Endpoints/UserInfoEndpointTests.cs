using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CoreDesign.Identity.Server;
using CoreDesign.Identity.Server.Tests.Fakers;
using CoreDesign.Identity.Server.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace CoreDesign.Identity.Server.Tests.Endpoints;

public class UserInfoEndpointTests : IClassFixture<IdentityServerFixture>
{
    private readonly IdentityServerFixture _fixture;

    public UserInfoEndpointTests(IdentityServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.IdentityStoreMock.Reset();
    }

    private string GenerateToken(string userId, DateTime? expires = null, DateTime? notBefore = null)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Email, "test@test.com"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore,
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            Issuer = _fixture.Options.Issuer,
            Audience = _fixture.Options.Audience,
            SigningCredentials = _fixture.SigningCredentials
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    [Fact]
    public async Task GetUserInfo_WithValidToken_ReturnsOk()
    {
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByIdAsync(user.UserId))
            .ReturnsAsync(user);
        var token = GenerateToken(user.UserId);

        var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUserInfo_WithValidToken_ReturnsUserClaims()
    {
        var user = IdentityFakers.IdentityRecord().Generate();
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByIdAsync(user.UserId))
            .ReturnsAsync(user);
        var token = GenerateToken(user.UserId);

        var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _fixture.Client.SendAsync(request);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.UserId, json.GetProperty("sub").GetString());
        Assert.Equal(user.Email, json.GetProperty("email").GetString());
    }

    [Fact]
    public async Task GetUserInfo_WithNoAuthHeader_ReturnsUnauthorized()
    {
        var response = await _fixture.Client.GetAsync("/connect/userinfo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserInfo_WithInvalidToken_ReturnsUnauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not.a.valid.jwt");
        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserInfo_WithExpiredToken_ReturnsUnauthorized()
    {
        var user = IdentityFakers.IdentityRecord().Generate();
        var token = GenerateToken(user.UserId, expires: DateTime.UtcNow.AddHours(-1), notBefore: DateTime.UtcNow.AddHours(-2));

        var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserInfo_WhenUserNotFoundInStore_ReturnsUnauthorized()
    {
        _fixture.IdentityStoreMock
            .Setup(s => s.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((IdentityRecord?)null);
        var token = GenerateToken(Guid.NewGuid().ToString());

        var request = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
