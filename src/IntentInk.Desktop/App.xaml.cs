using System.Threading;
using System.Windows;
using IntentInk.Desktop.Infrastructure;
using IntentInk.Desktop.Services;
using IntentInk.Desktop.Views;

namespace IntentInk.Desktop;

/// <summary>
/// Application entry point and lifecycle coordinator.
/// Uses ShutdownMode=OnExplicitShutdown so closing windows does not exit the tray app.
/// Single-instance mutex guarantees only one copy runs per user session.
/// </summary>
public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private TrayIcon? _trayIcon;
    private SettingsStore? _settings;
    private SettingsWindow? _settingsWindow;
    private LogViewerWindow? _logViewerWindow;
    private bool _resourcesDisposed;

    // Pipeline Services
    private OllamaClient? _ollamaClient;
    private OverlayPresenter? _overlayPresenter;
    private CorrectionCoordinator? _coordinator;

    private void App_Startup(object sender, StartupEventArgs e)
    {
        // 1. Single-instance check
        string mutexName = $"IntentInk_SingleInstance_{Environment.UserName}";
        _singleInstanceMutex = new Mutex(true, mutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            Console.WriteLine("IntentInk is already running in the background (system tray).");
            Shutdown(0);
            return;
        }

        // 2. Initialize Settings
        _settings = new SettingsStore();
        _settings.Load();

        DiagnosticsLogger.Log("App", "IntentInk started successfully.");

        // 3. Initialize Tray Icon
        _trayIcon = new TrayIcon(
            settings: _settings,
            checkConnectionFunc: CheckConnectionAsync,
            openSettingsAction: OpenSettingsWindow,
            openLogsAction: OpenLogViewerWindow,
            pauseToggledAction: OnPauseToggled,
            exitAction: ExitApplication);

        // 4. Initialize Selection Pipeline
        try
        {
            _ollamaClient = new OllamaClient(() => _settings.Current);
            _overlayPresenter = new OverlayPresenter();

            _coordinator = new CorrectionCoordinator(
                _settings,
                _ollamaClient,
                _overlayPresenter);

            DiagnosticsLogger.Log("App", "Selection-based grammar pipeline active.");
        }
        catch (Exception ex)
        {
            DiagnosticsLogger.LogError("App.PipelineStartup", ex);
        }

        // 5. Notify only for interactive launches (not sign-in startup).
        bool startMinimized = e.Args.Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        if (!startMinimized)
        {
            _trayIcon.ShowNotification("IntentInk Active", "IntentInk is running in the background and system tray.");
        }
    }

    private void OpenSettingsWindow()
    {
        if (_settings == null) return;

        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_settings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
        }
    }

    private async Task<bool> CheckConnectionAsync()
    {
        if (_ollamaClient == null) return false;
        try
        {
            var models = await _ollamaClient.ListModelsAsync();
            return models.Count > 0;
        }
        catch (Exception ex)
        {
            DiagnosticsLogger.LogError("ConnectionCheck", ex);
            return false;
        }
    }

    private void OnPauseToggled(bool isPaused)
    {
        DiagnosticsLogger.Log("App", $"Assistance pause toggled: {isPaused}");
        if (isPaused)
        {
            _overlayPresenter?.Hide();
        }
    }

    private void OpenLogViewerWindow()
    {
        if (_logViewerWindow == null || !_logViewerWindow.IsLoaded)
        {
            _logViewerWindow = new LogViewerWindow();
            _logViewerWindow.Closed += (_, _) => _logViewerWindow = null;
            _logViewerWindow.Show();
        }
        else
        {
            if (_logViewerWindow.WindowState == WindowState.Minimized)
                _logViewerWindow.WindowState = WindowState.Normal;
            _logViewerWindow.Activate();
        }
    }

    private void ExitApplication()
    {
        DiagnosticsLogger.Log("App", "Shutting down IntentInk.");

        _settingsWindow?.Close();
        _logViewerWindow?.Close();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeResources();
        base.OnExit(e);
    }

    private void DisposeResources()
    {
        if (_resourcesDisposed) return;
        _resourcesDisposed = true;

        _coordinator?.Dispose();
        _coordinator = null;
        _overlayPresenter = null; // Owned and disposed by CorrectionCoordinator.

        _ollamaClient?.Dispose();
        _ollamaClient = null;

        _trayIcon?.Dispose();
        _trayIcon = null;

        if (_singleInstanceMutex != null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch { }
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
    }
}
