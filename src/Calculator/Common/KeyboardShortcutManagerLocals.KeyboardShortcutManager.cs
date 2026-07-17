// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel;
using Utilities = CalculatorApp.ViewModel.Common.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using MUXC = Windows.UI.Xaml.Controls;

namespace CalculatorApp
{
    namespace Common
    {
        internal static class KeyboardShortcutManagerLocals
        {
            // Lights up all of the buttons in the given range
            // The range is defined by a pair of iterators
            public static void LightUpButtons(IEnumerable<WeakReference> buttons)
            {
                foreach (var button in buttons)
                {
                    if (button.Target is ButtonBase btn && btn.IsEnabled)
                    {
                        LightUpButton(btn);
                    }
                }
            }

            public static void LightUpButton(ButtonBase button)
            {
                // If the button is a toggle button then we don't need
                // to change the UI of the button
                if (button is ToggleButton)
                {
                    return;
                }

                // The button will go into the visual Pressed state with this call
                VisualStateManager.GoToState(button, "Pressed", true);
                // This timer will fire after lightUpTime and make the button
                // go back to the normal state.
                // This timer will only fire once after which it will be destroyed
                var timer = new DispatcherTimer();
                TimeSpan lightUpTime = TimeSpan.FromMilliseconds(50); // 5e5 100-ns
                timer.Interval = lightUpTime;
                var timerWeakReference = new WeakReference(timer);
                var buttonWeakReference = new WeakReference(button);
                timer.Tick += (sender, args) =>
                {
                    if (buttonWeakReference.Target is ButtonBase btn)
                    {
                        VisualStateManager.GoToState(button, "Normal", true);
                    }

                    if (timerWeakReference.Target is DispatcherTimer tmr)
                    {
                        tmr.Stop();
                    }
                };
                timer.Start();
            }

            // Looks for the first button reference that it can resolve
            // and execute its command.
            // NOTE: It is assumed that all buttons associated with a particular
            // key have the same command
            public static void RunFirstEnabledButtonCommand(IEnumerable<WeakReference> buttons)
            {
                foreach (var button in buttons)
                {
                    if (button.Target is ButtonBase btn && btn.IsEnabled)
                    {
                        RunButtonCommand(btn);
                        break;
                    }
                }
            }

            public static void RunButtonCommand(ButtonBase button)
            {
                if (button.IsEnabled)
                {
                    var command = button.Command;
                    var parameter = button.CommandParameter;
                    if (command != null && command.CanExecute(parameter))
                    {
                        command.Execute(parameter);
                    }

                    if (button is MUXC.RadioButton radio)
                    {
                        radio.IsChecked = true;
                        return;
                    }

                    if (button is ToggleButton toggle)
                    {
                        toggle.IsChecked = !(toggle.IsChecked != null && toggle.IsChecked.Value);
                        return;
                    }
                }
            }
        }
    }
}
