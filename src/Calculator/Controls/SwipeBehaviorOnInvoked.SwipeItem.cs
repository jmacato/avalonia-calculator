// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Direct managed port of microsoft-ui-xaml's SwipeItem API and invocation
// behavior from SwipeControl.idl and SwipeItem.cpp at
// commit 3cae15f071f1ab8565f9a7592dbf27f04bafe651.
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Input;

namespace CalculatorApp.Controls;

public enum SwipeBehaviorOnInvoked
{
    Auto,
    Close,
    RemainOpen,
}
