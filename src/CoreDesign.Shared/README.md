# CoreDesign.Shared

Shared infrastructure, error result types, and utility extension methods for .NET projects. Intended to be referenced by both host/API projects and AppHost projects.

## Requirements

- .NET 10.0
- Microsoft.EntityFrameworkCore 10.x
- Microsoft.Extensions.Configuration.Abstractions 10.x
- Microsoft.Extensions.Configuration.Json 10.x
- Microsoft.Extensions.DependencyInjection 10.x
- Microsoft.Extensions.Options 10.x
- Aspire.Hosting.AppHost 13.6.5 or later
- MessagePack 3.1.x
- Ulid 1.4.x

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
| `AddAppSettings` | `IHostApplicationBuilder` | Loads `appsettings.json` and `appsettings.{name}.json` from the app's output folder; the name is chosen as described under Setup |
| `AddAppSettings` | `IDistributedApplicationBuilder` | Same as above, for use in Aspire AppHost projects |

**Error result records**

Lightweight result records for use with discriminated unions (e.g., `OneOf`):

| Type | Use case |
| --- | --- |
| `NotFoundMessage` | Resource could not be found |
| `BadRequestMessage` | The request was invalid |
| `ErrorMessage` | A general error occurred |
| `InvalidOperationMessage` | The operation is not valid in the current state |

Each record carries a single `string Message` property.

### Extension Methods

**`ObjectExtensionMethods`**

| Method | Description |
| --- | --- |
| `ToJson<T>()` | Serializes any object to a JSON string, ignoring circular references |
| `SaveToJsonFile<T>(filePath)` | Serializes an object and writes it to a file at the given path |
| `DeepClone<T>(options?)` | Deep-clones an object via JSON round-trip; returns `null` if the source is `null` |

Note: `DeepClone` requires the type to be serializable by `System.Text.Json`. Types with non-default constructors may need a `[JsonConstructor]` attribute.

**`IpAddressExtensionMethods`**

| Method | Description |
| --- | --- |
| `IsPrivate()` | True for 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16 and IPv6 unique local addresses (fc00::/7) |
| `IsInternal()` | True for private, loopback and link-local addresses, for example to exempt internal callers from rate limiting |

Both recognize IPv4 addresses in IPv6 form. Behind a reverse proxy, check the address after `UseForwardedHeaders` runs.

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

`AddAppSettings` picks `appsettings.{name}.json` from `AppSettingsOptions.SettingsName`, then the `AppSettings:Name` configuration value, then the environment name. Use it to name settings files after deployment environments while the local environment stays `Development`:

```csharp
builder.AddAppSettings(o => o.SettingsName = builder.Environment.IsDevelopment() ? "LOCAL" : null);
```

The file loaded is logged at startup. User secrets, environment variables and command-line arguments override values in these files, as in any .NET app.

Then resolve `DatabaseOptions` wherever needed:

```csharp
var dbOptions = builder.Configuration
    .GetSection(nameof(DatabaseOptions))
    .Get<DatabaseOptions>();
```

## Usage

**Error results with OneOf**

```csharp
public async Task<OneOf<Widget, NotFoundMessage>> GetAsync(Ulid id, CancellationToken ct)
{
    var widget = await repository.GetAsync(w => w.Id == id, null, ct);
    if (widget is null)
        return new NotFoundMessage($"Widget {id} not found.");
    return widget;
}
```

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
