# Migrating CoreDesign to .NET 11

This release moves every CoreDesign package to .NET 11 and C# 15, and adds first-class support for C# 15 union types. It is the release new CoreDesign features will build on, including the upcoming activity tracking packages.

**You do not have to upgrade to keep running on .NET 11.** The current CoreDesign packages target .NET 10, and .NET 10 libraries run on the .NET 11 runtime. Stay on the current versions if you are on .NET 10 LTS, or if you want to keep your code exactly as it is for now. Upgrade when you want the new features.

## At a glance

| Change | Breaking? | Action |
| --- | --- | --- |
| All packages target `net11.0` only | Yes | Move your projects to `net11.0` and the .NET 11 SDK |
| `CoreDesign.Logging` no longer brings in OneOf | Yes, if you use OneOf without referencing it directly | Add `dotnet add package OneOf`, or migrate to `Union<>` |
| `CoreDesign.Shared` adds `Success`, which can clash with `OneOf.Types.Success` | Yes, if a file imports both namespaces | Remove `using OneOf.Types;` or alias one of them |
| `CoreDesign.SeedTool` requires the .NET 11 runtime | Yes, for tool users | Install the .NET 11 runtime or SDK |
| `CoreDesign.Identity.Client` requires `Microsoft.OpenApi` 3.x | Yes, if you reference `Microsoft.OpenApi` 2.x or write OpenAPI transformers | Move your `Microsoft.OpenApi` reference to 3.x |
| `Microsoft.Extensions.*.Abstractions` packages are now part of .NET 11 | No, but you will see NU1510 warnings | Remove the redundant package references |
| `CoreDesign.Shared` adds `Union<...>` | No | Optional: replace OneOf with `Union<>` |
| `CoreDesign.Logging` logs C# 15 union results per case | No | None, it is automatic |

## Step 1: Move to .NET 11

