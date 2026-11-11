using System.Net;
using CoreDesign.Shared.ExtensionMethods;

namespace CoreDesign.Shared.Tests.ExtensionMethods;

public class IpAddressExtensionMethodsTests
{
    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.0.237")]
    [InlineData("::ffff:10.1.2.3")]
    [InlineData("::ffff:192.168.1.1")]
    [InlineData("fd12:3456:789a::1")]
    public void IsPrivate_True_ForPrivateRanges(string address) =>
        Assert.True(IPAddress.Parse(address).IsPrivate());

    [Theory]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("192.169.0.1")]
    [InlineData("11.0.0.1")]
    [InlineData("8.8.8.8")]
    [InlineData("203.0.113.5")]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("169.254.1.1")]
    [InlineData("2001:4860:4860::8888")]
    public void IsPrivate_False_ForEverythingElse(string address) =>
        Assert.False(IPAddress.Parse(address).IsPrivate());

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("172.20.0.5")]
    [InlineData("192.168.1.10")]
    [InlineData("127.0.0.1")]
    [InlineData("127.10.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("169.254.10.20")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    public void IsInternal_True_ForPrivateLoopbackAndLinkLocal(string address) =>
        Assert.True(IPAddress.Parse(address).IsInternal());

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("203.0.113.5")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("2001:db8::1")]
    public void IsInternal_False_ForPublicAddresses(string address) =>
        Assert.False(IPAddress.Parse(address).IsInternal());
}
