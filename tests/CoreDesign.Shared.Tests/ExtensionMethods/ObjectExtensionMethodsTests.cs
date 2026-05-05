using CoreDesign.Shared.ExtensionMethods;

namespace CoreDesign.Shared.Tests.ExtensionMethods;

public class ObjectExtensionMethodsTests
{
    private record SampleRecord(string Name, int Value);

    private class Node
    {
        public string Name { get; set; } = string.Empty;
        public Node? Child { get; set; }
        public Node? Parent { get; set; }
    }

    [Fact]
    public void ToJson_SerializesObjectToJsonString()
    {
        var obj = new SampleRecord("Test", 42);

        var json = obj.ToJson();

        Assert.Contains("Test", json);
        Assert.Contains("42", json);
    }

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var obj = new SampleRecord("Hello", 1);

        var json = obj.ToJson();

        Assert.StartsWith("{", json.Trim());
    }

    [Fact]
    public void ToJson_HandlesCircularReferences_WithoutThrowing()
    {
        var parent = new Node { Name = "Parent" };
        var child = new Node { Name = "Child", Parent = parent };
        parent.Child = child;

        var exception = Record.Exception(() => parent.ToJson());

        Assert.Null(exception);
    }

    [Fact]
    public void SaveToJsonFile_WritesJsonFile()
    {
        var obj = new SampleRecord("FileTest", 99);
        var path = Path.GetTempFileName();
        try
        {
            obj.SaveToJsonFile(path);

            Assert.True(File.Exists(path));
            var content = File.ReadAllText(path);
            Assert.Contains("FileTest", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveToJsonFile_WritesValidJson()
    {
        var obj = new SampleRecord("Valid", 7);
        var path = Path.GetTempFileName();
        try
        {
            obj.SaveToJsonFile(path);
            var content = File.ReadAllText(path);
            Assert.StartsWith("{", content.Trim());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DeepClone_ReturnsEquivalentObject()
    {
        var original = new SampleRecord("Clone", 100);

        var clone = original.DeepClone();

        Assert.NotNull(clone);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Value, clone.Value);
    }

    [Fact]
    public void DeepClone_ReturnsNewObjectInstance()
    {
        var original = new SampleRecord("Instance", 5);

        var clone = original.DeepClone();

        Assert.NotSame(original, clone);
    }

    [Fact]
    public void DeepClone_ReturnsDefault_ForNullInput()
    {
        SampleRecord? source = null;

        var clone = source.DeepClone();

        Assert.Null(clone);
    }

    [Fact]
    public void DeepClone_UsesCustomOptions_WhenProvided()
    {
        var original = new SampleRecord("Custom", 3);
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var exception = Record.Exception(() => original.DeepClone(options));

        Assert.Null(exception);
    }
}
