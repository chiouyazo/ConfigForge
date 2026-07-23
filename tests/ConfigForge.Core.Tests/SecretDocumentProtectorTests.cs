using System.Text.Json.Nodes;
using ConfigForge.Abstractions;
using ConfigForge.Core.Secrets;
using Xunit;

namespace ConfigForge.Core.Tests;

/// <summary>
/// The document-level secret convention: loading hides stored secrets behind the marker, and
/// saving keeps an untouched secret, encrypts a newly typed one, and clears an emptied one.
/// </summary>
public sealed class SecretDocumentProtectorTests
{
    private static readonly string[] SecretPaths = ["Alerts/SmtpPassword", "RestApi/ApiKeys"];

    private static readonly FakeProtector Protector = new();

    [Fact]
    public void Redact_ReplacesStoredSecretWithMarker_AndLeavesOtherFields()
    {
        string document = """
            { "Alerts": { "SmtpPassword": "enc:hunter2", "SenderName": "Ops" } }
            """;

        JsonObject result = ParseObject(SecretDocumentProtector.Redact(document, SecretPaths));

        Assert.Equal(ConfigForgeSecret.StoredMarker, (string?)result["Alerts"]!["SmtpPassword"]);
        Assert.Equal("Ops", (string?)result["Alerts"]!["SenderName"]);
    }

    [Fact]
    public void Redact_LeavesEmptyOrAbsentSecretUntouched()
    {
        string document = """
            { "Alerts": { "SmtpPassword": "" } }
            """;

        JsonObject result = ParseObject(SecretDocumentProtector.Redact(document, SecretPaths));

        Assert.Equal(string.Empty, (string?)result["Alerts"]!["SmtpPassword"]);
        Assert.Null(result["RestApi"]);
    }

    [Fact]
    public void Merge_Marker_KeepsStoredValueUnchanged()
    {
        string stored = """
            { "Alerts": { "SmtpPassword": "enc:hunter2" } }
            """;
        string incoming = $$"""
            { "Alerts": { "SmtpPassword": "{{ConfigForgeSecret.StoredMarker}}" } }
            """;

        JsonObject result = ParseObject(
            SecretDocumentProtector.Merge(Protector, incoming, stored, SecretPaths)
        );

        Assert.Equal("enc:hunter2", (string?)result["Alerts"]!["SmtpPassword"]);
    }

    [Fact]
    public void Merge_NewPlaintext_IsProtected()
    {
        string incoming = """
            { "Alerts": { "SmtpPassword": "new-pass" } }
            """;

        JsonObject result = ParseObject(
            SecretDocumentProtector.Merge(Protector, incoming, storedJson: null, SecretPaths)
        );

        Assert.Equal("enc:new-pass", (string?)result["Alerts"]!["SmtpPassword"]);
    }

    [Fact]
    public void Merge_EmptyValue_ClearsSecret()
    {
        string stored = """
            { "Alerts": { "SmtpPassword": "enc:hunter2" } }
            """;
        string incoming = """
            { "Alerts": { "SmtpPassword": "" } }
            """;

        JsonObject result = ParseObject(
            SecretDocumentProtector.Merge(Protector, incoming, stored, SecretPaths)
        );

        Assert.False(result["Alerts"]!.AsObject().ContainsKey("SmtpPassword"));
    }

    [Fact]
    public void Merge_MarkerWithNoStoredValue_RemovesLeaf()
    {
        string incoming = $$"""
            { "Alerts": { "SmtpPassword": "{{ConfigForgeSecret.StoredMarker}}" } }
            """;

        JsonObject result = ParseObject(
            SecretDocumentProtector.Merge(Protector, incoming, storedJson: null, SecretPaths)
        );

        Assert.False(result["Alerts"]!.AsObject().ContainsKey("SmtpPassword"));
    }

