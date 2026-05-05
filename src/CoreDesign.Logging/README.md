# CoreDesign.Logging

`CoreDesign.Logging` provides a `DispatchProxy`-based logging middleware that wraps any service interface and automatically logs every method invocation, return value, and exception. Service classes stay free of log statements while still producing structured, consistent log output for every operation.

## Installation

```
dotnet add package CoreDesign.Logging
```

## Usage

### Register a service with the logging proxy

Replace the standard `AddTransient` (or `AddScoped`) call with `AddWithLogging`:

```csharp
services.AddWithLogging<IWeatherForecastService, WeatherForecastService>();
```

The DI container will resolve `IWeatherForecastService` as a proxy-wrapped instance. The concrete class needs no changes.

### What gets logged

| Situation | Level |
|---|---|
| Method called | Information (method name and serialized parameters) |
| Method returned a success result | Information (method name and serialized return value) |
| Method returned a `NotFoundMessage` or `BadRequestMessage` | Warning |
| Method threw an exception | Error (exception and method name) |

Both synchronous and `Task`/`Task<T>` methods are fully supported.

### Lifetime

`AddWithLogging` defaults to `Transient`. Pass a different lifetime when needed:

```csharp
services.AddWithLogging<IMyService, MyService>(ServiceLifetime.Scoped);
```

## Sensitive Data

By default, `LoggingMiddleware` serializes all method parameters and return values into the log output. Two attributes give you control over methods that handle passwords, tokens, or other sensitive information.

### `[SensitiveParameter]`

Apply `[SensitiveParameter]` to any parameter on the interface method that should not appear in logs. The middleware replaces that argument with `"[REDACTED]"` while still logging all other parameters normally.

```csharp
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, [SensitiveParameter] string password);
}
```

The log entry for the example above will include the `username` value and show `"[REDACTED]"` in place of `password`. The actual value is passed to the implementation unchanged.

### `[NoLog]`

Apply `[NoLog]` to a method on the interface to skip all logging for that method. No invocation, result, or exception entries are written.

```csharp
public interface ITokenService
{
    [NoLog]
    Task<string> IssueTokenAsync(string userId);
}
```

Use `[NoLog]` when the method name or parameter shape itself would be too revealing, or when call volume is high enough that logging every invocation creates more noise than value.

## Dependencies

- `CoreDesign.Shared` for `NotFoundMessage` and `BadRequestMessage` result types
- `OneOf` for discriminated-union result inspection
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
