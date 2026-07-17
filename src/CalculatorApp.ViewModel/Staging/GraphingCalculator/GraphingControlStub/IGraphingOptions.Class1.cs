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
    public interface IGraphingOptions
    {
        bool MarkZeros { get; set; }

        bool MarkYIntercept { get; set; }

        bool MarkMinima { get; set; }

        bool MarkMaxima { get; set; }

        bool MarkInflectionPoints { get; set; }

        bool MarkVerticalAsymptotes { get; set; }

        bool MarkHorizontalAsymptotes { get; set; }

        bool MarkObliqueAsymptotes { get; set; }

        ulong MaxExecutionTime { get; set; }

        void ResetMaxExecutionTime();
        IReadOnlyList<Color> GetGraphColors();
        bool SetGraphColors(IReadOnlyList<Color> colors);
        void ResetGraphColors();
        Color BackColor { get; set; }

        void ResetBackColor();
        bool AllowKeyGraphFeaturesForFunctionsWithParameters { get; set; }

        void ResetAllowKeyGraphFeaturesForFunctionsWithParameters();
        Color ZerosColor { get; set; }

        void ResetZerosColor();
        Color ExtremaColor { get; set; }

        void ResetExtremaColor();
        Color InflectionPointsColor { get; set; }

        void ResetInflectionPointsColor();
        Color AsymptotesColor { get; set; }

        void ResetAsymptotesColor();
        Color AxisColor { get; set; }

        void ResetAxisColor();
        Color BoxColor { get; set; }

        void ResetBoxColor();
        Color GridColor { get; set; }

        void ResetGridColor();
        Color FontColor { get; set; }

        void ResetFontColor();
        bool ShowAxis { get; set; }

        void ResetShowAxis();
        bool ShowGrid { get; set; }

        void ResetShowGrid();
        bool ShowBox { get; set; }

        void ResetShowBox();
        bool ForceProportional { get; set; }

        void ResetForceProportional();
        string AliasX { get; set; }

        void ResetAliasX();
        string AliasY { get; set; }

        void ResetAliasY();
        LineStyle LineStyle { get; set; }

        void ResetLineStyle();
        Tuple<double, double> GetDefaultXRange();
        bool SetDefaultXRange(Tuple<double, double> minmax);
        void ResetDefaultXRange();
        Tuple<double, double> GetDefaultYRange();
        bool SetDefaultYRange(Tuple<double, double> minmax);
        void ResetDefaultYRange();
    }
}
