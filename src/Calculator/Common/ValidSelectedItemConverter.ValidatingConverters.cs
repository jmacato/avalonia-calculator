// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using Microsoft.UI.Xaml.Data;

namespace CalculatorApp
{
    namespace Common
    {
        internal sealed class ValidSelectedItemConverter : IValueConverter
        {
            public ValidSelectedItemConverter()
            {
            }

            public object Convert(object value, Type targetType, object parameter, string language)
            {
                // Pass through as we don't want to change the value from the source
                return value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, string language)
            {
                if (value != null)
                {
                    return value;
                }

                // Stop the binding if the object is nullptr
                return Microsoft.UI.Xaml.DependencyProperty.UnsetValue;
            }
        }
    }
}
