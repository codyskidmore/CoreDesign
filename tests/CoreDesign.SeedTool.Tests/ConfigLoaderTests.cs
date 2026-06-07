using CoreDesign.SeedTool;

namespace CoreDesign.SeedTool.Tests;

public class ConfigLoaderTests
{
    [Fact]
    public void FindConfigPath_ExplicitPath_ReturnsFullPath_WhenFileExists()
    {
        var temp = Path.GetTempFileName();
        try
        {
            var result = ConfigLoader.FindConfigPath(temp);
            Assert.Equal(Path.GetFullPath(temp), result);
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void FindConfigPath_ExplicitPath_ReturnsNull_WhenFileNotFound()
    {
        var result = ConfigLoader.FindConfigPath("/nonexistent/path/seedtool.json");
        Assert.Null(result);
    }

    [Fact]
    public void FindConfigPath_WalksUpFromSubdirectory_FindsConfigInParent()
    {
        var parent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var child  = Path.Combine(parent, "subdir");
        Directory.CreateDirectory(child);
        var configFile = Path.Combine(parent, ConfigLoader.ConfigFileName);
        File.WriteAllText(configFile, "{}");

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(child);
            var result = ConfigLoader.FindConfigPath();
            Assert.Equal(configFile, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void FindConfigPath_ReturnsNull_WhenNoConfigFoundInTree()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(temp);
        var original = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(temp);
            var result = ConfigLoader.FindConfigPath();
            Assert.Null(result);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
            Directory.Delete(temp);
        }
    }

    [Fact]
    public void Load_DeserializesAllFields()
    {
        var json = """
            {
              "assemblyPath": "bin/Debug/App.dll",
              "outputDirectory": "MySeedData",
              "entities": ["Widget", "Gadget"]
            }
            """;
        var temp = WriteTempFile(json);
        try
        {
            var config = ConfigLoader.Load(temp);
            Assert.Equal("bin/Debug/App.dll", config.AssemblyPath);
            Assert.Equal("MySeedData", config.OutputDirectory);
            Assert.Equal(["Widget", "Gadget"], config.Entities);
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Load_UsesDefaults_WhenOptionalFieldsAbsent()
    {
        var temp = WriteTempFile("""{ "assemblyPath": "App.dll" }""");
        try
        {
            var config = ConfigLoader.Load(temp);
            Assert.Equal("SeedData", config.OutputDirectory);
            Assert.Empty(config.Entities);
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public void Load_IsCaseInsensitive()
    {
        var json = """
            {
              "AssemblyPath": "App.dll",
              "OutputDirectory": "MySeedData",
              "Entities": ["SiteContent"]
            }
            """;
        var temp = WriteTempFile(json);
        try
        {
            var config = ConfigLoader.Load(temp);
            Assert.Equal("App.dll", config.AssemblyPath);
            Assert.Equal("MySeedData", config.OutputDirectory);
            Assert.Single(config.Entities);
        }
        finally { File.Delete(temp); }
    }

    private static string WriteTempFile(string content)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, content);
        return path;
    }
}
