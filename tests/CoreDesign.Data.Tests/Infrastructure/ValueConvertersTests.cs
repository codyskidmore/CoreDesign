using CoreDesign.Data.Infrastructure;

namespace CoreDesign.Data.Tests.Infrastructure;

public class ValueConvertersTests
{
    private enum TestStatus { Active, Inactive, Pending }

    [Fact]
    public void GetUlidConverter_ToProvider_ConvertsUlidToString()
    {
        var converter = ValueConverters.GetUlidConverter();
        var id = Ulid.NewUlid();
        var result = converter.ConvertToProvider(id);
        Assert.Equal(id.ToString(), result);
    }

    [Fact]
    public void GetUlidConverter_FromProvider_ConvertsStringToUlid()
    {
        var converter = ValueConverters.GetUlidConverter();
        var id = Ulid.NewUlid();
        var str = id.ToString();
        var result = converter.ConvertFromProvider(str);
        Assert.Equal(id, result);
    }

    [Fact]
    public void GetUlidConverter_RoundTrip_PreservesValue()
    {
        var converter = ValueConverters.GetUlidConverter();
        var original = Ulid.NewUlid();
        var str = converter.ConvertToProvider(original) as string;
        var roundTripped = converter.ConvertFromProvider(str!);
        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void GetEnumConverter_ToProvider_ConvertsEnumToString()
    {
        var converter = ValueConverters.GetEnumConverter<TestStatus>();
        var result = converter.ConvertToProvider(TestStatus.Active);
        Assert.Equal("Active", result);
    }

    [Fact]
    public void GetEnumConverter_FromProvider_ConvertsStringToEnum()
    {
        var converter = ValueConverters.GetEnumConverter<TestStatus>();
        var result = converter.ConvertFromProvider("Pending");
        Assert.Equal(TestStatus.Pending, result);
    }

    [Fact]
    public void GetEnumConverter_RoundTrip_PreservesValue()
    {
        var converter = ValueConverters.GetEnumConverter<TestStatus>();
        var original = TestStatus.Inactive;
        var str = converter.ConvertToProvider(original) as string;
        var roundTripped = converter.ConvertFromProvider(str!);
        Assert.Equal(original, roundTripped);
    }
}
