// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using Microsoft.UI.Xaml;

namespace CalculatorApp
{
    namespace Converters
    {
        /// <summary>
        /// Value converter that translates false to <see cref = "Visibility.Visible"/> and true
        /// to <see cref = "Visibility.Collapsed"/>.
        /// </summary>
        internal sealed class BooleanToVisibilityNegationConverter : Microsoft.UI.Xaml.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, string language)
            {
                var boolValue = (value is bool boxedBool && boxedBool);
                return BooleanToVisibilityConverter.Convert(!boolValue);
            }

            public object ConvertBack(object value, Type targetType, object parameter, string language)
            {
                return (value is Visibility visibility && visibility != Visibility.Visible);
            }
        }
    }
}
