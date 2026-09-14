using ConfigForge.AspNet.RemoteInstances;
using ConfigForge.Hub.Core.Instances;
using Xunit;

namespace ConfigForge.Hub.Core.Tests;

public sealed class StoredInstanceMapperTests
{
    [Fact]
    public void ToRemoteInstanceOptions_MapsAllFields()
    {
        var stored = new StoredInstance
        {
            Name = "instanceA",
            BaseUrl = "https://instance-a.example.com",
            PathPrefix = "/cfg",
            Username = "user",
            Password = "pass",
        };

        RemoteInstanceOptions options = StoredInstanceMapper.ToRemoteInstanceOptions(stored);

        Assert.Equal("instanceA", options.Name);
        Assert.Equal(new Uri("https://instance-a.example.com"), options.BaseUrl);
        Assert.Equal("/cfg", options.PathPrefix);
        Assert.Equal("user", options.Username);
        Assert.Equal("pass", options.Password);
    }

    [Fact]
    public void ToRemoteInstanceOptions_BlankBaseUrl_FallsBackToPlaceholder()
    {
        var stored = new StoredInstance { Name = "x", BaseUrl = "" };

        RemoteInstanceOptions options = StoredInstanceMapper.ToRemoteInstanceOptions(stored);

        Assert.Equal(new Uri("http://unset.invalid"), options.BaseUrl);
    }

    [Fact]
    public void ToRemoteInstanceOptions_BlankPathPrefix_FallsBackToDefault()
    {
        var stored = new StoredInstance
        {
            Name = "x",
            BaseUrl = "https://x.example.com",
            PathPrefix = "",
        };

        RemoteInstanceOptions options = StoredInstanceMapper.ToRemoteInstanceOptions(stored);

        Assert.Equal("/config-ui", options.PathPrefix);
    }
}
