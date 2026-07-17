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

namespace GraphControl
{
    internal sealed class EquationCollection : ObservableCollection<Equation>
    {
        public event Action<Equation>? EquationChanged;
        public event Action<Equation>? EquationStyleChanged;
        public event Action<Equation>? EquationLineEnabledChanged;
        public EquationCollection()
        {
        }

        protected override void InsertItem(int index, Equation item)
        {
            ArgumentNullException.ThrowIfNull(item);
            item.PropertyChanged += OnEquationPropertyChanged;
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            var item = this[index];
            if (item != null)
            {
                item.PropertyChanged -= OnEquationPropertyChanged;
            }

            base.RemoveItem(index);
        }

        protected override void SetItem(int index, Equation item)
        {
            ArgumentNullException.ThrowIfNull(item);
            var oldItem = this[index];
            if (oldItem != null)
            {
                oldItem.PropertyChanged -= OnEquationPropertyChanged;
            }

            item.PropertyChanged += OnEquationPropertyChanged;
            base.SetItem(index, item);
        }

        protected override void ClearItems()
        {
            foreach (var item in this)
            {
                item.PropertyChanged -= OnEquationPropertyChanged;
            }

            base.ClearItems();
        }

        private void OnEquationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var equation = sender as Equation;
            if (equation == null)
                return;
            switch (e.PropertyName)
            {
                case nameof(Equation.LineColor):
                case nameof(Equation.IsSelected):
                case nameof(Equation.EquationStyle):
                    EquationStyleChanged?.Invoke(equation);
                    break;
                case nameof(Equation.Expression):
                    EquationChanged?.Invoke(equation);
                    break;
                case nameof(Equation.IsLineEnabled):
                    EquationLineEnabledChanged?.Invoke(equation);
                    break;
            }
        }
    }
}
