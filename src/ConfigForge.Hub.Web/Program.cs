using ConfigForge.Abstractions;
using ConfigForge.AspNet;
using ConfigForge.Hub.Core.Instances;
using ConfigForge.Hub.Web.Instances;
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
builder.Services.AddSingleton<HubDocumentProvider>();

// AddConfigForge only registers the remote-polling hosted service when RemoteInstances is
// non-empty at this call, so the stored instance list has to be loaded before it, not after
// WebApplicationBuilder.Build() - a later mutation of the same options instance does not
// retroactively register a service that was never added.
Dictionary<string, StoredInstance> storedInstances = await localInstanceStore.LoadAsync();

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

app.UseConfigForge();

await app.RunAsync();
