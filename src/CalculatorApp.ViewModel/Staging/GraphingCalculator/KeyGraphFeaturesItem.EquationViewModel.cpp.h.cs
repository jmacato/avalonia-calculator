using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.Foundation.Collections;
using Windows.UI;
using GraphControl;

namespace CalculatorApp.ViewModel
{
    public partial class KeyGraphFeaturesItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        //public KeyGraphFeaturesItem();
        private string m_Title = string.Empty;
        public string Title
        {
            get
            {
                return m_Title;
            }

            set
            {
                if (m_Title != value)
                {
                    m_Title = value;
                    RaisePropertyChanged(nameof(Title));
                }
            }
        }

        public ObservableCollection<string> DisplayItems { get; } = new();

        public ObservableCollection<GridDisplayItems> GridItems { get; } = new();

        private bool m_IsText;
        public bool IsText
        {
            get
            {
                return m_IsText;
            }

            set
            {
                if (m_IsText != value)
                {
                    m_IsText = value;
                    RaisePropertyChanged(nameof(IsText));
                }
            }
        }

        internal void RaisePropertyChanged(string p)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }
    }
}
