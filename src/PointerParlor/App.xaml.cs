using Microsoft.UI.Xaml;
using PointerParlor.Core;
using PointerParlor.Services;

namespace PointerParlor;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => AppLog.Error("Unhandled exception", e.Exception);
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
            AppLog.Initialize(Paths.Logs);
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
                Environment.Exit(1);
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
