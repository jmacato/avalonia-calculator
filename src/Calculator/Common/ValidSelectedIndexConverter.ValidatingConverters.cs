// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using Microsoft.UI.Xaml.Data;

namespace CalculatorApp
{
    namespace Common
    {
        internal sealed class ValidSelectedIndexConverter : Microsoft.UI.Xaml.Data.IValueConverter
        {
            public ValidSelectedIndexConverter()
            {
            }

            public object Convert(object value, Type targetType, object parameter, string language)
            {
                // Pass through as we don't want to change the value from the source
                return value;
            }

            public object ConvertBack(object value, Type targetType, object parameter, string language)
            {
                // The value to be valid has to be a boxed int32 value
                // extract that value and ensure it is valid, ie >= 0
                if (value is int v)
                {
                    int index = v;
                    if (index >= 0)
                    {
                        return value;
                    }
                }

                // The value is not valid therefore stop the binding right here
                return Microsoft.UI.Xaml.DependencyProperty.UnsetValue;
            }
        }
    }
}
