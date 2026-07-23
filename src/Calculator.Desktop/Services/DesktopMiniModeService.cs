// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using CalculatorApp.Services.Settings;
using CalculatorApp.Services.Windowing;

namespace CalculatorApp.Desktop.Services;

internal sealed class DesktopMiniModeService : IMiniModeService, IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private Window? _window;
    private double _normalWidth;
    private double _normalHeight;
    private double _normalMinWidth;
    private double _normalMinHeight;
    private bool _normalTopmost;
    private WindowState _normalWindowState;
    private bool _isActive;
    private bool _isDisposed;

    public DesktopMiniModeService(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore ??
            throw new ArgumentNullException(nameof(settingsStore));
    }

    public bool IsSupported => true;

    public bool IsActive => _isActive;

    public void Attach(Window window)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(window);
        if (ReferenceEquals(_window, window))
        {
            return;
        }

        if (_window is not null)
        {
            _window.Closing -= OnWindowClosing;
        }

        _window = window;
        _window.Closing += OnWindowClosing;
    }

    public bool TryEnter()
    {
        if (_isDisposed || _isActive || _window is not { } window)
        {
            return false;
        }

        _normalWidth = GetCurrentDimension(
            window.ClientSize.Width,
            window.Width,
            AppSettings.DefaultMiniModeWidth);
        _normalHeight = GetCurrentDimension(
            window.ClientSize.Height,
            window.Height,
            AppSettings.DefaultMiniModeHeight);
        _normalMinWidth = window.MinWidth;
        _normalMinHeight = window.MinHeight;
        _normalTopmost = window.Topmost;
        _normalWindowState = window.WindowState;

        AppSettings settings = _settingsStore.Current;
        window.WindowState = WindowState.Normal;
        window.MinWidth = AppSettings.DefaultMiniModeWidth;
        window.MinHeight = AppSettings.DefaultMiniModeHeight;
        window.Width = settings.MiniModeWidth;
        window.Height = settings.MiniModeHeight;
        window.Topmost = true;
        _isActive = true;
        return true;
    }

    public bool TryExit()
    {
        if (_isDisposed || !_isActive || _window is not { } window)
        {
            return false;
        }

        PersistMiniModeSize(window);
        window.MinWidth = _normalMinWidth;
        window.MinHeight = _normalMinHeight;
        window.Width = _normalWidth;
        window.Height = _normalHeight;
        window.Topmost = _normalTopmost;
        window.WindowState = _normalWindowState;
        _isActive = false;
        return true;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_window is { } window)
        {
            if (_isActive)
            {
                PersistMiniModeSize(window);
            }

            window.Closing -= OnWindowClosing;
            _window = null;
        }

        _isDisposed = true;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        _ = sender;
        if (_isActive && !eventArgs.Cancel && _window is { } window)
        {
            PersistMiniModeSize(window);
        }
    }

    private void PersistMiniModeSize(Window window)
    {
        double width = GetCurrentDimension(
            window.ClientSize.Width,
            window.Width,
            AppSettings.DefaultMiniModeWidth);
        double height = GetCurrentDimension(
            window.ClientSize.Height,
            window.Height,
            AppSettings.DefaultMiniModeHeight);
        _settingsStore.Update(settings => settings with
        {
            MiniModeWidth = width,
            MiniModeHeight = height
        });
    }

    private static double GetCurrentDimension(
        double clientDimension,
        double requestedDimension,
        double fallback)
    {
        if (double.IsFinite(clientDimension) && clientDimension > 0)
        {
            return clientDimension;
        }

        return double.IsFinite(requestedDimension) && requestedDimension > 0
            ? requestedDimension
            : fallback;
    }
}
