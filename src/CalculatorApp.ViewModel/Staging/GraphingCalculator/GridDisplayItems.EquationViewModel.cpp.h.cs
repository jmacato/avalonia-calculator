using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.Foundation.Collections;
using Windows.UI;
using GraphControl;

namespace CalculatorApp.ViewModel
{
    public partial class GridDisplayItems : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        //public GridDisplayItems();
        private string m_Expression = string.Empty;
        public string Expression
        {
            get
            {
                return m_Expression;
            }

            set
            {
                if (m_Expression != value)
                {
                    m_Expression = value;
                    RaisePropertyChanged(nameof(Expression));
                }
            }
        }

        private string m_Direction = string.Empty;
        public string Direction
        {
            get
            {
                return m_Direction;
            }

            set
            {
                if (m_Direction != value)
                {
                    m_Direction = value;
                    RaisePropertyChanged(nameof(Direction));
                }
            }
        }

        internal void RaisePropertyChanged(string p)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }
    }
}
