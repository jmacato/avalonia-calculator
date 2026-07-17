// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #include  "pch.h"
// #include  "NavCategory.h"
// #include  "AppResourceProvider.h"
// #include  "Common/LocalizationStringUtil.h"
// #include  <initializer_list>
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CalculatorApp;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel;
using ViewModeType = CalculatorApp.ViewModel.Common.ViewMode;

namespace CalculatorApp.ViewModel.Common;

public partial class NavCategoryGroup
{
    internal NavCategoryGroup(NavCategoryGroupInitializer groupInitializer)
    {
        m_Categories = new();
        m_GroupType = groupInitializer.Type;
        var resProvider = AppResourceProvider.Instance;
        m_Name = resProvider.GetResourceString((groupInitializer.HeaderResourceKey));
        String groupMode = resProvider.GetResourceString((groupInitializer.ModeResourceKey));
        String automationName = resProvider.GetResourceString((groupInitializer.AutomationResourceKey));
        String navCategoryHeaderAutomationNameFormat = resProvider.GetResourceString("NavCategoryHeader_AutomationNameFormat");
        m_AutomationName = LocalizationStringUtil.GetLocalizedString(navCategoryHeaderAutomationNameFormat, automationName);
        String navCategoryItemAutomationNameFormat = resProvider.GetResourceString("NavCategoryItem_AutomationNameFormat");
        foreach (var categoryInitializer in NavCategory.s_categoryManifest)
        {
            if (categoryInitializer.GroupType == groupInitializer.Type)
            {
                String nameResourceKey = (categoryInitializer.NameResourceKey);
                String categoryName = resProvider.GetResourceString(nameResourceKey + "Text");
                String categoryAutomationName = LocalizationStringUtil.GetLocalizedString(navCategoryItemAutomationNameFormat, categoryName, m_Name);
                m_Categories.Add(new NavCategory(categoryName, categoryAutomationName, (categoryInitializer.Glyph), categoryInitializer.AccessKey ?? resProvider.GetResourceString(nameResourceKey + "AccessKey"), groupMode, categoryInitializer.ViewMode, categoryInitializer.SupportsNegative, NavCategoryStates.IsViewModeEnabled(categoryInitializer.ViewMode)));
            }
        }
    }
}
