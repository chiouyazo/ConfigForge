using ConfigForge.AspNet;
using Serilog;

// PluginLoader logs through Serilog's static logger; wire it to the console so
// plugin loading from the plugins/ folder is visible.
Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

builder.Services.AddConfigForge(options =>
{
    string contentRoot = builder.Environment.ContentRootPath;
    string Resolve(string key, string fallback) =>
        builder.Configuration[$"ConfigForge:{key}"] ?? Path.Combine(contentRoot, fallback);

    options.ApplicationTitle =
        builder.Configuration["ConfigForge:ApplicationTitle"] ?? "ConfigForge Open Host";
    options.SchemaDirectory = Resolve("SchemaDirectory", "schemas");
    options.PluginDirectory = Resolve("PluginDirectory", "plugins");

    if (int.TryParse(builder.Configuration["ConfigForge:SchemaRefreshSeconds"], out int refresh))
    {
        options.SchemaRefreshSeconds = refresh;
    }

    options.UseLocalFileStore(Resolve("ConfigDirectory", "configs"), keepBackups: 10);
});

var app = builder.Build();

app.UseConfigForge();

app.Run();
