using Microsoft.UI.Xaml;
using PointerParlor.Core;
using PointerParlor.Services;

namespace PointerParlor;

public partial class App : Application
{
    public App()
    {
        AppLog.Initialize(Paths.Logs);
        InitializeComponent();
        UnhandledException += (_, e) => AppLog.Error("Unhandled exception", e.Exception);
        // Release builds report XAML load failures only as "XAML parsing failed"; these name the missing resource or binding.
        DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
        DebugSettings.XamlResourceReferenceFailed += (_, e) => AppLog.Error("XAML resource reference failed: " + e.Message);
        DebugSettings.BindingFailed += (_, e) => AppLog.Error("XAML binding failed: " + e.Message);
    }

    public static AppPaths Paths { get; } = AppPaths.ForCurrentUser();
    public static AppSettings Settings { get; private set; } = null!;
    public static CursorService Cursors { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var smokeTest = Environment.GetCommandLineArgs().Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        try
        {
            AppLog.Info($"Starting PointerParlor {typeof(App).Assembly.GetName().Version}{(smokeTest ? " (smoke test)" : "")}");
            Settings = AppSettings.Load(Paths.Settings);
            Cursors = new CursorService(Paths);
            Cursors.Initialize();
            MainWindow = new MainWindow();
        }
        catch (Exception ex)
        {
            AppLog.Error("Startup failed", ex);
            AppLog.Info(ex.ToString().Replace(Environment.NewLine, " | "));
            if (smokeTest)
            {
                if (ex is Microsoft.UI.Xaml.Markup.XamlParseException)
                    XamlDiagnostics.Diagnose("MainWindow.xaml");
                Environment.Exit(1);
            }
            throw;
        }

        if (smokeTest)
        {
            // CI check: services load and the window builds. Reads the registry but changes nothing.
            AppLog.Info($"Smoke test passed: {Cursors.Sets.Count} sets, active = {Cursors.Active.Kind}");
            Environment.Exit(0);
        }
        MainWindow.Activate();
    }
}
