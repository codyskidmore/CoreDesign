using CoreDesign.Shared.ExtensionMethods;

namespace CoreDesign.Shared.Tests.ExtensionMethods;

public class StringExtensionMethodsTests
{
    private record JsonModel(string Name, int Count);

    private enum Color { Red, Green, Blue }

    [Fact]
    public void LoadObjectFromJsonFile_DeserializesObjectFromFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"Name":"Test","Count":5}""");

            var result = path.LoadObjectFromJsonFile<JsonModel>();

            Assert.NotNull(result);
            Assert.Equal("Test", result.Name);
            Assert.Equal(5, result.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadObjectFromJsonFile_ReturnsNull_ForEmptyJsonObject()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "null");

            var result = path.LoadObjectFromJsonFile<JsonModel>();

            Assert.Null(result);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void To_ConvertsStringToInt()
    {
        var result = "42".To<int>();
        Assert.Equal(42, result);
    }

    [Fact]
    public void To_ConvertsStringToDouble()
    {
        var result = "3.14".To<double>();
        Assert.Equal(3.14, result, precision: 2);
    }

    [Fact]
    public void To_ConvertsStringToEnum()
    {
        var result = "Green".To<Color>();
        Assert.Equal(Color.Green, result);
    }

    [Fact]
    public void To_ConvertsStringToBool()
    {
        Assert.True("true".To<bool>());
        Assert.False("false".To<bool>());
    }

    [Fact]
    public void To_ThrowsArgumentException_ForInvalidConversion()
    {
        Assert.Throws<ArgumentException>(() => "notanumber".To<int>());
    }

    [Fact]
    public void PadRightWithZeros_PadsShortString()
    {
        var result = "123".PadRightWithZeros(6);
        Assert.Equal("123000", result);
    }

    [Fact]
    public void PadRightWithZeros_DoesNotTruncate_WhenInputLengthExceedsWidth()
    {
        var result = "1234567".PadRightWithZeros(4);
        Assert.Equal("1234567", result);
    }

    [Fact]
    public void PadRightWithZeros_ReturnsAllZeros_ForEmptyString()
    {
        var result = "".PadRightWithZeros(4);
        Assert.Equal("0000", result);
    }

    [Fact]
    public void PadRightWithZeros_ReturnsAllZeros_ForNullString()
    {
        string? input = null;
        var result = input!.PadRightWithZeros(3);
        Assert.Equal("000", result);
    }

    [Fact]
    public void PadRightWithZeros_ReturnsUnchanged_WhenExactWidth()
    {
        var result = "abc".PadRightWithZeros(3);
        Assert.Equal("abc", result);
    }
}
