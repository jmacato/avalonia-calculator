// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorApp.ViewModel.Common;
using System;
using Windows.UI.Xaml.Markup;

namespace CalculatorApp.Utils
{
    [MarkupExtensionReturnType(ReturnType = typeof(MyVirtualKey))]
    internal sealed class ResourceVirtualKey : MarkupExtension
    {
        public string? Name { get; set; }

        protected override object ProvideValue()
        {
            string? name = Name;
            if (name is null || name.Length == 0)
            {
                throw new InvalidOperationException("A resource name is required.");
            }

            string resourceString = AppResourceProvider.Instance.GetResourceString(name);
            return (MyVirtualKey)Enum.Parse(typeof(MyVirtualKey), resourceString);
        }
    }
}
