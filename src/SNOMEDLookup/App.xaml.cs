using System.Threading;
using System.Windows;

namespace SNOMEDLookup;

public partial class App : Application
{
    private Mutex? _mutex;
    private TrayAppContext? _ctx;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance guard
        _mutex = new Mutex(true, @"AEHRC.SNOMEDLookup.Win", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        Log.Info("App starting");

        _ctx = new TrayAppContext();
        _ctx.Start();

        // No main window
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _ctx?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();

        Log.Info("App exiting");
        base.OnExit(e);
    }
}
