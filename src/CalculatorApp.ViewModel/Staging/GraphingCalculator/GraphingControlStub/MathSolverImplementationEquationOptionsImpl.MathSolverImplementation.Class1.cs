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
    internal sealed class MathSolverImplementationEquationOptionsImpl : IEquationOptions
    {
        public Color GetGraphColor() => new Color();
        public void SetGraphColor(Color color)
        {
        }

        public void ResetGraphColor()
        {
        }

        public LineStyle GetLineStyle() => LineStyle.Solid;
        public void SetLineStyle(LineStyle value)
        {
        }

        public void ResetLineStyle()
        {
        }

        public float GetLineWidth() => 2.0f;
        public void SetLineWidth(float value)
        {
        }

        public void ResetLineWidth()
        {
        }

        public float GetSelectedEquationLineWidth() => 3.0f;
        public void SetSelectedEquationLineWidth(float value)
        {
        }

        public void ResetSelectedEquationLineWidth()
        {
        }

        public float GetPointRadius() => 3.0f;
        public void SetPointRadius(float value)
        {
        }

        public void ResetPointRadius()
        {
        }

        public float GetSelectedEquationPointRadius() => 4.0f;
        public void SetSelectedEquationPointRadius(float value)
        {
        }

        public void ResetSelectedEquationPointRadius()
        {
        }
    }
}
