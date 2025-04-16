// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #pragma once
// #include <cassert>
// #include <future>

using System.Threading.Tasks;

namespace CalculatorApp.ViewModel.DataLoaders
{
   public partial  class CurrencyHttpClient
    {
    // // public:
#if VIEWMODEL_FOR_UT
        public static bool ForceWebFailure { get; set; }
#endif
        // void Initialize(string  sourceCurrencyCode, string  responseLanguage);
        //
        // async Task<string > GetCurrencyMetadataAsync() const;
        // async Task<string > GetCurrencyRatiosAsync() const;

    // // private:
        string  m_sourceCurrencyCode;
        string  m_responseLanguage;
    };
}
