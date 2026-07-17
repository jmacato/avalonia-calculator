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
    internal sealed class MathSolverImplementationGraphRendererImpl : IGraphRenderer
    {
        public int SetGraphSize(uint width, uint height) => 0;
        public int SetDpi(float dpiX, float dpiY) => 0;
        public int DrawD2D1(object? d2dFactory, object? renderTarget, out bool hasSomeMissingDataOut)
        {
            hasSomeMissingDataOut = false;
            return 0;
        }

        public int GetClosePointData(double inScreenPointX, double inScreenPointY, double precision, out int formulaIdOut, out float xScreenPointOut, out float yScreenPointOut, out double xValueOut, out double yValueOut, out double rhoValueOut, out double thetaValueOut, out double tValueOut)
        {
            formulaIdOut = 0;
            xScreenPointOut = 0;
            yScreenPointOut = 0;
            xValueOut = 0;
            yValueOut = 0;
            rhoValueOut = 0;
            thetaValueOut = 0;
            tValueOut = 0;
            return 0;
        }

        public int ScaleRange(double centerX, double centerY, double scale) => 0;
        public int ChangeRange(ChangeRangeAction action) => 0;
        public int MoveRangeByRatio(double ratioX, double ratioY) => 0;
        public int ResetRange() => 0;
        public int GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax)
        {
            xMin = -10;
            xMax = 10;
            yMin = -10;
            yMax = 10;
            return 0;
        }

        public int SetDisplayRanges(double xMin, double xMax, double yMin, double yMax) => 0;
        public int PrepareGraph() => 0;
        public int GetBitmap(out IBitmap bitmapOut, out bool hasSomeMissingDataOut)
        {
            bitmapOut = new MathSolverImplementationBitmapImpl();
            hasSomeMissingDataOut = false;
            return 0;
        }
    }
}
