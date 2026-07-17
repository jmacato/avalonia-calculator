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
    [Flags]
    public enum PerformAnalysisType
    {
        None = 0,
        Domain = 0x01,
        Range = 0x02,
        Parity = 0x04,
        InterceptionPointsWithXAndYAxis = 0x08,
        CriticalPoints = 0x10,
        Asymptotes = 0x20,
        Monotonicity = 0x40,
        Period = 0x80,
        All = 0xFF
    }
}
