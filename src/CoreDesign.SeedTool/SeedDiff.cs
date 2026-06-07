using System.Reflection;
using System.Text.Json;
using CoreDesign.Data.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoreDesign.SeedTool;

/// <summary>Describes how an entity differs between a seed file and the database.</summary>
public enum SeedDiffStatus
{
    /// <summary>Present in the seed file but not in the database.</summary>
    OnlyInFile,
    /// <summary>Present in the database but not in the seed file.</summary>
    OnlyInDatabase,
    /// <summary>Present in both, but at least one field differs.</summary>
    Different
}

/// <param name="Id">The entity's ULID.</param>
/// <param name="Status">How the entity differs.</param>
public record SeedDiffEntry(Ulid Id, SeedDiffStatus Status);

/// <summary>
/// Compares a MigrationWorker seed file against the current state of the database.
/// Useful for verifying that a seed apply succeeded or for detecting database drift.
/// </summary>
public class SeedDiff<TContext>(TContext dbContext)
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Compares the entities in <paramref name="seedFilePath"/> to the current database
    /// rows for <typeparamref name="T"/>. Returns one entry per difference found.
    /// </summary>
    /// <remarks>
    /// Comparison uses JSON serialization of the full entity, including audit fields.
    /// An entry marked <see cref="SeedDiffStatus.Different"/> may differ only in audit
    /// timestamps if the row was modified after the seed was generated.
    /// </remarks>
    public async Task<IReadOnlyList<SeedDiffEntry>> DiffAsync<T>(
        string seedFilePath, CancellationToken ct = default)
        where T : BaseEntity
    {
        if (!File.Exists(seedFilePath))
            throw new FileNotFoundException($"Seed file not found: {seedFilePath}");

        var json = await File.ReadAllTextAsync(seedFilePath, ct);
        var fileEntities = JsonSerializer.Deserialize<List<T>>(json, Options) ?? [];
        var dbEntities = await dbContext.Set<T>().IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);

        var fileById = fileEntities.ToDictionary(e => e.Id);
        var dbById   = dbEntities.ToDictionary(e => e.Id);

        var results = new List<SeedDiffEntry>();

        foreach (var (id, fileEntity) in fileById)
        {
            if (!dbById.TryGetValue(id, out var dbEntity))
            {
                results.Add(new(id, SeedDiffStatus.OnlyInFile));
            }
            else
            {
                var fileJson = JsonSerializer.Serialize(fileEntity);
                var dbJson   = JsonSerializer.Serialize(dbEntity);
                if (fileJson != dbJson)
                    results.Add(new(id, SeedDiffStatus.Different));
            }
        }

        foreach (var id in dbById.Keys.Except(fileById.Keys))
            results.Add(new(id, SeedDiffStatus.OnlyInDatabase));

        return results;
    }

    /// <summary>
    /// Diffs multiple entity types by name against seed files in <paramref name="seedDirectory"/>.
    /// Each name can be a short class name (<c>SiteContent</c>) or a fully qualified type name.
    /// Entity types with no corresponding seed file in the directory are skipped silently.
    /// Unknown names are also skipped.
    /// </summary>
    /// <returns>
    /// A dictionary keyed by short class name. Entities with no seed file are absent from the result.
    /// </returns>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<SeedDiffEntry>>> DiffAsync(
        IEnumerable<string> entityNames,
        string seedDirectory,
        CancellationToken ct = default)
    {
        var modelTypes = dbContext.Model.GetEntityTypes()
            .Select(t => t.ClrType)
            .Where(t => typeof(BaseEntity).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var byShortName = modelTypes.ToDictionary(t => t.Name,     StringComparer.OrdinalIgnoreCase);
        var byFullName  = modelTypes.ToDictionary(t => t.FullName!, StringComparer.OrdinalIgnoreCase);

        var genericDiff = GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m.Name == nameof(DiffAsync) && m.IsGenericMethodDefinition);

        var results = new Dictionary<string, IReadOnlyList<SeedDiffEntry>>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in entityNames)
        {
            var entityType = byShortName.GetValueOrDefault(name)
                          ?? byFullName.GetValueOrDefault(name);
            if (entityType is null)
                continue;

            var seedFile = Path.Combine(seedDirectory, $"{entityType.FullName}.json");
            if (!File.Exists(seedFile))
                continue;

            var entries = await (Task<IReadOnlyList<SeedDiffEntry>>)genericDiff
                .MakeGenericMethod(entityType)
                .Invoke(this, [seedFile, ct])!;

            results[entityType.Name] = entries;
        }

        return results;
    }
}
