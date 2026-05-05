using CoreDesign.Identity.Client;
using CoreDesign.Identity.Client.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CoreDesign.Identity.Client.Tests;

public class IdentityApiClientTests
{
    private static readonly IdentityApiOptions DefaultOptions = new()
    {
        BaseUrl = "https://identity.test",
        Username = "svc-user",
        Password = "svc-pass"
    };

    private static IdentityApiClient CreateClient(MockHttpMessageHandler handler, IdentityApiOptions? options = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri((options ?? DefaultOptions).BaseUrl) };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("coredesign-identity")).Returns(httpClient);

        return new IdentityApiClient(factory.Object, Options.Create(options ?? DefaultOptions));
    }

    private static string TokenPayload(int expiresIn = 3600) =>
        $$"""{"access_token":"test-token-{{Guid.NewGuid()}}","expires_in":{{expiresIn}}}""";

    [Fact]
    public async Task GetAccessTokenAsync_FetchesTokenFromServer()
    {
        var client = CreateClient(MockHttpMessageHandler.WithJson(TokenPayload()));

        var token = await client.GetAccessTokenAsync();

        Assert.False(string.IsNullOrEmpty(token));
        Assert.StartsWith("test-token-", token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_CachesToken_OnSubsequentCalls()
    {
        var callCount = 0;
        var handler = new CountingMockHandler(() =>
        {
            callCount++;
            return TokenPayload(3600);
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(DefaultOptions.BaseUrl) };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("coredesign-identity")).Returns(httpClient);
        var identityClient = new IdentityApiClient(factory.Object, Options.Create(DefaultOptions));

        await identityClient.GetAccessTokenAsync();
        await identityClient.GetAccessTokenAsync();
        await identityClient.GetAccessTokenAsync();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ThrowsException_WhenServerFails()
    {
        var handler = MockHttpMessageHandler.WithJson("""{"error":"invalid_client"}""", System.Net.HttpStatusCode.Unauthorized);
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAccessTokenAsync());
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsSameToken_BeforeExpiry()
    {
        var client = CreateClient(MockHttpMessageHandler.WithJson(TokenPayload(3600)));

        var token1 = await client.GetAccessTokenAsync();
        var token2 = await client.GetAccessTokenAsync();

        Assert.Equal(token1, token2);
    }

    private class CountingMockHandler : HttpMessageHandler
    {
        private readonly Func<string> _payloadFactory;

        public CountingMockHandler(Func<string> payloadFactory) => _payloadFactory = payloadFactory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_payloadFactory(), System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
