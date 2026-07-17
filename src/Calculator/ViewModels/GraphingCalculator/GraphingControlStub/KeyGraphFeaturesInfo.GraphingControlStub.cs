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
    internal sealed partial class KeyGraphFeaturesInfo : DependencyObject
    {
        // Dependency Properties
        public static readonly DependencyProperty XInterceptProperty = DependencyProperty.Register(nameof(XIntercept), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty YInterceptProperty = DependencyProperty.Register(nameof(YIntercept), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ParityProperty = DependencyProperty.Register(nameof(Parity), typeof(int), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(0));
        public static readonly DependencyProperty PeriodicityDirectionProperty = DependencyProperty.Register(nameof(PeriodicityDirection), typeof(int), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(0));
        public static readonly DependencyProperty PeriodicityExpressionProperty = DependencyProperty.Register(nameof(PeriodicityExpression), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty MinimaProperty = DependencyProperty.Register(nameof(Minima), typeof(IList<string>), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(new ObservableCollection<string>()));
        public static readonly DependencyProperty MaximaProperty = DependencyProperty.Register(nameof(Maxima), typeof(IList<string>), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(new ObservableCollection<string>()));
        public static readonly DependencyProperty DomainProperty = DependencyProperty.Register(nameof(Domain), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty RangeProperty = DependencyProperty.Register(nameof(Range), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty InflectionPointsProperty = DependencyProperty.Register(nameof(InflectionPoints), typeof(IList<string>), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(new ObservableCollection<string>()));
        public static readonly DependencyProperty MonotonicityProperty = DependencyProperty.Register(nameof(Monotonicity), typeof(IDictionary<string, string>), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(new Dictionary<string, string>()));
        public static readonly DependencyProperty VerticalAsymptotesProperty = DependencyProperty.Register(nameof(VerticalAsymptotes), typeof(IList<string>), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(new ObservableCollection<string>()));
        public static readonly DependencyProperty HorizontalAsymptotesProperty = DependencyProperty.Register(nameof(HorizontalAsymptotes), typeof(IList<string>), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(new ObservableCollection<string>()));
        public static readonly DependencyProperty ObliqueAsymptotesProperty = DependencyProperty.Register(nameof(ObliqueAsymptotes), typeof(IList<string>), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(new ObservableCollection<string>()));
        public static readonly DependencyProperty TooComplexFeaturesProperty = DependencyProperty.Register(nameof(TooComplexFeatures), typeof(KeyGraphFeaturesFlag), typeof(KeyGraphFeaturesInfo), new PropertyMetadata((KeyGraphFeaturesFlag)0));
        public static readonly DependencyProperty AnalysisErrorProperty = DependencyProperty.Register(nameof(AnalysisError), typeof(AnalysisErrorType), typeof(KeyGraphFeaturesInfo), new PropertyMetadata((AnalysisErrorType)0));
        // Property wrappers
        public string XIntercept { get => (string)GetValue(XInterceptProperty); private set => SetValue(XInterceptProperty, value); }
        public string YIntercept { get => (string)GetValue(YInterceptProperty); private set => SetValue(YInterceptProperty, value); }
        public int Parity { get => (int)GetValue(ParityProperty); private set => SetValue(ParityProperty, value); }
        public int PeriodicityDirection { get => (int)GetValue(PeriodicityDirectionProperty); private set => SetValue(PeriodicityDirectionProperty, value); }
        public string PeriodicityExpression { get => (string)GetValue(PeriodicityExpressionProperty); private set => SetValue(PeriodicityExpressionProperty, value); }
        public IList<string> Minima { get => (IList<string>)GetValue(MinimaProperty); private set => SetValue(MinimaProperty, value); }
        public IList<string> Maxima { get => (IList<string>)GetValue(MaximaProperty); private set => SetValue(MaximaProperty, value); }
        public string Domain { get => (string)GetValue(DomainProperty); private set => SetValue(DomainProperty, value); }
        public string Range { get => (string)GetValue(RangeProperty); private set => SetValue(RangeProperty, value); }
        public IList<string> InflectionPoints { get => (IList<string>)GetValue(InflectionPointsProperty); private set => SetValue(InflectionPointsProperty, value); }
        public IDictionary<string, string> Monotonicity { get => (IDictionary<string, string>)GetValue(MonotonicityProperty); private set => SetValue(MonotonicityProperty, value); }
        public IList<string> VerticalAsymptotes { get => (IList<string>)GetValue(VerticalAsymptotesProperty); private set => SetValue(VerticalAsymptotesProperty, value); }
        public IList<string> HorizontalAsymptotes { get => (IList<string>)GetValue(HorizontalAsymptotesProperty); private set => SetValue(HorizontalAsymptotesProperty, value); }
        public IList<string> ObliqueAsymptotes { get => (IList<string>)GetValue(ObliqueAsymptotesProperty); private set => SetValue(ObliqueAsymptotesProperty, value); }
        public KeyGraphFeaturesFlag TooComplexFeatures { get => (KeyGraphFeaturesFlag)GetValue(TooComplexFeaturesProperty); private set => SetValue(TooComplexFeaturesProperty, value); }
        public AnalysisErrorType AnalysisError { get => (AnalysisErrorType)GetValue(AnalysisErrorProperty); private set => SetValue(AnalysisErrorProperty, value); }

        public static KeyGraphFeaturesInfo Create(Graphing.IGraphFunctionAnalysisData data)
        {
            var result = new KeyGraphFeaturesInfo
            {
                XIntercept = data.Zeros,
                YIntercept = data.YIntercept,
                Domain = data.Domain,
                Range = data.Range,
                Parity = data.Parity,
                PeriodicityDirection = data.PeriodicityDirection,
                PeriodicityExpression = data.PeriodicityExpression,
                Minima = new ObservableCollection<string>(data.Minima),
                Maxima = new ObservableCollection<string>(data.Maxima),
                InflectionPoints = new ObservableCollection<string>(data.InflectionPoints),
                Monotonicity = ConvertIntDictToStringDict(data.MonotoneIntervals),
                VerticalAsymptotes = new ObservableCollection<string>(data.VerticalAsymptotes),
                HorizontalAsymptotes = new ObservableCollection<string>(data.HorizontalAsymptotes),
                ObliqueAsymptotes = new ObservableCollection<string>(data.ObliqueAsymptotes),
                TooComplexFeatures = data.TooComplexFeatures,
                AnalysisError = 0 // No error
            };
            // Log analysis performance
            // TraceLogger.Instance.LogFunctionAnalysisPerformed(0, result.TooComplexFeatures);
            return result;
        }

        public static KeyGraphFeaturesInfo Create(AnalysisErrorType analysisErrorType)
        {
            var result = new KeyGraphFeaturesInfo
            {
                Minima = new ObservableCollection<string>(),
                Maxima = new ObservableCollection<string>(),
                InflectionPoints = new ObservableCollection<string>(),
                Monotonicity = new Dictionary<string, string>(),
                VerticalAsymptotes = new ObservableCollection<string>(),
                HorizontalAsymptotes = new ObservableCollection<string>(),
                ObliqueAsymptotes = new ObservableCollection<string>(),
                AnalysisError = analysisErrorType
            };
            // Log analysis performance
            // TraceLogger.Instance.LogFunctionAnalysisPerformed(analysisErrorType, 0);
            return result;
        }

        private static Dictionary<string, string> ConvertIntDictToStringDict(Dictionary<string, int> inMap)
        {
            var outMap = new Dictionary<string, string>();
            foreach (var kvp in inMap)
            {
                outMap[kvp.Key] = kvp.Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
            }

            return outMap;
        }
    }
}
