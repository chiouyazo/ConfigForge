using ConfigForge.AspNet;
using Serilog;

// PluginLoader logs through Serilog's static logger; wire it to the console so
// plugin loading from the plugins/ folder is visible.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConfigForge(options =>
{
    options.ApplicationTitle = "ConfigForge Open Host";
    options.SchemaDirectory = Path.Combine(builder.Environment.ContentRootPath, "schemas");
    options.PluginDirectory = Path.Combine(builder.Environment.ContentRootPath, "plugins");
    options.OnSave = (schemaId, json) =>
        File.WriteAllTextAsync(Path.Combine(builder.Environment.ContentRootPath, $"{schemaId}.config.json"), json);
    options.OnLoad = schemaId =>
    {
        string path = Path.Combine(builder.Environment.ContentRootPath, $"{schemaId}.config.json");
        return File.Exists(path) ? File.ReadAllTextAsync(path) : Task.FromResult<string?>(null);
    };
});

var app = builder.Build();

app.UseConfigForge();

app.Run();
