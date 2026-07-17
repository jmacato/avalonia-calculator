// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma  once
// #include  "AppResourceProvider.h"
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        internal static class LocalizationStringUtilInternal
        {
            /// <summary>
            /// Formats a localized string using .NET's string formatting capabilities.
            /// This replaces the Win32 FormatMessage API with FORMAT_MESSAGE_FROM_STRING flag.
            /// </summary>
            /// <param name = "pMessage">The first parameter is the format string, followed by arguments to format.</param>
            /// <returns>The formatted string, or an empty string if formatting fails.</returns>
            public static string GetLocalizedString(params string[] pMessage)
            {
                try
                {
                    if (pMessage == null || pMessage.Length == 0 || string.IsNullOrEmpty(pMessage[0]))
                    {
                        return string.Empty;
                    }

                    string formatString = pMessage[0];
                    // If there are no additional arguments, just return the format string
                    if (pMessage.Length == 1)
                    {
                        return formatString;
                    }

                    // The FormatMessage API uses %1, %2, etc. for placeholders
                    // We need to convert them to {0}, {1}, etc. for string.Format
                    string netFormatString = ConvertFormatMessageToStringFormat(formatString);
                    // Create an array of arguments (skipping the format string)
                    object[] args = new object[pMessage.Length - 1];
                    Array.Copy(pMessage, 1, args, 0, pMessage.Length - 1);
                    return string.Format(CultureInfo.CurrentCulture, netFormatString, args);
                }
                catch (FormatException)
                {
                    // If any exception occurs during formatting, return an empty string
                    // This matches the behavior of the original implementation
                    return string.Empty;
                }
            }

            /// <summary>
            /// Converts a format string from FormatMessage style (%1, %2) to .NET string.Format style ({0}, {1})
            /// </summary>
            private static string ConvertFormatMessageToStringFormat(string formatString)
            {
                // This regex matches Windows FormatMessage placeholders like %1, %2, etc.
                // and converts them to .NET string.Format placeholders {0}, {1}, etc.
                return Regex.Replace(formatString, @"%(\d+)", match =>
                {
                    // Convert from 1-based indexing to 0-based indexing
                    if (int.TryParse(match.Groups[1].Value, out int index) && index > 0)
                    {
                        return "{" + (index - 1) + "}";
                    }

                    return match.Value; // Keep the original value if parsing fails
                });
            }
        };
    }
}
