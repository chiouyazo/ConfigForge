using ConfigForge.Core.Schema;
using ConfigForge.Hub.Web.Instances;
using Xunit;

namespace ConfigForge.Hub.Core.Tests;

public sealed class HubLocalSchemaTests
{
    [Fact]
    public void Build_HasManageInstancesCategory()
    {
        ConfigSchema schema = HubLocalSchema.Build();

        Assert.Equal("hub", schema.Id);
        CategoryElement category = Assert.Single(
            schema.Categories,
            c => c.Label == "Manage Instances"
        );
        Assert.Equal("instances", category.CollectionKey);
        Assert.True(schema.Fields.ContainsKey("instances"));
    }

    [Fact]
    public void Build_HasSecurityCategory()
    {
        ConfigSchema schema = HubLocalSchema.Build();

        Assert.Contains(schema.Categories, c => c.Label == "Security");
        Assert.True(schema.Fields.ContainsKey("password"));
    }
}
