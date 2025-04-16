// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include  "pch.h"
// #include  "h"
// #include  "LocalizationSettings.h"
// #include  "AppResourceProvider.h"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel;
using Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Globalization;
using Windows.Globalization.DateTimeFormatting;
using Windows.Globalization.Fonts;
using Windows.Globalization.NumberFormatting;
using Windows.System.UserProfile;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace CalculatorApp.ViewModel.Common;

public partial class LocalizationService : DependencyObject
{
    static ReaderWriterLockSlim s_locServiceInstanceLock = new ReaderWriterLockSlim();

// LocalizationService  s_singletonInstance = null;

// Resources for the engine use numbers as keys. It's inconvenient, but also difficult to
// change given that the engine heavily relies on perfect ordering of certain elements.
// The key for open parenthesis, '(', is "48".
    const string s_openParenResourceKey = "48";

    public static LocalizationService GetInstance()
    {
        if (s_singletonInstance == null)
        {
            // Writer lock for the static maps
            using var lockscope = new WriterLockScope(s_locServiceInstanceLock);

            // reader_writer_lock.scoped_lock lock(s_locServiceInstanceLock);

            if (s_singletonInstance == null)
            {
                s_singletonInstance = new LocalizationService(null);
            }
        }

        return s_singletonInstance;
    }

