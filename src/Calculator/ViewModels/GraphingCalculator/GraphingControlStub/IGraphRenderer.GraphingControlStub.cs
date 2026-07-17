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
    internal interface IGraphRenderer
    {
        int SetGraphSize(uint width, uint height);
        int SetDpi(float dpiX, float dpiY);
        int DrawD2D1(object? d2dFactory, object? renderTarget, out bool hasSomeMissingDataOut);
        int GetClosePointData(double inScreenPointX, double inScreenPointY, double precision, out int formulaIdOut, out float xScreenPointOut, out float yScreenPointOut, out double xValueOut, out double yValueOut, out double rhoValueOut, out double thetaValueOut, out double tValueOut);
        int ScaleRange(double centerX, double centerY, double scale);
        int ChangeRange(ChangeRangeAction action);
        int MoveRangeByRatio(double ratioX, double ratioY);
        int ResetRange();
        int GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax);
        int SetDisplayRanges(double xMin, double xMax, double yMin, double yMax);
        int PrepareGraph();
        int GetBitmap(out IBitmap bitmapOut, out bool hasSomeMissingDataOut);
    }
}
