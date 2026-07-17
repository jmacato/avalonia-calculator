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
    internal struct IGraphFunctionAnalysisData : IEquatable<IGraphFunctionAnalysisData>
    {
        public string Domain { get; set; }
        public string Range { get; set; }
        public int Parity { get; set; }
        public int PeriodicityDirection { get; set; }
        public string PeriodicityExpression { get; set; }
        public string Zeros { get; set; }
        public string YIntercept { get; set; }
        public List<string> Minima { get; set; }
        public List<string> Maxima { get; set; }
        public List<string> InflectionPoints { get; set; }
        public List<string> VerticalAsymptotes { get; set; }
        public List<string> HorizontalAsymptotes { get; set; }
        public List<string> ObliqueAsymptotes { get; set; }
        public Dictionary<string, int> MonotoneIntervals { get; set; }
        public KeyGraphFeaturesFlag TooComplexFeatures { get; set; }

        public override bool Equals(object? obj)
        {
            return obj is IGraphFunctionAnalysisData other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Domain, StringComparer.Ordinal);
            hash.Add(Range, StringComparer.Ordinal);
            hash.Add(Parity);
            hash.Add(PeriodicityDirection);
            hash.Add(PeriodicityExpression, StringComparer.Ordinal);
            hash.Add(Zeros, StringComparer.Ordinal);
            hash.Add(YIntercept, StringComparer.Ordinal);
            hash.Add(Minima);
            hash.Add(Maxima);
            hash.Add(InflectionPoints);
            hash.Add(VerticalAsymptotes);
            hash.Add(HorizontalAsymptotes);
            hash.Add(ObliqueAsymptotes);
            hash.Add(MonotoneIntervals);
            hash.Add(TooComplexFeatures);
            return hash.ToHashCode();
        }

        public static bool operator ==(IGraphFunctionAnalysisData left, IGraphFunctionAnalysisData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(IGraphFunctionAnalysisData left, IGraphFunctionAnalysisData right)
        {
            return !(left == right);
        }

        public bool Equals(IGraphFunctionAnalysisData other)
        {
            return string.Equals(Domain, other.Domain, StringComparison.Ordinal)
                && string.Equals(Range, other.Range, StringComparison.Ordinal)
                && Parity == other.Parity
                && PeriodicityDirection == other.PeriodicityDirection
                && string.Equals(PeriodicityExpression, other.PeriodicityExpression, StringComparison.Ordinal)
                && string.Equals(Zeros, other.Zeros, StringComparison.Ordinal)
                && string.Equals(YIntercept, other.YIntercept, StringComparison.Ordinal)
                && EqualityComparer<List<string>>.Default.Equals(Minima, other.Minima)
                && EqualityComparer<List<string>>.Default.Equals(Maxima, other.Maxima)
                && EqualityComparer<List<string>>.Default.Equals(InflectionPoints, other.InflectionPoints)
                && EqualityComparer<List<string>>.Default.Equals(VerticalAsymptotes, other.VerticalAsymptotes)
                && EqualityComparer<List<string>>.Default.Equals(HorizontalAsymptotes, other.HorizontalAsymptotes)
                && EqualityComparer<List<string>>.Default.Equals(ObliqueAsymptotes, other.ObliqueAsymptotes)
                && EqualityComparer<Dictionary<string, int>>.Default.Equals(MonotoneIntervals, other.MonotoneIntervals)
                && TooComplexFeatures == other.TooComplexFeatures;
        }
    }
}
