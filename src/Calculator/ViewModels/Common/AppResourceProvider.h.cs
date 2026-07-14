// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//  // #pragma  once

using System.Resources;

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
        private readonly ResourceManager m_stringResLoader;
        private readonly ResourceManager m_cEngineStringResLoader;
    };
}
