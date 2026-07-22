using System.Text.Json.Nodes;
using ConfigForge.Abstractions;
using ConfigForge.AspNet;
using ConfigForge.Core.Schema;
using Xunit;

namespace ConfigForge.AspNet.Tests;

/// <summary>
/// The gateway drives the write-only secret convention off the schema's <c>secret</c> fields and is a
/// transparent pass-through when no cipher is registered.
/// </summary>
public sealed class ConfigSecretGatewayTests
{
    private static readonly ConfigSchema Schema = new()
    {
        Fields = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal)
        {
            ["Alerts/SmtpPassword"] = new FieldDefinition
            {
                Key = "Alerts/SmtpPassword",
                ControlType = "secret",
            },
            ["Alerts/SenderName"] = new FieldDefinition
            {
                Key = "Alerts/SenderName",
                ControlType = "text",
            },
        },
    };

    [Fact]
    public void RedactForEditor_HidesSecret_WhenProtectorRegistered()
    {
        ConfigSecretGateway gateway = new(new FakeProtector());
        string document = """
            { "Alerts": { "SmtpPassword": "enc:hunter2", "SenderName": "Ops" } }
            """;

        JsonObject result = (JsonObject)JsonNode.Parse(gateway.RedactForEditor(Schema, document))!;

        Assert.Equal(ConfigForgeSecret.StoredMarker, (string?)result["Alerts"]!["SmtpPassword"]);
        Assert.Equal("Ops", (string?)result["Alerts"]!["SenderName"]);
    }

    [Fact]
    public void MergeForStore_EncryptsNewSecret_WhenProtectorRegistered()
    {
        ConfigSecretGateway gateway = new(new FakeProtector());
        string incoming = """
            { "Alerts": { "SmtpPassword": "new-pass" } }
            """;

        JsonObject result = (JsonObject)
            JsonNode.Parse(gateway.MergeForStore(Schema, incoming, storedJson: null))!;

        Assert.Equal("enc:new-pass", (string?)result["Alerts"]!["SmtpPassword"]);
    }

    [Fact]
    public void PassThrough_WhenNoProtectorRegistered()
    {
        ConfigSecretGateway gateway = new();
        string document = """
            { "Alerts": { "SmtpPassword": "hunter2" } }
            """;

        Assert.False(gateway.Enabled);
        Assert.Equal(document, gateway.RedactForEditor(Schema, document));
        Assert.Equal(document, gateway.MergeForStore(Schema, document, storedJson: null));
    }

    private sealed class FakeProtector : IConfigSecretProtector
    {
        private const string Marker = "enc:";

        public string Protect(string plaintext) => Marker + plaintext;

        public string Unprotect(string protectedValue) => protectedValue[Marker.Length..];

        public bool IsProtected(string value) =>
            value is not null && value.StartsWith(Marker, StringComparison.Ordinal);
    }
}
