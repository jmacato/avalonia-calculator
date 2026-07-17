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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.System;
using Windows.Storage.Streams;
using Windows.System;
using Graphing;
using GraphControl;
using Microsoft.UI;
using Microsoft.UI.Windowing;

namespace GraphControl
{
    [Flags]
    internal enum KeyGraphFeaturesFlag
    {
        Domain = 1,
        Range = 2,
        Parity = 4,
        Periodicity = 8,
        Zeros = 16,
        YIntercept = 32,
        Minima = 64,
        Maxima = 128,
        InflectionPoints = 256,
        VerticalAsymptotes = 512,
        HorizontalAsymptotes = 1024,
        ObliqueAsymptotes = 2048,
        MonotoneIntervals = 4096
    };
}
