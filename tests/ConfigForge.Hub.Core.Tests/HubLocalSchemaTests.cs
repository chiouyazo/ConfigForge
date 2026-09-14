using ConfigForge.Core.Schema;
using ConfigForge.Hub.Web.Instances;
using Xunit;

namespace ConfigForge.Hub.Core.Tests;

public sealed class HubLocalSchemaTests
{
    [Fact]
    public void Build_HasExactlyOneManageInstancesCategory()
    {
        ConfigSchema schema = HubLocalSchema.Build();

        Assert.Equal("hub", schema.Id);
        CategoryElement category = Assert.Single(schema.Categories);
        Assert.Equal("Manage Instances", category.Label);
        Assert.Equal("instances", category.CollectionKey);
        Assert.True(schema.Fields.ContainsKey("instances"));
    }
}
