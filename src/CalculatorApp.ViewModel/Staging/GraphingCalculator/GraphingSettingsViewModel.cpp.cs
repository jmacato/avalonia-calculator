// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include "pch.h"
// #include "GraphingSettingsViewModel.cpp.h"

using System.Globalization;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;
using GraphControl;
using Windows.UI.Xaml;

namespace CalculatorApp.ViewModel
{
    public sealed partial class GraphingSettingsViewModel
    {
        public GraphingSettingsViewModel()
        {
            m_XMinValue = 0;
            m_XMaxValue = 0;
            m_YMinValue = 0;
            m_YMaxValue = 0;
            m_XMinError = false;
            m_XMaxError = false;
            m_YMinError = false;
            m_YMaxError = false;
            m_dontUpdateDisplayRange = false;
        }

        public void SetGrapher(Grapher grapher)
        {
            if (grapher != null)
            {
                if (grapher.TrigUnitMode == (int)Graphing.EvalTrigUnitMode.Invalid)
                {
                    grapher.TrigUnitMode = (int)Graphing.EvalTrigUnitMode.Radians;
                }
            }
            Graph = grapher;
            InitRanges();
            RaisePropertyChanged(nameof(TrigUnit));
        }

        public void InitRanges()
        {
            double xMin = 0, xMax = 0, yMin = 0, yMax = 0;
            if (m_Graph != null)
            {
                m_Graph.GetDisplayRanges(out xMin, out xMax, out yMin, out yMax);
            }
            m_dontUpdateDisplayRange = true;
            m_XMinValue = xMin;
            m_XMaxValue = xMax;
            m_YMinValue = yMin;
            m_YMaxValue = yMax;

            XMin = m_XMinValue.ToString(CultureInfo.CurrentCulture);
            XMax = m_XMaxValue.ToString(CultureInfo.CurrentCulture);
            YMin = m_YMinValue.ToString(CultureInfo.CurrentCulture);
            YMax = m_YMaxValue.ToString(CultureInfo.CurrentCulture);

            m_dontUpdateDisplayRange = false;
        }

        public void ResetView()
        {
            if (m_Graph != null)
            {
                m_Graph.ResetGrid();
                InitRanges();
                m_XMinError = false;
                m_XMaxError = false;
                m_YMinError = false;
                m_YMaxError = false;
                RaisePropertyChanged(nameof(XError));
                RaisePropertyChanged(nameof(XMin));
                RaisePropertyChanged(nameof(XMax));
                RaisePropertyChanged(nameof(YError));
                RaisePropertyChanged(nameof(YMin));
                RaisePropertyChanged(nameof(YMax));
            }
        }

        public void UpdateDisplayRange()
        {
            if (m_Graph == null || m_dontUpdateDisplayRange || HasError())
            {
                return;
            }
            m_Graph.SetDisplayRanges(m_XMinValue, m_XMaxValue, m_YMinValue, m_YMaxValue);
            CalculatorApp.ViewModel.Common.TraceLogger.LogGraphSettingsChanged(CalculatorApp.ViewModel.Common.GraphSettingsType.Grid, "");
        }

        public bool HasError()
        {
            return m_XMinError || m_YMinError || m_XMaxError || m_YMaxError || XError || YError;
        }
    }
}
