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

namespace GraphControl
{
    public sealed class EquationCollection : ObservableCollection<Equation>
    {
        public event EventHandler<EquationChangedEventArgs>? EquationChanged;
        public event EventHandler<EquationChangedEventArgs>? EquationStyleChanged;
        public event EventHandler<EquationChangedEventArgs>? EquationLineEnabledChanged;
        public EquationCollection()
        {
        }

        protected override void InsertItem(int index, Equation item)
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

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
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

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

        private void OnEquationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var equation = sender as Equation;
            if (equation == null)
                return;
            switch (e.PropertyName)
            {
                case nameof(Equation.LineColor):
                case nameof(Equation.IsSelected):
                case nameof(Equation.EquationStyle):
                    EquationStyleChanged?.Invoke(this, new EquationChangedEventArgs(equation));
                    break;
                case nameof(Equation.Expression):
                    EquationChanged?.Invoke(this, new EquationChangedEventArgs(equation));
                    break;
                case nameof(Equation.IsLineEnabled):
                    EquationLineEnabledChanged?.Invoke(this, new EquationChangedEventArgs(equation));
                    break;
            }
        }
    }
}
