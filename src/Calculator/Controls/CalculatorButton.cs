// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Windows.System;
using CalculatorApp.ViewModel.Common;

using Microsoft.Windows.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;

namespace CalculatorApp
{
    namespace Controls
    {
        public sealed class CalculatorButton : Button
        {
            public CalculatorButton()
            {
                // Set the default bindings for this button, these can be overwritten by Xaml if needed
                // These are a replacement for binding in styles
                Binding commandBinding = new Binding
                {
                    Path = new PropertyPath("ButtonPressed")
                };
                this.SetBinding(CommandProperty, commandBinding);
            }

            public NumbersAndOperatorsEnum ButtonId
            {
                get => (NumbersAndOperatorsEnum)GetValue(ButtonIdProperty);
                set => SetValue(ButtonIdProperty, value);
            }

            // Using a DependencyProperty as the backing store for ButtonId.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty ButtonIdProperty =
                DependencyProperty.Register(nameof(ButtonId), typeof(NumbersAndOperatorsEnum), typeof(CalculatorButton), new PropertyMetadata(default(NumbersAndOperatorsEnum), (sender, args) =>
                {
                    var self = (CalculatorButton)sender;
                    self.OnButtonIdPropertyChanged((NumbersAndOperatorsEnum)args.OldValue, (NumbersAndOperatorsEnum)args.NewValue);
                }));

            public string AuditoryFeedback
            {
                get => (string)GetValue(AuditoryFeedbackProperty);
                set => SetValue(AuditoryFeedbackProperty, value);
            }

            // Using a DependencyProperty as the backing store for AuditoryFeedback.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty AuditoryFeedbackProperty =
                DependencyProperty.Register(nameof(AuditoryFeedback), typeof(string), typeof(CalculatorButton), new PropertyMetadata(string.Empty, (sender, args) =>
                {
                    var self = (CalculatorButton)sender;
                    self.OnAuditoryFeedbackPropertyChanged((string)args.OldValue, (string)args.NewValue);
                }));

            public Microsoft.UI.Xaml.Media.Brush HoverBackground
            {
                get => (Microsoft.UI.Xaml.Media.Brush)GetValue(HoverBackgroundProperty);
                set => SetValue(HoverBackgroundProperty, value);
            }

            // Using a DependencyProperty as the backing store for HoverBackground.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty HoverBackgroundProperty =
                DependencyProperty.Register(nameof(HoverBackground), typeof(Microsoft.UI.Xaml.Media.Brush), typeof(CalculatorButton), new PropertyMetadata(default(Microsoft.UI.Xaml.Media.Brush)));

            public Microsoft.UI.Xaml.Media.Brush HoverForeground
            {
                get => (Microsoft.UI.Xaml.Media.Brush)GetValue(HoverForegroundProperty);
                set => SetValue(HoverForegroundProperty, value);
            }

            // Using a DependencyProperty as the backing store for HoverForeground.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty HoverForegroundProperty =
                DependencyProperty.Register(nameof(HoverForeground), typeof(Microsoft.UI.Xaml.Media.Brush), typeof(CalculatorButton), new PropertyMetadata(default(Microsoft.UI.Xaml.Media.Brush)));

            public Microsoft.UI.Xaml.Media.Brush PressBackground
            {
                get => (Microsoft.UI.Xaml.Media.Brush)GetValue(PressBackgroundProperty);
                set => SetValue(PressBackgroundProperty, value);
            }

            // Using a DependencyProperty as the backing store for PressBackground.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty PressBackgroundProperty =
                DependencyProperty.Register(nameof(PressBackground), typeof(Microsoft.UI.Xaml.Media.Brush), typeof(CalculatorButton), new PropertyMetadata(default(Microsoft.UI.Xaml.Media.Brush)));

            public Microsoft.UI.Xaml.Media.Brush PressForeground
            {
                get => (Microsoft.UI.Xaml.Media.Brush)GetValue(PressForegroundProperty);
                set => SetValue(PressForegroundProperty, value);
            }

            // Using a DependencyProperty as the backing store for PressForeground.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty PressForegroundProperty =
                DependencyProperty.Register(nameof(PressForeground), typeof(Microsoft.UI.Xaml.Media.Brush), typeof(CalculatorButton), new PropertyMetadata(default(Microsoft.UI.Xaml.Media.Brush)));

            public Microsoft.UI.Xaml.Media.Brush DisabledBackground
            {
                get => (Microsoft.UI.Xaml.Media.Brush)GetValue(DisabledBackgroundProperty);
                set => SetValue(DisabledBackgroundProperty, value);
            }

            // Using a DependencyProperty as the backing store for DisabledBackground.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty DisabledBackgroundProperty =
                DependencyProperty.Register(nameof(DisabledBackground), typeof(Microsoft.UI.Xaml.Media.Brush), typeof(CalculatorButton), new PropertyMetadata(default(Microsoft.UI.Xaml.Media.Brush)));

            public Microsoft.UI.Xaml.Media.Brush DisabledForeground
            {
                get => (Microsoft.UI.Xaml.Media.Brush)GetValue(DisabledForegroundProperty);
                set => SetValue(DisabledForegroundProperty, value);
            }

            // Using a DependencyProperty as the backing store for DisabledForeground.  This enables animation, styling, binding, etc...
            public static readonly DependencyProperty DisabledForegroundProperty =
                DependencyProperty.Register(nameof(DisabledForeground), typeof(Microsoft.UI.Xaml.Media.Brush), typeof(CalculatorButton), new PropertyMetadata(default(Microsoft.UI.Xaml.Media.Brush)));

            protected override void OnKeyDown(KeyRoutedEventArgs e)
            {
                // Ignore the Enter key
                if (e.Key == VirtualKey.Enter)
                {
                    return;
                }

                base.OnKeyDown(e);
            }

            protected override void OnKeyUp(KeyRoutedEventArgs e)
            {
                // Ignore the Enter key
                if (e.Key == VirtualKey.Enter)
                {
                    return;
                }

                base.OnKeyUp(e);
            }

            private void OnButtonIdPropertyChanged(NumbersAndOperatorsEnum oldValue, NumbersAndOperatorsEnum newValue)
            {
                this.CommandParameter = new CalculatorButtonPressedEventArgs(AuditoryFeedback, newValue);
            }

            private void OnAuditoryFeedbackPropertyChanged(string oldValue, string newValue)
            {
                this.CommandParameter = new CalculatorButtonPressedEventArgs(newValue, ButtonId);
            }
        }
    }
}
