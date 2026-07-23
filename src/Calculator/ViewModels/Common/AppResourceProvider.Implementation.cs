// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//  // #include  "pch.h"
//  // #include  "AppResourceProvider.h"

using System.Globalization;
using System.Resources;

namespace CalculatorApp.ViewModel.Common;

internal sealed partial class AppResourceProvider
{
    public AppResourceProvider()
    {
        var assembly = typeof(AppResourceProvider).Assembly;
        m_stringResLoader = new ResourceManager("CalculatorApp.Resources.Resources", assembly);
        m_cEngineStringResLoader = new ResourceManager("CalculatorApp.Resources.CEngineStrings", assembly);
    }

    private static readonly AppResourceProvider s_instance = new();


    public static AppResourceProvider Instance => s_instance;

    public string GetResourceString(string key)
    {
        return m_stringResLoader.GetString(key, CultureInfo.CurrentUICulture) ?? string.Empty;
    }

    public string GetCEngineString(string key)
    {
        return m_cEngineStringResLoader.GetString(key, CultureInfo.CurrentUICulture) ?? string.Empty;
    }
}
