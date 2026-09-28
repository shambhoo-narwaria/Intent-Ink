using System.Windows.Forms;
using IntentInk.Desktop.Infrastructure;
using Application = System.Windows.Application;

namespace IntentInk.Desktop.Views;

/// <summary>
/// Manages the system tray icon, context menu, and balloon notifications.
/// Lives for the duration of the application process.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly SettingsStore _settings;
    private readonly Func<Task<bool>> _checkConnectionFunc;
    private readonly Action _openLogsAction;
    private readonly Action _openSettingsAction;
    private readonly Action<bool> _pauseToggledAction;
    private readonly Action _exitAction;

    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseItem;

    public TrayIcon(
        SettingsStore settings,
        Func<Task<bool>> checkConnectionFunc,
        Action openSettingsAction,
        Action openLogsAction,
        Action<bool> pauseToggledAction,
        Action exitAction)
    {
        _settings = settings;
        _checkConnectionFunc = checkConnectionFunc;
        _openSettingsAction = openSettingsAction;
        _openLogsAction = openLogsAction;
        _pauseToggledAction = pauseToggledAction;
        _exitAction = exitAction;

        var contextMenu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("IntentInk — Active")
        {
            Enabled = false,
            Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
        };
        contextMenu.Items.Add(_statusItem);
        contextMenu.Items.Add(new ToolStripSeparator());

        _pauseItem = new ToolStripMenuItem("Pause Assistance", null, OnTogglePause);
        contextMenu.Items.Add(_pauseItem);

        var logsItem = new ToolStripMenuItem("Live Activity Logs...", null, (_, _) => _openLogsAction());
        contextMenu.Items.Add(logsItem);

        var openLogFolderItem = new ToolStripMenuItem("Open Log Folder...", null, (_, _) =>
        {
            try
            {
                var folder = DiagnosticsLogger.WorkspaceLogDir;
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
            }
            catch { }
        });
        contextMenu.Items.Add(openLogFolderItem);

        var settingsItem = new ToolStripMenuItem("Settings...", null, (_, _) => _openSettingsAction());
        contextMenu.Items.Add(settingsItem);

        var checkItem = new ToolStripMenuItem("Check Model Connection...", null, async (_, _) => await RunConnectionCheckAsync());
        contextMenu.Items.Add(checkItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit IntentInk", null, (_, _) => _exitAction());
        contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Text = "IntentInk — Grammar Assistant",
            Icon = IconFactory.GetActiveIcon(),
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => _openSettingsAction();

        UpdateState(_settings.Current.IsPaused);
    }

    public void UpdateState(bool isPaused)
    {
        if (isPaused)
        {
            _notifyIcon.Icon = IconFactory.GetPausedIcon();
            _notifyIcon.Text = "IntentInk (Paused)";
            _statusItem.Text = "IntentInk — Paused";
            _pauseItem.Text = "Resume Assistance";
            _pauseItem.Checked = true;
        }
        else
        {
            _notifyIcon.Icon = IconFactory.GetActiveIcon();
            _notifyIcon.Text = "IntentInk — Active";
            _statusItem.Text = "IntentInk — Active";
            _pauseItem.Text = "Pause Assistance";
            _pauseItem.Checked = false;
        }
    }

    private void OnTogglePause(object? sender, EventArgs e)
    {
        bool newPaused = !_settings.Current.IsPaused;
        _settings.Update(s => s.IsPaused = newPaused);
        UpdateState(newPaused);
        _pauseToggledAction(newPaused);
    }

    private async Task RunConnectionCheckAsync()
    {
        ShowNotification("Checking Ollama...", "Connecting to local model endpoint...", ToolTipIcon.Info);
        bool isOk = await _checkConnectionFunc();
        if (isOk)
        {
            ShowNotification("Ollama Connected", $"Model ready: {_settings.Current.ModelName}", ToolTipIcon.Info);
        }
        else
        {
            ShowNotification("Connection Failed", "Unable to connect to Ollama. Make sure 'ollama serve' is running.", ToolTipIcon.Warning);
        }
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
