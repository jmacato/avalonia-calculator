using System;
using System.ComponentModel;
using Microsoft.UI.Xaml.Data;
using GraphControl;

namespace CalculatorApp.ViewModel
{
    [Microsoft.UI.Xaml.Data.Bindable]
    public sealed partial class GraphingSettingsViewModel : INotifyPropertyChanged
    {
        // OBSERVABLE_OBJECT() expansion
        public event PropertyChangedEventHandler PropertyChanged;

        internal void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // OBSERVABLE_PROPERTY_R(bool, YMinError) expansion
        private bool m_YMinError;
        public bool YMinError
        {
            get { return m_YMinError; }
            private set { m_YMinError = value; }
        }

        // OBSERVABLE_PROPERTY_R(bool, XMinError) expansion
        private bool m_XMinError;
        public bool XMinError
        {
            get { return m_XMinError; }
            private set { m_XMinError = value; }
        }

        // OBSERVABLE_PROPERTY_R(bool, XMaxError) expansion
        private bool m_XMaxError;
        public bool XMaxError
        {
            get { return m_XMaxError; }
            private set { m_XMaxError = value; }
        }

        // OBSERVABLE_PROPERTY_R(bool, YMaxError) expansion
        private bool m_YMaxError;
        public bool YMaxError
        {
            get { return m_YMaxError; }
            private set { m_YMaxError = value; }
        }

        // OBSERVABLE_PROPERTY_R(GraphControl.Grapher ^, Graph) expansion
        private Grapher m_Graph;
        public Grapher Graph
        {
            get { return m_Graph; }
            private set { m_Graph = value; }
        }

 
        public bool XError
        {
            get
            {
                return !m_XMinError && !m_XMaxError && m_XMinValue >= m_XMaxValue;
            }
        }

        public bool YError
        {
            get
            {
                return !m_YMinError && !m_YMaxError && m_YMinValue >= m_YMaxValue;
            }
        }

        private string m_XMin;
        public string XMin
        {
            get
            {
                return m_XMin;
            }
            set
            {
                if (m_XMin == value)
                {
                    return;
                }
                m_XMin = value;
                m_XIsMinLastChanged = true;
                if (m_Graph != null)
                {
                    double number;
                    if (double.TryParse(value, out number))
                    {
                        m_Graph.XAxisMin = m_XMinValue = number;
                        XMinError = false;
                    }
                    else
                    {
                        XMinError = true;
                    }
                }
                RaisePropertyChanged(nameof(XError));
                RaisePropertyChanged(nameof(XMin));
                UpdateDisplayRange();
            }
        }

        private string m_XMax;
        public string XMax
        {
            get
            {
                return m_XMax;
            }
            set
            {
                if (m_XMax == value)
                {
                    return;
                }
                m_XMax = value;
                m_XIsMinLastChanged = false;
                if (m_Graph != null)
                {
                    double number;
                    if (double.TryParse(value, out number))
                    {
                        m_Graph.XAxisMax = m_XMaxValue = number;
                        XMaxError = false;
                    }
                    else
                    {
                        XMaxError = true;
                    }
                }
                RaisePropertyChanged(nameof(XError));
                RaisePropertyChanged(nameof(XMax));
                UpdateDisplayRange();
            }
        }

        private string m_YMin;
        public string YMin
        {
            get
            {
                return m_YMin;
            }
            set
            {
                if (m_YMin == value)
                {
                    return;
                }
                m_YMin = value;
                m_YIsMinLastChanged = true;
                if (m_Graph != null)
                {
                    double number;
                    if (double.TryParse(value, out number))
                    {
                        m_Graph.YAxisMin = m_YMinValue = number;
                        YMinError = false;
                    }
                    else
                    {
                        YMinError = true;
                    }
                }
                RaisePropertyChanged(nameof(YError));
                RaisePropertyChanged(nameof(YMin));
                UpdateDisplayRange();
            }
        }

