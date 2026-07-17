// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Direct managed/Avalonia port of microsoft-ui-xaml's SwipeControl API,
// state machine, thresholds, content construction, clipping, colors, and
// dismissal behavior from controls/dev/SwipeControl at commit
// 3cae15f071f1ab8565f9a7592dbf27f04bafe651. Avalonia pointer tracking and
// render transforms replace WinUI's InteractionTracker/Composition plumbing.
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.Core;
using FluentAvalonia.UI.Controls;

namespace CalculatorApp.Controls;

internal enum SwipeControlCreatedContent
{
    Left,
    Top,
    Bottom,
    Right,
    None,
}
