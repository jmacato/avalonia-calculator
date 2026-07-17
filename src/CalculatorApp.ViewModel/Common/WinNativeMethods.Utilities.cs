// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace CalculatorApp.ViewModel.Common
{
    static class WinNativeMethods
    {
        // Define the PInvoke signature for GetIntegratedDisplaySize
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [DllImport("kernelbase.dll", SetLastError = true)]
        public static extern int GetIntegratedDisplaySize(out double sizeInInches);
    }
}
