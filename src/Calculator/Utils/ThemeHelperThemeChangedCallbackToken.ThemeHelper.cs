// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using Windows.Storage;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CalculatorApp.Utils
{
    internal struct ThemeHelperThemeChangedCallbackToken : IEquatable<ThemeHelperThemeChangedCallbackToken>
    {
        public WeakReference? RootFrame;
        public long Token;

        public override bool Equals(object? obj)
        {
            return obj is ThemeHelperThemeChangedCallbackToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RootFrame, Token);
        }

        public static bool operator ==(ThemeHelperThemeChangedCallbackToken left, ThemeHelperThemeChangedCallbackToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ThemeHelperThemeChangedCallbackToken left, ThemeHelperThemeChangedCallbackToken right)
        {
            return !(left == right);
        }

        public bool Equals(ThemeHelperThemeChangedCallbackToken other)
        {
            return ReferenceEquals(RootFrame, other.RootFrame) && Token == other.Token;
        }
    }
}