    [Fact]
    public void Merge_AlreadyProtectedValue_IsLeftAsIs()
    {
        string incoming = """
            { "RestApi": { "ApiKeys": "enc:key-a,key-b" } }
            """;

        JsonObject result = ParseObject(
            SecretDocumentProtector.Merge(Protector, incoming, storedJson: null, SecretPaths)
        );

        Assert.Equal("enc:key-a,key-b", (string?)result["RestApi"]!["ApiKeys"]);
    }

    [Fact]
    public void Merge_LeavesNonSecretFieldsUntouched()
    {
        string incoming = """
            { "Alerts": { "SmtpPassword": "p", "Recipients": ["a@x"] }, "Sync": { "Cron": "* * * * *" } }
            """;

        JsonObject result = ParseObject(
            SecretDocumentProtector.Merge(Protector, incoming, storedJson: null, SecretPaths)
        );

        Assert.Equal("* * * * *", (string?)result["Sync"]!["Cron"]);
        Assert.Equal("a@x", (string?)result["Alerts"]!["Recipients"]!.AsArray()[0]);
    }

    [Fact]
    public void Redact_ArrayNestedSecret_MasksEverySetElement()
    {
        string[] paths = ["Pairs/*/Baseline/Token"];
        string document = """
            { "Pairs": [ { "Baseline": { "Token": "enc:a", "Url": "u1" } }, { "Baseline": { "Token": "", "Url": "u2" } } ] }
            """;

        JsonObject result = ParseObject(SecretDocumentProtector.Redact(document, paths));
        JsonArray pairs = result["Pairs"]!.AsArray();

        Assert.Equal(ConfigForgeSecret.StoredMarker, (string?)pairs[0]!["Baseline"]!["Token"]);
        Assert.Equal("u1", (string?)pairs[0]!["Baseline"]!["Url"]);
        Assert.Equal(string.Empty, (string?)pairs[1]!["Baseline"]!["Token"]);
    }

    [Fact]
    public void Merge_ArrayNestedSecret_EncryptsNewPlaintextPerElement()
    {
        string[] paths = ["Pairs/*/Baseline/Token"];
        string incoming = """
            { "Pairs": [ { "Baseline": { "Token": "p0" } }, { "Baseline": { "Token": "p1" } } ] }
            """;

        JsonObject result = ParseObject(
            SecretDocumentProtector.Merge(Protector, incoming, storedJson: null, paths)
        );
        JsonArray pairs = result["Pairs"]!.AsArray();

        Assert.Equal("enc:p0", (string?)pairs[0]!["Baseline"]!["Token"]);
        Assert.Equal("enc:p1", (string?)pairs[1]!["Baseline"]!["Token"]);
    }

    [Fact]
    public void Merge_ArrayNestedSecret_MarkerKeepsStoredValueByIndex()
    {
        string[] paths = ["Pairs/*/Baseline/Token"];
        string stored = """
            { "Pairs": [ { "Baseline": { "Token": "enc:zero" } }, { "Baseline": { "Token": "enc:one" } } ] }
            """;
        string incoming = $$"""
            { "Pairs": [ { "Baseline": { "Token": "{{ConfigForgeSecret.StoredMarker}}" } }, { "Baseline": { "Token": "new1" } } ] }
            """;

        JsonObject result = ParseObject(
            SecretDocumentProtector.Merge(Protector, incoming, stored, paths)
        );
        JsonArray pairs = result["Pairs"]!.AsArray();

        Assert.Equal("enc:zero", (string?)pairs[0]!["Baseline"]!["Token"]);
        Assert.Equal("enc:new1", (string?)pairs[1]!["Baseline"]!["Token"]);
    }

    private static JsonObject ParseObject(string json) => (JsonObject)JsonNode.Parse(json)!;

    private sealed class FakeProtector : IConfigSecretProtector
    {
        private const string Marker = "enc:";

        public string Protect(string plaintext) => Marker + plaintext;

        public string Unprotect(string protectedValue) => protectedValue[Marker.Length..];

        public bool IsProtected(string value) =>
            value is not null && value.StartsWith(Marker, StringComparison.Ordinal);
    }
}
