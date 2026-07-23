# CoreDesign.Logging

`CoreDesign.Logging` provides compile-time logging decorators generated from a single attribute. Place `[LoggingDecorator]` on any interface and the included Roslyn source generator produces a decorator class that logs every method invocation, return value, and exception. Implementations stay free of log statements while still producing structured, consistent log output for every operation.

## Installation

```
dotnet add package CoreDesign.Logging
```

## Source Generator (Recommended)

### Mark an interface

Apply `[LoggingDecorator]` to any interface you want wrapped:

```csharp
public union CreateResult(WeatherForecast, BadRequestMessage);

[LoggingDecorator]
public interface ICreateForecastHandler
{
    Task<CreateResult> CreateAsync(Request request, Guid userId, CancellationToken ct);
}
```

The generator creates `CreateForecastHandlerLoggingDecorator` at compile time. No hand-written boilerplate, no reflection at runtime.

Generic interfaces are fully supported. The decorator class carries the same type parameters and constraint clauses as the interface:

```csharp
[LoggingDecorator]
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct);
    Task SaveAsync(T entity, CancellationToken ct);
}
```

The generator produces `RepositoryLoggingDecorator<T> : IRepository<T> where T : class`. Registration in `DecorateWithLogging()` uses the open-generic form so all concrete registrations of the interface are covered by a single call.

### Register

After marking all interfaces, call the generated extension method once during startup:

```csharp
services.AddTransient<ICreateForecastHandler, CreateForecastHandler>();
// ... other registrations ...
services.DecorateWithLogging();
```

`DecorateWithLogging()` is generated alongside the decorators and wraps every marked interface in one call.

### What gets logged

| Situation | Level |
|---|---|
| Method called | Information (method name and each parameter) |
| Method returned a success result | Information (method name and return value) |
| Method returned `Task` (no value) | Information (method name and "completed") |
| Method returned a `NotFoundMessage`, `BadRequestMessage`, or other error type | Warning (method name and return value) |
| Method threw an exception | Error (exception and method name) |

Both synchronous and `Task`/`Task<T>` methods are fully supported. For native C# `union` return types (.NET 11 preview), each arm is logged at the appropriate level: success arms at Information, arms whose type name contains `NotFound`, `BadRequest`, `Error`, `Failure`, `Unauthorized`, `Forbidden`, `Conflict`, or `InvalidOperation` at Warning.

```csharp
public union CreateResult(WeatherForecast, BadRequestMessage);

[LoggingDecorator]
public interface ICreateForecastHandler
{
    Task<CreateResult> CreateAsync(Request request, CancellationToken ct);
}
```

The generator recognizes a return type as a union by its `[System.Runtime.CompilerServices.Union]` attribute and derives the arms from its public single-parameter constructors, the same case types the language itself uses for union conversions and pattern matching.

### Sensitive data

#### `[Redact]`

Apply `[Redact]` to any parameter that should not appear in logs. The generated decorator replaces that argument with `"[REDACTED]"` in the invocation log while passing the actual value to the implementation.

```csharp
[LoggingDecorator]
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, [Redact] string password);
}
```

#### `[Suppress]`

Apply `[Suppress]` to a method to skip all logging for it. The generated decorator passes the call straight through to the inner implementation with no log entries.

```csharp
[LoggingDecorator]
public interface ITokenService
{
    [Suppress]
    Task<string> IssueTokenAsync(string userId);
}
```

### Interface properties and indexers

Properties and indexers declared on the interface are implemented as pure pass-throughs in the generated decorator. No logging is emitted for property access; only ordinary methods are logged.

```csharp
[LoggingDecorator]
public interface ISessionStore
{
    string this[string key] { get; set; }
    int Count { get; }
    Task<bool> ExistsAsync(string key, CancellationToken ct);
}
```

The generated decorator delegates `this[key]` and `Count` directly to `_inner` with no log entries. `ExistsAsync` is logged normally.

### Controlling log output size

Log output size control belongs in the structured logging configuration, where limits apply consistently to all sinks and can be adjusted per environment without a code change.

With Serilog, add a `Destructure` block to the `Serilog` section of `appsettings.json`:

```json
"Serilog": {
  "Destructure": [
    { "Name": "ToMaximumDepth", "Args": { "maximumDestructuringDepth": 5 } },
    { "Name": "ToMaximumStringLength", "Args": { "maximumStringLength": 500 } },
    { "Name": "ToMaximumCollectionCount", "Args": { "maximumCollectionCount": 10 } }
  ]
}
```

`ToMaximumDepth` caps how many levels deep Serilog will traverse when destructuring an object. Without this setting, a deeply nested object graph (such as an EF Core entity with navigation properties) will cause Serilog to stream an enormous payload into every sink, which can hang the application. `ToMaximumStringLength` truncates any string value captured during destructuring. `ToMaximumCollectionCount` caps how many elements are captured from arrays and collections. All three settings are tunable per environment: raise limits in `appsettings.Development.json` to see full payloads while debugging, or tighten them in `appsettings.Production.json` under load.

### AOT compatibility

Generated decorators are plain C# classes with direct method calls. There is no reflection, no `DispatchProxy`, and no runtime code generation. They are fully compatible with .NET Native AOT.

## Dependencies

- `CoreDesign.Shared` for `NotFoundMessage`, `BadRequestMessage`, and `Success` result types
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`

## Feedback

Feedback on this package is welcome. If you run into a missing feature, an unexpected behavior, or something that required more effort than it should have, open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues) or tag [@codyskidmore](https://github.com/codyskidmore). Suggestions about missing features and priority input are especially appreciated.
