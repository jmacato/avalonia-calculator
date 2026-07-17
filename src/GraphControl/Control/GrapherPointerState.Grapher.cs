using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Buffers;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Graphing;
using Graphing.Analyzer;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

internal readonly record struct GrapherPointerState(Point Current, Point Applied);