        private string m_YMax;
        public string YMax
        {
            get
            {
                return m_YMax;
            }
            set
            {
                if (m_YMax == value)
                {
                    return;
                }
                m_YMax = value;
                m_YIsMinLastChanged = false;
                if (m_Graph != null)
                {
                    double number;
                    if (double.TryParse(value, out number))
                    {
                        m_Graph.YAxisMax = m_YMaxValue = number;
                        YMaxError = false;
                    }
                    else
                    {
                        YMaxError = true;
                    }
                }
                RaisePropertyChanged(nameof(YError));
                RaisePropertyChanged(nameof(YMax));
                UpdateDisplayRange();
            }
        }

        public int TrigUnit
        {
            get
            {
                return m_Graph == null ? (int)Graphing.EvalTrigUnitMode.Invalid : m_Graph.TrigUnitMode;
            }
            set
            {
                if (m_Graph == null)
                {
                    return;
                }
                m_Graph.TrigUnitMode = value;
                RaisePropertyChanged(nameof(TrigUnit));
            }
        }

        public bool TrigModeRadians
        {
            get
            {
                return m_Graph != null && m_Graph.TrigUnitMode == (int)Graphing.EvalTrigUnitMode.Radians;
            }
            set
            {
                if (value && m_Graph != null && m_Graph.TrigUnitMode != (int)Graphing.EvalTrigUnitMode.Radians)
                {
                    m_Graph.TrigUnitMode = (int)Graphing.EvalTrigUnitMode.Radians;

                    RaisePropertyChanged(nameof(TrigModeRadians));
                    RaisePropertyChanged(nameof(TrigModeDegrees));
                    RaisePropertyChanged(nameof(TrigModeGradians));

                    CalculatorApp.ViewModel.Common.TraceLogger.GetInstance().LogGraphSettingsChanged(CalculatorApp.ViewModel.Common.GraphSettingsType.TrigUnits, "Radians");
                }
            }
        }

        public bool TrigModeDegrees
        {
            get
            {
                return m_Graph != null && m_Graph.TrigUnitMode == (int)Graphing.EvalTrigUnitMode.Degrees;
            }
            set
            {
                if (value && m_Graph != null && m_Graph.TrigUnitMode != (int)Graphing.EvalTrigUnitMode.Degrees)
                {
                    m_Graph.TrigUnitMode = (int)Graphing.EvalTrigUnitMode.Degrees;

                    RaisePropertyChanged(nameof(TrigModeDegrees));
                    RaisePropertyChanged(nameof(TrigModeRadians));
                    RaisePropertyChanged(nameof(TrigModeGradians));

                    CalculatorApp.ViewModel.Common.TraceLogger.GetInstance().LogGraphSettingsChanged(CalculatorApp.ViewModel.Common.GraphSettingsType.TrigUnits, "Degrees");
                }
            }
        }

        public bool TrigModeGradians
        {
            get
            {
                return m_Graph != null && m_Graph.TrigUnitMode == (int)Graphing.EvalTrigUnitMode.Grads;
            }
            set
            {
                if (value && m_Graph != null && m_Graph.TrigUnitMode != (int)Graphing.EvalTrigUnitMode.Grads)
                {
                    m_Graph.TrigUnitMode = (int)Graphing.EvalTrigUnitMode.Grads;

                    RaisePropertyChanged(nameof(TrigModeGradians));
                    RaisePropertyChanged(nameof(TrigModeDegrees));
                    RaisePropertyChanged(nameof(TrigModeRadians));

                    CalculatorApp.ViewModel.Common.TraceLogger.GetInstance().LogGraphSettingsChanged(CalculatorApp.ViewModel.Common.GraphSettingsType.TrigUnits, "Gradians");
                }
            }
        } 

        private double m_XMinValue;
        private double m_XMaxValue;
        private double m_YMinValue;
        private double m_YMaxValue;
        private bool m_dontUpdateDisplayRange;
        private bool m_XIsMinLastChanged;
        private bool m_YIsMinLastChanged;
    }
}
