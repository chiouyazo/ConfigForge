using Bunit;
using ConfigForge.Abstractions;
using ConfigForge.AspNet.Endpoints;
using ConfigForge.AspNet.RemoteInstances;
using ConfigForge.Blazor.Components;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Documents;
using ConfigForge.Core.Schema;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigForge.AspNet.Tests;

/// <summary>
/// Renders a mirrored category through the real Blazor component tree the Hub uses (not just
/// asserting on the intermediate model), built the same way <see cref="RemoteSchemaAggregator"/>
/// and <see cref="RemoteDocumentMergeService"/> build it for a live remote instance: a manifest
/// carrying a real host's nested "Instance"/"Hosting" tabs under "General", plus real merged field
/// values overlaid onto the document.
/// </summary>
public sealed class RemoteMirroredCategoryRenderTests : BunitContext
{
    public RemoteMirroredCategoryRenderTests()
    {
        Services.AddLogging();
        Services.AddConfigForgeCore();
        Services.AddConfigForgeBlazor();
    }

    private static ManifestResponse OrdersServiceGeneralManifest() =>
        new(
            SchemaId: "orders-service",
            Name: "orders-service",
            Version: null,
            Categories:
            [
                new ManifestCategory(
                    "General",
                    null,
                    null,
                    null,
                    [
                        new ManifestElement(
                            "Categorization",
                            null,
                            null,
                            [
                                new ManifestElement(
                                    "Category",
                                    "Instance",
                                    null,
                                    [
                                        new ManifestElement(
                                            "Control",
                                            null,
                                            new ManifestField(
                                                "instanceName",
                                                "text",
                                                "Instance Name",
                                                false
                                            ),
                                            []
                                        ),
                                    ]
                                ),
                                new ManifestElement(
                                    "Category",
                                    "Hosting",
                                    null,
                                    [
                                        new ManifestElement(
                                            "Control",
                                            null,
                                            new ManifestField(
                                                "hosting/listenUrls",
                                                "taglist",
                                                "Listen URLs",
                                                false
                                            ),
                                            []
                                        ),
                                    ]
                                ),
                            ]
                        ),
                    ]
                ),
            ],
            Actions: []
        );

    /// <summary>
    /// Builds the schema and document the Hub actually renders from: categories/fields via
    /// <see cref="RemoteSchemaAggregator.BuildContent"/>, values overlaid the same way
    /// <see cref="RemoteDocumentMergeService.ApplyRemoteValues"/> does (straight from the
    /// origin's raw document JSON, keyed by <see cref="RemoteFieldOrigin"/>).
    /// </summary>
    private (ConfigSchema Schema, ConfigDocument Document) BuildMirroredSchemaAndDocument()
    {
        RemoteInstanceSnapshot snapshot = RemoteInstanceSnapshot.Online(
            "orders-service",
            DateTimeOffset.UtcNow,
            OrdersServiceGeneralManifest(),
            new Dictionary<string, IReadOnlyList<CollectionEntryResponse>>(StringComparer.Ordinal),
            documentJson: """
            { "instanceName": null, "hosting": { "listenUrls": ["http://127.0.0.1:5821"] } }
            """
        );

        (
            IReadOnlyList<CategoryElement> categories,
            IReadOnlyDictionary<string, FieldDefinition> fields,
            _,
            IReadOnlyDictionary<string, RemoteFieldOrigin> fieldOrigins
        ) = RemoteSchemaAggregator.BuildContent([snapshot]);

        ConfigSchema schema = new()
        {
            Id = "hub",
            Categories = categories,
            Fields = fields,
        };

        IConfigDocumentEngine engine = Services.GetRequiredService<IConfigDocumentEngine>();
        ConfigDocument document = new();
        foreach ((string compositeKey, RemoteFieldOrigin origin) in fieldOrigins)
        {
            ConfigDocument originDocument = engine
                .Parse(snapshot.DocumentJson!, new ConfigSchema())
                .Document;
            document[compositeKey] = originDocument[origin.OriginalKey];
        }

        return (schema, document);
    }

    [Fact]
    public void MirroredGeneralCategory_RendersInstanceAndHostingAsDistinctTabs()
    {
        (ConfigSchema schema, ConfigDocument document) = BuildMirroredSchemaAndDocument();

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        List<string?> tabLabels =
        [
            .. cut.FindAll("button.cf-tab").Select(b => b.TextContent.Trim()),
        ];
        Assert.Equal(["Instance", "Hosting"], tabLabels);
    }

    [Fact]
    public async Task MirroredGeneralCategory_HostingTab_RendersTheRealMergedListenUrlValue()
    {
        (ConfigSchema schema, ConfigDocument document) = BuildMirroredSchemaAndDocument();

        // Confirms the value the render must show is really there before rendering, so a failure
        // below is about rendering, not about this test's own setup.
        Assert.Equal(
            "http://127.0.0.1:5821",
            Assert
                .IsAssignableFrom<IEnumerable<object?>>(
                    document["orders-service__hosting/listenUrls"]
                )
                .Single()
        );

        IRenderedComponent<ConfigForgeShell> cut = Render<ConfigForgeShell>(parameters =>
            parameters.Add(p => p.Schema, schema).Add(p => p.Document, document)
        );

        await cut.FindAll("button.cf-tab")
            .Single(b => b.TextContent.Trim() == "Hosting")
            .ClickAsync(new MouseEventArgs());

        string renderedTags = string.Join(
            ", ",
            cut.FindAll(".cf-tag").Select(tag => tag.TextContent.Trim())
        );
        Assert.Contains("http://127.0.0.1:5821", renderedTags, StringComparison.Ordinal);
    }
}
