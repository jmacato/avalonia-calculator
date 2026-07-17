// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #pragma  once
// #include  "LocalizationService.h"

// #include  <iterator>

using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        internal sealed partial class LocalizationSettings
        {
            LocalizationSettings()
            // Use DecimalFormatter as it respects the locale and the user setting
            {
                Initialize(CultureInfo.CurrentCulture);
            }

            // This is only public for unit testing purposes.
            // LocalizationSettings(Windows.Globalization.NumberFormatting.DecimalFormatter  formatter)
            // {
            //     Initialize(formatter);
            // }
            private static readonly LocalizationSettings localizationSettings = new();

            // Provider of the singleton LocalizationSettings instance.
            public static LocalizationSettings Instance => localizationSettings;

            public string LocaleName => m_resolvedName;

            public bool IsDigitEnUsSetting()
            {
                return (this.GetDigitSymbolFromEnUsDigit('0') == '0');
            }

            public string GetEnglishValueFromLocalizedDigits(string localizedString)
            {
                System.ArgumentNullException.ThrowIfNull(localizedString);
                if (m_resolvedName == "en-US")
                {
                    return localizedString;
                }


                var englishString = new System.Text.StringBuilder(localizedString.Length);

                foreach (char ch in localizedString)
                {
                    char convertedChar = ch;
                    if (!IsEnUsDigit(ch))
                    {
                        int index = Array.IndexOf(m_digitSymbols, ch);
                        if (index != -1)
                        {
                            convertedChar = index.ToString(CultureInfo.InvariantCulture)[0];
                        }
                    }
                    if (ch == m_decimalSeparator)
                    {
                        convertedChar = '.';
                    }
                    englishString.Append(convertedChar);
                }

                return englishString.ToString();

                // var englishString = localizedString.ToCharArray();
                // //englishString.reserve(localizedString.Length());
                //
                // foreach (var  pair in localizedString.Select((ch,i)=>(ch,i)))
                // {
                //     if (!IsEnUsDigit(pair.ch))
                //     {
                //         var it = find(.begin(), m_digitSymbols.end(), ch);
                //
                //         if (it != m_digitSymbols.end())
                //         {
                //             var index = distance(m_digitSymbols.begin(), it);
                //             ch = index.ToString().Data()[0];
                //         }
                //     }
                //     if (ch == m_decimalSeparator)
                //     {
                //         ch = '.';
                //     }
                //     englishString += ch;
                // }
                //
                // return new  string(englishString);
            }

            public string RemoveGroupSeparators(string source)
            {
                return string.Concat(source.Where(c => c != ' ' && c != m_numberGroupSeparator));
                // string destination;
                // copy_if(
                //     begin(source), end(source), back_inserter(destination), [this](var const c) { return c != ' ' && c != m_numberGroupSeparator; });
                //
                // return new  string(destination.c_str());
            }

            public string CalendarIdentifier => m_calendarIdentifier;

            public DayOfWeek FirstDayOfWeek => m_firstDayOfWeek;

            public int CurrencyTrailingDigits => m_currencyTrailingDigits;

            public int CurrencySymbolPrecedence => m_currencySymbolPrecedence;

            public char DecimalSeparator => m_decimalSeparator;

            public char GetDigitSymbolFromEnUsDigit(char digitSymbol)
            {
                Debug.Assert(digitSymbol >= '0' && digitSymbol <= '9');
                int digit = digitSymbol - '0';
                return m_digitSymbols[digit]; // throws on out of range
            }

            public char NumberGroupSeparator => m_numberGroupSeparator;

            public static bool IsEnUsDigit(char digit)
            {
                return (digit >= '0' && digit <= '9');
            }

            public bool IsLocalizedDigit(char digit)
            {
                return
                    m_digitSymbols
                        .Contains(digit); //find(m_digitSymbols.begin(), m_digitSymbols.end(), digit) != m_digitSymbols.end();
            }

            public bool IsLocalizedHexDigit(char digit)
            {
                if (IsLocalizedDigit(digit))
                {
                    return true;
                }

                return s_hexSymbols.Contains(digit);
            }

            public static void LocalizeDisplayValue(ref string stringToLocalize)


            {
                System.ArgumentNullException.ThrowIfNull(stringToLocalize);
                if (ViewModel.Common.LocalizationSettings.Instance.IsDigitEnUsSetting())
                {
                    return;
                }

                char[] ret = stringToLocalize.ToCharArray();

                for (int chI = 0; chI < ret.Length; chI++)
                {
                    var ch = ret[chI];
                    if (ViewModel.Common.LocalizationSettings.IsEnUsDigit(ch))
                    {
                        ret[chI] = ViewModel.Common.LocalizationSettings.Instance.GetDigitSymbolFromEnUsDigit(ch);
                    }
                }

                stringToLocalize = new string(ret);
            }


            public string GetDecimalSeparatorStr()
            {
                return m_decimalSeparator.ToString();
            }

            public string GetNumberGroupingSeparatorStr()
            {
                return m_numberGroupSeparator.ToString();
            }

            public string NumberGrouping => m_numberGrouping;

            public string ListSeparator => m_listSeparator;

            private void Initialize(CultureInfo cultureInfo)
            {
                NumberFormatInfo numberFormat = cultureInfo.NumberFormat;

                // Populate digit symbols
                for (int i = 0; i < m_digitSymbols.Length; i++)
                {
                    m_digitSymbols[i] = numberFormat.NativeDigits[i][0];
                }

                m_resolvedName = cultureInfo.Name;
                m_decimalSeparator = numberFormat.NumberDecimalSeparator[0];
                m_numberGroupSeparator = numberFormat.NumberGroupSeparator[0];

                // Convert number grouping to string representation similar to Win32 API
                int[] groupSizes = numberFormat.NumberGroupSizes;
                m_numberGrouping = string.Join(";", groupSizes.Select(size => size.ToString(CultureInfo.InvariantCulture)));
                //TODO: Do this better. Need to find cross platform way of getting the proper digit grouping.
                m_numberGrouping += ";0";

                m_listSeparator = cultureInfo.TextInfo.ListSeparator;
                m_currencyTrailingDigits = numberFormat.CurrencyDecimalDigits;

                // Currency symbol precedence is either 0 or 1.
                // A value of 0 indicates the symbol follows the currency value,
                // matching LOCALE_IPOSSYMPRECEDES in the original C++ source.
                m_currencySymbolPrecedence = numberFormat.CurrencyPositivePattern is 0 or 2
                    ? 1
                    : 0;

                // Get the system calendar type
                m_calendarIdentifier = GetCalendarIdentifierFromCalendarType(cultureInfo.Calendar);

                // Get FirstDayOfWeek from culture
                m_firstDayOfWeek = cultureInfo.DateTimeFormat.FirstDayOfWeek;
            }

            private static string GetCalendarIdentifierFromCalendarType(Calendar calendar)
            {
                if (calendar is GregorianCalendar)
                    return "GregorianCalendar";
                else if (calendar is HebrewCalendar)
                    return "HebrewCalendar";
                else if (calendar is HijriCalendar)
                    return "HijriCalendar";
                else if (calendar is JapaneseCalendar)
                    return "JapaneseCalendar";
                else if (calendar is KoreanCalendar)
                    return "KoreanCalendar";
                else if (calendar is TaiwanCalendar)
                    return "TaiwanCalendar";
                else if (calendar is ThaiBuddhistCalendar)
                    return "ThaiCalendar";
                else if (calendar.GetType().Name == "UmAlQuraCalendar") // Not available in .NET Standard 2.0
                    return "UmAlQuraCalendar";
                else
                    return "GregorianCalendar"; // Default
            }
            char m_decimalSeparator;
            char m_numberGroupSeparator;
            string m_numberGrouping = string.Empty;
            char[] m_digitSymbols = new char[10];
            // Hexadecimal characters are not currently localized
            static char[] s_hexSymbols = ['A', 'B', 'C', 'D', 'E', 'F'];
            string m_listSeparator = string.Empty;
            string m_calendarIdentifier = string.Empty;
            System.DayOfWeek m_firstDayOfWeek;
            int m_currencySymbolPrecedence;
            string m_resolvedName = string.Empty;
            int m_currencyTrailingDigits;
        };
    }
}
