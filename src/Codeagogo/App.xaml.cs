// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Windows;
using Velopack;

namespace Codeagogo;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private bool _mutexAcquired;
    private TrayAppContext? _ctx;

    /// <summary>
    /// Custom entry point required by Velopack — VelopackApp.Build().Run() must execute
    /// before any WPF code to handle install/update/uninstall hooks.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch all unhandled exceptions so they get logged instead of silently crashing
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log.Error($"FATAL unhandled exception: {ex}");
            else
                Log.Error($"FATAL unhandled exception: {args.ExceptionObject}");
        };

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error($"FATAL dispatcher exception: {args.Exception}");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error($"Unobserved task exception: {args.Exception}");
            args.SetObserved();
        };

        // Single-instance guard
        _mutex = new Mutex(true, @"AEHRC.Codeagogo.Win", out bool createdNew);
        _mutexAcquired = createdNew;
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        Log.Info("App starting");

        var settings = Settings.Load();

        // Sync startup shortcut with setting (but not on first launch —
        // the welcome screen handles that to give users a choice)
        if (settings.WelcomeShown && settings.StartWithWindows && !StartupManager.IsEnabled)
        {
            try { StartupManager.SetEnabled(true); }
            catch (Exception ex) { Log.Error($"Failed to create startup shortcut: {ex.Message}"); }
        }

        // Set shutdown mode before creating any windows — WPF default is
        // OnLastWindowClose which would kill the app when the welcome window closes
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _ctx = new TrayAppContext();
        _ctx.Start();

        // Show welcome window on first launch (non-blocking)
        if (!settings.WelcomeShown)
        {
            var welcome = new WelcomeWindow();
            welcome.Closed += (_, _) =>
            {
                settings.WelcomeShown = true;
                settings.Save();
            };
            welcome.Show();
        }

        // Populate code systems from server on first launch (non-blocking)
        _ = CodeSystemSettings.PopulateFromServerAsync(
            new OntoserverClient(baseUrl: settings.FhirBaseUrl));

        // Check for updates on startup and every 24 hours
        _ = CheckForUpdatesAsync();
        var updateTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromHours(24)
        };
        updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync();
        updateTimer.Start();
    }

    /// <summary>
    /// Checks for updates from GitHub Releases, downloads them, and notifies
    /// the user via the system tray balloon tip.
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var mgr = new UpdateManager(new Velopack.Sources.GithubSource("https://github.com/aehrc/codeagogo-win", null, false));

            var update = await mgr.CheckForUpdatesAsync();
            if (update == null)
            {
                Log.Info("No updates available");
                return;
            }

            Log.Info($"Update available: {update.TargetFullRelease.Version}");
            await mgr.DownloadUpdatesAsync(update);
            Log.Info("Update downloaded, ready to apply");

            // Notify user via tray icon — don't force restart
            _ctx?.NotifyUpdateReady(update.TargetFullRelease.Version.ToString(), () =>
            {
                mgr.ApplyUpdatesAndRestart(update);
            });
        }
        catch (Exception ex)
        {
            // Don't crash on update check failure — it's not critical
            Log.Debug($"Update check failed: {ex.Message}");
        }
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
