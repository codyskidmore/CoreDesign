# Proposal: A Clean Dependency Graph for CoreDesign, and Audit History in CoreDesign.Data

| | |
|---|---|
| Scope | Every CoreDesign NuGet package; audit history for CoreDesign.Data |
| Date | 2026-11-19 |
| Status | Proposed, for review |
| Evidence | Measured on `main` at `6a56e80` and on trimmed copies built outside the repository (section 10) |

## 1. Summary

CoreDesign has close to 15,000 downloads. The packages are meant to be adopted one at a time: install the one you need, get that feature and nothing else. For most packages that holds. For the most widely referenced one, CoreDesign.Shared, it does not, and it affects every package that depends on it.

**A project that installs only CoreDesign.Logging pulls in 69 NuGet packages today**, including Aspire's hosting stack, gRPC, the Kubernetes client, EF Core and MessagePack. None of them are used by Logging. After the changes below, the same project pulls in **none**.

There is also a **release blocker**: EF Core 11.0.0 was added to CoreDesign.Data and CoreDesign.Shared in #52 (merged today, not yet released), while the PostgreSQL provider for EF Core 11 is still a release candidate. Released as is, it would push EF Core 11 into every PostgreSQL app that uses CoreDesign.Data, Shared or Logging.

| Release | Changes | Breaking |
|---|---|---|
| **11.0.2** (now) | EF Core back to 10.0.15; Shared declares the two configuration packages it silently takes from Aspire; CI dependency checks so the graph cannot get worse | No |
| **11.1.0** | New `AddAppSettings` overload that needs no Aspire types; the Aspire overload marked `[Obsolete]`; central package management; a "Dependencies" section in every README; audit history in CoreDesign.Data (opt-in) | No |
| **12.0.0** | Shared drops Aspire, EF Core and MessagePack; Logging drops its reference to Shared | Yes, announced in 11.1.0 |

Section 9 lists the decisions needed, including whether to bring the 12.0.0 removals forward.

## 2. Findings

### 2.1 Release blocker: EF Core 11 in CoreDesign.Data and CoreDesign.Shared

| | Released 11.0.1 | `main` after #52 |
|---|---|---|
| CoreDesign.Data: `Microsoft.EntityFrameworkCore`, `.Relational` | 10.0.15 | **11.0.0** |
| CoreDesign.Shared: `Microsoft.EntityFrameworkCore` | 10.0.15 | **11.0.0** |
| `Npgsql.EntityFrameworkCore.PostgreSQL` latest on NuGet | | **11.0.0-rc.3** (no stable 11.x) |

The release notes and the CoreDesign skill both state that EF Core stays on 10.x until the Npgsql provider for EF Core 11 is stable. A NuGet package's dependency is a minimum, so CoreDesign.Data 11.0.2 as it stands on `main` would lift any consuming app to EF Core 11 while it still runs the Npgsql 10 provider. Providers are tied to their EF Core major version, so that combination is unsupported and is expected to fail at runtime, not at restore.

Because Shared also references EF Core, the lift reaches apps that never asked for EF Core features from CoreDesign: any app on CoreDesign.Logging (which references Shared) and EF Core 10.

Sample did not catch it, because Sample references the **published** 11.0.1 packages, not the code on `main`. Section 6.3 closes that gap.

**Fix:** revert both to 10.0.15 before the next release. Move to 11.x for all CoreDesign packages at once, when Npgsql 11 is stable.

### 2.2 CoreDesign.Shared is the heaviest package, and the most shared

| Dependency of Shared | What in Shared uses it |
|---|---|
| `Aspire.Hosting.AppHost` | One method: the `AddAppSettings(this IDistributedApplicationBuilder)` overload for Aspire AppHost projects |
| `Microsoft.EntityFrameworkCore` | Nothing |
| `MessagePack` | Nothing. It is a security pin for a vulnerable version that Aspire brings in |
| `Microsoft.Extensions.Configuration.Json`, `DependencyInjection`, `Options.ConfigurationExtensions` | `AddAppSettings`, `DatabaseOptions` |
| `Ulid` | Result types and helpers |

Shared's own description says it is "intended to be referenced by both host/API projects and AppHost projects". That dual role is the root cause: everything an AppHost needs is forced on every API that uses Shared, and on every package that references Shared.

### 2.3 What it costs users

A project referencing only CoreDesign.Logging restores **69 packages** today. Among them:

