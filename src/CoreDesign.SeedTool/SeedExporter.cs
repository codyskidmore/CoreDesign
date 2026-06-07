using System.Reflection;
using System.Text.Json;
using CoreDesign.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoreDesign.SeedTool;

/// <summary>
/// Exports entity data from a <typeparamref name="TContext"/> to JSON files compatible
/// with <c>MigrationWorker</c>'s seed directory format. Each file is named using the
/// entity type's fully qualified name so the worker can resolve it on apply.
/// </summary>
public class SeedExporter<TContext>(TContext dbContext, ILogger<SeedExporter<TContext>>? logger = null)
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = true
    };

    /// <summary>
    /// Exports all rows for <typeparamref name="T"/> (including soft-deleted) to a JSON
    /// file in <paramref name="outputDirectory"/>. The file is named
    /// <c>{T.FullName}.json</c> to match the MigrationWorker naming convention.
    /// </summary>
    public async Task ExportAsync<T>(string outputDirectory, CancellationToken ct = default)
        where T : BaseEntity
    {
        Directory.CreateDirectory(outputDirectory);
        var entities = await dbContext.Set<T>().IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var path = Path.Combine(outputDirectory, $"{typeof(T).FullName}.json");
        var json = JsonSerializer.Serialize(entities, WriteOptions);
        await File.WriteAllTextAsync(path, json, ct);
        logger?.LogInformation("Exported {Count} {Type} records to {File}.", entities.Count, typeof(T).Name, path);
    }

    /// <summary>
    /// Exports the entity types matching <paramref name="entityNames"/> from the DbContext
    /// model. Each name can be a short class name (<c>SiteContent</c>) or a fully qualified
    /// type name. Unresolved names are logged and skipped.
    /// </summary>
    public async Task ExportAsync(IEnumerable<string> entityNames, string outputDirectory, CancellationToken ct = default)
    {
        var modelTypes = dbContext.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => typeof(BaseEntity).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var byShortName = modelTypes.ToDictionary(t => t.Name,     StringComparer.OrdinalIgnoreCase);
        var byFullName  = modelTypes.ToDictionary(t => t.FullName!, StringComparer.OrdinalIgnoreCase);

        var genericExport = GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == nameof(ExportAsync) && m.IsGenericMethodDefinition);

        foreach (var name in entityNames)
        {
            var entityType = byShortName.GetValueOrDefault(name)
                          ?? byFullName.GetValueOrDefault(name);

            if (entityType is null)
            {
                logger?.LogWarning("Entity type '{Name}' not found in DbContext model. Skipping.", name);
                Console.Error.WriteLine($"  Warning: '{name}' not found in DbContext model. Check SeedTool:Entities in appsettings.json.");
                continue;
            }

            await (Task)genericExport.MakeGenericMethod(entityType).Invoke(this, [outputDirectory, ct])!;
        }
    }

    /// <summary>
    /// Exports all <see cref="BaseEntity"/> subtypes registered in the DbContext model.
    /// Abstract types are skipped. Each type is exported to its own file.
    /// </summary>
    public async Task ExportAllAsync(string outputDirectory, CancellationToken ct = default)
    {
        var entityTypes = dbContext.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => typeof(BaseEntity).IsAssignableFrom(t) && !t.IsAbstract);

        var genericExport = GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == nameof(ExportAsync) && m.IsGenericMethodDefinition);

        foreach (var entityType in entityTypes)
            await (Task)genericExport.MakeGenericMethod(entityType).Invoke(this, [outputDirectory, default(CancellationToken)])!;
    }
}
