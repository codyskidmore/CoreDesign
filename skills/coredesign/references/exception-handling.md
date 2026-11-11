# CoreDesign.ExceptionHandling

Namespace: `CoreDesign.ExceptionHandling`. The generated `AddGeneratedProblemMappings()` is in `Microsoft.Extensions.DependencyInjection`.

## Purpose

A single global `IExceptionHandler` that turns any exception escaping the pipeline into an RFC 7807 `ProblemDetails` response. It is the last line of defense for the unplanned. Expected outcomes (not found, validation, conflicts) are union results returned by handlers, not exceptions; see `shared-and-unions.md`.

## Setup

```csharp
builder.Services.AddCoreDesignExceptionHandling();
builder.Services.AddGeneratedProblemMappings();   // generated; exists even with no mappings
// ...
app.UseExceptionHandler();                         // first in the pipeline
```

With no mappings, every unhandled exception becomes a 500 ProblemDetails. `detail` contains the exception only in `Development`.

## Mapping exceptions you own

```csharp
[ProblemMapping(409, Title = "Concurrency conflict")]
public sealed class ConcurrencyConflictException(string entity, Ulid id)
    : Exception($"{entity} '{id}' was changed by someone else.");
```

`detail` is the exception's `Message`. Set `IncludeMessage = false` when the message could leak internals.

## Mapping exceptions you don't own

Use the assembly-level form with `ExceptionType`:

```csharp
[assembly: ProblemMapping(503, ExceptionType = typeof(Npgsql.NpgsqlException), Title = "Database unavailable", IncludeMessage = false)]
[assembly: ProblemMapping(408, ExceptionType = typeof(TaskCanceledException), Title = "Request timed out")]
```

Put these in one file, for example `Infrastructure/ProblemMappings.cs`. A common one for EF Core projects, so a unique-index violation that slips past a handler's own check returns 409 instead of 500 without exposing SQL details:

```csharp
[assembly: ProblemMapping(409, ExceptionType = typeof(Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException), Title = "Changed by someone else", IncludeMessage = false)]
[assembly: ProblemMapping(409, ExceptionType = typeof(Microsoft.EntityFrameworkCore.DbUpdateException), Title = "Conflict", IncludeMessage = false)]
```

`DbUpdateConcurrencyException` derives from `DbUpdateException`; both lines are fine together because the most derived mapping wins. Note that `DbUpdateException` also covers other write failures (for example foreign key violations), so only map it if 409 is right for all of them in your app.

## Inheritance

A mapping covers subtypes (`MatchDerived = true`) unless a subtype has its own mapping; the most derived mapping wins regardless of declaration order. Set `MatchDerived = false` to limit a mapping to the exact type.

## Options

| Property | Default | Meaning |
|---|---|---|
| `StatusCode` | required (constructor) | HTTP status |
| `Title` | reason phrase | ProblemDetails `title` |
| `Type` | omitted | ProblemDetails `type` URI |
| `MatchDerived` | `true` | Also cover unmapped subtypes |
| `IncludeMessage` | `true` | Write `Exception.Message` to `detail` |
| `ExceptionType` | none | Required at assembly level, invalid on a class |

Build errors: `CDEH001` for two mappings of the same exact type, `CDEH002` for an assembly-level mapping without `ExceptionType`.

## When to use a mapping, and when not to

- **Use it** for exceptions that can legitimately escape: infrastructure failures, timeouts, concurrency conflicts thrown by EF Core, third-party library errors.
- **Don't** create `NotFoundException` or `ValidationException` types to avoid writing a switch. Return `NotFoundMessage` or `BadRequestMessage` in a union instead; it is cheaper, testable, and logged per case.
- **Don't** wrap endpoints in `try`/`catch` to build error responses; let the global handler do it.
