# CoreDesign 1.1.0 Announcement

## Technical Audience

CoreDesign 1.1.0 ships today with two new packages and a set of breaking changes in `CoreDesign.Data`.

---

**`dotnet seed`: a new CLI tool for seed data management**

`CoreDesign.SeedTool` is a global/local `dotnet tool` that makes it straightforward to capture live entity data and keep it in sync with the deployment pipeline.

```
dotnet tool install -g CoreDesign.SeedTool
dotnet seed setup
dotnet seed export
dotnet seed diff
```

`setup` is an interactive wizard that writes a `coredesign.seedtool.json` config file. It offers four connection modes covering Aspire AppHost projects (reads from the project's own config and user secrets, resolves `{ParameterName}` Aspire placeholders automatically), template-plus-secrets, template-only with `--password` / `SEEDTOOL_PASSWORD`, and direct. Once configured, `export` and `diff` require no additional flags.

`diff` exits with code 2 when differences are found, making it usable as a CI gate.

The tool discovers your `DbContext` by reflection. Add a `CoreDesignSeedFactory<TContext>` to your application project once and the tool picks it up automatically:

```csharp
public class AppSeedFactory : CoreDesignSeedFactory<AppDbContext>
{
    protected override void Configure(DbContextOptionsBuilder<AppDbContext> builder, string cs)
        => builder.UseSqlServer(cs);

    protected override AppDbContext Create(DbContextOptions<AppDbContext> options)
        => new(options);
}
```

---

**`PurgeBeforeSeed`: replace seed data instead of only filling gaps**

`MigrationWorker` now supports a `PurgeBeforeSeed` list. Entity types listed by short or fully qualified name have their table cleared before their seed file is applied. Configure it in appsettings, via environment variable, or in code:

```json
"MigrationWorker": {
  "PurgeBeforeSeed": [ "SiteContent" ]
}
```

The typical workflow: edit content in the UI, run `dotnet seed export`, review with `git diff`, commit, add the entity to `PurgeBeforeSeed`, deploy, then remove it again. `MigrationWorker` handles the rest.

---

**Breaking changes in `CoreDesign.Data` 1.1.0**

The `userId` parameter has been removed from all `ICudRepository` methods. `AuditInterceptor` fills in `CreatedBy` and `UpdatedBy` automatically, so the parameter was no longer doing anything useful.

`DeleteAsync` is now `SoftDeleteAsync` and `DeleteRangeAsync` is now `SoftDeleteRangeAsync`. Two cascade variants have been added: `SoftDeleteCascadeAsync` and `HardDeleteCascadeAsync`. Both walk the EF Core navigation graph recursively, so parent-and-children scenarios no longer require custom loop code.

`BaseEntityExtensionMethods` (`InitializeAuditFields`, `UpdateAuditFields`) has been removed. The interceptor handles this.

`IReadRepository` optional parameters and `GetAsync`/`GetAttachedAsync` return types are now properly nullable.

All breaking changes are compile-time errors, so the migration is mechanical.

---

**Packages in this release**

| Package | Version | Notes |
|---|---|---|
| CoreDesign.Data | 1.1.0 | Breaking changes; cascade delete; PurgeBeforeSeed |
| CoreDesign.SeedTool | 1.0.0 | New dotnet tool |
| CoreDesign.Shared | 1.0.3 | Aspire 13.4.2 |
| CoreDesign.Logging | 1.1.2 | Dep update |
| CoreDesign.Identity.Server | 1.0.8 | JWT dep update |
| CoreDesign.Identity.Client | 1.0.8 | Version alignment |

GitHub: github.com/codyskidmore/CoreDesign

---

## General Audience

I shipped a new version of CoreDesign this week.

The headline feature is `dotnet seed`, a command-line tool for teams who manage reference data in their database alongside code. The idea is simple: instead of manually writing seed SQL scripts or maintaining entity lists in code, you run `dotnet seed export` against your database and it writes JSON files that your migration service picks up on the next deploy.

The diff command (`dotnet seed diff`) compares those JSON files against the live database and tells you exactly what has changed. You can wire it into CI so deployments fail if seed data has drifted from what was committed.

It handles the fiddly parts: Aspire connection strings with placeholder parameters, user secrets, Docker SQL Server IPv6 routing, and multi-environment configs.

The other big addition is cascade delete. If you have a parent entity with children and you want to soft-delete or hard-delete the whole tree, the repository now has `SoftDeleteCascadeAsync` and `HardDeleteCascadeAsync`. They walk the EF Core navigation graph automatically.

There are breaking changes: `userId` is gone from all write methods because the audit interceptor handles that now, and the soft-delete methods were renamed to make their intent explicit. Everything shows up as a compile error so it's easy to find and fix.

Packages are on NuGet now.
