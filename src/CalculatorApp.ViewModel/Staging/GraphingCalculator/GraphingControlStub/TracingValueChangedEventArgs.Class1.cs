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

namespace GraphControl
{
    public sealed class TracingValueChangedEventArgs : EventArgs
    {
        public TracingValueChangedEventArgs(double xPointValue, double yPointValue)
        {
            XPointValue = xPointValue;
            YPointValue = yPointValue;
        }

        public double XPointValue { get; }
        public double YPointValue { get; }
    }
}
