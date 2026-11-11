# CoreDesign.Logging

Namespace: `CoreDesign.Logging` for the attributes. The generated `DecorateWithLogging()` lives in `Microsoft.Extensions.DependencyInjection`, so it needs no extra using.

## How it works

A Roslyn source generator finds every interface marked `[LoggingDecorator]` and writes a decorator class for it at build time (`IOrderService` gets `OrderServiceLoggingDecorator`). It also writes one `DecorateWithLogging()` extension that wraps each registered implementation in its decorator. No reflection, no proxies, AOT-safe. Implementations contain no log statements.

## Setup

1. Reference `CoreDesign.Logging` in the project that declares the interfaces. The generator runs in that project.
2. Mark interfaces:

   ```csharp
   [LoggingDecorator]
   public interface IOrderService
   {
       Task<Union<Order, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct);
       Task<Union<Order, BadRequestMessage>> CreateAsync(CreateOrder request, CancellationToken ct);
   }
   ```

3. Register implementations normally, then call the generated method **once, after all of them**:

   ```csharp
   services.AddScoped<IOrderService, OrderService>();
   services.AddTransient<ICreateOrderHandler, CreateOrderHandler>();
   // ... every other [LoggingDecorator] interface in this assembly
   services.DecorateWithLogging();
   ```

   The lifetime of each registration is kept. Keyed registrations are not decorated.

## Startup failures and their cause

| Symptom | Cause | Fix |
|---|---|---|
| `InvalidOperationException: No registration for X was found. Call DecorateWithLogging() after registering your services.` | A marked interface is not registered yet when `DecorateWithLogging()` runs | Move the call after all module registrations, or register the interface |
| Every log line appears twice | `DecorateWithLogging()` called more than once (for example once per module) | Call it once for the whole application |
| No logs for a service | Interface not marked, the implementation is resolved by its concrete type, or it was registered after the call | Inject the interface, register before the call |
| `DecorateWithLogging` not found | The project with the marked interfaces does not reference CoreDesign.Logging, or has no marked interface | Add the package reference and at least one `[LoggingDecorator]` |

## What gets logged

| Situation | Level |
|---|---|
| Method called, with each parameter | Information |
| Success value returned | Information |
| `Task` or `ValueTask` completed | Information |
| Union case whose type name contains NotFound, BadRequest, Unauthorized, Forbidden, Conflict, Error, Failure or InvalidOperation | Warning |
| Exception thrown (then rethrown) | Error |

Per-case levels apply to `Task<Union<...>>` and `ValueTask<Union<...>>` return types, including your own `union` declarations and OneOf (recognized by name, so OneOf projects keep working without CoreDesign referencing OneOf). A synchronous method returning a union is logged as a plain value at Information, so make result-returning methods async.

## Sensitive data

```csharp
[LoggingDecorator]
public interface IAccountService
{
    Task<Union<Session, BadRequestMessage>> SignInAsync(string username, [Redact] string password, CancellationToken ct);

    [Suppress]                       // no logging at all for this method
    Task<string> IssueTokenAsync(Guid userId, CancellationToken ct);
}
```

`[Redact]` writes `[REDACTED]` in place of the argument in the call log and still passes the real value. `[Suppress]` passes the call through untouched. Use `[Suppress]` for methods returning secrets or very large payloads, and for chatty methods called in tight loops. The returned value of a method is logged too, so a method returning a token or password must be suppressed; redacting a parameter does not hide the return value.

Properties and indexers on a marked interface are passed through without logging.

## Generic interfaces

```csharp
[LoggingDecorator]
public interface IRepository<T> where T : class { Task<T?> GetByIdAsync(Guid id, CancellationToken ct); }
```

The decorator keeps the type parameters and constraints, and `DecorateWithLogging()` wraps every closed registration (`IRepository<Order>`, `IRepository<Customer>`).

## Keeping log size under control

Return values are logged whole. Cap destructuring in Serilog configuration rather than in code:

```json
"Serilog": {
  "Destructure": [
    { "Name": "ToMaximumDepth", "Args": { "maximumDestructuringDepth": 5 } },
    { "Name": "ToMaximumStringLength", "Args": { "maximumStringLength": 500 } },
    { "Name": "ToMaximumCollectionCount", "Args": { "maximumCollectionCount": 10 } }
  ]
}
```

Without a depth cap, an EF entity with navigation properties can produce enormous entries or stall the app.

## What not to do

- Don't add `ILogger` calls to implementations to log calls or results; the decorator already does, and the two will disagree over time. Log only things the decorator cannot see, such as the outcome of an external call made inside a method.
- Don't rely on the decorator for exceptions at the HTTP boundary; it logs and rethrows. CoreDesign.ExceptionHandling turns what escapes into a response.
- To inspect the generated code, set `<EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>` and look under `obj/`.
