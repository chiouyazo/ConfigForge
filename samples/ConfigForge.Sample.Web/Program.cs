using ConfigForge.Abstractions;
using ConfigForge.Blazor.Services;
using ConfigForge.Core;
using ConfigForge.Core.Plugins;
using ConfigForge.Plugin.Template;
using ConfigForge.Sample.Web.Components;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddSingleton<ConfigForge.Abstractions.IThemeProvider, ConfigForge.Sample.Web.DemoThemeProvider>();

builder.Services.AddConfigForgeCore();
builder.Services.AddConfigForgeBlazor();

WebApplication app = builder.Build();

IPluginRegistry registry = app.Services.GetRequiredService<IPluginRegistry>();
new ExamplePlugin().Register(registry);

IPluginLoader pluginLoader = app.Services.GetRequiredService<IPluginLoader>();
await pluginLoader.LoadFromDirectoryAsync(
    Path.Combine(app.Environment.ContentRootPath, "plugins")
);

app.UseStaticFiles();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
