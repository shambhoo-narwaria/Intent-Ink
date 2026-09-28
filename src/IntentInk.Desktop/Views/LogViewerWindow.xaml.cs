using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using IntentInk.Desktop.Infrastructure;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Clipboard = System.Windows.Clipboard;

namespace IntentInk.Desktop.Views;

public partial class LogViewerWindow : Window
{
    private static readonly SolidColorBrush SuccessBrush = new(Color.FromRgb(0x4A, 0xDE, 0x80));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xFB, 0xBF, 0x24));
    private static readonly SolidColorBrush ErrorBrush   = new(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly SolidColorBrush InfoBrush    = new(Color.FromRgb(0x38, 0xBD, 0xF8));
    private static readonly SolidColorBrush AiBrush      = new(Color.FromRgb(0xA7, 0x8B, 0xFA));
    private static readonly SolidColorBrush DebugBrush   = new(Color.FromRgb(0x64, 0x74, 0x8B));

    private readonly Paragraph _paragraph = new();

    public LogViewerWindow()
    {
        InitializeComponent();

        RtbLogs.Document = new FlowDocument(_paragraph)
        {
            PagePadding = new Thickness(0)
        };

        // Load existing logs from memory
        foreach (var line in DiagnosticsLogger.GetRecentLogs())
        {
            AppendLogLine(line, DetermineLevel(line));
        }

        DiagnosticsLogger.LogEmitted += OnLogEmitted;
        Closed += (_, _) => DiagnosticsLogger.LogEmitted -= OnLogEmitted;
    }

    private void OnLogEmitted(string line, LogLevel level)
    {
        Dispatcher.InvokeAsync(() =>
        {
            AppendLogLine(line, level);
        });
    }

    private void AppendLogLine(string line, LogLevel level)
    {
        var brush = level switch
        {
            LogLevel.Success => SuccessBrush,
            LogLevel.Warning => WarningBrush,
            LogLevel.Error   => ErrorBrush,
            LogLevel.Debug   => DebugBrush,
            _ => line.Contains("[AI]") ? AiBrush : InfoBrush
        };

        var run = new Run(line + Environment.NewLine) { Foreground = brush };
        _paragraph.Inlines.Add(run);

        if (ChkAutoScroll.IsChecked == true)
        {
            RtbLogs.ScrollToEnd();
        }
    }

    private static LogLevel DetermineLevel(string line)
    {
        if (line.Contains("[OK]", StringComparison.Ordinal)) return LogLevel.Success;
        if (line.Contains("[WARN]", StringComparison.Ordinal)) return LogLevel.Warning;
        if (line.Contains("[ERROR]", StringComparison.Ordinal)) return LogLevel.Error;
        if (line.Contains("[DEBUG]", StringComparison.Ordinal)) return LogLevel.Debug;
        return LogLevel.Info;
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _paragraph.Inlines.Clear();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        var tr = new TextRange(RtbLogs.Document.ContentStart, RtbLogs.Document.ContentEnd);
        try
        {
            Clipboard.SetText(tr.Text);
        }
        catch { }
    }

    private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dir = DiagnosticsLogger.WorkspaceLogDir;
        var file = DiagnosticsLogger.PrimaryLogPath;

        if (File.Exists(file))
        {
            Process.Start(new ProcessStartInfo { FileName = file, UseShellExecute = true });
        }
        else
        {
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
    }
}
