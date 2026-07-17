using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.Foundation.Collections;
using Windows.UI;
using GraphControl;

namespace CalculatorApp.ViewModel
{
    public partial class EquationViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        //public EquationViewModel(GraphControl.Equation equation, int functionLabelIndex, Color color, int colorIndex);
        private Equation m_GraphEquation;
        public Equation GraphEquation
        {
            get
            {
                return m_GraphEquation;
            }

            private set
            {
                m_GraphEquation = value;
            }
        }

        private int m_FunctionLabelIndex;
        public int FunctionLabelIndex
        {
            get
            {
                return m_FunctionLabelIndex;
            }

            set
            {
                if (m_FunctionLabelIndex != value)
                {
                    m_FunctionLabelIndex = value;
                    RaisePropertyChanged(nameof(FunctionLabelIndex));
                }
            }
        }

        private bool m_IsLastItemInList;
        public bool IsLastItemInList
        {
            get
            {
                return m_IsLastItemInList;
            }

            set
            {
                if (m_IsLastItemInList != value)
                {
                    m_IsLastItemInList = value;
                    RaisePropertyChanged(nameof(IsLastItemInList));
                }
            }
        }

        private int m_LineColorIndex;
        public int LineColorIndex
        {
            get
            {
                return m_LineColorIndex;
            }

            set
            {
                m_LineColorIndex = value;
            }
        }

        public string Expression
        {
            get
            {
                return GraphEquation.Expression;
            }

            set
            {
                if (GraphEquation.Expression != value)
                {
                    GraphEquation.Expression = value;
                    RaisePropertyChanged(nameof(Expression));
                }
            }
        }

        public Color LineColor
        {
            get
            {
                return GraphEquation.LineColor;
            }

            set
            {
                if (!(GraphEquation.LineColor == value))
                {
                    GraphEquation.LineColor = value;
                    RaisePropertyChanged(nameof(LineColor));
                }
            }
        }

        public bool IsLineEnabled
        {
            get
            {
                return GraphEquation.IsLineEnabled;
            }

            set
            {
                if (GraphEquation.IsLineEnabled != value)
                {
                    GraphEquation.IsLineEnabled = value;
                    RaisePropertyChanged(nameof(IsLineEnabled));
                }
            }
        }

        private string m_AnalysisErrorString = string.Empty;
        public string AnalysisErrorString
        {
            get
            {
                return m_AnalysisErrorString;
            }

            private set
            {
                m_AnalysisErrorString = value;
            }
        }

        private bool m_AnalysisErrorVisible;
        public bool AnalysisErrorVisible
        {
            get
            {
                return m_AnalysisErrorVisible;
            }

            private set
            {
                m_AnalysisErrorVisible = value;
            }
        }

        private ObservableCollection<KeyGraphFeaturesItem> m_KeyGraphFeaturesItems;
        public ObservableCollection<KeyGraphFeaturesItem> KeyGraphFeaturesItems
        {
            get
            {
                return m_KeyGraphFeaturesItems;
            }

            private set
            {
                m_KeyGraphFeaturesItems = value;
            }
        }

        //public void PopulateKeyGraphFeatures(GraphControl.KeyGraphFeaturesInfo info);
        //public static string EquationErrorText(GraphControl.ErrorType errorType, int errorCode);
        internal void RaisePropertyChanged(string p)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }

        //private void AddKeyGraphFeature(string title, string expression, string errorString);
        //private void AddKeyGraphFeature(
        //    string title,
        //    IList<string> expressionVector,
        //    string errorString);
        //private void AddParityKeyGraphFeature(GraphControl.KeyGraphFeaturesInfo info);
        //private void AddPeriodicityKeyGraphFeature(GraphControl.KeyGraphFeaturesInfo info);
        //private void AddMonotoncityKeyGraphFeature(GraphControl.KeyGraphFeaturesInfo info);
        //private void AddTooComplexKeyGraphFeature(GraphControl.KeyGraphFeaturesInfo info);
        private Windows.ApplicationModel.Resources.ResourceLoader m_resourceLoader;
    }
}
