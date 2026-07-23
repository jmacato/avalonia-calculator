// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CalculatorApp;

public sealed partial class KeyGraphFeaturesPanel : UserControl, IDisposable
{
    private bool _disposed;

    public KeyGraphFeaturesPanel()
    {
        InitializeComponent();
    }

    public event EventHandler<RoutedEventArgs>? KeyGraphFeaturesClosed;

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        KeyGraphFeaturesClosed?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        IDisposable? disposable = DataContext as IDisposable;
        DataContext = null;
        KeyGraphFeaturesClosed = null;
        disposable?.Dispose();
    }
}
