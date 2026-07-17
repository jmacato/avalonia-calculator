// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//  // #pragma  once

namespace CalculatorApp.ViewModel.Common
{
    public partial class AppResourceProvider
    {
        //public:
        //    static AppResourceProvider GetInstance();
        //    string GetResourceString( string key);
        //    string GetCEngineString( string key);

        //private:
        //    AppResourceProvider();
        Windows.ApplicationModel.Resources.ResourceLoader m_stringResLoader;
        Windows.ApplicationModel.Resources.ResourceLoader m_cEngineStringResLoader;
    };
}

