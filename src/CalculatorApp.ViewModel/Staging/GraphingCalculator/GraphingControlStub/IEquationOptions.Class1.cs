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
    public interface IEquationOptions
    {
        Color GetGraphColor();
        void SetGraphColor(Color color);
        void ResetGraphColor();
        LineStyle GetLineStyle();
        void SetLineStyle(LineStyle value);
        void ResetLineStyle();
        float GetLineWidth();
        void SetLineWidth(float value);
        void ResetLineWidth();
        float GetSelectedEquationLineWidth();
        void SetSelectedEquationLineWidth(float value);
        void ResetSelectedEquationLineWidth();
        float GetPointRadius();
        void SetPointRadius(float value);
        void ResetPointRadius();
        float GetSelectedEquationPointRadius();
        void SetSelectedEquationPointRadius(float value);
        void ResetSelectedEquationPointRadius();
    }
}
