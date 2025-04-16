// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #pragma  once

// #include  "Utils.h"

using System.Collections.Generic;
using System.Globalization;
using Windows.UI.Text;
using Microsoft.UI.Xaml;


namespace CalculatorApp.ViewModel.Common;

public static class LocalizationServiceProperties
{
    public static string DefaultCurrencyCode = "USD";
}

public enum LanguageFontType
{
    UIText,
    UICaption,
};

public partial class LocalizationService : DependencyObject
{
    public static readonly DependencyProperty FontTypeProperty =
        DependencyProperty.RegisterAttached(
            "FontType",
            typeof(LanguageFontType),
            typeof(LocalizationService),
            new PropertyMetadata(LanguageFontType.UIText, OnFontTypePropertyChanged));

    public static LanguageFontType GetFontType(DependencyObject target)
    {
        return (LanguageFontType)target.GetValue(FontTypeProperty);
    }

    public static void SetFontType(DependencyObject target, LanguageFontType value)
    {
        target.SetValue(FontTypeProperty, value);
    }

    private static void OnFontTypePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        OnFontTypePropertyChanged(sender, (LanguageFontType)args.OldValue, (LanguageFontType)args.NewValue);
    }


    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.RegisterAttached(
            "FontSize",
            typeof(double),
            typeof(LocalizationService),
            new PropertyMetadata(default(double), OnFontSizePropertyChanged));

    public static double GetFontSize(DependencyObject target)
    {
        return (double)target.GetValue(FontSizeProperty);
    }

    public static void SetFontSize(DependencyObject target, double value)
    {
        target.SetValue(FontSizeProperty, value);
    }

    private static void OnFontSizePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        OnFontSizePropertyChanged(sender, (double)args.OldValue, (double)args.NewValue);
    }


    // static LocalizationService  GetInstance();
    // Microsoft.UI.Xaml.FlowDirection GetFlowDirection();
    // bool IsRtlLayout();
    // bool GetOverrideFontApiValues();
    // string  GetLanguage();
    // Microsoft.UI.Xaml.Media.FontFamily  GetLanguageFontFamilyForType(LanguageFontType fontType);
    // string  GetFontFamilyOverride();
    // FontWeight GetFontWeightOverride();
    // double GetFontScaleFactorOverride(LanguageFontType fontType);
    // Windows.Globalization.NumberFormatting.DecimalFormatter  GetRegionalSettingsAwareDecimalFormatter();
    // Windows.Globalization.DateTimeFormatting.DateTimeFormatter  GetRegionalSettingsAwareDateTimeFormatter( string  format);
    // Windows.Globalization.DateTimeFormatting.DateTimeFormatter
    //      GetRegionalSettingsAwareDateTimeFormatter(
    //          string  format,
    //          string  calendarIdentifier,
    //          string  clockIdentifier);
    // Windows.Globalization.NumberFormatting.CurrencyFormatter  GetRegionalSettingsAwareCurrencyFormatter();

    // internal:
    //     static void OverrideWithLanguage(   string   language);
    //     void Sort(List<string>& source);
    //
    //     template <typename T>
    //     void Sort(List<T>& source, function<string  (T)> func)
    //     {
    //         const collate<wchar_t>& coll = use_facet<collate<wchar_t>>(m_locale);
    //         sort(source.begin(), source.end(), [&coll, &func](T obj1, T obj2) {
    //             string  str1 = func(obj1);
    //             string  str2 = func(obj2);
    //             return coll.compare(str1.Begin(), str1.End(), str2.Begin(), str2.End()) < 0;
    //         });
    //     }

    //     static string  GetNarratorReadableToken(string  rawToken);
    //     static string  GetNarratorReadableString(string  rastring);
    //
    // private:
    //     LocalizationService(   string   overridedLanguage);
    //     Windows.Globalization.Fonts.LanguageFont  GetLanguageFont(LanguageFontType fontType);
    //     FontWeight ParseFontWeight(string  fontWeight);
    //
    //     Windows.Foundation.Collections.IIterable<string>  GetLanguageIdentifiers() const;
    //
    //     // Attached property callbacks
    //     static void OnFontTypePropertyChanged(Microsoft.UI.Xaml.DependencyObject  target, LanguageFontType oldValue, LanguageFontType newValue);
    //     static void OnFontWeightPropertyChanged(
    //         Microsoft.UI.Xaml.DependencyObject  target,
    //         FontWeight oldValue,
    //         FontWeight newValue);
    //     static void OnFontSizePropertyChanged(Microsoft.UI.Xaml.DependencyObject  target, double oldValue, double newValue);
    //
    //     static void UpdateFontFamilyAndSize(Microsoft.UI.Xaml.DependencyObject  target);
    //
    //     static unordered_map<string, string> GetTokenToReadableNameMap();

    static LocalizationService s_singletonInstance;

    Windows.Globalization.Fonts.LanguageFontGroup m_fontGroup;
    string m_language;
    Microsoft.UI.Xaml.FlowDirection m_flowDirection;
    bool m_overrideFontApiValues;
    string m_fontFamilyOverride;
    bool m_isLanguageOverrided;
    FontWeight m_fontWeightOverride;
    double m_uiTextFontScaleFactorOverride;

    double m_uiCaptionFontScaleFactorOverride;
    CultureInfo m_locale;
};
