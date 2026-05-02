# Sharing appsettings Across Projects

This document describes how to maintain a single set of appsettings files in a shared folder and link them into multiple projects, so all projects always read from one source of truth without duplicating configuration.

## Why

When multiple projects in a solution share configuration (JWT issuer URLs, shared secrets, environment-specific endpoints), keeping separate copies leads to drift. Linking the files ensures every project compiles and runs against the same values.

## Directory Structure

Place shared configuration files in a folder at the solution root. Projects then link to those files rather than owning their own copies.

```
<My Solution Folder>/
  shared/
    appsettings.json
    appsettings.Development.json
    appsettings.Production.json
  src/
    CoreDesign.Identity.Server/
      CoreDesign.Identity.Server.csproj   (links to shared/)
    CoreDesign.Identity.Client/
      CoreDesign.Identity.Client.csproj   (links to shared/)
  CoreDesign.Identity.slnx
```

## Setting Up the Shared Folder

1. Create the `shared/` folder at the solution root.
2. Add your appsettings files there:

```json
// shared/appsettings.json
{
  "Identity": {
    "Issuer": "https://identity.example.com",
    "Audience": "api"
  }
}
```

```json
// shared/appsettings.Development.json
{
  "Identity": {
    "Issuer": "https://localhost:5001",
    "Audience": "api"
  }
}
```

Do not add appsettings files directly inside the project folders. Those project-level files will be replaced by the links.

## Linking Files into Each Project

Add an `ItemGroup` to each `.csproj` file that references the shared files using relative paths. The `Link` attribute controls the filename as it appears in the project and in the output directory.

```xml
<!-- src/CoreDesign.Identity.Server/CoreDesign.Identity.Server.csproj -->
<ItemGroup>
  <Content Include="..\..\shared\appsettings.json"
           Link="appsettings.json"
           CopyToOutputDirectory="PreserveNewest" />
  <Content Include="..\..\shared\appsettings.Development.json"
           Link="appsettings.Development.json"
           CopyToOutputDirectory="PreserveNewest" />
  <Content Include="..\..\shared\appsettings.Production.json"
           Link="appsettings.Production.json"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Repeat the same block in `CoreDesign.Identity.Client.csproj`, adjusting the relative path if the project sits at a different depth.

### Attribute Reference

| Attribute | Purpose |
|---|---|
| `Include` | Path to the real file, relative to the `.csproj` |
| `Link` | Virtual name shown in Solution Explorer and used in the output folder |
| `CopyToOutputDirectory` | `PreserveNewest` copies only when the source is newer; `Always` copies on every build |

## How It Works at Runtime

MSBuild resolves the `Include` path at build time and copies the physical file from `shared/` into the project's output directory (`bin/Debug/net10.0/`) under the name given by `Link`. ASP.NET Core's configuration system then finds `appsettings.json` and the environment-specific overlay exactly as if each project owned its own copy.

Editing `shared/appsettings.Development.json` propagates to every linked project on the next build.

## IDE Behavior

Visual Studio and Rider display linked files in Solution Explorer under the project node using the `Link` name. The file is shown with a shortcut arrow to indicate it is not physically located inside the project folder. Edits made through the IDE modify the original file in `shared/`.

## Source Control

Commit only the files in `shared/`. Do not commit the copies that appear in `bin/` output directories (these are already excluded by the standard `.gitignore` for .NET projects).

If a file contains secrets (signing keys, connection strings), add `shared/appsettings.Production.json` to `.gitignore` and distribute it through a secrets manager or environment variables instead.

## Project-Specific Overrides

If one project needs a value that differs from the shared default, use a project-local appsettings file that is not linked. ASP.NET Core merges configuration sources in order, so a project-owned `appsettings.Local.json` loaded after the shared files will take precedence for any key it defines.

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
```

Keep `appsettings.Local.json` in `.gitignore` so developer-specific overrides are never committed.
