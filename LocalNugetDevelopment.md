# Local NuGet Development

A guide for testing package changes without publishing to nuget.org, and switching cleanly between local and published packages.

## Testing Options

### Option 1: Local Folder Feed

Pack the project and point NuGet at a local directory:

```powershell
# Build and pack the package
dotnet pack -c Release -o C:\local-nuget

# In the consuming project, add the local feed
dotnet nuget add source C:\local-nuget --name local
```

Or add it to `nuget.config` in the consuming project:

```xml
<configuration>
  <packageSources>
    <add key="local" value="C:\local-nuget" />
  </packageSources>
</configuration>
```

Then reference it normally in the `.csproj`:

```xml
<PackageReference Include="YourPackage" Version="1.0.0" />
```

### Option 2: Direct Project Reference (fastest iteration)

Skip packaging entirely during development:

```xml
<ItemGroup>
  <ProjectReference Include="..\YourLibrary\YourLibrary.csproj" />
</ItemGroup>
```

Switch back to `PackageReference` once the package is stable. This is the fastest feedback loop but does not test the actual package behavior (assembly loading, transitive dependencies, etc.).

### Option 3: NuGet Package Explorer

Install [NuGet Package Explorer](https://github.com/NuGetPackageExplorer/NuGetPackageExplorer) to inspect `.nupkg` file contents before consuming the package anywhere.

### Option 4: Prerelease Versions

Publish a prerelease version to a private feed (GitHub Packages, Azure Artifacts, or MyGet) instead of nuget.org:

```xml
<Version>1.2.3-beta.1</Version>
```

## Switching Back to nuget.org

Update the version in your `.csproj` to a version that exists on nuget.org. NuGet checks all configured sources and pulls from whichever one satisfies the version.

To remove the local source:

```powershell
dotnet nuget remove source local
```

Or delete the entry from `nuget.config` manually.

### Gotcha: Cached Packages

NuGet caches packages in `%USERPROFILE%\.nuget\packages`. If your local test package and the real release share the same version number, NuGet may use the cached local copy. Force a clean resolution with:

```powershell
dotnet nuget locals all --clear
dotnet restore
```

Use a higher or prerelease version for local test packages (e.g. `1.0.1-local`) to avoid this ambiguity entirely, making the switch back to nuget.org as simple as changing the version number.
