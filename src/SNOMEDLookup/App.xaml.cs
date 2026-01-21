using System.Threading;
using System.Windows;

namespace SNOMEDLookup;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private bool _mutexAcquired;
    private TrayAppContext? _ctx;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance guard
        _mutex = new Mutex(true, @"AEHRC.SNOMEDLookup.Win", out bool createdNew);
        _mutexAcquired = createdNew;
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

        // Release mutex - catch any synchronization exceptions
        try
        {
            if (_mutexAcquired && _mutex != null)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch (System.ApplicationException)
        {
            // Mutex was not acquired by this thread - ignore
        }

        _mutex?.Dispose();

        Log.Info("App exiting");
        base.OnExit(e);
    }
}