* `Aspire.Hosting`, `Aspire.Hosting.AppHost`, `KubernetesClient`, `StreamJsonRpc`, `Nerdbank.MessagePack`
* `Grpc.AspNetCore`, `Grpc.Net.Client`, `Grpc.Tools` and five more gRPC packages
* `Microsoft.EntityFrameworkCore` and its analyzers
* `MessagePack`, `Newtonsoft.Json`

The cost is not only size and restore time:

* **Security patches.** The Aspire chain has already forced three security releases of Shared, each of which every Logging user had to take: `MessagePack` pinned to 3.1.7 (GHSA-hv8m-jj95-wg3x), `MessagePack` to 3.1.12 (GHSA-qhrr-8q5h-9q3h), and Aspire to 13.6.5 (CVE-2026-120006). Vulnerability scanners in users' pipelines flag these packages in apps that never run Aspire.
* **Version conflicts.** Shared decides the minimum EF Core and Aspire versions for apps that use neither through CoreDesign (section 2.1).
* **Perception.** The dependency list is the first thing a careful evaluator reads on nuget.org. "Logging decorators" depending on a container orchestrator reads as poor design.

### 2.4 Shared silently depends on Aspire for two configuration packages

Removing Aspire from a copy of Shared fails to compile: `AddAppSettings` uses `Microsoft.Extensions.Configuration.EnvironmentVariables` and `Microsoft.Extensions.Configuration.CommandLine`, which Shared never declared and received through Aspire. Declaring them makes the build pass. This is fixed in 11.0.2 independently of everything else, because an undeclared dependency breaks the moment the package that supplies it changes.

### 2.5 CoreDesign.Logging does not need CoreDesign.Shared

Logging's runtime code and its source generator use nothing from Shared. The generator recognises unions through .NET's own `System.Runtime.CompilerServices.IUnion`, so it works with `Union<...>` from Shared, with custom unions and with C# 15 `union` types alike. A copy of Logging with the Shared reference removed builds with no errors. The reference exists for convenience, so that Logging users get `Union<...>` and the result records without a second install.

### 2.6 The rest of the graph is in good shape

| Package | Depends on | Assessment |
|---|---|---|
| CoreDesign.Data | EF Core, Ulid | Right |
| CoreDesign.ExceptionHandling | Its own source generator | Right |
| CoreDesign.Identity.Client | JWT bearer, OpenAPI, HTTP client factory | Right |
| CoreDesign.Identity.Server | `System.IdentityModel.Tokens.Jwt` | Right |
| CoreDesign.UserJourney | `OpenTelemetry.Extensions.Hosting` | Right |
| CoreDesign.UserJourney.Blazor | CoreDesign.UserJourney | Right; every Blazor user needs the core package |
| CoreDesign.SeedTool | CoreDesign.Data | Right; a .NET tool, so nothing flows to apps |
| `@coredesign.dev/user-journey` (npm) | Nothing (React is an optional peer) | Right |
| CoreDesign.Logging | **CoreDesign.Shared** | Convenience only (2.5) |
| CoreDesign.Shared | **Aspire, EF Core, MessagePack**, configuration, DI, Ulid | Wrong (2.2) |

Two of eleven packages need work. The problem is concentrated, which makes it fixable in one release cycle.

## 3. Rules for the ecosystem

These apply to every existing and future CoreDesign package. They go into the repository's contributor guide and are enforced by CI where possible (section 6).

1. **A package references another only when every user of the first needs the second.** If only some users need it, it belongs in a separate package or in the app.
2. **A new package is justified by a new dependency or a different host** (ASP.NET Core, Blazor, the browser, an Aspire AppHost, a .NET tool). A new feature alone is not a reason: it goes in the package whose dependencies it already uses, switched off by default.
3. **Packages connect through conventions, not references.** Trace IDs, standard tag names and small interfaces let packages work together without depending on each other. UserJourney and the audit history in section 5 meet through the trace ID; neither references the other.
4. **Opt-in features cost nothing when off.** No model changes, no middleware, no hosted services, no telemetry, no migrations until the app turns them on.
5. **Shared is the lightest package.** It holds only types and helpers that need nothing beyond `Microsoft.Extensions.*` configuration and DI. No hosting stacks, no data access, no serialisers.
6. **Every dependency is declared.** A package that uses a type references the package that defines it, never relying on it arriving through something else.
7. **One version of each third-party dependency across all CoreDesign packages,** managed centrally, so packages cannot drift apart (as EF Core did in Shared and Data).
8. **Dependency changes are visible.** Adding, removing or changing the major version of a dependency is called out in the release notes, and removing one is a breaking change that waits for the next major version unless a decision says otherwise (section 9).

