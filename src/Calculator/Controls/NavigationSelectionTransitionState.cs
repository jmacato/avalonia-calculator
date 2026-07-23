// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

internal sealed class NavigationSelectionTransitionState
{
    private readonly Button _button;
    private NavigationSelectionTransitionSettings _settings;
    private NavigationSelectionCoordinator? _coordinator;
    private bool _isDisposed;
    private bool _isSelected;

    internal NavigationSelectionTransitionState(
        Button button,
        NavigationSelectionTransitionSettings settings,
        bool isSelected)
    {
        _button = button;
        _settings = settings;
        _isSelected = isSelected;
        _button.AttachedToVisualTree += OnAttachedToVisualTree;
        _button.DetachedFromVisualTree += OnDetachedFromVisualTree;
        if (CompositionVisualMotion.GetVisual(_button) is not null)
        {
            Attach();
        }
    }

    internal void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        if (isSelected)
        {
            _coordinator?.Select(_button, _settings);
        }
    }

    internal void SetSettings(
        NavigationSelectionTransitionSettings settings)
    {
        _settings = settings;
    }

    internal void DetachBehavior()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _button.AttachedToVisualTree -= OnAttachedToVisualTree;
        _button.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        Detach();
    }

    private void OnAttachedToVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;
        Attach();
    }

    private void OnDetachedFromVisualTree(
        object? sender,
        VisualTreeAttachmentEventArgs e)
    {
        _ = sender;
        _ = e;
        Detach();
    }

    private void Attach()
    {
        if (_isDisposed || _coordinator is not null)
        {
            return;
        }

        ItemsControl? owner = _button.GetVisualAncestors()
            .OfType<ItemsControl>()
            .FirstOrDefault();
        if (owner is null)
        {
            return;
        }

        _coordinator = owner.GetValue(
            NavigationSelectionTransition.CoordinatorProperty);
        if (_coordinator is null)
        {
            _coordinator = new NavigationSelectionCoordinator(owner);
            owner.SetValue(
                NavigationSelectionTransition.CoordinatorProperty,
                _coordinator);
        }

        if (_isSelected)
        {
            _coordinator.Select(_button, _settings);
        }
    }

    private void Detach()
    {
        _coordinator?.Remove(_button);
        _coordinator = null;
    }
}
