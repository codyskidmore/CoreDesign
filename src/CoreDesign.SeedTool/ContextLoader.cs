using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using Microsoft.EntityFrameworkCore;

namespace CoreDesign.SeedTool;

internal static class ContextLoader
{
    private static string? _assemblyDir;
    private static AssemblyDependencyResolver? _depResolver;
    private static IReadOnlyList<string>? _rids;

    internal static DbContext Load(string assemblyPath, string connectionString)
    {
        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        _assemblyDir = Path.GetDirectoryName(fullAssemblyPath)!;
        _depResolver = new AssemblyDependencyResolver(fullAssemblyPath);
        _rids = GetRidFallbacks(RuntimeInformation.RuntimeIdentifier);

        // Register once; handlers must remain active past Load() because EF Core
        // resolves assembly dependencies lazily when the first query runs.
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        AssemblyLoadContext.Default.Resolving += OnAlcResolving;

        // Pre-load platform-specific managed assemblies before the app assembly so
        // the CLR sees them in the "already loaded" list and never falls back to
        // the cross-platform stubs in the root output directory.
        // Register NativeLibrary resolvers immediately after each load so P/Invoke
        // (e.g. SQL Server SNI) finds native dependencies regardless of OS search flags.
        PreloadPlatformAssemblies(_assemblyDir, _rids);

        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullAssemblyPath);
        var factoryType = FindFactoryType(assembly);

        var factory = Activator.CreateInstance(factoryType)
            ?? throw new InvalidOperationException($"Could not create {factoryType.Name}.");

        var createMethod = factoryType.GetMethod("CreateContext", [typeof(string)])
            ?? throw new InvalidOperationException(
                $"{factoryType.Name} does not have a CreateContext(string) method.");

        return (DbContext)createMethod.Invoke(factory, [connectionString])!;
    }

    private static Assembly? OnAssemblyResolve(object? _, ResolveEventArgs args)
        => ResolveAssembly(new AssemblyName(args.Name));

    private static Assembly? OnAlcResolving(AssemblyLoadContext _, AssemblyName name)
        => ResolveAssembly(name);

    private static Assembly? ResolveAssembly(AssemblyName name)
    {
        var shortName = name.Name;
        if (string.IsNullOrWhiteSpace(shortName) || _assemblyDir is null || _depResolver is null)
            return null;

        var already = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == shortName);
        if (already is not null)
            return already;

        var resolved = _depResolver.ResolveAssemblyToPath(name);
        if (resolved is not null)
        {
            try { return AssemblyLoadContext.Default.LoadFromAssemblyPath(resolved); }
            catch { }
        }

        var candidate = Path.Combine(_assemblyDir, $"{shortName}.dll");
        if (!File.Exists(candidate))
            return null;
        try { return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate); }
        catch { return null; }
    }

    private static void PreloadPlatformAssemblies(string assemblyDir, IReadOnlyList<string> rids)
    {
        var loadedNames = new HashSet<string>(
            AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name!),
            StringComparer.OrdinalIgnoreCase);

        foreach (var r in rids)
        {
            var nativeDir = Path.Combine(assemblyDir, "runtimes", r, "native");
            if (Directory.Exists(nativeDir))
            {
                foreach (var lib in Directory.EnumerateFiles(nativeDir))
                {
                    try { NativeLibrary.Load(lib); }
                    catch { }
                }
            }

            var libRoot = Path.Combine(assemblyDir, "runtimes", r, "lib");
            if (!Directory.Exists(libRoot))
                continue;

            var tfmDir = Directory.GetDirectories(libRoot)
                .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (tfmDir is null)
                continue;

            foreach (var dll in Directory.EnumerateFiles(tfmDir, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(dll);
                if (loadedNames.Contains(name))
                    continue;

                try
                {
                    var loaded = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
                    loadedNames.Add(name);
                    RegisterNativeResolver(loaded, assemblyDir, rids);
                }
                catch { }
            }
        }
    }

    private static void RegisterNativeResolver(Assembly assembly, string assemblyDir, IReadOnlyList<string> rids)
    {
        try
        {
            NativeLibrary.SetDllImportResolver(assembly, (libraryName, _, _) =>
            {
                foreach (var r in rids)
                {
                    var nativeDir = Path.Combine(assemblyDir, "runtimes", r, "native");
                    if (!Directory.Exists(nativeDir))
                        continue;

                    var stems = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? new[] { libraryName, libraryName + ".dll" }
                        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                            ? new[] { libraryName, "lib" + libraryName + ".dylib", libraryName + ".dylib" }
                            : new[] { libraryName, "lib" + libraryName + ".so", libraryName + ".so" };

                    foreach (var stem in stems)
                    {
                        var path = Path.Combine(nativeDir, stem);
                        if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
                            return handle;
                    }
                }
                return IntPtr.Zero;
            });
        }
        catch { }
    }

    private static IReadOnlyList<string> GetRidFallbacks(string rid)
    {
        if (string.IsNullOrWhiteSpace(rid))
            return [];

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddChain(string[] parts)
        {
            for (var i = parts.Length; i >= 1; i--)
            {
                var candidate = string.Join('-', parts.Take(i));
                if (seen.Add(candidate))
                    results.Add(candidate);
            }
        }

        var parts = rid.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return [];

        AddChain(parts);

        var portableOs = StripOsVersion(parts[0]);
        if (!portableOs.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
            AddChain(new[] { portableOs }.Concat(parts.Skip(1)).ToArray());

        return results;
    }

    private static string StripOsVersion(string osPart)
    {
        var end = osPart.Length;
        while (end > 0 && (char.IsDigit(osPart[end - 1]) || osPart[end - 1] == '.'))
            end--;
        return end > 0 ? osPart[..end] : osPart;
    }

    private static Type FindFactoryType(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.OfType<Type>().ToArray();
        }

        var factoryType = types
            .FirstOrDefault(t => !t.IsAbstract && !t.IsInterface && t.GetInterfaces()
                .Any(i => i.IsGenericType && i.Name == "ICoreDesignSeedFactory`1"));

        return factoryType ?? throw new InvalidOperationException(
            $"No ICoreDesignSeedFactory<T> implementation found in '{assembly.GetName().Name}'. " +
            "Add a class that inherits CoreDesignSeedFactory<TContext> to your project. " +
            "See the CoreDesign.Data README for setup instructions.");
    }
}
