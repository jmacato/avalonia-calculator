// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Markup.Xaml;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.Utils;

public sealed class ResourceString : MarkupExtension
{
    public string Name { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // RESW property paths such as "Control/Content" become dotted RESX
        // keys during the in-place resource conversion.
        return AppResourceProvider.Instance.GetResourceString(Name.Replace('/', '.'));
    }
}
