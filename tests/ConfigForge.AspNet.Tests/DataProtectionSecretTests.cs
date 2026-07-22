using ConfigForge.Abstractions;
using ConfigForge.AspNet;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ConfigForge.AspNet.Tests;

/// <summary>
/// The Data Protection cipher round-trips secrets and marks them, and the encrypting JSON provider
/// transparently decrypts protected values as configuration loads.
/// </summary>
public sealed class DataProtectionSecretTests
{
    [Fact]
    public void Protect_RoundTrips_AndMarksProtected()
    {
        DataProtectionConfigSecretProtector protector = CreateProtector(out string keysDir);
        try
        {
            string token = protector.Protect("hunter2");

            Assert.True(protector.IsProtected(token));
            Assert.False(protector.IsProtected("hunter2"));
            Assert.NotEqual("hunter2", token);
            Assert.Equal("hunter2", protector.Unprotect(token));
        }
        finally
        {
            Directory.Delete(keysDir, recursive: true);
        }
    }

    [Fact]
    public void Unprotect_Throws_ForPlaintext()
    {
        DataProtectionConfigSecretProtector protector = CreateProtector(out string keysDir);
        try
        {
            Assert.Throws<ArgumentException>(() => protector.Unprotect("not-a-token"));
        }
        finally
        {
            Directory.Delete(keysDir, recursive: true);
        }
    }

    [Fact]
    public void EncryptedJsonFile_DecryptsProtectedValuesOnLoad()
    {
        FakeProtector protector = new();
        string dir = Directory.CreateTempSubdirectory("cf-enc-").FullName;
        try
        {
            string file = Path.Combine(dir, "appsettings.json");
            File.WriteAllText(
                file,
                """
                { "RestApi": { "ApiKeys": "enc:key-a,key-b" }, "Plain": "visible" }
                """
            );

            IConfigurationRoot config = new ConfigurationBuilder()
                .SetBasePath(dir)
                .AddEncryptedJsonFile(
                    protector,
                    "appsettings.json",
                    optional: false,
                    reloadOnChange: false
                )
                .Build();

            Assert.Equal("key-a,key-b", config["RestApi:ApiKeys"]);
            Assert.Equal("visible", config["Plain"]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static DataProtectionConfigSecretProtector CreateProtector(out string keysDir)
    {
        keysDir = Directory.CreateTempSubdirectory("cf-keys-").FullName;
        IDataProtectionProvider provider = DataProtectionProvider.Create(
            new DirectoryInfo(keysDir)
        );
        return new DataProtectionConfigSecretProtector(provider);
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
