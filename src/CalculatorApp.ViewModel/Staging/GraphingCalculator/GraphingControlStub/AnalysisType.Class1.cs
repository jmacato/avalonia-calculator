using System;
using System.Collections.Generic;
using Windows.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.System;
using Windows.Storage.Streams;
using Graphing;
using GraphControl;

namespace Graphing
{
    public enum AnalysisType
    {
        Domain = 0,
        Range = 1,
        Parity = 2,
        Zeros = 3,
        YIntercept = 4,
        Minima = 5,
        Maxima = 6,
        InflectionPoints = 7,
        VerticalAsymptotes = 8,
        HorizontalAsymptotes = 9,
        ObliqueAsymptotes = 10,
        Monotonicity = 11,
        Period = 12
    }
}
