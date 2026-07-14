// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorApp.ViewModel.Common;
using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FluentAvalonia.UI.Controls;

namespace CalculatorApp.TemplateSelectors
{
    internal sealed class NavViewMenuItemTemplateSelector : FADataTemplateSelector
    {
        public IDataTemplate CategoryItemTemplate { get; set; } = null!;
        public IDataTemplate CategoryGroupItemTemplate { get; set; } = null!;

        protected override IDataTemplate SelectTemplateCore(object item)
        {
            if (item is NavCategory)
            {
                return CategoryItemTemplate;
            }
            else if (item is NavCategoryGroup)
            {
                return CategoryGroupItemTemplate;
            }
            else
            {
                throw new NotSupportedException($"typeof(item) must be {nameof(NavCategory)} or {nameof(NavCategoryGroup)}.");
            }
        }

        protected override IDataTemplate SelectTemplateCore(object item, Control container)
        {
            return SelectTemplateCore(item);
        }
    }
}