1. Install the [.NET 11 SDK](https://dotnet.microsoft.com/download).
2. Set `<TargetFramework>net11.0</TargetFramework>` in every project that references a CoreDesign package.
3. If you have a `global.json`, point it at an 11.0 SDK.
4. Do not set `<LangVersion>`. C# 15 is the default for `net11.0`. Avoid `preview` and `latest`.
5. Update every CoreDesign package reference to `11.0.0`. All CoreDesign packages now share one version, and the major version matches .NET.

## Step 2: Fix what the upgrade breaks

### OneOf is no longer included with CoreDesign.Logging

Previous versions of `CoreDesign.Logging` declared a NuGet dependency on `OneOf`, so installing Logging also installed OneOf. CoreDesign never used OneOf itself; the dependency only existed for convenience. It has been removed.

If your code uses OneOf and you never referenced it yourself, the build fails with `CS0246: The type or namespace name 'OneOf' could not be found`. Add the reference:

```bash
dotnet add package OneOf
```

The logging decorators keep logging OneOf results per case, success at Information and error cases at Warning, exactly as before. The generator recognizes OneOf by name and needs no reference to it.

### `Success` is ambiguous

`CoreDesign.Shared` now includes a `Success` record for union results. OneOf ships its own `OneOf.Types.Success`. A file that imports both namespaces and mentions `Success` fails with `CS0104: 'Success' is an ambiguous reference`.

Fix it one of these ways:

- Remove `using OneOf.Types;` if the file only needs `Success` (it will then use CoreDesign's).
- Alias the one you mean: `using Success = OneOf.Types.Success;`.
- Migrate the file to `Union<>` (Step 3), which uses CoreDesign's `Success`.

### CoreDesign.SeedTool needs the .NET 11 runtime

The `dotnet seed` tool now targets .NET 11. Update it with `dotnet tool update -g CoreDesign.SeedTool` and make sure the .NET 11 runtime is installed on any machine that runs it.

### CoreDesign.Identity.Client needs Microsoft.OpenApi 3.x

`Microsoft.AspNetCore.OpenApi` 11 requires `Microsoft.OpenApi` 3.x, so `CoreDesign.Identity.Client` now references `Microsoft.OpenApi` 3.11.0. If your project references or pins `Microsoft.OpenApi` 2.x, restore fails with `NU1605: Detected package downgrade`. Move your reference to 3.x:

```xml
<PackageReference Include="Microsoft.OpenApi" Version="3.11.0" />
```

If you wrote your own OpenAPI document or operation transformers, rebuild them against 3.x and check for API changes. CoreDesign's own `BearerSecurityTransformer` needed no changes.

### NU1510 warnings for Microsoft.Extensions packages

.NET 11 moved these assemblies into the base framework, so referencing them as packages is redundant:

- `Microsoft.Extensions.Caching.Abstractions`
- `Microsoft.Extensions.Configuration.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Diagnostics.Abstractions`
- `Microsoft.Extensions.FileProviders.Abstractions`
- `Microsoft.Extensions.Hosting.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Options`
- `Microsoft.Extensions.Primitives`

NuGet reports `NU1510: PackageReference ... will not be pruned` for each one. Remove the `PackageReference` to clear the warning. `CoreDesign.Logging` has already dropped its own references to the logging and dependency injection abstractions for this reason.

## Step 3 (optional): Replace OneOf with `Union<>`

C# 15 unions are built into the language. `CoreDesign.Shared` provides general-purpose unions that work like OneOf, so most code migrates with a rename:

```csharp
// Before
Task<OneOf<Cabin, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct);

// After
Task<Union<Cabin, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct);
```

`Union<T0, T1>`, `Union<T0, T1, T2>` and `Union<T0, T1, T2, T3>` are in the `CoreDesign.Shared.Infrastructure` namespace, the same namespace as `NotFoundMessage` and the other result records. Methods still return case values directly; the conversion to the union is implicit.

### Member by member

| OneOf | Union |
| --- | --- |
| `OneOf<A, B>` | `Union<A, B>` (up to four cases) |
| `result.Match(a => ..., b => ...)` | Same, `Match` is included. A switch expression is preferred (see below). |
| `result.Switch(a => ..., b => ...)` | A `switch` statement, or `if (result is A a) ...` |
| `result.IsT0` | `result is A` |
| `result.AsT0` | `result is A a` then use `a`, or `(A)result.Value!` |
| `result.TryPickT0(out var a, out var rest)` | `result is A a` |
| `result.Value` | `result.Value` (`object?`) |
| `result.Index` | No equivalent. Match on the case type instead. |
| `OneOf<IEnumerable<T>, B>.FromT0(list)` | Not needed. `return list;` converts implicitly, even when the case is an interface. |
| `OneOf.Types.Success` | `CoreDesign.Shared.Infrastructure.Success` |
| More than four cases | Declare your own union: `public union MyResult(A, B, C, D, E);` |

### Prefer switch expressions over `Match`

`Match` exists so migrated code keeps compiling. New code should use a switch expression. The compiler checks it for exhaustiveness and warns (CS8509) when a case is missing:

```csharp
var result = await service.GetAsync(id, ct);
return result switch
{
    Cabin cabin           => Results.Ok(cabin.ToResponse()),
    NotFoundMessage error => Results.NotFound(error.Message),
};
```

Switch-expression arms cannot contain statements. Run side effects for a case before the switch:

```csharp
if (result is Cabin)
    _ = outputCacheStore.EvictByTagAsync("cabins", ct);

return result switch { ... };
```

### Things that behave differently

- **`default` holds no value.** A `default(Union<A, B>)` (an uninitialized field or array element, for example) has `Value == null`. `Match` throws `InvalidOperationException` for it, and a switch expression without a `null` arm throws `SwitchExpressionException`. Add a `null =>` arm wherever a default union can arrive. Values returned from a method that always returns a case are safe.
- **Prefer a `null` arm over `_`.** A discard arm hides newly added cases from the exhaustiveness check; a `null` arm keeps the check working.
- **Don't test for the union type itself.** `result is Union<A, B>` is error CS8121. Use `result is not null` to check that a union holds a value.
- **Avoid repeating a case type.** `Union<string, string>` compiles, but converting a `string` to it is ambiguous (CS0457). Give each case its own type.
- **Value-type cases are boxed.** `Union<bool, NotFoundMessage>` stores the `bool` as an object. Prefer `Success` over `bool` for "it worked" results.
- **Tests:** Moq's `.ReturnsAsync(new NotFoundMessage(...))` keeps working against a `Union<>` return type, through the implicit conversion. Replace `Assert.True(result.IsT0)` with `Assert.True(result is A)`.

You can migrate one method at a time. OneOf and `Union<>` results can live in the same interface, and the logging decorator handles both.

### Your own union types

Any C# 15 union works with CoreDesign, not only `Union<>`:

```csharp
public union CreateResult(WeatherForecast, BadRequestMessage);
```

The logging generator recognizes every type that implements `System.Runtime.CompilerServices.IUnion`, which all compiler-generated unions do, and logs each case by its type name.

### Removing OneOf

Once no code uses OneOf, remove the package reference and any `using OneOf;` and `using OneOf.Types;` directives. Nothing in CoreDesign needs it.

## Dependency versions

- Microsoft ASP.NET Core and `Microsoft.Extensions.*` packages move to 11.0.0.
- **EF Core stays on 10.0.x (10.0.15)** in this release. The Npgsql EF Core provider for EF Core 11 has not reached a stable release yet, and EF Core 11 cannot run with the Npgsql 10 provider. EF Core 10 runs on .NET 11. If you use SQL Server and want EF Core 11 now, reference EF Core 11 in your own project; NuGet resolves the higher version.
- **Aspire moves from 13.4.6 to 13.6.5**, which fixes CVE-2026-120006 (GHSA-f7vh-h5g3-7jrh), a high severity elevation of privilege issue in Aspire hosting. Move your own Aspire packages and the AppHost SDK to 13.6.5 as well.
- `MessagePack` moves to 3.1.12, which fixes GHSA-qhrr-8q5h-9q3h.

## IDE support

Union types need an IDE that understands C# 15. With an older JetBrains Rider or Visual Studio, the code builds from the command line but the editor shows false errors. Update the IDE if you see red squiggles on `union` declarations or `Union<>` conversions.
