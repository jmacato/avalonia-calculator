// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorApp.ViewModel.Common;
using Microsoft.UI.Xaml.Controls;

namespace CalculatorApp
{
    namespace Controls
    {
        public sealed class RadixButton : RadioButton
        {
            public RadixButton()
            { }

            internal string GetRawDisplayValue()
            {
                string radixContent = Content?.ToString();
                return LocalizationSettings.GetInstance().RemoveGroupSeparators(radixContent);
            }
        }
    }
}
