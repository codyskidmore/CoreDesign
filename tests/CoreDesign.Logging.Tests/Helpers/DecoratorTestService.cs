namespace CoreDesign.Logging.Tests.Helpers;

public interface IDecoratorTestService
{
    Task<string> GetValueAsync(string key);
    Task<OneOf<string, NotFoundMessage>> FindAsync(string id);
    Task RunAsync();
    string Login(string username, [Redact] string password);
    [Suppress] string GetSecret();
    [Suppress] Task<string> GetSecretAsync();
    // Properties (Gap 2 coverage)
    string Status { get; set; }
    string Label { get; }
    string this[int index] { get; set; }
}

public class DecoratorTestService : IDecoratorTestService
{
    public Task<string> GetValueAsync(string key) => Task.FromResult(key);
    public Task<OneOf<string, NotFoundMessage>> FindAsync(string id) =>
        Task.FromResult<OneOf<string, NotFoundMessage>>(id);
    public Task RunAsync() => Task.CompletedTask;
    public string Login(string username, string password) => username;
    public string GetSecret() => "secret";
    public Task<string> GetSecretAsync() => Task.FromResult("secret");
    public string Status { get; set; } = string.Empty;
    public string Label { get; } = "label";
    public string this[int index] { get => index.ToString(); set { } }
}
