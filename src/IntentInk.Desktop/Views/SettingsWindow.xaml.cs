using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using IntentInk.Desktop.Infrastructure;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace IntentInk.Desktop.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly ObservableCollection<string> _excludedProcesses = [];

    public SettingsWindow(SettingsStore settings)
    {
        InitializeComponent();
        _settings = settings;

        LstExcludedProcesses.ItemsSource = _excludedProcesses;

        Loaded += async (_, _) =>
        {
            LoadCurrentSettings();
            await RefreshModelsAsync();
        };
    }

    private void LoadCurrentSettings()
    {
        var current = _settings.Current;
        TxtBaseUrl.Text = current.BaseUrl;
        TxtTextLimit.Text = current.TextLimit.ToString();
        ChkRunAtSignIn.IsChecked = current.RunAtSignIn;

        _excludedProcesses.Clear();
        foreach (var p in current.ExcludedProcessNames)
        {
            _excludedProcesses.Add(p);
        }
    }

    private async Task RefreshModelsAsync()
    {
        BtnRefreshModels.IsEnabled = false;
        TxtStatus.Text = "Detecting installed Ollama models...";
        TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA));

        try
        {
            var baseUrl = TxtBaseUrl.Text.Trim();
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = "http://localhost:11434/";
            if (!baseUrl.EndsWith('/')) baseUrl += "/";

            using var client = new OllamaClient(() => new OllamaSettings { BaseUrl = baseUrl });
            var models = await client.ListModelsAsync();

            CmbModels.Items.Clear();
            foreach (var m in models)
            {
                CmbModels.Items.Add(m);
            }

            if (!string.IsNullOrEmpty(_settings.Current.ModelName) && models.Contains(_settings.Current.ModelName))
            {
                CmbModels.SelectedItem = _settings.Current.ModelName;
            }
            else if (models.Count > 0)
            {
                CmbModels.SelectedIndex = 0;
            }

            TxtStatus.Text = models.Count > 0
                ? $"Connected: Found {models.Count} model(s)"
                : "Connected to Ollama, but no models found (run: ollama pull <model>)";
            TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Connection failed: {ex.Message}";
            TxtStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
        }
        finally
        {
            BtnRefreshModels.IsEnabled = true;
        }
    }

    private async void BtnRefreshModels_Click(object sender, RoutedEventArgs e)
    {
        await RefreshModelsAsync();
    }

    private void BtnAddExclusion_Click(object sender, RoutedEventArgs e)
    {
        var proc = TxtNewExcludedProcess.Text.Trim().ToLowerInvariant();
        if (proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            proc = proc[..^4];
        }

        if (!string.IsNullOrWhiteSpace(proc) && !_excludedProcesses.Contains(proc))
        {
            _excludedProcesses.Add(proc);
            TxtNewExcludedProcess.Clear();
        }
    }

    private void BtnRemoveExclusion_Click(object sender, RoutedEventArgs e)
    {
        if (LstExcludedProcesses.SelectedItem is string selected)
        {
            _excludedProcesses.Remove(selected);
        }
    }

    private void BtnOpenLogs_Click(object sender, RoutedEventArgs e)
    {
        var dir = DiagnosticsLogger.WorkspaceLogDir;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true
        });
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var model = CmbModels.SelectedItem?.ToString() ?? "";
        int.TryParse(TxtTextLimit.Text, out int textLimit);
        if (textLimit <= 0) textLimit = 1500;

        bool runAtSignIn = ChkRunAtSignIn.IsChecked == true;

        _settings.Update(s =>
        {
            s.BaseUrl = TxtBaseUrl.Text.Trim();
            s.ModelName = model;
            s.TextLimit = textLimit;
            s.RunAtSignIn = runAtSignIn;
            s.ExcludedProcessNames = [.. _excludedProcesses];
        });

        AutoStartHelper.SetEnabled(runAtSignIn);
        Close();
    }
}
