// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml;
using System;
using System.Linq;
using System.Text;

using System;
using System.Windows.Input;
using Windows.Foundation;
using Windows.UI.Xaml.Input;


namespace CalculatorApp.ViewModel.Common
{

        // Equivalent to the C++ DelegateCommandHandler delegate
        public delegate void DelegateCommandHandler(object parameter);

        // Equivalent to the C++ DelegateCommand class
        public sealed class DelegateCommand : ICommand
        {
            private readonly DelegateCommandHandler _handler;
            private event EventHandler _canExecuteChanged;

            public DelegateCommand(DelegateCommandHandler handler)
            {
                _handler = handler;
            }

            // ICommand implementation
            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                _handler?.Invoke(parameter);
            }

            event EventHandler ICommand.CanExecuteChanged
            {
                add
                {
                    _canExecuteChanged += value;
                }
                remove
                {
                    _canExecuteChanged -= value;
                }
            }
        }

        // Static helper class to provide the MakeDelegateCommandHandler functionality
        public static class CommandHelpers
        {
            // Generic method to create a command handler with weak reference to the target
            public static DelegateCommandHandler MakeDelegateCommandHandler<T>(T target, Action<T, object> function) where T : class
            {
                WeakReference weakTarget = new WeakReference(target);

                return parameter =>
                {
                    if (weakTarget.Target is T thatTarget)
                    {
                        function(thatTarget, parameter);
                    }
                };
            }
        }


    public static class Utilities
    {
        public static int GetWindowId()
        {
            int windowId = -1;

            var window = CoreWindow.GetForCurrentThread();
            if (window != null)
            {
                windowId = ApplicationView.GetApplicationViewIdForWindow(window);
            }

            return windowId;
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
            // Construct a default special characters if not provided.
            char[] specialCharacters = new char[] { '&', '\"', '\'', '<', '>' };

            bool replaceCharacters = false;
            string replacementString = null;

            // First step is scanning the string for special characters.
            // If there isn't any special character, we simply return the original string 
            replaceCharacters = replacementString.Any(x => specialCharacters.Contains(x));

            if (replaceCharacters)
            {
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
                replacementString = buffer.ToString();
            }

            return replaceCharacters ? replacementString : originalString;
        }

        public static string RemoveUnwantedCharsFromString(string inputString, char[] unwantedChars)
        {
            foreach (char unwantedChar in unwantedChars)
            {
                inputString = inputString.Replace(unwantedChar.ToString(), "");
            }
            return inputString;
        }
    }
}
