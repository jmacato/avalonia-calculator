// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using CalculatorApp.ViewModel.Common;
using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Foundation.Diagnostics;
using Windows.UI.ViewManagement;

namespace CalculatorApp
{
    internal static class Globals
    {
#if SEND_DIAGNOSTICS
        // c.f. WINEVENT_KEYWORD_RESERVED_63-56 0xFF00000000000000 // Bits 63-56 - channel keywords
        // c.f. WINEVENT_KEYWORD_*              0x00FF000000000000 // Bits 55-48 - system-reserved keywords
        public const long MICROSOFT_KEYWORD_LEVEL_1 = 0x0000800000000000; // Bit 47
        public const long MICROSOFT_KEYWORD_LEVEL_2 = 0x0000400000000000;      // Bit 46
        public const long MICROSOFT_KEYWORD_LEVEL_3 = 0x0000200000000000;     // Bit 45
        public const long MICROSOFT_KEYWORD_RESERVED_44 = 0x0000100000000000;   // Bit 44 (reserved for future assignment)
#else
        // define all Keyword options as 0 when we do not want to upload app diagnostics
        public const long MICROSOFT_KEYWORD_LEVEL_1 = 0;
        public const long MICROSOFT_KEYWORD_LEVEL_2 = 0;
        public const long MICROSOFT_KEYWORD_LEVEL_3 = 0;
        public const long MICROSOFT_KEYWORD_RESERVED_44 = 0;
#endif
    }
}
