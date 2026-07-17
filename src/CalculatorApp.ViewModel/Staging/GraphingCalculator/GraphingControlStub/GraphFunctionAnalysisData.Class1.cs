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
    public sealed class GraphFunctionAnalysisData
    {
        public string Domain { get; set; } = string.Empty;
        public string Range { get; set; } = string.Empty;
        public int Parity { get; set; }
        public int PeriodicityDirection { get; set; }
        public string PeriodicityExpression { get; set; } = string.Empty;
        public string Zeros { get; set; } = string.Empty;
        public string YIntercept { get; set; } = string.Empty;
        public Collection<string> Minima { get; } = new();
        public Collection<string> Maxima { get; } = new();
        public Collection<string> InflectionPoints { get; } = new();
        public Collection<string> VerticalAsymptotes { get; } = new();
        public Collection<string> HorizontalAsymptotes { get; } = new();
        public Collection<string> ObliqueAsymptotes { get; } = new();
        public Dictionary<string, int> MonotoneIntervals { get; } = new();
        public KeyGraphFeatures TooComplexFeatures { get; set; }
    }
}
