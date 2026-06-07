using System.Text.Json;
using CoreDesign.Data.Infrastructure;
using CoreDesign.SeedTool.Tests.Helpers;

namespace CoreDesign.SeedTool.Tests.Seeding;

public class SeedExporterTests
{
    // ---- Generic ExportAsync<T> ----

    [Fact]
    public async Task ExportAsync_Generic_WritesJsonFile_WithFullyQualifiedName()
    {
        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            ctx.Set<TestEntity>().Add(new TestEntity { Name = "Exported" });
            await ctx.SaveChangesAsync();

            var exporter = new SeedExporter<TestDbContext>(ctx);
            await exporter.ExportAsync<TestEntity>(outDir);

            var expectedFile = Path.Combine(outDir, $"{typeof(TestEntity).FullName}.json");
            Assert.True(File.Exists(expectedFile));
        }
        finally { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); }
    }

    [Fact]
    public async Task ExportAsync_Generic_IncludesSoftDeletedRows()
    {
        var dbName = Guid.NewGuid().ToString();
        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            Ulid softDeletedId;
            await using (var ctx = DbContextFactory.CreateContext(dbName))
            {
                var soft = new TestEntity { Name = "SoftDeleted" };
                ctx.Set<TestEntity>().Add(soft);
                await ctx.SaveChangesAsync();
                softDeletedId = soft.Id;
                soft.IsDeleted = true;
                await ctx.SaveChangesAsync();
            }

            await using var readCtx = DbContextFactory.CreateContext(dbName);
            var exporter = new SeedExporter<TestDbContext>(readCtx);
            await exporter.ExportAsync<TestEntity>(outDir);

            var path = Path.Combine(outDir, $"{typeof(TestEntity).FullName}.json");
            var docs = JsonSerializer.Deserialize<List<JsonElement>>(await File.ReadAllTextAsync(path))!;
            Assert.Contains(docs, d => d.GetProperty("Id").GetString() == softDeletedId.ToString());
        }
        finally { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); }
    }

    [Fact]
    public async Task ExportAsync_Generic_CreatesOutputDirectory_WhenMissing()
    {
        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nested");
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            var exporter = new SeedExporter<TestDbContext>(ctx);
            await exporter.ExportAsync<TestEntity>(outDir);
            Assert.True(Directory.Exists(outDir));
        }
        finally
        {
            var parent = Directory.GetParent(outDir)!.FullName;
            if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task ExportAsync_Generic_WritesEmptyArray_WhenTableEmpty()
    {
        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            var exporter = new SeedExporter<TestDbContext>(ctx);
            await exporter.ExportAsync<TestEntity>(outDir);

            var path = Path.Combine(outDir, $"{typeof(TestEntity).FullName}.json");
            var items = JsonSerializer.Deserialize<List<JsonElement>>(await File.ReadAllTextAsync(path))!;
            Assert.Empty(items);
        }
        finally { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); }
    }

    // ---- Named ExportAsync(IEnumerable<string>) ----

    [Fact]
    public async Task ExportAsync_ByShortName_WritesExpectedFile()
    {
        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            ctx.Set<TestEntity>().Add(new TestEntity { Name = "ByShortName" });
            await ctx.SaveChangesAsync();

            var exporter = new SeedExporter<TestDbContext>(ctx);
            await exporter.ExportAsync(["TestEntity"], outDir);

            var expectedFile = Path.Combine(outDir, $"{typeof(TestEntity).FullName}.json");
            Assert.True(File.Exists(expectedFile));
        }
        finally { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); }
    }

    [Fact]
    public async Task ExportAsync_ByFullyQualifiedName_WritesExpectedFile()
    {
        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            ctx.Set<TestEntity>().Add(new TestEntity { Name = "ByFQN" });
            await ctx.SaveChangesAsync();

            var exporter = new SeedExporter<TestDbContext>(ctx);
            await exporter.ExportAsync([typeof(TestEntity).FullName!], outDir);

            var expectedFile = Path.Combine(outDir, $"{typeof(TestEntity).FullName}.json");
            Assert.True(File.Exists(expectedFile));
        }
        finally { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); }
    }

    [Fact]
    public async Task ExportAsync_ByName_ExportsCorrectRows()
    {
        var dbName = Guid.NewGuid().ToString();
        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var id = Ulid.NewUlid();
            await using (var ctx = DbContextFactory.CreateContext(dbName))
            {
                ctx.Set<TestEntity>().Add(new TestEntity { Id = id, Name = "Named" });
                await ctx.SaveChangesAsync();
            }

            await using var readCtx = DbContextFactory.CreateContext(dbName);
            var exporter = new SeedExporter<TestDbContext>(readCtx);
            await exporter.ExportAsync(["TestEntity"], outDir);

            var path = Path.Combine(outDir, $"{typeof(TestEntity).FullName}.json");
            var docs = JsonSerializer.Deserialize<List<JsonElement>>(await File.ReadAllTextAsync(path))!;
            Assert.Single(docs);
            Assert.Equal(id.ToString(), docs[0].GetProperty("Id").GetString());
        }
        finally { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); }
    }

    [Fact]
    public async Task ExportAsync_UnknownName_DoesNotThrow_AndWritesNoFile()
    {
        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            var exporter = new SeedExporter<TestDbContext>(ctx);

            var ex = await Record.ExceptionAsync(() =>
                exporter.ExportAsync(["NonExistentEntity"], outDir));

            Assert.Null(ex);
            Assert.False(Directory.Exists(outDir) &&
                Directory.GetFiles(outDir).Any());
        }
        finally { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); }
    }

    [Fact]
    public async Task ExportAsync_MixedNames_ExportsKnownSkipsUnknown()
    {
        var outDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await using var ctx = DbContextFactory.CreateContext();
            ctx.Set<TestEntity>().Add(new TestEntity { Name = "Mixed" });
            await ctx.SaveChangesAsync();

            var exporter = new SeedExporter<TestDbContext>(ctx);
            await exporter.ExportAsync(["TestEntity", "DoesNotExist"], outDir);

            var files = Directory.GetFiles(outDir);
            Assert.Single(files);
            Assert.Contains(typeof(TestEntity).FullName!, files[0]);
        }
        finally { if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); }
    }
}
