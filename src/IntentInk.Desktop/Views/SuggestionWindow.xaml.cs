using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using IntentInk.Core.Models;
using IntentInk.Core.Services;
using IntentInk.Desktop.Infrastructure;
using IntentInk.Desktop.Interop;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Clipboard = System.Windows.Clipboard;

namespace IntentInk.Desktop.Views;

public partial class SuggestionWindow : Window
{
    private ScreenRect _lastBounds = ScreenRect.Empty;
    private Func<CancellationToken, Task<CorrectionCandidate?>>? _checkFunc;
    private Func<CorrectionCandidate, Task<ApplyResult>>? _applyFunc;
    private CorrectionCandidate? _currentCandidate;
    private string _appName = "application";
    private readonly DiffEngine _diff = new();

    private CancellationTokenSource? _inferenceCts;
    private DispatcherTimer? _loadingTimer;
    private Stopwatch? _loadingStopwatch;

    private static readonly SolidColorBrush InsertBg = new(Color.FromRgb(0x0E, 0x38, 0x20));
    private static readonly SolidColorBrush DeleteBg = new(Color.FromRgb(0x3B, 0x12, 0x19));
    private static readonly SolidColorBrush InsertFg = new(Color.FromRgb(0x34, 0xD3, 0x99));
    private static readonly SolidColorBrush DeleteFg = new(Color.FromRgb(0xF8, 0x71, 0x71));
    private static readonly SolidColorBrush EqualFg  = new(Color.FromRgb(0xE2, 0xE8, 0xF0));

