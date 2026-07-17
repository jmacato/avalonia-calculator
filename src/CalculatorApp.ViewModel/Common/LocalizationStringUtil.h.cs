// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma  once
// #include  "AppResourceProvider.h"
using System;
using System.Text.RegularExpressions;

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        public static partial class LocalizationStringUtil
        {
            public static string GetLocalizedString(string pMessage)
            {
                return LocalizationStringUtilInternal.GetLocalizedString(pMessage);
            }

            public static string GetLocalizedString(string pMessage, string param1)
            {
                return LocalizationStringUtilInternal.GetLocalizedString(pMessage, param1);
            }

            public static string GetLocalizedString(string pMessage, string param1, string param2)
            {
                return LocalizationStringUtilInternal.GetLocalizedString(pMessage, param1, param2);
            }

            public static string GetLocalizedString(string pMessage, string param1, string param2, string param3)
            {
                return LocalizationStringUtilInternal.GetLocalizedString(pMessage, param1, param2, param3);
            }

            public static string GetLocalizedString(string pMessage, string param1, string param2, string param3, string param4)
            {
                return LocalizationStringUtilInternal.GetLocalizedString(pMessage, param1, param2, param3, param4);
            }

            public static string GetLocalizedString(string pMessage, string param1, string param2, string param3, string param4, string param5)
            {
                return LocalizationStringUtilInternal.GetLocalizedString(pMessage, param1, param2, param3, param4, param5);
            }
        };
    }
}
