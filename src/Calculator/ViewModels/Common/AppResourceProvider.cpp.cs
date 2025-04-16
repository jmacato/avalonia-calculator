// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//  // #include  "pch.h"
//  // #include  "AppResourceProvider.h"

using Windows.ApplicationModel.Resources;
namespace CalculatorApp.ViewModel.Common;
public partial class AppResourceProvider
{
    public AppResourceProvider()
    {
        m_stringResLoader = ResourceLoader.GetForViewIndependentUse();
        m_cEngineStringResLoader = ResourceLoader.GetForViewIndependentUse("CEngineStrings");
    }

    static AppResourceProvider s_instance = new AppResourceProvider();


    public static AppResourceProvider GetInstance()
    {
        return s_instance;
    }

    public string GetResourceString(string key)
    {
        return m_stringResLoader.GetString(key);
    }

    public string GetCEngineString(string key)
    {
        return m_cEngineStringResLoader.GetString(key);
    }

}