    /// <summary>
    /// Replace (or create) the single instance of this singleton class by one with the language passed as parameter
    /// </summary>
    /// <param name="language">RFC-5646 identifier of the language to use</param>
    /// <remarks>
    /// Should only be used for test purpose
    /// </remarks>
    void OverrideWithLanguage(string language)
    {
        s_singletonInstance = new LocalizationService(language);
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="overridedLanguage">RFC-5646 identifier of the language to use, if null, will use the current language of the system</param>
    public LocalizationService(string overridedLanguage)
    {
        m_isLanguageOverrided = overridedLanguage != null;
        m_language = m_isLanguageOverrided ? (overridedLanguage) : ApplicationLanguages.Languages[0];
        m_flowDirection = ResourceContext.GetForViewIndependentUse().QualifierValues["LayoutDirection"]
                          != "LTR"
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
        string localeName = (m_language);
        localeName += ".UTF8";

        // try
        // {
        //     // Convert string to string for locale
        //     int size_needed = WideCharToMultiByte(CP_UTF8, 0, &localeName[0], (int)localeName.size(), NULL, 0, NULL, NULL);
        //     string localeNameStr(size_needed, 0);
        //     WideCharToMultiByte(CP_UTF8, 0, &localeName[0], (int)localeName.size(), &localeNameStr[0], size_needed, NULL, NULL);
        //
        //     m_locale = locale(localeNameStr.data());
        // }
        // catch (...)
        // {
        //     m_locale = locale("");
        // }


        try
        {
            // In C#, we don't need the WideCharToMultiByte conversion since strings are already Unicode
            // We just need to create the CultureInfo with the language and UTF-8 encoding

            // Create CultureInfo from the locale name
            m_locale = new CultureInfo(m_language);

            // If you need to work with encodings specifically
            Encoding utf8Encoding = Encoding.UTF8;

            // Additional encoding-specific operations can go here if needed
        }
        catch (Exception)
        {
            // Fall back to current culture if there's an error
            m_locale = CultureInfo.CurrentCulture;
        }

        var resourceLoader = AppResourceProvider.GetInstance();
        m_fontFamilyOverride = resourceLoader.GetResourceString("LocalizedFontFamilyOverride");

        String reserved = "RESERVED_FOR_FONTLOC";

        m_overrideFontApiValues = ((m_fontFamilyOverride != null) && (m_fontFamilyOverride != reserved));
        if (m_overrideFontApiValues)
        {
            String localizedUICaptionFontSizeFactorOverride =
                resourceLoader.GetResourceString("LocalizedUICaptionFontSizeFactorOverride");
            String localizedUITextFontSizeFactorOverride =
                resourceLoader.GetResourceString("LocalizedUITextFontSizeFactorOverride");
            String localizedFontWeightOverride = resourceLoader.GetResourceString("LocalizedFontWeightOverride");

            // If any of the font overrides are modified then all of them need to be modified
            Debug.Assert(localizedFontWeightOverride != reserved);
            Debug.Assert(localizedUITextFontSizeFactorOverride != reserved);
            Debug.Assert(localizedUICaptionFontSizeFactorOverride != reserved);

            m_fontWeightOverride = ParseFontWeight(localizedFontWeightOverride);
            m_uiTextFontScaleFactorOverride = double.Parse(localizedUITextFontSizeFactorOverride);
            m_uiCaptionFontScaleFactorOverride = double.Parse(localizedUICaptionFontSizeFactorOverride);
        }

        m_fontGroup = new LanguageFontGroup(m_language);
    }

    FontWeight ParseFontWeight(String fontWeight)
    {
        string weight = fontWeight.ToLowerInvariant();
        // transform(weight.begin(), weight.end(), weight.begin(), towlower);
        // fontWeight = new  weight.c_str());

        if (weight == "black")
        {
            return FontWeights.Black;
        }
        else if (weight == "bold")
        {
            return FontWeights.Bold;
        }
        else if (weight == "extrablack")
        {
            return FontWeights.ExtraBlack;
        }
        else if (weight == "extrabold")
        {
            return FontWeights.ExtraBold;
        }
        else if (weight == "extralight")
        {
            return FontWeights.ExtraLight;
        }
        else if (weight == "light")
        {
            return FontWeights.Light;
        }
        else if (weight == "medium")
        {
            return FontWeights.Medium;
        }
        else if (weight == "norma")
        {
            return FontWeights.Normal;
        }
        else if (weight == "semibold")
        {
            return FontWeights.SemiBold;
        }
        else if (weight == "semilight")
        {
            return FontWeights.SemiLight;
        }
        else if (weight == "thin")
        {
            return FontWeights.Thin;
        }
        else
        {
            throw new ArgumentException(nameof(fontWeight));
        }
    }

    public FlowDirection GetFlowDirection()
    {
        return m_flowDirection;
    }

  public  bool IsRtlLayout()
    {
        return m_flowDirection == FlowDirection.RightToLeft;
    }

    public String GetLanguage()
    {
        return m_language;
    }

    bool GetOverrideFontApiValues()
    {
        return m_overrideFontApiValues;
    }

    public FontFamily GetLanguageFontFamilyForType(LanguageFontType fontType)
    {
        if (m_overrideFontApiValues)
        {
            return new FontFamily(m_fontFamilyOverride);
        }
        else
        {
            return new FontFamily(GetLanguageFont(fontType).FontFamily);
        }
    }

    LanguageFont GetLanguageFont(LanguageFontType fontType)
    {
        Debug.Assert(!m_overrideFontApiValues);
        Debug.Assert(m_fontGroup != null);

        switch (fontType)
        {
            case LanguageFontType.UIText:
                return m_fontGroup.UITextFont;
            case LanguageFontType.UICaption:
                return m_fontGroup.UICaptionFont;
            default:
                throw new ArgumentException(nameof(fontType));
        }
    }

    String GetFontFamilyOverride()
    {
        Debug.Assert(m_overrideFontApiValues);
        return m_fontFamilyOverride;
    }

    FontWeight GetFontWeightOverride()
    {
        Debug.Assert(m_overrideFontApiValues);
        return m_fontWeightOverride;
    }

    double GetFontScaleFactorOverride(LanguageFontType fontType)
    {
        Debug.Assert(m_overrideFontApiValues);

        switch (fontType)
        {
            case LanguageFontType.UIText:
                return m_uiTextFontScaleFactorOverride;
            case LanguageFontType.UICaption:
                return m_uiCaptionFontScaleFactorOverride;
            default:
                throw new AggregateException(nameof(fontType));
        }
    }

    static void OnFontTypePropertyChanged(DependencyObject target, LanguageFontType oldValue, LanguageFontType newValue)
    {
        UpdateFontFamilyAndSize(target);
    }

    static void OnFontWeightPropertyChanged(DependencyObject target, FontWeight oldValue, FontWeight newValue)
    {
        UpdateFontFamilyAndSize(target);
    }

    static void OnFontSizePropertyChanged(DependencyObject target, double oldValue, double newValue)
    {
        UpdateFontFamilyAndSize(target);
    }

    static void UpdateFontFamilyAndSize(DependencyObject target)
    {
        FontFamily fontFamily;
        FontWeight fontWeight = FontWeights.Normal;
        bool fOverrideFontWeight = false;
        double scaleFactor;

        var service = LocalizationService.GetInstance();
        var fontType = GetFontType(target);

        if (service.GetOverrideFontApiValues())
        {
            fontFamily = new FontFamily(service.GetFontFamilyOverride());
            scaleFactor = service.GetFontScaleFactorOverride(fontType) / 100.0;
            fontWeight = service.GetFontWeightOverride();
            fOverrideFontWeight = true;
        }
        else
        {
            var languageFont = service.GetLanguageFont(fontType);
            fontFamily = new FontFamily(languageFont.FontFamily);
            scaleFactor = languageFont.ScaleFactor / 100.0;
        }

        double sizeToUse = GetFontSize(target) * scaleFactor;

        var control = (Control)(target);
        if (control != null)
        {
            control.FontFamily = fontFamily;
            if (fOverrideFontWeight)
            {
                control.FontWeight = fontWeight;
            }

            if (sizeToUse != 0.0)
            {
                control.FontSize = sizeToUse;
            }
            else
            {
                control.ClearValue(Control.FontSizeProperty);
            }
        }
        else
        {
            var textBlock = (TextBlock)(target);
            if (textBlock != null)
            {
                textBlock.FontFamily = fontFamily;
                if (fOverrideFontWeight)
                {
                    textBlock.FontWeight = fontWeight;
                }

                if (sizeToUse != 0.0)
                {
                    textBlock.FontSize = sizeToUse;
                }
                else
                {
                    textBlock.ClearValue(TextBlock.FontSizeProperty);
                }
            }
            else
            {
                RichTextBlock richTextBlock = (RichTextBlock)(target);
                if (richTextBlock != null)
                {
                    richTextBlock.FontFamily = fontFamily;
                    if (fOverrideFontWeight)
                    {
                        richTextBlock.FontWeight = fontWeight;
                    }

                    if (sizeToUse != 0.0)
                    {
                        richTextBlock.FontSize = sizeToUse;
                    }
                    else
                    {
                        richTextBlock.ClearValue(RichTextBlock.FontSizeProperty);
                    }
                }
                else
                {
                    TextElement textElement = (TextElement)(target);
                    if (textElement != null)
                    {
                        textElement.FontFamily = fontFamily;
                        if (fOverrideFontWeight)
                        {
                            textElement.FontWeight = fontWeight;
                        }

                        if (sizeToUse != 0.0)
                        {
                            textElement.FontSize = sizeToUse;
                        }
                        else
                        {
                            textElement.ClearValue(TextElement.FontSizeProperty);
                        }
                    }
                }
            }
        }
    }

// If successful, returns a formatter that respects the user's regional format settings,
// as configured by running intl.cpl.
    public DecimalFormatter GetRegionalSettingsAwareDecimalFormatter()
    {
        IReadOnlyList<String> languageIdentifiers = GetLanguageIdentifiers();
        if (languageIdentifiers != null)
        {
            return new DecimalFormatter(languageIdentifiers, GlobalizationPreferences.HomeGeographicRegion);
        }

        return new DecimalFormatter();
    }

// If successful, returns a formatter that respects the user's regional format settings,
// as configured by running intl.cpl.
//
// This helper function creates a DateTimeFormatter with a TwentyFour hour clock
    public DateTimeFormatter GetRegionalSettingsAwareDateTimeFormatter(String format)
    {
        IReadOnlyList<String> languageIdentifiers = GetLanguageIdentifiers();
        if (languageIdentifiers == null)
        {
            languageIdentifiers = ApplicationLanguages.Languages;
        }

        return new DateTimeFormatter(format, languageIdentifiers);
    }

    // If successful, returns a formatter that respects the user's regional format settings,
    // as configured by running intl.cpl.
    public DateTimeFormatter GetRegionalSettingsAwareDateTimeFormatter(String format, String calendarIdentifier,
        String clockIdentifier)
    {
        IReadOnlyList<String> languageIdentifiers = GetLanguageIdentifiers();
        if (languageIdentifiers == null)
        {
            languageIdentifiers = ApplicationLanguages.Languages;
        }

        return new DateTimeFormatter(format, languageIdentifiers, GlobalizationPreferences.HomeGeographicRegion,
            calendarIdentifier, clockIdentifier);
    }

    public CurrencyFormatter GetRegionalSettingsAwareCurrencyFormatter()
    {
        String userCurrency =
            (GlobalizationPreferences.Currencies.Count > 0)
                ? GlobalizationPreferences.Currencies[0]
                : LocalizationServiceProperties.DefaultCurrencyCode;

        IReadOnlyList<String> languageIdentifiers = GetLanguageIdentifiers();
        if (languageIdentifiers == null)
        {
            languageIdentifiers = ApplicationLanguages.Languages;
        }

        var currencyFormatter = new CurrencyFormatter(userCurrency, languageIdentifiers,
            GlobalizationPreferences.HomeGeographicRegion);

        int fractionDigits = LocalizationSettings.GetInstance().GetCurrencyTrailingDigits();
        currencyFormatter.FractionDigits = fractionDigits;

        return currencyFormatter;
    }

    // IReadOnlyList<String> GetLanguageIdentifiers()
    // {
    //     WCHAR currentLocale[LOCALE_NAME_MAX_LENGTH] =  {
    //     }
    //     ;
    //     int result = GetUserDefaultLocaleName(currentLocale, LOCALE_NAME_MAX_LENGTH);
    //
    //     if (m_isLanguageOverrided)
    //     {
    //         var overridedLanguageList = new List<String>();
    //         overridedLanguageList.Append(m_language);
    //         return overridedLanguageList;
    //     }
    //
    //     if (result != 0)
    //     {
    //         // GetUserDefaultLocaleName may return an invalid bcp47 language tag with trailing non-BCP47 friendly characters,
    //         // which if present would start with an underscore, for example sort order
    //         // (see https://msdn.microsoft.com/en-us/library/windows/desktop/dd373814(v=vs.85).aspx).
    //         // Therefore, if there is an underscore in the locale name, trim all characters from the underscore onwards.
    //         WCHAR* underscore = wcschr(currentLocale, '_');
    //         if (underscore != null)
    //         {
    //             *underscore = '\0';
    //         }
    //
    //         String localeString = (currentLocale);
    //         // validate if the locale we have is valid
    //         // otherwise we fallback to the default.
    //         if (Language.IsWellFormed(localeString))
    //         {
    //             var languageList = new List<String>();
    //             languageList.Append(localeString);
    //             return languageList;
    //         }
    //     }
    //
    //     return null;
    // }

    /// <summary>
    /// Temporary replacement for the Win32 heavy localization stuff.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<string> GetLanguageIdentifiers()
    {
        // Handle overridden language case
        if (m_isLanguageOverrided)
        {
            return new List<string> { m_language };
        }

        try
        {
            // Get the current culture in a cross-platform way
            string localeString = m_locale.Name;

            // Remove any potential suffix after underscore if needed
            int underscorePos = localeString.IndexOf('_');
            if (underscorePos >= 0)
            {
                localeString = localeString.Substring(0, underscorePos);
            }

            // Validate if the locale is well-formed
            if (Language.IsWellFormed(localeString))
            {
                return new List<string> { localeString };
            }

            // Fallback to common language tag format if needed
            return new List<string> { m_locale.TwoLetterISOLanguageName };
        }
        catch (Exception)
        {
            // Fallback to invariant culture or a default
            return new List<string> { "en" }; // Default to English or another appropriate default
        }
    }
// Resources for the engine use numbers as keys. It's inconvenient, but also difficult to
        // change given that the engine heavily relies on perfect ordering of certain elements.
        // To compromise, we'll declare a map from engine resource key to automation name from the
        // standard project resources.
       static  List<(string, string)> s_parenEngineKeyResourceMap = new()
        {
            // Sine permutations
            ("67", "SineDegrees"),
            ("73", "SineRadians"),
            ("79", "SineGradians"),
            ("70", "InverseSineDegrees"),
            ("76", "InverseSineRadians"),
            ("82", "InverseSineGradians"),
            ("25", "HyperbolicSine"),
            ("85", "InverseHyperbolicSine"),

            // Cosine permutations
            ("68", "CosineDegrees"),
            ("74", "CosineRadians"),
            ("80", "CosineGradians"),
            ("71", "InverseCosineDegrees"),
            ("77", "InverseCosineRadians"),
            ("83", "InverseCosineGradians"),
            ("26", "HyperbolicCosine"),
            ("86", "InverseHyperbolicCosine"),

            // Tangent permutations
            ("69", "TangentDegrees"),
            ("75", "TangentRadians"),
            ("81", "TangentGradians"),
            ("72", "InverseTangentDegrees"),
            ("78", "InverseTangentRadians"),
            ("84", "InverseTangentGradians"),
            ("27", "HyperbolicTangent"),
            ("87", "InverseHyperbolicTangent"),

            // Secant permutations
            ("SecDeg", "SecantDegrees"),
            ("SecRad", "SecantRadians"),
            ("SecGrad", "SecantGradians"),
            ("InverseSecDeg", "InverseSecantDegrees"),
            ("InverseSecRad", "InverseSecantRadians"),
            ("InverseSecGrad", "InverseSecantGradians"),
            ("Sech", "HyperbolicSecant"),
            ("InverseSech", "InverseHyperbolicSecant"),

            // Cosecant permutations
            ("CscDeg", "CosecantDegrees"),
            ("CscRad", "CosecantRadians"),
            ("CscGrad", "CosecantGradians"),
            ("InverseCscDeg", "InverseCosecantDegrees"),
            ("InverseCscRad", "InverseCosecantRadians"),
            ("InverseCscGrad", "InverseCosecantGradians"),
            ("Csch", "HyperbolicCosecant"),
            ("InverseCsch", "InverseHyperbolicCosecant"),

            // Cotangent permutations
            ("CotDeg", "CotangentDegrees"),
            ("CotRad", "CotangentRadians"),
            ("CotGrad", "CotangentGradians"),
            ("InverseCotDeg", "InverseCotangentDegrees"),
            ("InverseCotRad", "InverseCotangentRadians"),
            ("InverseCotGrad", "InverseCotangentGradians"),
            ("Coth", "HyperbolicCotangent"),
            ("InverseCoth", "InverseHyperbolicCotangent"),

            // Miscellaneous Scientific functions
            ("94", "Factoria"),
            ("35", "DegreeMinuteSecond"),
            ("28", "NaturalLog"),
            ("91", "Square"),
            ("CubeRoot", "CubeRoot"),
            ("Abs", "AbsoluteValue")
        };



        static List<(string, string)> s_noParenEngineKeyResourceMap = new()
        {
            // Programmer mode functions
            ("9", "LeftShift"),
            ("10", "RightShift"),
            ("LogBaseY", "Logy"),

            // Y Root scientific function
            ("16", "YRoot")
        };

    public static Dictionary<string, string> GetTokenToReadableNameMap()
    {


        Dictionary<string, string> tokenToReadableNameMap = new();
        var resProvider = AppResourceProvider.GetInstance();

        string openParen = resProvider.GetCEngineString((s_openParenResourceKey));

        foreach (var keyPair in s_parenEngineKeyResourceMap)
        {
            string engineStr = resProvider.GetCEngineString((keyPair.Item1));
            string automationName = resProvider.GetResourceString((keyPair.Item2));

            tokenToReadableNameMap.Add(engineStr + openParen, automationName);
        }

        s_parenEngineKeyResourceMap.Clear();

        foreach (var keyPair in s_noParenEngineKeyResourceMap)
        {
            string engineStr = resProvider.GetCEngineString((keyPair.Item1));
            string automationName = resProvider.GetResourceString((keyPair.Item2));

            tokenToReadableNameMap.Add(engineStr, automationName);
        }

        s_noParenEngineKeyResourceMap.Clear();

        // Also replace hyphens with "minus"
        string minusText = resProvider.GetResourceString("minus");
        tokenToReadableNameMap.Add("-", minusText);

        return tokenToReadableNameMap;
    }

    public static String GetNarratorReadableToken(String rawToken)
    {
        var s_tokenToReadableNameMap = GetTokenToReadableNameMap();

        // var itr = s_tokenToReadableNameMap[rawToken];
        if (!s_tokenToReadableNameMap.TryGetValue(rawToken, out var itr)) //(itr == s_tokenToReadableNameMap.end())
        {
            return rawToken;
        }


        var openParen = AppResourceProvider.GetInstance().GetCEngineString((s_openParenResourceKey));
        return (itr) + " " + openParen;
    }

    public static String GetNarratorReadableString(String rastring)
    {
        string readableString = "";
        string asstring = rastring;
        foreach (var c in asstring)
        {
            readableString += GetNarratorReadableToken(new String(c, 1));
        }

        return (readableString);
    }

    public void Sort<T>(List<T> source, Func<T, string> keySelector)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (keySelector == null)
            throw new ArgumentNullException(nameof(keySelector));

        // Get the CompareInfo from the current culture
        CompareInfo compareInfo = m_locale.CompareInfo;

        // Sort the list in place
        source.Sort((x, y) =>
        {
            string keyX = keySelector(x);
            string keyY = keySelector(y);
            return compareInfo.Compare(keyX, keyY);
        });
    }

    public CultureInfo GetCurrentCultureInfo()
    {
        return m_locale;
    }
}
