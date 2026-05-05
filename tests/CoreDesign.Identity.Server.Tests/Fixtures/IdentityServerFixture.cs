using System.Security.Cryptography;
using CoreDesign.Identity.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace CoreDesign.Identity.Server.Tests.Fixtures;

public class IdentityServerFixture : IAsyncLifetime
{
    private WebApplication? _app;

    public HttpClient Client { get; private set; } = null!;
    public Mock<IIdentityStore> IdentityStoreMock { get; } = new();
    public IdentityOptions Options { get; } = new()
    {
        Issuer = "https://identity.test",
        Audience = "test-api",
        KeyId = "test-signing-key",
        TokenLifetimeHours = 1
    };
    public RsaSecurityKey RsaKey { get; private set; } = null!;
    public SigningCredentials SigningCredentials { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var rsa = RSA.Create(2048);
        RsaKey = new RsaSecurityKey(rsa) { KeyId = Options.KeyId };
        SigningCredentials = new SigningCredentials(RsaKey, SecurityAlgorithms.RsaSha256);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(Options);
        builder.Services.AddSingleton(RsaKey);
        builder.Services.AddSingleton(SigningCredentials);
        builder.Services.AddSingleton<IIdentityStore>(IdentityStoreMock.Object);

        _app = builder.Build();
        _app.MapIdentityEndpoints();

        await _app.StartAsync();
        Client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        if (_app is not null)
            await _app.StopAsync();
    }
}
