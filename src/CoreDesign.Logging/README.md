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

## Dependencies

- `CoreDesign.Shared` for `NotFoundMessage` and `BadRequestMessage` result types
- `OneOf` for discriminated-union result inspection
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
