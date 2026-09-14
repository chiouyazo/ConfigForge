using ConfigForge.Core.Schema;

namespace ConfigForge.Hub.Web.Instances;

/// <summary>
/// The Hub's own local schema: "Security" and "Manage Instances". Every other category comes
/// from <see cref="ConfigForge.AspNet.AspNetConfigForgeOptions.RemoteInstances"/> and is merged
/// in automatically, ahead of these, so the Hub's own categories always render last. Curating
/// which categories a particular viewer sees (e.g. a single-purpose embedded panel) is done
/// per-request via <c>ConfigForgePage</c>'s <c>ShowOnlyCategories</c>/<c>Fullscreen</c> query
/// parameters, not by anything stored here - the full dashboard is always the default.
/// </summary>
internal static class HubLocalSchema
{
    public const string SchemaId = "hub";
    public const string ManageInstancesLabel = "Manage Instances";
    public const string ManageInstancesCollectionKey = "instances";
    public const string SecurityLabel = "Security";
    public const string PasswordKey = "password";

    public static ConfigSchema Build() =>
        new()
        {
            Id = SchemaId,
            Name = "ConfigForge Hub",
            Categories = [BuildSecurityCategory(), BuildManageInstancesCategory()],
            Fields = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal)
            {
                [ManageInstancesCollectionKey] = BuildManageInstancesField(),
                [PasswordKey] = BuildPasswordField(),
            },
        };

    private static CategoryElement BuildSecurityCategory() =>
        new()
        {
            Label = SecurityLabel,
            Description =
                "HTTP Basic Auth for the Hub itself. Leave empty to leave the Hub unprotected.",
            Elements = [new UiElement { Type = "Control", Scope = $"#/properties/{PasswordKey}" }],
        };

    private static FieldDefinition BuildPasswordField() =>
        new()
        {
            Key = PasswordKey,
            ControlType = "secret",
            Title = "Login Password",
        };

    private static CategoryElement BuildManageInstancesCategory() =>
        new()
        {
            Label = ManageInstancesLabel,
            Description =
                "Connected ConfigForge instances the Hub polls. Add, edit, or remove instances here.",
            CollectionKey = ManageInstancesCollectionKey,
            CollectionEntryLabelKey = "name",
            CollectionAddLabel = "Add instance",
        };

    private static FieldDefinition BuildManageInstancesField() =>
        new()
        {
            Key = ManageInstancesCollectionKey,
            ControlType = "map",
            KeyFormat = "uuid",
            ValueField = new FieldDefinition
            {
                ControlType = "object",
                Children =
                [
                    new FieldDefinition
                    {
                        Key = "name",
                        ControlType = "text",
                        Title = "Name",
                        Required = true,
                    },
                    new FieldDefinition
                    {
                        Key = "baseUrl",
                        ControlType = "text",
                        Title = "Base URL",
                        Required = true,
                    },
                    new FieldDefinition
                    {
                        Key = "pathPrefix",
                        ControlType = "text",
                        Title = "Path prefix",
                        DefaultValue = "/config-ui",
                    },
                    new FieldDefinition
                    {
                        Key = "username",
                        ControlType = "text",
                        Title = "Username",
                    },
                    new FieldDefinition
                    {
                        Key = "password",
                        ControlType = "password",
                        Title = "Password",
                    },
                    new FieldDefinition
                    {
                        Key = "capabilityAssemblyPaths",
                        ControlType = "tags",
                        Title = "Capability Assembly Paths",
                        Placeholder = "Add a file path and press Enter",
                        Description =
                            "Local file paths to this instance's deployed capability assemblies, "
                            + "if it exposes custom widget fields. A product commonly splits its "
                            + "widgets across more than one assembly (its own plus any it shares "
                            + "with other products), so add every one. Leave empty otherwise.",
                    },
                ],
            },
        };
}
