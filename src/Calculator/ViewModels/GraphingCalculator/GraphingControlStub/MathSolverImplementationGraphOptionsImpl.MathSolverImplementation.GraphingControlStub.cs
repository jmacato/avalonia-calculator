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
    internal sealed class MathSolverImplementationGraphOptionsImpl : IGraphingOptions
    {
        public bool MarkZeros { get; set; } = true;
        public bool MarkYIntercept { get; set; } = true;
        public bool MarkMinima { get; set; } = true;
        public bool MarkMaxima { get; set; } = true;
        public bool MarkInflectionPoints { get; set; } = true;
        public bool MarkVerticalAsymptotes { get; set; } = true;
        public bool MarkHorizontalAsymptotes { get; set; } = true;
        public bool MarkObliqueAsymptotes { get; set; } = true;
        public ulong MaxExecutionTime { get; set; } = 10000;

        public void ResetMaxExecutionTime()
        {
        }

        public IReadOnlyList<Color> GetGraphColors() => new List<Color>();
        public bool SetGraphColors(IReadOnlyList<Color> colors) => true;
        public void ResetGraphColors()
        {
        }

        public Color BackColor { get; set; }

        public void ResetBackColor()
        {
        }

        public bool AllowKeyGraphFeaturesForFunctionsWithParameters { get; set; } = true;

        public void ResetAllowKeyGraphFeaturesForFunctionsWithParameters()
        {
        }

        public Color ZerosColor { get; set; }

        public void ResetZerosColor()
        {
        }

        public Color ExtremaColor { get; set; }

        public void ResetExtremaColor()
        {
        }

        public Color InflectionPointsColor { get; set; }

        public void ResetInflectionPointsColor()
        {
        }

        public Color AsymptotesColor { get; set; }

        public void ResetAsymptotesColor()
        {
        }

        public Color AxisColor { get; set; }

        public void ResetAxisColor()
        {
        }

        public Color BoxColor { get; set; }

        public void ResetBoxColor()
        {
        }

        public Color GridColor { get; set; }

        public void ResetGridColor()
        {
        }

        public Color FontColor { get; set; }

        public void ResetFontColor()
        {
        }

        public bool ShowAxis { get; set; } = true;

        public void ResetShowAxis()
        {
        }

        public bool ShowGrid { get; set; } = true;

        public void ResetShowGrid()
        {
        }

        public bool ShowBox { get; set; } = true;

        public void ResetShowBox()
        {
        }

        public bool ForceProportional { get; set; } = true;

        public void ResetForceProportional()
        {
        }

        public string AliasX { get; set; } = "x";

        public void ResetAliasX()
        {
        }

        public string AliasY { get; set; } = "y";

        public void ResetAliasY()
        {
        }

        public LineStyle LineStyle { get; set; } = LineStyle.Solid;

        public void ResetLineStyle()
        {
        }

        public Tuple<double, double> GetDefaultXRange() => new Tuple<double, double>(-10, 10);
        public bool SetDefaultXRange(Tuple<double, double> minmax) => true;
        public void ResetDefaultXRange()
        {
        }

        public Tuple<double, double> GetDefaultYRange() => new Tuple<double, double>(-10, 10);
        public bool SetDefaultYRange(Tuple<double, double> minmax) => true;
        public void ResetDefaultYRange()
        {
        }
    }
}
