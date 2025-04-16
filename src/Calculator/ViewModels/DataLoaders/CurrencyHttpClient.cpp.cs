// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include "pch.h"
// #include "CurrencyHttpClient.cpp.h"

using System.Threading.Tasks;

namespace CalculatorApp.ViewModel.DataLoaders
{
    public partial class CurrencyHttpClient
    {
        private const string MockCurrencyConverterData = @"
        [
          { ""An"": ""MAR"", ""Rt"": 1.00 },
          { ""An"": ""MON"", ""Rt"": 0.50 },
          { ""An"": ""NEP"", ""Rt"": 0.00125 },
          { ""An"": ""SAT"", ""Rt"": 0.25 },
          { ""An"": ""URA"", ""Rt"": 2.75 },
          { ""An"": ""VEN"", ""Rt"": 900.00 },
          { ""An"": ""JUP"", ""Rt"": 1.23456789123456789 },
          { ""An"": ""MER"", ""Rt"": 2.00 },
          { ""An"": ""JPY"", ""Rt"": 0.00125 },
          { ""An"": ""JOD"", ""Rt"": 0.25 }
        ]";

        private const string MockCurrencyStaticData = @"
        [
          { ""CountryCode"": ""MAR"", ""CountryName"": ""Mars"", ""CurrencyCode"": ""MAR"", ""CurrencyName"": ""The Martian Dollar"", ""CurrencySymbol"": ""\u2642"" },
          { ""CountryCode"": ""MON"", ""CountryName"": ""Moon"", ""CurrencyCode"": ""MON"", ""CurrencyName"": ""Moon Bucks"", ""CurrencySymbol"": ""\u263E"" },
          { ""CountryCode"": ""NEP"", ""CountryName"": ""Neptune"", ""CurrencyCode"": ""NEP"", ""CurrencyName"": ""Space Coins"", ""CurrencySymbol"": ""\u2646"" },
          { ""CountryCode"": ""SAT"", ""CountryName"": ""Saturn"", ""CurrencyCode"": ""SAT"", ""CurrencyName"": ""Rings"", ""CurrencySymbol"": ""\u2644"" },
          { ""CountryCode"": ""URA"", ""CountryName"": ""Uranus"", ""CurrencyCode"": ""URA"", ""CurrencyName"": ""Galaxy Credits"", ""CurrencySymbol"": ""\u2645"" },
          { ""CountryCode"": ""VEN"", ""CountryName"": ""Venus"", ""CurrencyCode"": ""VEN"", ""CurrencyName"": ""Venusian Seashells"", ""CurrencySymbol"": ""\u2640"" },
          { ""CountryCode"": ""JUP"", ""CountryName"": ""Jupiter"", ""CurrencyCode"": ""JUP"", ""CurrencyName"": ""Gas Money"", ""CurrencySymbol"": ""\u2643"" },
          { ""CountryCode"": ""MER"", ""CountryName"": ""Mercury"", ""CurrencyCode"": ""MER"", ""CurrencyName"": ""Sun Notes"", ""CurrencySymbol"": ""\u263F"" },
          { ""CountryCode"": ""TEST1"", ""CountryName"": ""Test No Fractional Digits"", ""CurrencyCode"": ""JPY"", ""CurrencyName"": ""Test No Fractional Digits"", ""CurrencySymbol"": ""\u00A4"" },
          { ""CountryCode"": ""TEST2"", ""CountryName"": ""Test Fractional Digits"", ""CurrencyCode"": ""JOD"", ""CurrencyName"": ""Test Fractional Digits"", ""CurrencySymbol"": ""\u00A4"" }
        ]";



        public void Initialize(string sourceCurrencyCode, string responseLanguage)
        {
            m_sourceCurrencyCode = sourceCurrencyCode;
            m_responseLanguage = responseLanguage;
        }

        public Task<string> GetCurrencyMetadataAsync()
        {
#if VIEWMODEL_FOR_UT
            if (ForceWebFailure)
            {
                return Task.FromException<string>(new System.Net.Http.HttpRequestException("Simulated network failure"));
            }
#endif
            return Task.FromResult(MockCurrencyStaticData);
        }

        public Task<string> GetCurrencyRatiosAsync()
        {
#if VIEWMODEL_FOR_UT
            if (ForceWebFailure)
            {
                return Task.FromException<string>(new System.Net.Http.HttpRequestException("Simulated network failure"));
            }
#endif
            return Task.FromResult(MockCurrencyConverterData);
        }
    }
}
