// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

internal sealed class CompositionMotionState
{
    private readonly Control _target;
    private readonly CompositionMotionProfile _profile;
    private readonly HashSet<string> _dataContextPropertyNames;
    private readonly List<Visual> _visibilityChain = new();
    private INotifyPropertyChanged? _dataContext;
    private bool _isAttached;
    private bool _isDisposed;
    private bool _isDataContextAnimationQueued;
    private bool _wasEffectivelyVisible;
    private int _activationCount;

    internal CompositionMotionState(
        Control target,
        CompositionMotionProfile profile)
    {
        _target = target;
        _profile = profile;
        _dataContextPropertyNames =
            string.IsNullOrWhiteSpace(profile.DataContextPropertyNames)
                ? new HashSet<string>(StringComparer.Ordinal)
                : profile.DataContextPropertyNames
                    .Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.Ordinal);
        _target.AttachedToVisualTree += OnAttachedToVisualTree;
        _target.DetachedFromVisualTree += OnDetachedFromVisualTree;
        _target.DataContextChanged += OnDataContextChanged;
        if (CompositionVisualMotion.GetVisual(_target) is not null)
        {
            Attach();
        }
    }

    internal int StartCount { get; private set; }

    internal void DetachBehavior()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _target.AttachedToVisualTree -= OnAttachedToVisualTree;
        _target.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        _target.DataContextChanged -= OnDataContextChanged;
        Detach();
        CompositionMotionRunner.Apply(
            _target,
            _profile,
            useFromValues: false);
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
        if (_isDisposed || _isAttached)
        {
            return;
        }

        _isAttached = true;
        _activationCount = 0;
        if (_profile.Trigger == CompositionMotionTrigger.PointerOver)
        {
            _target.PointerEntered += OnPointerEntered;
            _target.PointerExited += OnPointerExited;
            CompositionMotionRunner.Apply(
                _target,
                _profile,
                useFromValues: true);
        }
        else
        {
            SubscribeToVisibilityChain();
            _wasEffectivelyVisible = _target.IsEffectivelyVisible;
            if (_wasEffectivelyVisible)
            {
                Activate();
            }
            else
            {
                CompositionMotionRunner.Apply(
                    _target,
                    _profile,
                    useFromValues: true);
            }
        }

        SubscribeToDataContext();
    }

    private void Detach()
    {
        if (!_isAttached)
        {
            return;
        }

        _isAttached = false;
        _target.PointerEntered -= OnPointerEntered;
        _target.PointerExited -= OnPointerExited;
        foreach (Visual visual in _visibilityChain)
        {
            visual.PropertyChanged -= OnVisibilityPropertyChanged;
        }

        _visibilityChain.Clear();
        if (_dataContext is not null)
        {
            _dataContext.PropertyChanged -= OnDataContextPropertyChanged;
            _dataContext = null;
        }
    }

    private void SubscribeToVisibilityChain()
    {
        _visibilityChain.Add(_target);
        _visibilityChain.AddRange(_target.GetVisualAncestors());
        foreach (Visual visual in _visibilityChain)
        {
            visual.PropertyChanged += OnVisibilityPropertyChanged;
        }
    }

    private void OnVisibilityPropertyChanged(
        object? sender,
        AvaloniaPropertyChangedEventArgs e)
    {
        _ = sender;
        if (e.Property != Visual.IsVisibleProperty)
        {
            return;
        }

        bool isVisible = _target.IsEffectivelyVisible;
        if (isVisible == _wasEffectivelyVisible)
        {
            return;
        }

        _wasEffectivelyVisible = isVisible;
        if (isVisible)
        {
            Activate();
        }
        else
        {
            CompositionMotionRunner.Apply(
                _target,
                _profile,
                useFromValues: true);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        SubscribeToDataContext();
    }

    private void SubscribeToDataContext()
    {
        if (_dataContext is not null)
        {
            _dataContext.PropertyChanged -= OnDataContextPropertyChanged;
        }

        _dataContext = _target.DataContext as INotifyPropertyChanged;
        if (_isAttached && _dataContext is not null)
        {
            _dataContext.PropertyChanged += OnDataContextPropertyChanged;
        }
    }

    private void OnDataContextPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        _ = sender;
        if (!_isAttached ||
            _dataContextPropertyNames.Count == 0 ||
            (!string.IsNullOrEmpty(e.PropertyName) &&
             !_dataContextPropertyNames.Contains(e.PropertyName)) ||
            _isDataContextAnimationQueued)
        {
            return;
        }

        _isDataContextAnimationQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _isDataContextAnimationQueued = false;
                if (!_isDisposed &&
                    _isAttached &&
                    _target.IsEffectivelyVisible)
                {
                    Start(forward: true);
                }
            },
            DispatcherPriority.Send);
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _ = sender;
        if (_profile.IncludeTouchPointers ||
            e.Pointer.Type is PointerType.Mouse or PointerType.Pen)
        {
            Start(forward: true);
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _ = sender;
        _ = e;
        if (_profile.ReverseOnExit)
        {
            Start(forward: false);
        }
    }

    private void Activate()
    {
        _activationCount++;
        if (_activationCount == 1 && !_profile.AnimateOnFirstActivation)
        {
            CompositionMotionRunner.Apply(
                _target,
                _profile,
                useFromValues: false);
            return;
        }

        Start(forward: true);
    }

    private void Start(bool forward)
    {
        if (!FAUISettings.AreAnimationsEnabled())
        {
            CompositionMotionRunner.Apply(
                _target,
                _profile,
                useFromValues: !forward);
            return;
        }

        if (CompositionMotionRunner.Play(_target, _profile, forward))
        {
            StartCount++;
        }
    }
}
