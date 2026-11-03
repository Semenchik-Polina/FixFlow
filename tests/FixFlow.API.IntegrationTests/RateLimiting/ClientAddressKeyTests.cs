using System.Net;
using FixFlow.API.RateLimiting;

namespace FixFlow.API.IntegrationTests.RateLimiting;

/// <summary>The client part of the auth policy's partition key, without a host.</summary>
public class ClientAddressKeyTests
{
    [Theory]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2:ffff:ffff:ffff:ffff")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2:abcd::7")]
    public void ClientAddressKey_SameIPv6Slash64_GivesOneKey(string first, string second)
    {
        Assert.Equal(Key(first), Key(second));
    }

    [Theory]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:3::1")]
    [InlineData("2001:db8:1:2::1", "2001:db8:2:2::1")]
    public void ClientAddressKey_DifferentIPv6Slash64s_GiveDifferentKeys(string first, string second)
    {
        Assert.NotEqual(Key(first), Key(second));
    }

    [Fact]
    public void ClientAddressKey_IPv6_IsItsSlash64Prefix()
    {
        Assert.Equal("2001:db8:1:2::/64", Key("2001:db8:1:2:3:4:5:6"));
    }

    [Fact]
    public void ClientAddressKey_IPv4MappedIPv6_GivesItsIPv4Key()
    {
        Assert.Equal(Key("192.0.2.10"), Key("::ffff:192.0.2.10"));
        Assert.Equal("192.0.2.10", Key("::ffff:192.0.2.10"));
    }

    [Fact]
    public void ClientAddressKey_DifferentIPv4Addresses_GiveDifferentKeys()
    {
        Assert.NotEqual(Key("192.0.2.10"), Key("192.0.2.11"));
    }

    [Fact]
    public void ClientAddressKey_NoAddress_GivesOneFixedKey()
    {
        Assert.Equal(RateLimitingExtensions.UnknownClient, RateLimitingExtensions.ClientAddressKey(null));
        Assert.NotEqual(RateLimitingExtensions.UnknownClient, Key("192.0.2.10"));
    }

    private static string Key(string address) => RateLimitingExtensions.ClientAddressKey(IPAddress.Parse(address));
}
