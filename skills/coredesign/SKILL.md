---
name: coredesign
description: How to build .NET 11 applications with the CoreDesign NuGet packages (CoreDesign.Shared, CoreDesign.Data, CoreDesign.Logging, CoreDesign.ExceptionHandling, CoreDesign.Identity.Server, CoreDesign.Identity.Client, CoreDesign.SeedTool, CoreDesign.UserJourney, CoreDesign.UserJourney.Blazor and the npm package @coredesign.dev/user-journey) the way they are designed to be used. Use this skill whenever code references a CoreDesign namespace or package, or uses BaseEntity, CoreDesignDbContext, IReadRepository, ICudRepository, MigrationWorker, Union<...>, NotFoundMessage, BadRequestMessage, Success, [LoggingDecorator], DecorateWithLogging, [ProblemMapping], AddIdentityClient, RequireAuthorization with permission strings, AddAppSettings, AddUserJourney, UseUserJourney, Journey.Viewed, JourneyView, MapUserJourneyEvents, or the dotnet seed tool. Also use it when creating a new solution or API that should use CoreDesign (including Aspire AppHost, migration service and local identity server setup), when adding a feature, endpoint, entity, handler, seed file or test to a CoreDesign-based API, when adding per-user activity or user journey tracking to an ASP.NET Core, Blazor or React app, when upgrading CoreDesign from 1.x or OneOf to 11.x, or when debugging missing logs, missing seed data, soft-delete surprises, missing user tags in telemetry or 401/403 errors in such a project, even if the user does not name CoreDesign.
---

# CoreDesign

CoreDesign is a set of NuGet packages for ASP.NET Core APIs on .NET 11: result unions instead of exceptions for expected outcomes, an EF Core repository layer with audit fields and soft delete, source-generated logging decorators, global ProblemDetails exception handling, a development OIDC server, and a seed data tool. The packages are designed to work together; the patterns below are what make them pay off.

## Versions and platform

- All packages share one version. **The major version matches .NET: CoreDesign 11.x targets `net11.0` only.** Use 11.0.1 or later; 11.0.0 was withdrawn. Minor and patch releases never break; breaking changes wait for the next .NET major.
- Do not set `<LangVersion>`; C# 15 is the default for `net11.0` and unions need it.
- EF Core is 10.x in CoreDesign 11.0.x (the PostgreSQL provider for EF Core 11 was not yet stable). It runs on .NET 11. Do not add EF Core 11 packages to a PostgreSQL project until Npgsql 11 is stable.
- Projects still on CoreDesign 1.x target .NET 10. If you see `OneOf<...>`, `.AsT0`, `FromT0` or 1.x versions, read `references/upgrading.md` before changing anything.

## Where to start

| Task | Read |
|---|---|
| Create a new solution, or add CoreDesign to an existing API | `references/project-setup.md`: every project, file and line of wiring, in order, ending with a checklist of all features |
| Add a feature to a CoreDesign API | The recipe below, then the reference for each package you touch |
| Upgrade from CoreDesign 1.x or OneOf | `references/upgrading.md` |
| Write tests | `references/testing.md` |
| Track what users view and do | `references/user-journey.md` |
| Something is missing at runtime (logs, seed rows, auth, user tags) | The rules below, then the troubleshooting table in the relevant reference |

## Which package does what

| Package | Use it for | Reference |
|---|---|---|
| CoreDesign.Shared | `Union<...>`, `Success`, `NotFoundMessage` and other result records, `DatabaseOptions`, `AddAppSettings`, `IsPrivate()` / `IsInternal()` | `references/shared-and-unions.md` |
| CoreDesign.Data | `BaseEntity`, `CoreDesignDbContext`, read and CUD repositories, audit interceptor, `MigrationWorker` and JSON seeding | `references/data.md` |
| CoreDesign.Logging | `[LoggingDecorator]` on interfaces, generated decorators, `[Redact]`, `[Suppress]` | `references/logging.md` |
| CoreDesign.ExceptionHandling | Global RFC 7807 responses for unexpected exceptions, `[ProblemMapping]` | `references/exception-handling.md` |
| CoreDesign.Identity.Server / .Client | Local OIDC server for development, JWT validation, permission-based authorization, switching to Entra | `references/identity.md` |
| CoreDesign.SeedTool | `dotnet seed export` and `diff` between a live database and seed files | `references/seedtool.md` |
| CoreDesign.UserJourney (+ .Blazor, npm `@coredesign.dev/user-journey`) | Per-user activity tracking: user tags on traces, items viewed, Blazor clicks, browser page views and clicks | `references/user-journey.md` |

Read only the reference files the task needs.

## The design in one paragraph

Each HTTP operation is a vertical slice: an endpoint that binds and delegates, a handler behind an interface marked `[LoggingDecorator]`, and repositories injected straight into the handler. Handlers return a `Union<...>` of a success value and result records (`NotFoundMessage`, `BadRequestMessage`, ...) instead of throwing for expected outcomes. Endpoints turn the union into an HTTP result with an exhaustive switch expression. Logging comes from the generated decorator, so handlers contain no log statements. Anything nobody planned for is thrown and becomes a ProblemDetails response through CoreDesign.ExceptionHandling. Audit fields and soft delete are handled by CoreDesign.Data, never by hand.

## Adding a feature: the recipe

Follow this order; each step is small. The Sample application in the CoreDesign repository (`Sample/src/Sample.Api/WeatherForecasts/`) is the canonical example of every step.