    private static readonly SolidColorBrush TabActiveBg = new(Color.FromRgb(0x1E, 0x1E, 0x1E));
    private static readonly SolidColorBrush TabActiveFg = new(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush TabInactiveBg = new(Color.FromRgb(0x25, 0x25, 0x26));
    private static readonly SolidColorBrush TabInactiveFg = new(Color.FromRgb(0x88, 0x88, 0x88));

    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    public SuggestionWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        Win32.SetNonActivating(handle);

        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_MOUSEACTIVATE)
        {
            handled = true;
            return MA_NOACTIVATE;
        }
        return nint.Zero;
    }

    /// <summary>
    /// Displays the lightweight floating button near the text selection.
    /// </summary>
    public void ShowButton(
        ScreenRect bounds,
        string appName,
        Func<CancellationToken, Task<CorrectionCandidate?>> checkFunc,
        Func<CorrectionCandidate, Task<ApplyResult>> applyFunc)
    {
        CancelActiveInference();

        _lastBounds = bounds;
        _appName = appName;
        _checkFunc = checkFunc;
        _applyFunc = applyFunc;
        _currentCandidate = null;

        // Reset to Pill state
        PillContainer.Visibility = Visibility.Visible;
        LoadingContainer.Visibility = Visibility.Collapsed;
        ResultContainer.Visibility = Visibility.Collapsed;

        TxtPillLabel.Text = "Fix Grammar";
        PillContainer.IsEnabled = true;

        AnchorTo(bounds);
        if (!IsVisible) Show();
    }

    private async void Pill_Click(object sender, MouseButtonEventArgs e)
    {
        if (_checkFunc == null) return;

        DiagnosticsLogger.LogInfo("UI", "User clicked floating pill. Switching to AI loading state...");

        // Transition to Loading State
        PillContainer.Visibility = Visibility.Collapsed;
        LoadingContainer.Visibility = Visibility.Visible;
        ResultContainer.Visibility = Visibility.Collapsed;
        AnchorTo(_lastBounds);

        // Start Stopwatch & Timer for live elapsed time
        _loadingStopwatch = Stopwatch.StartNew();
        _loadingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _loadingTimer.Tick += (_, _) =>
        {
            if (_loadingStopwatch != null)
            {
                TxtLoadingTimer.Text = $"({_loadingStopwatch.Elapsed.TotalSeconds:F1}s)";
            }
        };
        _loadingTimer.Start();

        _inferenceCts = new CancellationTokenSource();
        var token = _inferenceCts.Token;

        try
        {
            var candidate = await _checkFunc(token);

            StopLoadingTimer();

            if (token.IsCancellationRequested) return;

            if (candidate == null)
            {
                DiagnosticsLogger.LogWarning("UI", "No correction result is available.");
                LoadingContainer.Visibility = Visibility.Collapsed;
                PillContainer.Visibility = Visibility.Collapsed;
                ResultContainer.Visibility = Visibility.Visible;

                TxtAppBadge.Text = _appName;
                TxtCorrectedParagraph.Text = "Could not read or process the selected text.";
                SelectCleanTextTab();

                TxtCardStatus.Text = "Select editable text and try again.";
                TxtCardStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
                TxtCardStatus.Visibility = Visibility.Visible;

                BtnReplace.IsEnabled = false;
                BtnReplace.Content = "No Changes";
                BtnReplace.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));

                AnchorTo(_lastBounds);
                return;
            }

            if (candidate.CorrectedText == candidate.Source.OriginalText)
            {
                // Grammar is already acceptable or no changes proposed
                DiagnosticsLogger.LogInfo("UI", "Text is already grammatically acceptable. Showing status card.");
                LoadingContainer.Visibility = Visibility.Collapsed;
                PillContainer.Visibility = Visibility.Collapsed;
                ResultContainer.Visibility = Visibility.Visible;

                TxtAppBadge.Text = _appName;
                TxtCorrectedParagraph.Text = candidate.Source.OriginalText;
                SelectCleanTextTab();

                TxtCardStatus.Text = "Text is already acceptable. No changes proposed by AI.";
                TxtCardStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x3D, 0xDC, 0x84));
                TxtCardStatus.Visibility = Visibility.Visible;

                BtnReplace.IsEnabled = false;
                BtnReplace.Content = "No Changes Needed";
                BtnReplace.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));

                AnchorTo(_lastBounds);
                return;
            }

            // Transition to Result State
            _currentCandidate = candidate;
            ShowResultCard(candidate);
        }
        catch (OperationCanceledException)
        {
            StopLoadingTimer();
            Hide();
        }
        catch (Exception ex)
        {
            StopLoadingTimer();
            DiagnosticsLogger.LogError("UI.Pill_Click", ex);
            Hide();
        }
    }

    private void ShowResultCard(CorrectionCandidate candidate)
    {
        LoadingContainer.Visibility = Visibility.Collapsed;
        PillContainer.Visibility = Visibility.Collapsed;
        ResultContainer.Visibility = Visibility.Visible;

        TxtAppBadge.Text = _appName;
        TxtCardStatus.Visibility = Visibility.Collapsed;

        // Set Clean Paragraph
        TxtCorrectedParagraph.Text = candidate.CorrectedText;

        // Render Inline Diff
        RenderDiff(candidate.Source.OriginalText, candidate.CorrectedText);

        // Reset to Clean Text view by default
        SelectCleanTextTab();

        BtnReplace.IsEnabled = true;
        BtnReplace.Content = "Replace";
        BtnReplace.Background = (SolidColorBrush)FindResource("PrimaryAccentBrush");

        AnchorTo(_lastBounds);
    }

    private void RenderDiff(string original, string corrected)
    {
        var segments = _diff.Compute(original, corrected);
        var paragraph = new Paragraph();

        foreach (var seg in segments)
        {
            var run = new Run(seg.Text);
            switch (seg.Kind)
            {
                case DiffEngine.ChangeKind.Insert:
                    run.Background = InsertBg;
                    run.Foreground = InsertFg;
                    run.FontWeight = FontWeights.Bold;
                    break;
                case DiffEngine.ChangeKind.Delete:
                    run.Background = DeleteBg;
                    run.Foreground = DeleteFg;
                    run.TextDecorations = TextDecorations.Strikethrough;
                    break;
                default:
                    run.Foreground = EqualFg;
                    break;
            }
            paragraph.Inlines.Add(run);
        }

        RtbDiff.Document = new FlowDocument(paragraph)
        {
            PagePadding = new Thickness(0)
        };
    }

    private void BtnTabClean_Click(object sender, RoutedEventArgs e)
    {
        SelectCleanTextTab();
    }

    private void BtnTabDiff_Click(object sender, RoutedEventArgs e)
    {
        SelectDiffTab();
    }

    private void SelectCleanTextTab()
    {
        BtnTabClean.Background = TabActiveBg;
        BtnTabClean.Foreground = TabActiveFg;

        BtnTabDiff.Background = TabInactiveBg;
        BtnTabDiff.Foreground = TabInactiveFg;

        PanelCleanText.Visibility = Visibility.Visible;
        PanelDiffText.Visibility = Visibility.Collapsed;
    }

    private void SelectDiffTab()
    {
        BtnTabDiff.Background = TabActiveBg;
        BtnTabDiff.Foreground = TabActiveFg;

        BtnTabClean.Background = TabInactiveBg;
        BtnTabClean.Foreground = TabInactiveFg;

        PanelDiffText.Visibility = Visibility.Visible;
        PanelCleanText.Visibility = Visibility.Collapsed;
    }

    private async void BtnReplace_Click(object sender, RoutedEventArgs e)
    {
        if (_currentCandidate == null || _applyFunc == null) return;

        DiagnosticsLogger.LogInfo("UI", "User clicked 'Replace' button. Executing replacement...");
        BtnReplace.IsEnabled = false;
        BtnReplace.Content = "Replacing...";

        var result = await _applyFunc(_currentCandidate);
        if (result.Success)
        {
            BtnReplace.Content = "Replaced!";
            BtnReplace.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x4D, 0x2B));

            await Task.Delay(650);
            Hide();
        }
        else
        {
            TxtCardStatus.Text = $"Replacement failed: {result.FailureReason}";
            TxtCardStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0x71, 0x71));
            TxtCardStatus.Visibility = Visibility.Visible;
            BtnReplace.IsEnabled = true;
            BtnReplace.Content = "Replace";
            BtnReplace.Background = (SolidColorBrush)FindResource("PrimaryAccentBrush");
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        CancelActiveInference();
        Hide();
    }

    private void BtnCancelLoading_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsLogger.LogInfo("UI", "User cancelled AI loading.");
        CancelActiveInference();
        Hide();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_currentCandidate == null) return;
        try
        {
            Clipboard.SetText(_currentCandidate.CorrectedText);
            TxtCardStatus.Text = "Copied to clipboard!";
            TxtCardStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
            TxtCardStatus.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            TxtCardStatus.Text = $"Copy failed: {ex.Message}";
            TxtCardStatus.Visibility = Visibility.Visible;
        }
    }

    private void StopLoadingTimer()
    {
        _loadingTimer?.Stop();
        _loadingTimer = null;
        _loadingStopwatch?.Stop();
        _loadingStopwatch = null;
    }

    private void CancelActiveInference()
    {
        StopLoadingTimer();
        _inferenceCts?.Cancel();
        _inferenceCts?.Dispose();
        _inferenceCts = null;
    }

    public void AnchorTo(ScreenRect bounds)
    {
        UpdateLayout();
        double w = ActualWidth > 0 ? ActualWidth : 200;
        double h = ActualHeight > 0 ? ActualHeight : 50;

        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
        if (dpiX <= 0) dpiX = 1.0;
        if (dpiY <= 0) dpiY = 1.0;

        double targetX;
        double targetY;

        var workArea = SystemParameters.WorkArea;

        if (bounds != ScreenRect.Empty && bounds.Width > 0 && bounds.Height > 0)
        {
            double bx = bounds.X / dpiX;
            double by = bounds.Y / dpiY;
            double bw = bounds.Width / dpiX;
            double bh = bounds.Height / dpiY;

            targetX = bx + bw + 10;
            targetY = by + bh + 4;

            if (targetX + w > workArea.Right - 10)
            {
                targetX = bx - w - 10;
            }
            if (targetY + h > workArea.Bottom - 10)
            {
                targetY = by - h - 6;
            }
        }
        else
        {
            var mouse = System.Windows.Forms.Cursor.Position;
            targetX = (mouse.X / dpiX) + 15;
            targetY = (mouse.Y / dpiY) + 15;
        }

        targetX = Math.Max(workArea.Left + 10, Math.Min(targetX, workArea.Right - w - 10));
        targetY = Math.Max(workArea.Top + 10, Math.Min(targetY, workArea.Bottom - h - 10));

        Left = targetX;
        Top = targetY;
    }

    /// <summary>
    /// Returns true if the user is actively viewing or interacting with the Loading or Result card.
    /// In this state, mouse clicks elsewhere or typing in background should not auto-dismiss the card.
    /// </summary>
    public bool IsCardOpen => IsVisible && (LoadingContainer.Visibility == Visibility.Visible || ResultContainer.Visibility == Visibility.Visible);

    /// <summary>
    /// Checks whether physical screen coordinates (x, y) fall within this window's bounds.
    /// </summary>
    public bool ContainsScreenPoint(int screenX, int screenY)
    {
        if (!IsVisible) return false;

        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double left = Left * dpiX;
        double top = Top * dpiY;
        double width = ActualWidth * dpiX;
        double height = ActualHeight * dpiY;

        return screenX >= left - 6 && screenX <= left + width + 6 &&
               screenY >= top - 6 && screenY <= top + height + 6;
    }
}
