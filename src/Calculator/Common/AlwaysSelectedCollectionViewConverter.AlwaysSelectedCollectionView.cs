// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;
using CommunityToolkit.Common;
using System.Collections.Specialized;
using ObservableCollectionShim;

namespace CalculatorApp
{
    namespace Common
    {
        internal sealed class AlwaysSelectedCollectionViewConverter : IValueConverter
        {
            public AlwaysSelectedCollectionViewConverter()
            {
            }

            public object Convert(object value, Type targetType, object parameter, string language)
            {
                if (value is IList result)
                {
                    return new AlwaysSelectedCollectionView(result);
                }

                return DependencyProperty.UnsetValue; // Can't convert
            }

            public object ConvertBack(object value, Type targetType, object parameter, string language)
            {
                return DependencyProperty.UnsetValue;
            }
        }
    }
}
