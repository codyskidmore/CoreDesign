# CoreDesign.Logging

`CoreDesign.Logging` provides a `DispatchProxy`-based logging middleware that wraps any class behind an interface and automatically logs every method invocation, return value, and exception. Classes stay free of log statements while still producing structured, consistent log output for every operation.

## Installation

```
dotnet add package CoreDesign.Logging
```

## Usage

### Register a single class with the logging proxy

Replace the standard `AddTransient` (or `AddScoped`) call with `AddWithLogging`:

```csharp
services.AddWithLogging<IWeatherForecastService, WeatherForecastService>();
```

The DI container will resolve `IWeatherForecastService` as a proxy-wrapped instance. The concrete class needs no changes.

### Automatic registration with `ILoggable`

For larger applications, implement the `ILoggable` marker interface on any class to opt it into automatic logging registration. `ILoggable` can be applied to services, handlers, or any other class in your application regardless of naming convention.

```csharp
public class CreateForecastHandler(...) : ICreateForecastHandler, ILoggable { ... }
public class GetForecastHandler(...) : IGetForecastHandler, ILoggable { ... }
public class OrderProcessingService(...) : IOrderProcessingService, ILoggable { ... }
```

Then register all marked classes in a single call:

```csharp
services.AddWithLogging(typeof(Program).Assembly);
```

The overload scans the assembly for every non-abstract class implementing `ILoggable`, pairs it with each of its non-marker interfaces, and registers a logging proxy for each one. Renaming a class has no effect on whether it gets logging — only the presence of `ILoggable` matters.

### Choosing between the two approaches

| Approach | When to use |
|---|---|
| `AddWithLogging<TInterface, TImplementation>()` | Explicit, per-class control. Useful when only a small number of classes need logging, or when you want each registration to be visible at the call site. |
| `AddWithLogging(assembly)` | Opt-in at the class level via `ILoggable`. Useful when many classes across an assembly should be logged and you want a single registration call. |

### What gets logged

| Situation | Level |
|---|---|
| Method called | Information (method name and serialized parameters) |
| Method returned a success result | Information (method name and serialized return value, truncated to 500 chars by default) |
| Method returned a `NotFoundMessage` or `BadRequestMessage` | Warning (same truncation applies) |
| Method threw an exception | Error (exception and method name) |

Both synchronous and `Task`/`Task<T>` methods are fully supported.

### Lifetime

Both overloads default to `Transient`. Pass a different lifetime when needed:

```csharp
services.AddWithLogging<IMyService, MyService>(ServiceLifetime.Scoped);
services.AddWithLogging(typeof(Program).Assembly, ServiceLifetime.Scoped);
```

## Sensitive Data

By default, `LoggingMiddleware` serializes all method parameters and return values into the log output. Two attributes give you control over methods that handle passwords, tokens, or other sensitive information.

### `[Redact]`

Apply `[Redact]` to any parameter on the interface method that should not appear in logs. The middleware replaces that argument with `"[REDACTED]"` while still logging all other parameters normally.

```csharp
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, [Redact] string password);
}
```

The log entry for the example above will include the `username` value and show `"[REDACTED]"` in place of `password`. The actual value is passed to the implementation unchanged.

### `[Suppress]`

Apply `[Suppress]` to a method on the interface to skip all logging for that method. No invocation, result, or exception entries are written.

```csharp
public interface ITokenService
{
    [Suppress]
    Task<string> IssueTokenAsync(string userId);
}
```

Use `[Suppress]` when the method name or parameter shape itself would be too revealing, or when call volume is high enough that logging every invocation creates more noise than value.

### `[TruncateLog]`

Return values are serialized to JSON and truncated at 500 characters by default. The log suffix reflects the reason for any truncation:

| Suffix | Meaning |
|---|---|
| `... [truncated, total N chars]` | Output exceeded the length limit; `N` is the full serialized length |
| `... [depth limit reached]` | Object nesting exceeded the internal depth cap; partial JSON shown |
| `... [truncated, depth limit reached]` | Both limits were hit; output is both cut and depth-capped |

Serialization is cycle-safe. Objects that contain circular references (for example, a parent entity that holds a child which holds back a reference to the parent) are serialized without throwing; the back-reference is written as `null` and serialization continues normally.

Apply `[TruncateLog]` to a method on the interface to override the length limit for that method:

```csharp
public interface IWeatherForecastService
{
    // Raise the limit for a method expected to return larger payloads.
    [TruncateLog(2000)]
    Task<IReadOnlyList<WeatherForecast>> GetAllAsync(CancellationToken ct);

    // Disable truncation entirely for a method that returns a small, critical diagnostic object.
    [TruncateLog(0)]
    Task<ServiceStatus> GetStatusAsync();
}
```

The default limit of 500 characters is defined by `LoggingMiddleware.DefaultMaxResultLength`. Parameters are not truncated; use `[Redact]` to suppress a sensitive parameter entirely.

## Further Reading

Design rationale and comparison with Serilog and Serilog.Enrichers.Sensitive: [SerilogVsMiddleware.md](SerilogVsMiddleware.md)

## Dependencies

- `CoreDesign.Shared` for `NotFoundMessage` and `BadRequestMessage` result types
- `OneOf` for discriminated-union result inspection
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`

## Feedback

Feedback on this package is welcome. If you run into a missing feature, an unexpected behavior, or something that required more effort than it should have, open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues) or tag [@codyskidmore](https://github.com/codyskidmore). Suggestions about missing features and priority input are especially appreciated.