## 4. The plan in detail

### 4.1 11.0.2: patch, now

| Change | Files |
|---|---|
| EF Core and EF Core Relational back to 10.0.15 | `src/CoreDesign.Data/CoreDesign.Data.csproj`, `src/CoreDesign.Shared/CoreDesign.Shared.csproj`, and the EF Core packages in the test projects (`Microsoft.EntityFrameworkCore.InMemory` is 11.0.0 in `CoreDesign.Data.Tests`) |
| Declare `Microsoft.Extensions.Configuration.EnvironmentVariables` and `.CommandLine` (11.0.0) | `src/CoreDesign.Shared/CoreDesign.Shared.csproj` |
| Dependency allowlist test and consumer checks (section 6), with today's graph as the allowlist so it can only shrink | New test project; CI workflow |
| Release notes entry explaining the EF Core revert | `RELEASE_NOTES.md` |

The workbook and user journey changes on `user-journey-workbook-fix` can ship in this release, or in 11.1.0 with the click naming feature, which adds public API.

### 4.2 11.1.0: minor, additive

**An `AddAppSettings` overload that needs no Aspire types.** Today the shared implementation is a private method taking the three things both builders have:

```csharp
private static void AddAppSettings(IConfigurationManager configuration, IHostEnvironment env,
    IServiceCollection services, Action<AppSettingsOptions>? configure)
```

Make it public as an extension on `IConfigurationManager`:

```csharp
public static IConfigurationManager AddAppSettings(this IConfigurationManager configuration,
    IHostEnvironment environment, IServiceCollection services, Action<AppSettingsOptions>? configure = null)
```

An AppHost then calls it in one line, with no CoreDesign package depending on Aspire:

```csharp
builder.Configuration.AddAppSettings(builder.Environment, builder.Services);
```

No new package is needed. A dedicated `CoreDesign.Aspire.Hosting` package for one three-line method would add a package to install, version and document for no benefit (rule 2).

**Mark the Aspire overload obsolete,** with a message that tells users exactly what to change:

```csharp
[Obsolete("Use builder.Configuration.AddAppSettings(builder.Environment, builder.Services). " +
          "This overload will be removed in CoreDesign 12, which removes the Aspire dependency from CoreDesign.Shared.")]
```

The warning appears in every AppHost build, which announces the change to exactly the users it affects.

**Central package management.** One `Directory.Packages.props` with every third-party version, so EF Core, `Microsoft.Extensions.*` and the rest cannot differ between CoreDesign packages (rule 7).

**A "Dependencies" section in every package README,** listing direct dependencies and why each is there. For Shared and Logging it states what changes in 12.0.

**Audit history** in CoreDesign.Data (section 5).

### 4.3 12.0.0: major, with .NET 12

| Change | Effect |
|---|---|
| Shared removes `Aspire.Hosting.AppHost`, `Microsoft.EntityFrameworkCore`, `MessagePack` and the obsolete Aspire overload | A Shared user restores **11** packages instead of 69 |
| Logging removes its reference to Shared | A Logging user restores **0** packages. Apps using `Union<...>` or the result records add CoreDesign.Shared themselves; most already reference it directly |
| Shared's description drops "and AppHost projects" | |

Who is affected, and how they find out:

| Who | What breaks | What they do |
|---|---|---|
| AppHost projects calling `builder.AddAppSettings()` | Compile error (CS1061) | Use the 11.1.0 overload; they will have seen the obsolete warning for a year |
| Projects using EF Core types without referencing EF Core | Compile error | Add `Microsoft.EntityFrameworkCore`, which they were already using |
| Projects using `Union<...>` with only Logging installed | Compile error | Add CoreDesign.Shared |

The upgrade guide for 12.0 gets a section for each.

## 5. Audit history in CoreDesign.Data

### 5.1 Why Data, and not a new package

Audit history needs EF Core change tracking, `BaseEntity`, `ICurrentUserAccessor` and the existing `AuditInterceptor`. All of them are already in CoreDesign.Data, and Data depends only on EF Core and Ulid. The feature adds no dependency, so by rule 2 it is not a new package. A separate package would have to reach into Data's interceptor or duplicate it, and would depend on Data anyway. By rule 4 it costs nothing until switched on.

### 5.2 What it records

One row per changed property, written in the same `SaveChanges` transaction as the change, so history and data can never disagree.

