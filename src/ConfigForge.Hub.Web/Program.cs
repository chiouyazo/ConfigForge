using System.Security.Cryptography;
using System.Text;
using ConfigForge.Abstractions;
using ConfigForge.AspNet;
using ConfigForge.Hub.Core.Instances;
using ConfigForge.Hub.Web.Instances;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.WindowsServices;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options => options.ServiceName = "ConfigForge Hub");

// Program Files (where the installer puts the exe) is not writable by the service account for
// its own instance list, so an installed service keeps its data in ProgramData instead; a local
// dev run (dotnet run, F5) keeps using the project's own App_Data as before.
string dataDirectory = WindowsServiceHelpers.IsWindowsService()
    ? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ConfigForge Hub",
        "App_Data"
    )
    : Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);

string instanceStorePath = Path.Combine(dataDirectory, "instances.json");
LocalInstanceStore localInstanceStore = new(instanceStorePath);
builder.Services.AddSingleton(localInstanceStore);

HubSecurityStore hubSecurityStore = new(Path.Combine(dataDirectory, "hub-security.json"));
builder.Services.AddSingleton(hubSecurityStore);
HubAuthState hubAuthState = new();
builder.Services.AddSingleton(hubAuthState);

builder.Services.AddSingleton<HubDocumentProvider>();

// Data Protection keys must persist across restarts (and, for a Windows service, be readable by
// the service account only) so a previously-set Hub password does not silently stop verifying
// the moment the process recycles. A fixed application name is required too: without it, the key
// ring is isolated by the app's physical content-root path, so moving/reinstalling the exe to a
// different folder would make every already-set password permanently undecryptable.
builder
    .Services.AddDataProtection()
    .SetApplicationName("ConfigForge.Hub")
    .PersistKeysToFileSystem(new DirectoryInfo(dataDirectory));
builder.Services.AddSingleton<IConfigSecretProtector, DataProtectionConfigSecretProtector>();

// AddConfigForge only registers the remote-polling hosted service when RemoteInstances is
// non-empty at this call, so the stored instance list has to be loaded before it, not after
// WebApplicationBuilder.Build() - a later mutation of the same options instance does not
// retroactively register a service that was never added.
Dictionary<string, StoredInstance> storedInstances = await localInstanceStore.LoadAsync();
hubAuthState.PasswordProtected = await hubSecurityStore.LoadPasswordAsync();

IServiceProvider? services = null;

builder.AddConfigForge(options =>
{
    options.ApplicationTitle = "ConfigForge Hub";
    options.Mode = ConfigForgeMode.Locked;
    options.RemoteInstancePollIntervalSeconds = builder.Configuration.GetValue(
        "ConfigForgeHub:PollIntervalSeconds",
        5
    );
    options.RemoteInstances =
    [
        .. storedInstances.Values.Select(StoredInstanceMapper.ToRemoteInstanceOptions),
    ];
    options.OnLoad = schemaId =>
        services!.GetRequiredService<HubDocumentProvider>().LoadAsync(schemaId);
    options.OnSave = (schemaId, json) =>
        services!.GetRequiredService<HubDocumentProvider>().SaveAsync(schemaId, json);
});

WebApplication app = builder.Build();
services = app.Services;

app.Services.GetRequiredService<IConfigForgeHostState>().UpsertSchema(HubLocalSchema.Build());

// Opt-in: with no password ever set (PasswordProtected null), the Hub stays reachable without
// auth exactly as before this was added. The username is never checked - only the password,
// same convention as the remote instances this Hub aggregates.
IConfigSecretProtector secretProtector = app.Services.GetRequiredService<IConfigSecretProtector>();
app.Use(
    async (context, next) =>
    {
        string? protectedPassword = hubAuthState.PasswordProtected;
        if (
            string.IsNullOrEmpty(protectedPassword)
            || IsAuthorized(context.Request, secretProtector.Unprotect(protectedPassword))
        )
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Basic realm=\"ConfigForge Hub\"";
    }
);

app.UseConfigForge();

await app.RunAsync();

static bool IsAuthorized(HttpRequest request, string expectedPassword)
{
    string? header = request.Headers.Authorization;
    if (header is null || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    try
    {
        string decoded = Encoding.UTF8.GetString(
            Convert.FromBase64String(header["Basic ".Length..])
        );
        int separatorIndex = decoded.IndexOf(':', StringComparison.Ordinal);
        string providedPassword = separatorIndex < 0 ? decoded : decoded[(separatorIndex + 1)..];
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedPassword),
            Encoding.UTF8.GetBytes(expectedPassword)
        );
    }
    catch (FormatException)
    {
        return false;
    }
}
