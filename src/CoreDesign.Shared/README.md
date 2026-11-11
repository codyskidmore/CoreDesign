# CoreDesign.Shared

Shared infrastructure, error result types, and utility extension methods for .NET projects. Intended to be referenced by both host/API projects and AppHost projects.

## Requirements

- .NET 11.0 (C# 15)
- Microsoft.EntityFrameworkCore 10.x (moves to 11.x once the PostgreSQL provider for EF Core 11 is stable)
- Microsoft.Extensions.Configuration.Json 11.x
- Microsoft.Extensions.DependencyInjection 11.x
- Microsoft.Extensions.Options.ConfigurationExtensions 11.x
- Aspire.Hosting.AppHost 13.6.5 or later
- MessagePack 3.1.x
- Ulid 1.4.x

`Microsoft.Extensions.Configuration.Abstractions` and `Microsoft.Extensions.Options` are part of .NET 11 and need no package reference.

## What Is Included

### Infrastructure

**`DatabaseOptions`**

A record that holds the database connection settings read from configuration:

| Property | Description |
| --- | --- |
| `HostName` | Container or server host name (the Aspire resource name) |
| `DatabaseName` | Name of the database |
| `HostPort` | Port the host is listening on |
| `ConnectionStringName` | Named connection string key |

**`Configuration` (extension methods)**

| Method | Target | Description |
| --- | --- | --- |
| `AddDatabaseConfiguration` | `IHostApplicationBuilder` | Binds the `DatabaseOptions` section from `appsettings.json` into the options system |
| `AddAppSettings` | `IHostApplicationBuilder` | Loads `appsettings.json` and the environment-specific `appsettings.{Environment}.json` |
| `AddAppSettings` | `IDistributedApplicationBuilder` | Same as above, for use in Aspire AppHost projects |

**Result records**

Lightweight records used as the cases of a result union:

| Type | Use case |
| --- | --- |
| `Success` | The operation succeeded and returns no value |
| `NotFoundMessage` | Resource could not be found |
| `BadRequestMessage` | The request was invalid |
| `ErrorMessage` | A general error occurred |
| `InvalidOperationMessage` | The operation is not valid in the current state |

Each message record carries a single `string Message` property. `Success` has no properties.

**`Union<...>`**

General-purpose C# 15 unions for returning one of several results: `Union<T0, T1>`, `Union<T0, T1, T2>` and `Union<T0, T1, T2, T3>`. A method returns any case value directly and the conversion to the union is implicit. Callers handle the result with a switch expression, which the compiler checks for exhaustiveness, or with `Match`, which is provided for code migrating from `OneOf<...>`.

For more than four cases, or when a named type reads better, declare your own union instead: `public union CreateResult(WeatherForecast, BadRequestMessage);`.

### Extension Methods

**`ObjectExtensionMethods`**

| Method | Description |
| --- | --- |
| `ToJson<T>()` | Serializes any object to a JSON string, ignoring circular references |
| `SaveToJsonFile<T>(filePath)` | Serializes an object and writes it to a file at the given path |
| `DeepClone<T>(options?)` | Deep-clones an object via JSON round-trip; returns `null` if the source is `null` |

Note: `DeepClone` requires the type to be serializable by `System.Text.Json`. Types with non-default constructors may need a `[JsonConstructor]` attribute.

**`StringExtensionMethods`**

| Method | Description |
| --- | --- |
| `LoadObjectFromJsonFile<T>()` | Reads a JSON file at the given path and deserializes it to `T` |
| `To<T>()` | Converts a string to any type `T` using `TypeDescriptor`, throwing `ArgumentException` if the conversion is not supported or fails |
| `PadRightWithZeros(totalWidth)` | Pads a string to `totalWidth` characters using `'0'` on the right; returns all zeros if input is null or empty |

## Setup

**1. Install the NuGet package**

Using the .NET CLI:

```bash
dotnet add package CoreDesign.Shared
```

Using the NuGet Package Manager Console:

```powershell
Install-Package CoreDesign.Shared
```

Or add directly to your `.csproj`:

```xml
<ItemGroup>
    <PackageReference Include="CoreDesign.Shared" Version="*" />
</ItemGroup>
```

**2. Add the `DatabaseOptions` section to `appsettings.json`**

```json
{
  "DatabaseOptions": {
    "HostName": "my-sql-server",
    "DatabaseName": "MyDatabase",
    "HostPort": 1433,
    "ConnectionStringName": "MyDatabase"
  }
}
```

**3. Register configuration in your host**

Call both extension methods during startup to load settings and bind database options:

```csharp
builder.AddAppSettings();
builder.AddDatabaseConfiguration();
```

Then resolve `DatabaseOptions` wherever needed:

```csharp
var dbOptions = builder.Configuration
    .GetSection(nameof(DatabaseOptions))
    .Get<DatabaseOptions>();
```

## Usage

**Results with `Union<...>`**

```csharp
public async Task<Union<Widget, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct)
{
    var widget = await repository.GetAsync(w => w.Id == id, null, ct);
    if (widget is null)
        return new NotFoundMessage($"Widget {id} not found.");
    return widget;
}

var result = await service.GetAsync(id, ct);
return result switch
{
    Widget widget         => Results.Ok(widget),
    NotFoundMessage error => Results.NotFound(error.Message),
};
```

A `default` union holds no value. Add a `null =>` arm to switches that can receive one, such as an uninitialized field. Migrating from OneOf: see [CoreDesign.NET11-migration.md](../../CoreDesign.NET11-migration.md).

**Deep clone**

```csharp
var copy = original.DeepClone();
```

**JSON file round-trip**

```csharp
myObject.SaveToJsonFile("output.json");
var loaded = "output.json".LoadObjectFromJsonFile<MyObject>();
```

**String conversion**

```csharp
var number = "42".To<int>();
var date = "2026-01-15".To<DateOnly>();
```

## Feedback

Feedback on this package is welcome. If you run into a missing feature, an unexpected behavior, or something that required more effort than it should have, open an issue at [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues) or tag [@codyskidmore](https://github.com/codyskidmore). Suggestions about missing features and priority input are especially appreciated.
