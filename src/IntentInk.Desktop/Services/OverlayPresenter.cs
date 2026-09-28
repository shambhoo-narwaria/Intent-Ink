using System.Windows;
using Application = System.Windows.Application;
using IntentInk.Core.Models;
using IntentInk.Desktop.Views;

namespace IntentInk.Desktop.Services;

/// <summary>
/// Presents and manages the floating lightweight button and replacement card on the WPF UI thread.
/// </summary>
public sealed class OverlayPresenter : IDisposable
{
    private SuggestionWindow? _window;

    public bool IsCardOpen
    {
        get
        {
            if (_window == null) return false;
            return Application.Current?.Dispatcher?.Invoke(() => _window != null && _window.IsCardOpen) ?? false;
        }
    }

    public bool ContainsScreenPoint(int screenX, int screenY)
    {
        if (_window == null) return false;
        return Application.Current?.Dispatcher?.Invoke(() => _window != null && _window.ContainsScreenPoint(screenX, screenY)) ?? false;
    }

    public void ShowPill(
        ScreenRect bounds,
        string appName,
        Func<CancellationToken, Task<CorrectionCandidate?>> checkFunc,
        Func<CorrectionCandidate, Task<ApplyResult>> applyFunc)
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            if (_window == null)
            {
                _window = new SuggestionWindow();
            }

            _window.ShowButton(bounds, appName, checkFunc, applyFunc);
        });
    }

    public void Hide()
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            _window?.Hide();
        });
    }

    public void Dispose()
    {
        Application.Current?.Dispatcher?.Invoke(() =>
        {
            _window?.Close();
            _window = null;
        });
    }
}
