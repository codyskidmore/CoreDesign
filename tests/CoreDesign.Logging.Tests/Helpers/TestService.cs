namespace CoreDesign.Logging.Tests.Helpers;

public interface ITestService
{
    string GetValue(string input);
    void Execute(string input);
    Task RunAsync();
    Task<string> FetchAsync(string input);
    Task<OneOf<string, NotFoundMessage>> FindAsync(string id);
    Task<OneOf<string, BadRequestMessage>> ValidateAsync(string input);
    string Login(string username, [Redact] string password);
    [Suppress] string GetSecret(string input);
    [Suppress] Task<string> FetchSecretAsync(string input);
    [TruncateLog(10)] string GetValueWithLimit(string input);
    [TruncateLog(0)] string GetValueUnlimited(string input);
    [TruncateLog(10)] Task<string> FetchWithLimitAsync(string input);
}

public class TestService : ITestService
{
    public string GetValue(string input) => input;
    public void Execute(string input) { }
    public Task RunAsync() => Task.CompletedTask;
    public Task<string> FetchAsync(string input) => Task.FromResult(input);
    public Task<OneOf<string, NotFoundMessage>> FindAsync(string id) =>
        Task.FromResult<OneOf<string, NotFoundMessage>>(id);
    public Task<OneOf<string, BadRequestMessage>> ValidateAsync(string input) =>
        Task.FromResult<OneOf<string, BadRequestMessage>>(input);
    public string Login(string username, string password) => username;
    public string GetSecret(string input) => input;
    public Task<string> FetchSecretAsync(string input) => Task.FromResult(input);
    public string GetValueWithLimit(string input) => input;
    public string GetValueUnlimited(string input) => input;
    public Task<string> FetchWithLimitAsync(string input) => Task.FromResult(input);
}
