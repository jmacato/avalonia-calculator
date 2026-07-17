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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;

namespace CalculatorApp.ViewModel.Common
{
    internal static class Utilities
    {
        public static int GetWindowId()
        {
            var window = AppWindow.Create();
            // TODO Windows.UI.ViewManagement.ApplicationView is no longer supported. Use Microsoft.UI.Windowing.AppWindow instead. For more details see https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/migrate-to-windows-app-sdk/guides/windowing
            return (int)window.Id.Value; // ApplicationView.GetApplicationViewIdForWindow(window);
        }

        static long WINEVENT_KEYWORD_RESPONSE_TIME = 0x1000000000000;
        public static long GetConst_WINEVENT_KEYWORD_RESPONSE_TIME()
        {
            return WINEVENT_KEYWORD_RESPONSE_TIME;
        }

        // This method calculates the luminance ratio between White and the given background color.
        // The luminance is calculate using the RGB values and does not use the A value.
        // White or Black is returned
        public static SolidColorBrush GetContrastColor(Color backgroundColor)
        {
            var luminance = 0.2126 * backgroundColor.R + 0.7152 * backgroundColor.G + 0.0722 * backgroundColor.B;
            if ((255 + 0.05) / (luminance + 0.05) >= 2.5)
            {
                return (SolidColorBrush)(Application.Current.Resources["WhiteBrush"]);
            }

            return (SolidColorBrush)(Application.Current.Resources["BlackBrush"]);
        }

        public static string EscapeHtmlSpecialCharacters(string originalString)
        {
            System.ArgumentNullException.ThrowIfNull(originalString);            // Construct a default special characters if not provided.
            char[] specialCharacters = new char[]
            {
                '&',
                '\"',
                '\'',
                '<',
                '>'
            };
            bool replaceCharacters = false;
            // First step is scanning the string for special characters.
            // If there isn't any special character, we simply return the original string
            replaceCharacters = originalString.Any(x => specialCharacters.Contains(x));
            if (!replaceCharacters)
            {
                return originalString;
            }

            // If we indeed find a special character, we step back one character (the special
            // character), and we create a new string where we replace those characters one by one
            var buffer = new StringBuilder();
            foreach (var x in originalString)
            {
                switch (x)
                {
                    case '&':
                        buffer.Append("&amp;");
                        break;
                    case '\"':
                        buffer.Append("&quot;");
                        break;
                    case '\'':
                        buffer.Append("&apos;");
                        break;
                    case '<':
                        buffer.Append("&lt;");
                        break;
                    case '>':
                        buffer.Append("&gt;");
                        break;
                    default:
                        buffer.Append(x);
                        break;
                }
            }

            return buffer.ToString();
        }

        public static string RemoveUnwantedCharsFromString(string inputString, char[] unwantedChars)
        {
            System.ArgumentNullException.ThrowIfNull(unwantedChars); System.ArgumentNullException.ThrowIfNull(inputString); foreach (char unwantedChar in unwantedChars)
            {
                inputString = inputString.Replace(unwantedChar.ToString(), "", StringComparison.Ordinal);
            }

            return inputString;
        }

        // Returns if the last character of a wstring is the target wchar_t
        public static bool IsLastCharacterTarget(string input, char target)
        {
            System.ArgumentNullException.ThrowIfNull(input); return input.Length != 0 && input.Last() == target;
        }

        public static bool IsDateTimeOlderThan(DateTime dateTime, long duration)
        {
            DateTime now = GetUniversalSystemTime();
            return dateTime.Ticks + duration < now.Ticks;
        }

        public static DateTime GetUniversalSystemTime()
        {
            return DateTime.Now.ToUniversalTime();
        }

        public static async Task<string?> ReadFileFromFolder(StorageFolder folder, string fileName)
        {
            if (folder == null)
            {
                return null;
            }

            StorageFile file = await folder.GetFileAsync(fileName);
            if (file == null)
            {
                return null;
            }

            return await FileIO.ReadTextAsync(file);
        }

        public static async Task WriteFileToFolder(IStorageFolder folder, String fileName, String contents, CreationCollisionOption collisionOption)
        {
            if (folder == null)
            {
                return;
            }

            StorageFile file = await folder.CreateFileAsync(fileName, collisionOption);
            if (file == null)
            {
                return;
            }

            await FileIO.WriteTextAsync(file, contents);
        }

        public static bool GetIntegratedDisplaySize(out double size)
        {
            return WinNativeMethods.GetIntegratedDisplaySize(out size) != 0;
            //bool res = ViewModel.Common.Utilities.GetIntegratedDisplaySize(out size);
            //Console.WriteLine($"{GetIntegratedDisplaySize} {size}");
            //return res;
        }
    }
}
