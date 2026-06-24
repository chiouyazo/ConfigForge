using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Windows;
using Serilog;

namespace ConfigForge.Standalone;

/// <summary>
/// The WPF application entry point. Configures Serilog file logging, creates and
/// shows the main window on startup, and flushes the log on exit.
/// </summary>
public sealed partial class App : Application
{
    /// <inheritdoc />
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Top-level startup guard: any failure constructing or showing the "
            + "main window is logged as fatal and turned into a clean exit-code-1 shutdown."
    )]
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(AppContext.BaseDirectory, "logs", "configforge-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                flushToDiskInterval: TimeSpan.FromMilliseconds(250),
                formatProvider: CultureInfo.InvariantCulture
            )
            .CreateLogger();

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Dispatcher unhandled exception.");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error(args.ExceptionObject as Exception, "AppDomain unhandled exception.");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception.");
            args.SetObserved();
        };

        try
        {
            var window = new ConfigForgeWindow();
            window.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "ConfigForge failed to start.");
            Shutdown(1);
        }
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
