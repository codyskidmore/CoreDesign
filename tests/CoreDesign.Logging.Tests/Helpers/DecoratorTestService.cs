namespace CoreDesign.Logging.Tests.Helpers;

public union FindResult(string, NotFoundMessage);

public interface IDecoratorTestService
{
    Task<string> GetValueAsync(string key);
    Task<FindResult> FindAsync(string id);
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
    public Task<FindResult> FindAsync(string id) =>
        Task.FromResult<FindResult>(id);
    public Task RunAsync() => Task.CompletedTask;
    public string Login(string username, string password) => username;
    public string GetSecret() => "secret";
    public Task<string> GetSecretAsync() => Task.FromResult("secret");
    public string Status { get; set; } = string.Empty;
    public string Label { get; } = "label";
    public string this[int index] { get => index.ToString(); set { } }
}