| Column | Type | Content |
|---|---|---|
| `Id` | Ulid | Row ID |
| `EntityType` | string | Entity's CLR name, for example `Tms.Tags.Tag` |
| `EntityId` | Ulid | The changed row. `BaseEntity` IDs are generated in the app, so the ID is known before the save |
| `Action` | string | `Created`, `Updated`, `Deleted` (soft), `Restored`, `Removed` (hard) |
| `Property` | string, nullable | Property name; empty for `Removed` |
| `OldValue`, `NewValue` | string, nullable | Invariant culture text, capped at 4,000 characters, or `(hidden)` |
| `ChangedBy` | Guid | From `ICurrentUserAccessor`, as `UpdatedBy` is today |
| `ChangedAt` | DateTime (UTC) | Same timestamp as `UpdatedAt` |
| `TraceId` | string(32) | `Activity.Current?.TraceId`, which is `operation_Id` in Application Insights |

Indexes on (`EntityType`, `EntityId`, `ChangedAt`) for "history of this record" and on `TraceId` for "what did this click change".

Rules:

* Scalar properties only. Navigations are recorded through their foreign key properties.
* `CreatedAt`, `CreatedBy`, `UpdatedAt` and `UpdatedBy` are not recorded; the row already carries who and when.
* A soft delete (`IsDeleted` false to true) is recorded as `Deleted`, the reverse as `Restored`, not as a property change.
* Seeding through `MigrationWorker` writes no history, so seed files do not flood the table.

### 5.3 Turning it on

```csharp
// In the DbContext: adds the AuditHistory entity to the model.
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.AddAuditHistory();
}

// In each entity configuration that should be audited.
public override void Configure(EntityTypeBuilder<Tag> builder)
{
    base.Configure(builder);
    builder.Audited();
    builder.Property(t => t.Notes).AuditValuesHidden();   // records "changed", never the text
}
```

Then generate the migration with the tooling as usual (`dotnet ef migrations add AddAuditHistory`). Apps that do not call `AddAuditHistory()` get no table, no migration and no extra work on save.

`AuditValuesHidden()` is Data's own setting. It deliberately does not reuse `[Redact]` from CoreDesign.Logging, because that would make Data reference Logging (rule 1).

### 5.4 Reading it

```csharp
IReadRepository<AppDbContext, AuditHistory> history;
var changes = await history.GetAllAsync(h => h.EntityType == typeof(Tag).FullName && h.EntityId == id, token: ct);
var byTrace = await history.GetAllAsync(h => h.TraceId == operationId, token: ct);
```

The user journey workbook's timeline gains a **Trace** column (the `operation_Id`). Reproducing a bug becomes: find the user's timeline, take the trace ID of the click, and query `AuditHistory` for exactly which values it changed. The two features meet through the trace ID only (rule 3).

### 5.5 Limits, stated in the README

* Changes made with `ExecuteUpdate`, `ExecuteDelete` or raw SQL bypass change tracking and are not recorded. The CoreDesign repositories use change tracking for every write; only `MigrationWorker`'s `PurgeBeforeSeed` uses `ExecuteDelete`.
* Retention is the app's decision. The history is personal data (who changed what), so apps need a retention period, restricted access to the table and a line in their privacy policy. A purge helper can follow if teams ask.
* Values are text. Complex and JSON-mapped properties are recorded as their serialised form, capped in length.

### 5.6 Tests

Created, updated, soft-deleted, restored and hard-deleted rows; hidden values; unaudited entities untouched; seeding skipped; trace ID captured inside an `Activity` and empty outside one; a model without `AddAuditHistory()` unchanged (snapshot comparison). The existing Data tests use EF Core's in-memory provider, which does not enforce transactions or relational constraints, so the "same transaction" guarantee also needs a relational test (SQLite in-memory is the lightest option).

## 6. Keeping the graph clean: CI checks

### 6.1 Direct dependency allowlist

A test project, `tests/CoreDesign.Dependencies.Tests`, reads every packable `.csproj` under `src` and compares its `PackageReference` and `ProjectReference` items with `dependencies.allowed.json`:

```json
{
  "CoreDesign.Data": ["Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Relational", "Ulid"],
  "CoreDesign.UserJourney": ["OpenTelemetry.Extensions.Hosting"],
  "CoreDesign.UserJourney.Blazor": ["CoreDesign.UserJourney"]
}
```

A new dependency fails the build until the allowlist changes in the same pull request, so every addition is a reviewed decision. In 11.0.2 the allowlist matches today's graph; 12.0 shrinks it.

