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

namespace Graphing
{
    [Flags]
    internal enum PerformAnalysisType
    {
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