1. **Entity** inherits `BaseEntity` (gives `Id` as a `Ulid`, audit fields, `IsDeleted`). Do not add your own `Id`, `CreatedAt` or `IsDeleted`.
2. **Configuration** inherits `BaseEntityConfiguration<T>` and calls `base.Configure(builder)` first. Skipping the base call loses the key, the soft-delete query filter and the audit constraints.
3. **Migration** through the EF tooling (`dotnet ef migrations add <Name>`). Never write or edit migration files by hand.
4. **Handler interface** marked `[LoggingDecorator]`, returning `Task<Union<...>>`:

   ```csharp
   [LoggingDecorator]
   public interface IGetWidgetHandler
   {
       Task<Union<Widget, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct);
   }

   public class GetWidgetHandler(IReadRepository<AppDbContext, Widget> repository) : IGetWidgetHandler
   {
       public async Task<Union<Widget, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct)
       {
           var widget = await repository.GetAsync(w => w.Id == id, null, ct);
           if (widget is null)
               return new NotFoundMessage($"Widget {id} not found.");
           return widget;   // implicit conversion to the union
       }
   }
   ```

5. **Endpoint** binds, calls the handler, and switches:

   ```csharp
   app.MapGet(Paths.Widgets.GetById, async (Ulid id, IGetWidgetHandler handler, CancellationToken ct) =>
       {
           var result = await handler.GetAsync(id, ct);
           return result switch
           {
               Widget widget         => Results.Ok(Response.From(widget)),
               NotFoundMessage error => Results.NotFound(error.Message),
           };
       })
       .RequireAuthorization(Permissions.WidgetsRead);
   ```

   Side effects for one case (such as cache eviction after a successful write) go in an `if (result is Widget)` before the switch, because switch expression arms cannot hold statements.

6. **Registration** in the module: repositories, then handlers, then **one** `services.DecorateWithLogging()` after every `[LoggingDecorator]` interface in the assembly is registered.

## Rules that prevent the common mistakes

These come from real projects. Each one explains what breaks if it is ignored.

- **Return results, don't throw, for expected outcomes.** Not found, validation failure and conflicts are union cases. Throwing a custom exception just to get a 404 models a normal outcome as an error and bypasses the per-case logging. Exceptions are for the unplanned; ExceptionHandling catches those.
- **Prefer switch expressions over `Match`, and don't use a `_` discard arm.** The compiler warns (CS8509) when a case is missing, which is the main benefit of unions. A discard arm silences that warning for cases added later. If a `default` union can arrive (an uninitialized field), add a `null =>` arm instead.
- **Use `Success`, not `bool`, for "it worked" results.** `Union<Success, NotFoundMessage>` says what happened; a `bool` case is boxed and ambiguous.
- **Call `DecorateWithLogging()` exactly once, after all registrations.** It decorates every `[LoggingDecorator]` interface in the compilation and throws `InvalidOperationException` ("No registration for ... was found") if one is not registered yet. Calling it twice wraps services twice and doubles every log line.
- **Per-case logging needs an async union return.** `Task<Union<...>>` and `ValueTask<Union<...>>` get Information for success cases and Warning for error cases (type names containing NotFound, BadRequest, Error, Failure, Unauthorized, Forbidden, Conflict or InvalidOperation). A synchronous union return is logged as a plain value.
- **Don't write audit fields or user IDs.** The `AuditInterceptor` sets `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` on save from your `ICurrentUserAccessor`. Repository methods take no user ID.
- **Repository writes return `bool`.** `InsertAsync`, `UpdateAsync`, `SoftDeleteAsync` and the rest return `Task<bool>`; turn `false` into a result record (`BadRequestMessage` for a failed write, `NotFoundMessage` for a delete that found nothing).
- **Soft delete is the default; queries hide deleted rows automatically.** Don't add `!IsDeleted` filters. Use `HardDeleteAsync` only when the row must physically go.
- **Update a slim entity.** Load the entity without `includes`, change it, then `UpdateAsync`. Calling `UpdateAsync` on an entity loaded with navigation includes can corrupt EF change tracking. Reload with includes afterwards if the response needs them.
- **`InsertAsync` does not walk navigation graphs.** To link two entities in a many-to-many relationship, insert both, add one to the other's collection, then `UpdateAsync` the owner (details in `references/data.md`).
- **Check unique values in the handler.** A duplicate on a unique index throws `DbUpdateException`, which surfaces as a 500. Look the value up first and return a result record; keep the index as the real guarantee.
- **Never enable `PurgeBeforeSeed` against Production.** It deletes the whole table before reseeding. It is for local development and staging only.
- **Seed file names are fully qualified type names** (`MyApp.Orders.Models.Order.json`). A wrong name is skipped with only a warning, and renaming an entity silently breaks the match.
- **Use named constants for permissions and paths.** Endpoints pass permission strings to `RequireAuthorization(Permissions.X)`; keep them in one `Permissions` class so they match the Entra app role values exactly.
- **Register UserJourney before the service defaults, and `UseUserJourney()` after authentication.** Registered after `AddServiceDefaults()` or `UseAzureMonitor()`, Blazor clicks are exported before the user is added; placed before authentication, requests have no user.
- **Configuration precedence is standard.** `AddAppSettings` files are overridden by user secrets, environment variables and command-line arguments. To name settings files after deployment environments, use `AddAppSettings(o => o.SettingsName = ...)` rather than renaming the environment, because user secrets and development-only features need `Development`.
- **Set local token injection explicitly.** `IdentityApi:EnableLocalTokenInjection` is `true` in local settings and `false` in every deployed environment; never rely on the environment name alone for a sign-in shortcut.
- **Remove NU1510 package references.** On .NET 11 the `Microsoft.Extensions.*.Abstractions` packages are part of the framework; referencing them only adds warnings.

## Before you finish

Build and run the tests. If you touched entities, confirm `dotnet ef migrations has-pending-model-changes` reports what you expect. If you added an `[LoggingDecorator]` interface, confirm it is registered before `DecorateWithLogging()`, or the app throws at startup rather than at build time.