### 6.2 Transitive budget per package

A CI step creates a throwaway project per package, references it, restores, and counts packages with `dotnet list package --include-transitive`. Each package has a ceiling (Logging 0 after 12.0, Shared 11, Data around its EF Core count), and exceeding it fails the build. This catches heavy dependencies that arrive transitively, which the allowlist alone cannot see.

### 6.3 Sample against the code being released

Sample references the published packages, which is why the EF Core 11 change was not caught. Add a CI job that packs every package from the branch into a local feed and builds and tests Sample against it (`LocalNugetDevelopment.md` already describes the local feed). Run it on every pull request that touches a `.csproj`.

## 7. Communication

With this many downloads, how the change is announced matters as much as the change.

* **11.0.2 release notes:** say plainly that EF Core 11 was reverted because the PostgreSQL provider is not stable yet, and that no released package was affected.
* **11.1.0 release notes and an announcement:** "CoreDesign packages will only bring the dependencies you use." State the numbers (69 to 0 for Logging), the obsolete overload and what to change, and the 12.0 date. Publishing the plan ourselves, with numbers, is what keeps it from being raised by someone else first.
* **README "Dependencies" sections** on nuget.org, so the dependency list on the package page is explained where people read it.

## 8. Alternatives considered

| Alternative | Why not |
|---|---|
| A separate `CoreDesign.Aspire.Hosting` package for the AppHost overload | A package for one three-line method. The public `IConfigurationManager` overload does the same with nothing to install |
| A separate `CoreDesign.Data.Auditing` package | Adds no dependency, so it does not meet rule 2; it would depend on Data's internals anyway |
| Keep Logging referencing Shared after 12.0, since trimmed Shared is light | Rule 1: Logging works without it, so users who do not use `Union<...>` should not get it. It is a one-line fix for those who do |
| Keep EF Core 11 and tell PostgreSQL users to wait | Breaks the documented promise and the largest group of Data users (Sample itself uses PostgreSQL) |
| Remove everything in 11.1.0 | Faster, but breaks the versioning promise that minor releases never break. See decision 1 |

## 9. Decisions needed

1. **Timing of the removals.** Recommended: keep the versioning promise. Obsolete in 11.1.0, remove in 12.0.0. The alternative is a documented exception in 11.1.0 for the parts with no public API: dropping EF Core from Shared (nothing in Shared uses it) would break only projects using EF Core without declaring it. MessagePack cannot go before Aspire, because it patches Aspire's vulnerable copy. Aspire itself cannot go before 12.0 without removing a public method.
2. **Logging's reference to Shared.** Recommended: remove in 12.0 (section 4.3).
3. **11.0.2 contents.** Recommended: the EF Core revert, the configuration package declarations and the CI checks, plus the workbook fixes from `user-journey-workbook-fix`. The click naming feature adds public API and goes to 11.1.0.
4. **Audit history storage.** Recommended: one row per property (section 5.2). The alternative, one row per save with the changes as JSON, is more compact but harder to query in SQL and depends on provider JSON support.

## 10. How the findings were verified

| Claim | How |
|---|---|
| 69 packages for a Logging user | Throwaway `net11.0` project referencing `src/CoreDesign.Logging` on `main`; `dotnet list package --include-transitive` |
| 11 packages after the Shared changes | Copy of Shared with Aspire, EF Core and MessagePack removed, the Aspire overload removed and the two configuration packages added; same count |
| 0 packages for Logging without Shared | Copy of Logging with the Shared reference removed; builds with no errors; same count |
| Shared's undeclared configuration dependencies | Build errors CS0234 for `EnvironmentVariables` and `CommandLine` without Aspire |
| EF Core 11 in #52 | `git log -S` on both project files; 11.0.1 (`03e9447`) had 10.0.15 |
| Npgsql EF Core 11 not stable | NuGet version index: newest is 11.0.0-rc.3 |
| Logging uses nothing from Shared | Source search of Logging and its generator; the generator matches `System.Runtime.CompilerServices.IUnion` |
| MessagePack pinned because of Aspire | `RELEASE_NOTES.md`: "transitive vulnerability ... introduced through the `Aspire.Hosting.AppHost` dependency chain" |
| Repository writes use change tracking | Source search of `CudRepository`: no `ExecuteUpdate` or `ExecuteDelete`; only `MigrationWorker` purge uses `ExecuteDelete` |

None of the trimmed copies were made in the repository; the repository is unchanged by this proposal.
