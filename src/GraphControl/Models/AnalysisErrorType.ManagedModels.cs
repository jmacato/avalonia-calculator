using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Graphing;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

public enum AnalysisErrorType
{
    NoError = 0,
    AnalysisCouldNotBePerformed = 1,
    AnalysisNotSupported = 2,
    VariableIsNotX = 3
}
