# Upgrading to CoreDesign 11 (from 1.x and OneOf)

CoreDesign 1.x targets .NET 10. CoreDesign 11 targets .NET 11 only, and every package moved to the same version together. Use 11.0.1 or later; 11.0.0 was published briefly and withdrawn. The full upgrade guide is `CoreDesign.NET11-migration.md` in the CoreDesign repository; this is the working checklist.

## Should the project upgrade?

1.x packages run on the .NET 11 runtime unchanged. Upgrade when the project moves to .NET 11 or wants new CoreDesign features (all new features ship only on 11.x). Ask before upgrading a project that targets .NET 10 LTS on purpose.

## Steps

1. `global.json`: SDK `11.0.100`, `rollForward: latestFeature`, `allowPrerelease: false`.
2. Every project: `<TargetFramework>net11.0</TargetFramework>`; remove `<LangVersion>`.
3. Every CoreDesign package reference: `11.0.1`.
4. Microsoft ASP.NET Core and `Microsoft.Extensions.*` packages: 11.0.0. EF Core stays 10.x for PostgreSQL until Npgsql 11 is stable. Aspire packages and the AppHost SDK: 13.6.5 or later (13.4.x has a high severity advisory).
5. Remove references that now raise NU1510 (`Microsoft.Extensions.*.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Primitives`).
6. If the project references or pins `Microsoft.OpenApi` 2.x, move it to 3.x (CoreDesign.Identity.Client needs it).
7. Build, fix the breaks below, run the tests, then run `dotnet ef migrations has-pending-model-changes` to confirm the model did not change.

## Breaks you will see

| Error | Cause | Fix |
|---|---|---|
| CS0246 `OneOf` not found | CoreDesign.Logging no longer brings OneOf in | `dotnet add package OneOf`, or migrate to `Union<>` |
| CS0104 `Success` is ambiguous | `OneOf.Types.Success` and CoreDesign's `Success` both imported | Remove `using OneOf.Types;` or alias one |
| NU1605 downgrade of `Microsoft.OpenApi` | A 2.x pin | Reference 3.x |
| NU1510 warnings | Framework-provided packages referenced | Remove the references |
| A setting has a different value in a deployed environment | Environment variables, user secrets and command-line arguments now override `AddAppSettings` files | Remove whichever duplicate should not apply |
| `dotnet seed` fails to start | Tool needs the .NET 11 runtime | `dotnet tool update -g CoreDesign.SeedTool` |

## Replacing OneOf with Union

OneOf keeps working with CoreDesign 11 (the logging generator recognizes it by name), so migrate when convenient, one method at a time if needed.

| OneOf | Union |
|---|---|
| `OneOf<A, B>` | `Union<A, B>` (up to four cases; declare a `union` for more) |
| `OneOf<A, B>.FromT0(x)` / `FromT1(x)` | `new Union<A, B>(x)`, or just `return x;` |
| `.Match(a => ..., b => ...)` | Same; prefer a switch expression |
| `.Switch(a => ..., b => ...)` | `switch` statement or `if (result is A a)` |
| `.IsT0` | `result is A` |
| `.AsT0` | `result is A a` then `a`, or `(A)result.Value!` |
| `.TryPickT0(out var a, out _)` | `result is A a` |
| `.Index` | No equivalent; match on the type |
| `OneOf.Types.Success` | `CoreDesign.Shared.Infrastructure.Success` |
| `OneOf<bool, NotFoundMessage>` | `Union<Success, NotFoundMessage>`; replace `return true;` with `return new Success();` and check each `false` path |

A mechanical approach for a large code base: replace `OneOf<` with `Union<`, replace `.FromT0(` style factories with `new Union<...>(`, remove `using OneOf;`, build, and fix what remains by hand. Then search for `Union<bool,` and convert those to `Success`, reading each `return true;` site. Remove the OneOf package once nothing uses it.

After migrating, confirm logging is unchanged: the generated decorators should have the same Warning cases as before (compare the files under `obj/` with `EmitCompilerGeneratedFiles` on).
