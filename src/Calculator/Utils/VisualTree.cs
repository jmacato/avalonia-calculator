// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation.Collections;



using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;

// Light C++/CX port of Microsoft.Toolkit.Uwp.UI.Extensions.VisualTree from the Windows Community toolkit
// Original version here:
// https://raw.githubusercontent.com/windows-toolkit/WindowsCommunityToolkit/master/Microsoft.Toolkit.Uwp.UI/Extensions/Tree/VisualTree.cs

namespace Calculator.Utils
{
    /// <summary>
    /// Defines a collection of extensions methods for UI.
    /// </summary>
    internal static class VisualTree
    {
        /// <summary>
        /// Find descendant <see cref="Microsoft.UI.Xaml.FrameworkElement ^"/> control using its name.
        /// </summary>
        /// <param name="element">Parent element.</param>
        /// <param name="name">Name of the control to find</param>
        /// <returns>Descendant control or null if not found.</returns>
        internal static FrameworkElement FindDescendantByName(DependencyObject element, string name)
        {
            if (element == null || name == null || name.Length == 0)
            {
                return null;
            }

            if (element is FrameworkElement frameworkElement && name.Equals(frameworkElement.Name))
            {
                return frameworkElement;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(element);
            for (var i = 0; i < childCount; i++)
            {
                var result = FindDescendantByName(VisualTreeHelper.GetChild(element, i), name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Find first descendant control of a specified type.
        /// </summary>
        /// <param name="element">Parent element.</param>
        /// <param name="typeName">Type of descendant.</param>
        /// <returns>Descendant control or null if not found.</returns>
        private static DependencyObject FindDescendant(DependencyObject element, Type typeName)
        {
            DependencyObject retValue = null;
            var childrenCount = VisualTreeHelper.GetChildrenCount(element);

            for (var i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                if (child.GetType() == typeName)
                {
                    retValue = child;
                    break;
                }

                retValue = FindDescendant(child, typeName);

                if (retValue != null)
                {
                    break;
                }
            }

            return retValue;
        }

        /// <summary>
        /// Find visual ascendant <see cref="Microsoft.UI.Xaml.FrameworkElement ^"/> control using its name.
        /// </summary>
        /// <param name="element">Parent element.</param>
        /// <param name="name">Name of the control to find</param>
        /// <returns>Descendant control or null if not found.</returns>
        private static FrameworkElement FindAscendantByName(DependencyObject element, string name)
        {
            if (element == null || name == null || name.Length == 0)
            {
                return null;
            }

            var parent = VisualTreeHelper.GetParent(element);

            if (parent == null)
            {
                return null;
            }

            if (parent is FrameworkElement frameworkElement && name.Equals(frameworkElement.Name))
            {
                return frameworkElement;
            }

            return FindAscendantByName(parent, name);
        }

        /// <summary>
        /// Find first visual ascendant control of a specified type.
        /// </summary>
        /// <param name="element">Child element.</param>
        /// <param name="type">Type of ascendant to look for.</param>
        /// <returns>Ascendant control or null if not found.</returns>
        private static object FindAscendant(DependencyObject element, Type typeName)
        {
            var parent = VisualTreeHelper.GetParent(element);

            if (parent == null)
            {
                return null;
            }

            if (parent.GetType() == typeName)
            {
                return parent;
            }

            return FindAscendant(parent, typeName);
        }
    }
}




namespace ObservableCollectionShim
{ 
    /// <summary>
    /// Adapts an ObservableCollection to implement IObservableVector
    /// </summary>
    public class ObservableCollectionShim<T> : IObservableVector<T>
    {
        private ObservableCollection<T> _adaptee;

        public ObservableCollectionShim(ObservableCollection<T> adaptee)
        {
            _adaptee = adaptee;
            _adaptee.CollectionChanged += Adaptee_CollectionChanged;
        }

        /// <summary>
        /// Handles and adapts CollectionChanged events
        /// </summary>
        private void Adaptee_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            VectorChangedEventArgs args = new VectorChangedEventArgs();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    args.CollectionChange = CollectionChange.ItemInserted;
                    args.Index = (uint)e.NewStartingIndex;
                    break;

                case NotifyCollectionChangedAction.Remove:
                    args.CollectionChange = CollectionChange.ItemRemoved;
                    args.Index = (uint)e.OldStartingIndex;
                    break;
                case NotifyCollectionChangedAction.Replace:
                    args.CollectionChange = CollectionChange.ItemChanged;
                    args.Index = (uint)e.NewStartingIndex;
                    break;
                case NotifyCollectionChangedAction.Reset:
                case NotifyCollectionChangedAction.Move:
                    args.CollectionChange = CollectionChange.Reset;
                    break;
            }
            OnVectorChanged(args);
        }

        #region IObservableVector interface

        public event VectorChangedEventHandler<T> VectorChanged;

        protected void OnVectorChanged(IVectorChangedEventArgs args)
        {
            if (VectorChanged != null)
            {
                VectorChanged(this, args);
            }
        }

        #endregion

        #region IList<T> implementation

        public int IndexOf(T item)
        {
            return _adaptee.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _adaptee.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            _adaptee.RemoveAt(index);
        }

        public T this[int index]
        {
            get
            {
                return _adaptee[index];
            }
            set
            {
                _adaptee[index] = value;
            }
        }

        public void Add(T item)
        {
            _adaptee.Add(item);
        }

        public void Clear()
        {
            _adaptee.Clear();
        }

        public bool Contains(T item)
        {
            return _adaptee.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _adaptee.CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return _adaptee.Count; ; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            return _adaptee.Remove(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _adaptee.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _adaptee.GetEnumerator();
        }

        #endregion
    }

    public class VectorChangedEventArgs : IVectorChangedEventArgs
    {
        public CollectionChange CollectionChange
        {
            get;
            set;
        }

        public uint Index
        {
            get;
            set;
        }
    }
}
