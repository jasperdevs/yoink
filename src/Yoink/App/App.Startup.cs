using System.Windows;
using System.Windows.Threading;
using Yoink.Services;
using Yoink.UI;

namespace Yoink;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) || a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            base.OnStartup(e);
            try { UninstallService.RemoveInstalledAppEntry(); } catch (Exception ex) { AppDiagnostics.LogError("startup.uninstall.remove-installed-entry", ex); }
            try { UninstallService.RemoveStartMenuShortcut(); } catch (Exception ex) { AppDiagnostics.LogError("startup.uninstall.remove-start-menu", ex); }
            try { UninstallService.RemoveStartupEntry(); } catch (Exception ex) { AppDiagnostics.LogError("startup.uninstall.remove-startup-entry", ex); }
            try { UninstallService.RemoveAppData(); } catch (Exception ex) { AppDiagnostics.LogError("startup.uninstall.remove-appdata", ex); }
            try { UninstallService.ScheduleInstallFolderRemoval(); } catch (Exception ex) { AppDiagnostics.LogError("startup.uninstall.schedule-folder-removal", ex); }
            Shutdown();
            return;
        }

        if (TryApplyUpdateAndExit(e))
            return;

        bool isPostInstall = e.Args.Any(a => a.Equals("--post-install", StringComparison.OrdinalIgnoreCase));

        bool shouldInstall = !isPostInstall && InstallService.ShouldShowInstaller();
        if (shouldInstall)
        {
            try
            {
                base.OnStartup(e);
                Theme.Refresh();
                Theme.ApplyTo(Resources);
                var installer = new InstallWizard();
                installer.ShowDialog();
                if (installer.InstallCompleted && installer.LaunchAfter)
                    InstallService.LaunchInstalled(installer.InstalledPath, true);
            }
            catch (Exception ex)
            {
                try { base.OnStartup(e); } catch (Exception startupEx) { AppDiagnostics.LogError("startup.install-wizard.base", startupEx); }
                AppDiagnostics.LogError("startup.install-wizard", ex);
                MessageBox.Show($"Install wizard failed to start:\n\n{ex}", "Yoink", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Shutdown();
            return;
        }

        _mutex = new Mutex(false, "YoinkScreenshotTool_SingleInstance");
        bool acquired;
        try
        {
            acquired = _mutex.WaitOne(TimeSpan.FromSeconds(8), false);
        }
        catch (AbandonedMutexException)
        {
            acquired = true;
        }

        if (!acquired)
        {
            base.OnStartup(e);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        WireUnhandledExceptionLogging();

        try { UninstallService.RegisterInstalledAppEntry(); } catch (Exception ex) { AppDiagnostics.LogError("startup.register-installed-entry", ex); }
        try { UninstallService.EnsureStartMenuShortcut(); } catch (Exception ex) { AppDiagnostics.LogError("startup.ensure-start-menu-shortcut", ex); }

        _settingsService = new SettingsService();
        _settingsService.Load();
        BackgroundRuntimeJobService.Initialize();
        StartBackgroundPreloads();

        if (isPostInstall)
            _settingsService.Settings.HasCompletedSetup = false;

        try { SyncStartupRegistry(_settingsService.Settings.StartWithWindows); } catch (Exception ex) { AppDiagnostics.LogError("startup.sync-startup-registry", ex); }
        System.Windows.Forms.Application.EnableVisualStyles();
        SoundService.Muted = _settingsService.Settings.MuteSounds;
        SoundService.SetPack(_settingsService.Settings.SoundPack);
        UI.Motion.Disabled = _settingsService.Settings.DisableAnimations;
        Theme.Refresh();
        Theme.ApplyTo(Resources);
        Helpers.UiChrome.DetectRefreshRate();
        ToastWindow.SetPosition(_settingsService.Settings.ToastPosition);
        ToastWindow.SetDuration(_settingsService.Settings.ToastDurationSeconds);
        ToastWindow.SetButtonLayout(_settingsService.Settings.ToastButtons);
        ToastWindow.SetFadeOutBehavior(_settingsService.Settings.ToastFadeOutEnabled, _settingsService.Settings.ToastFadeOutSeconds);

        _idleTrimTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _idleTrimTimer.Tick += (_, _) => TrimIdleMemory();
        ScheduleIdleMemoryTrim();

        bool openSettingsAfterWizard = false;
        if (!_settingsService.Settings.HasCompletedSetup)
        {
            var wizard = new SetupWizard(_settingsService);
            wizard.ShowDialog();
            openSettingsAfterWizard = wizard.Tag as string == "OpenSettings";
        }

        ConfigureTrayIcon();
        RegisterHotkeys();
        WarmDxgiCapture();
        Helpers.StreamlineIcons.Preload();

        if (_settingsService.Settings.AutoCheckForUpdates)
            _ = CheckForUpdatesOnStartupAsync();

        if (openSettingsAfterWizard)
            ShowSettings();
    }

    private bool TryApplyUpdateAndExit(StartupEventArgs e)
    {
        var index = Array.FindIndex(e.Args, arg => arg.Equals("--apply-update", StringComparison.OrdinalIgnoreCase));
        if (index < 0)
            return false;

        if (e.Args.Length < index + 3)
        {
            base.OnStartup(e);
            MessageBox.Show("Yoink update helper was launched with invalid arguments.", "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return true;
        }

        var packagePath = e.Args[index + 1];
        var targetDir = e.Args[index + 2];
        var nextArg = e.Args.Length > index + 3 ? e.Args[index + 3] : null;
        var versionLabel = nextArg != null && !nextArg.StartsWith("--") ? nextArg : null;

        var pidIndex = Array.FindIndex(e.Args, arg => arg.Equals("--wait-pid", StringComparison.OrdinalIgnoreCase));
        if (pidIndex >= 0 && e.Args.Length > pidIndex + 1 && int.TryParse(e.Args[pidIndex + 1], out int parentPid))
        {
            try
            {
                using var parent = System.Diagnostics.Process.GetProcessById(parentPid);
                parent.WaitForExit(15000);
            }
            catch (Exception ex) { AppDiagnostics.LogWarning("startup.apply-update.wait-parent", "Parent process was already gone or couldn't be inspected.", ex); }
        }

        base.OnStartup(e);

        try
        {
            InstallService.KillRunningInstances();
            InstallService.ApplyUpdateFromZip(packagePath, targetDir, versionLabel, launchAfter: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Shutdown();
        }

        return true;
    }

    private void WireUnhandledExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                AppDiagnostics.LogError("appdomain.unhandled", ex);
            else
                AppDiagnostics.LogWarning("appdomain.unhandled", args.ExceptionObject?.ToString() ?? "Unknown unhandled exception.");
        };
        DispatcherUnhandledException += (_, args) => AppDiagnostics.LogError("dispatcher.unhandled", args.Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppDiagnostics.LogError("tasks.unobserved", args.Exception);
            args.SetObserved();
        };
    }

    private void StartBackgroundPreloads()
    {
        _ = Task.Run(() =>
        {
            try
            {
                var historyService = EnsureHistoryService();
                SettingsWindow.WarmHistoryThumbsInBackground(historyService.ImageEntries, maxCount: 192, immediateCount: 48, batchSize: 24);
                EnsureImageSearchIndexService();
            }
            catch (Exception ex)
            {
                AppDiagnostics.LogError("startup.preload-history-search", ex);
            }
        });

        _ = Task.Run(async () =>
        {
            try
            {
                await LocalClipRuntimeService.EnsureInstalledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppDiagnostics.LogError("startup.preload-semantic-runtime", ex);
            }
        });
    }

    private void ConfigureTrayIcon()
    {
        _trayIcon = new TrayIcon(_settingsService?.Settings);
        _trayIcon.OnCapture += OnHotkeyPressed;
        _trayIcon.OnOcr += OnOcrHotkeyPressed;
        _trayIcon.OnColorPicker += OnPickerHotkeyPressed;
        _trayIcon.OnGifRecord += OnGifHotkeyPressed;
        _trayIcon.OnScrollCapture += OnScrollCaptureHotkeyPressed;
        _trayIcon.OnSettings += ShowSettings;
        _trayIcon.OnHistory += ShowHistory;
        _trayIcon.OnQuit += () => Shutdown();
    }

    private static void WarmDxgiCapture()
    {
        _ = Task.Run(() =>
        {
            try { Yoink.Capture.DxgiScreenCapture.WarmUp(); } catch (Exception ex) { AppDiagnostics.LogError("startup.dxgi-warmup", ex); }
        });
    }
}
