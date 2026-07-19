// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using Windows.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CalculatorApp.Utils
{
    /// <summary>
    /// Class providing functionality around switching and restoring theme settings
    /// </summary>
    internal static class ThemeHelper
    {
        private const string SelectedAppThemeKey = "SelectedAppTheme";
        /// <summary>
        /// Get or set (with LocalSettings persistence) the RequestedTheme of the root element.
        /// </summary>
        public static ElementTheme RootTheme
        {
            get
            {
                if (App.Window.Content is FrameworkElement rootElement)
                {
                    return rootElement.RequestedTheme;
                }

                return ElementTheme.Default;
            }

            set
            {
                if (App.Window.Content is FrameworkElement rootElement)
                {
                    rootElement.RequestedTheme = value;
                    ApplicationData.Current.LocalSettings.Values[SelectedAppThemeKey] = rootElement.RequestedTheme.ToString();
                }
            }
        }

        public static TEnum GetEnum<TEnum>(string text)
            where TEnum : struct, Enum
        {
            return Enum.Parse<TEnum>(text);
        }

        public static void InitializeAppTheme()
        {
            string? savedTheme = ApplicationData.Current.LocalSettings.Values[SelectedAppThemeKey]?.ToString();
            if (!string.IsNullOrEmpty(savedTheme))
            {
                RootTheme = GetEnum<ElementTheme>(savedTheme);
            }
        }

        public static ThemeHelperThemeChangedCallbackToken RegisterAppThemeChangedCallback(DependencyPropertyChangedCallback callback)
        {
            Frame rootFrame = App.Window.Content as Frame
                ?? throw new InvalidOperationException("The application window content must be a Frame.");
            long token = rootFrame.RegisterPropertyChangedCallback(FrameworkElement.RequestedThemeProperty, callback);
            return new ThemeHelperThemeChangedCallbackToken
            {
                RootFrame = new WeakReference(rootFrame),
                Token = token
            };
        }

        public static void UnregisterAppThemeChangedCallback(ThemeHelperThemeChangedCallbackToken callbackToken)
        {
            if (callbackToken.RootFrame?.Target is Frame rootFrame)
            {
                rootFrame.UnregisterPropertyChangedCallback(Frame.RequestedThemeProperty, callbackToken.Token);
            }
        }
    }
}
