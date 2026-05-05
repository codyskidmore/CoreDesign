namespace CoreDesign.Logging.Tests.Helpers;

public interface ITestService
{
    string GetValue(string input);
    void Execute(string input);
    Task RunAsync();
    Task<string> FetchAsync(string input);
    Task<OneOf<string, NotFoundMessage>> FindAsync(string id);
    Task<OneOf<string, BadRequestMessage>> ValidateAsync(string input);
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
}
