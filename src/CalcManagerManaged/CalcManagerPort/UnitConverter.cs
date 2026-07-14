// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalcEngine;
using System.Diagnostics;
using System.Globalization;
using UnitConversionManager;

namespace UnitConversionManager;

public class UnitConverter : IUnitConverter
{
    IConverterDataLoader m_dataLoader;

    IConverterDataLoader? m_currencyDataLoader;

    IUnitConverterVMCallback? m_vmCallback;

    IViewModelCurrencyCallback? m_vmCurrencyCallback;

    IList<Category> m_categories = [];

    CategoryToUnitVectorMap m_categoryToUnits = [];

    UnitToUnitToConversionDataMap m_ratioMap = new();

    Category m_currentCategory = new();

    Unit m_fromType = Unit.EmptyUnit;

    Unit m_toType = Unit.EmptyUnit;

    wstring m_currentDisplay = "0";

    wstring m_returnDisplay = "";

    bool m_currentHasDecimal;

    bool m_returnHasDecimal;

    bool m_switchedActive;

    const uint32_t EXPECTEDSERIALIZEDCATEGORYTOKENCOUNT = 3U;

    const uint32_t EXPECTEDSERIALIZEDUNITTOKENCOUNT = 6U;

    const uint32_t MAXIMUMDIGITSALLOWED = 15U;

    const uint32_t OPTIMALDIGITSALLOWED = 7U;

    const wchar_t LEFTESCAPECHAR = '{';

    const wchar_t RIGHTESCAPECHAR = '}';

    const double OPTIMALDECIMALALLOWED = 1e-6; // pow(10, -1 * (OPTIMALDIGITSALLOWED - 1));

    const double MINIMUMDECIMALALLOWED = 1e-14; // pow(10, -1 * (MAXIMUMDIGITSALLOWED - 1));

    Dictionary<wchar_t, wstring> quoteConversions = new();

    Dictionary<wstring, wchar_t> unquoteConversions = new();

    /// <summary>
    /// Constructor, sets up all the variables and requires a configLoader
    /// </summary>
    /// <param name="dataLoader">An instance of the IConverterDataLoader interface which we use to read in category/unit names and conversion data</param>
    public UnitConverter(IConverterDataLoader dataLoader)
        : this(dataLoader, null)
    {
    }

    /// <summary>
    /// Constructor, sets up all the variables and requires two configLoaders
    /// </summary>
    /// <param name="dataLoader">An instance of the IConverterDataLoader interface which we use to read in category/unit names and conversion data</param>
    /// <param name="currencyDataLoader">An instance of the IConverterDataLoader interface, specialized for loading currency data from an internet service</param>
    public UnitConverter(IConverterDataLoader dataLoader, IConverterDataLoader? currencyDataLoader)
    {
        m_dataLoader = dataLoader;
        m_currencyDataLoader = currencyDataLoader;
        // declaring the delimiter character conversion map
        quoteConversions['|'] = "{p}";
        quoteConversions['['] = "{lc}";
        quoteConversions[']'] = "{rc}";
        quoteConversions[':'] = "{co}";
        quoteConversions[','] = "{cm}";
        quoteConversions[';'] = "{sc}";
        quoteConversions[LEFTESCAPECHAR] = "{lb}";
        quoteConversions[RIGHTESCAPECHAR] = "{rb}";
        unquoteConversions["{p}"] = '|';
        unquoteConversions["{lc}"] = '[';
        unquoteConversions["{rc}"] = ']';
        unquoteConversions["{co}"] = ':';
        unquoteConversions["{cm}"] = ',';
        unquoteConversions["{sc}"] = ';';
        unquoteConversions["{lb}"] = LEFTESCAPECHAR;
        unquoteConversions["{rb}"] = RIGHTESCAPECHAR;
        ClearValues();
        ResetCategoriesAndRatios();
    }

    public void Initialize()
    {
        m_dataLoader.LoadData();
    }

    bool CheckLoad()
    {
        if (m_categories.Count == 0)
        {
            ResetCategoriesAndRatios();
        }

        return m_categories.Count != 0;
    }

    /// <summary>
    /// Returns a list of the categories in use by this converter
    /// </summary>
    public IList<Category> GetCategories()
    {
        CheckLoad();
        return m_categories;
    }

    /// <summary>
    /// Sets the current category in use by this converter,
    /// and returns a list of unit types that exist under the given category.
    /// </summary>
    /// <param name="input">Category struct which we are setting</param>
    public CategorySelectionInitializer SetCurrentCategory(Category input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (m_currencyDataLoader != null && m_currencyDataLoader.SupportsCategory(input))
        {
            m_currencyDataLoader.LoadData();
        }

        IList<Unit> newUnitList = new List<Unit>();
        if (CheckLoad())
        {
            if (m_currentCategory.Id != input.Id)
            {
                foreach (var unit in m_categoryToUnits[m_currentCategory.Id])
                {
                    unit.IsConversionSource = (unit.Id == m_fromType.Id);
                    unit.IsConversionTarget = (unit.Id == m_toType.Id);
                }

                m_currentCategory = input;
                if (!m_currentCategory.SupportsNegative && m_currentDisplay.First() == '-')
                {
                    // TODO: Check this.
                    m_currentDisplay = m_currentDisplay.Remove(0, 1);
                }
            }

            newUnitList = m_categoryToUnits[input.Id];
        }

        InitializeSelectedUnits();
        return (newUnitList, m_fromType, m_toType);
    }

    /// <summary>
    /// Gets the category currently being used
    /// </summary>
    public Category GetCurrentCategory()
    {
        return m_currentCategory;
    }

    /// <summary>
    /// Sets the current unit types to be used, indicates a likely change in the
    /// display values, so we re-calculate and callback the updated values
    /// </summary>
    /// <param name="fromType">Unit struct which the user is modifying</param>
    /// <param name="toType">Unit struct we are converting to</param>
    public void SetCurrentUnitTypes(Unit fromType, Unit toType)
    {
        if (!CheckLoad())
        {
            return;
        }

        if (m_fromType != fromType)
        {
            m_switchedActive = true;
        }

        m_fromType = fromType;
        m_toType = toType;
        Calculate();

        UpdateCurrencySymbols();
    }

    /// <summary>
    /// Switches the active field, indicating that we are now entering data into
    /// what was originally the return field, and storing results into what was
    /// originally the current field. We swap appropriate values,
    /// but do not callback, as values have not changed.
    /// </summary>
    /// <param name="newValue">
    /// wstring representing the value user had in the field they've just activated.
    /// We use this to handle cases where the front-end may choose to trim more digits
    /// than we have been storing internally, in which case appending will not function
    /// as expected without the use of this parameter.
    /// </param>
    public void SwitchActive(wstring newValue)
    {
        if (!CheckLoad())
        {
            return;
        }

        (m_fromType, m_toType) = (m_toType, m_fromType);
        (m_currentHasDecimal, m_returnHasDecimal) = (m_returnHasDecimal, m_currentHasDecimal);

        m_returnDisplay = m_currentDisplay;
        m_currentDisplay = newValue;
        m_currentHasDecimal = (m_currentDisplay.IndexOf('.') != -1);
        m_switchedActive = true;

        if (m_currencyDataLoader != null && m_vmCurrencyCallback != null)
        {
            ICurrencyConverterDataLoader currencyDataLoader = (ICurrencyConverterDataLoader)m_currencyDataLoader;
            (wstring, wstring) currencyRatios = currencyDataLoader.GetCurrencyRatioEquality(m_fromType, m_toType);

            m_vmCurrencyCallback.CurrencyRatiosCallback(currencyRatios.Item1, currencyRatios.Item2);
        }
    }

    public bool IsSwitchedActive()
    {
        return m_switchedActive;
    }

    public wstring CategoryToString(Category c, wstring_view delimiter)
    {
        if (c is null)
        {
            throw new ArgumentNullException(nameof(c));
        }

        return Quote((c.Id.ToString(CultureInfo.InvariantCulture)))
               + (delimiter)
               + (Quote((c.SupportsNegative.ToString())))
               + (delimiter)
               + (Quote(c.Name))
               + (delimiter);
    }

    public static IList<wstring> StringToVector(wstring_view w, wstring_view delimiter, bool addRemainder = false)
    {
        if (w is null)
        {
            throw new ArgumentNullException(nameof(w));
        }

        if (delimiter is null)
        {
            throw new ArgumentNullException(nameof(delimiter));
        }

        var delimiterIndex = w.IndexOf(delimiter, StringComparison.Ordinal);
        var startIndex = 0;
        List<wstring> serializedTokens = new List<wstring>();
        while (delimiterIndex != -1)
        {
            serializedTokens.Add(w.Substring(startIndex, delimiterIndex - startIndex));
            startIndex = delimiterIndex + (int)(delimiter.Length);
            delimiterIndex = w.IndexOf(delimiter, startIndex, StringComparison.Ordinal);
        }

        if (addRemainder)
        {
            delimiterIndex = w.Length;
            serializedTokens.Add(w.Substring(startIndex, delimiterIndex - startIndex));
        }

        return serializedTokens;
    }

    wstring UnitToString(Unit u, wstring_view delimiter)
    {
        return Quote(u.Id.ToString(CultureInfo.InvariantCulture))
               + (delimiter)
               + (Quote(u.Name))
               + (delimiter)
               + (Quote(u.Abbreviation))
               + (delimiter)
               + (u.IsConversionSource.ToString())
               + (delimiter)
               + (u.IsConversionTarget.ToString())
               + (delimiter)
               + (u.IsWhimsical.ToString())
               + (delimiter);
    }

    Unit StringToUnit(wstring_view w)
    {
        IList<wstring> tokenList = StringToVector(w, ";");
        Debug.Assert(tokenList.Count == EXPECTEDSERIALIZEDUNITTOKENCOUNT);
        Unit serializedUnit = new Unit();
        serializedUnit.Id = int.Parse(Unquote(tokenList[0]), CultureInfo.InvariantCulture);
        serializedUnit.Name = Unquote(tokenList[1]);
        serializedUnit.AccessibleName = serializedUnit.Name;
        serializedUnit.Abbreviation = Unquote(tokenList[2]);
        serializedUnit.IsConversionSource = (tokenList[3] == "1");
        serializedUnit.IsConversionTarget = (tokenList[4] == "1");
        serializedUnit.IsWhimsical = (tokenList[5] == "1");
        return serializedUnit;
    }

    Category StringToCategory(wstring_view w)
    {
        IList<wstring> tokenList = StringToVector(w, ";");
        Debug.Assert(tokenList.Count == EXPECTEDSERIALIZEDCATEGORYTOKENCOUNT);
        Category serializedCategory = new();
        serializedCategory.Id = int.Parse(Unquote(tokenList[0]), CultureInfo.InvariantCulture);
        serializedCategory.SupportsNegative = (tokenList[1] == "1");
        serializedCategory.Name = Unquote(tokenList[2]);
        return serializedCategory;
    }

    /// <summary>
    /// De-Serializes the data in the converter from a string
    /// </summary>
    /// <param name="userPreferences">wstring_view holding the serialized data. If it does not have expected number of parameters, we will ignore it</param>
    public void RestoreUserPreferences(wstring_view userPreferences)
    {
        if (string.IsNullOrEmpty(userPreferences))
        {
            return;
        }

        IList<wstring> outerTokens = StringToVector(userPreferences, "|");
        if (outerTokens.Count != 3)
        {
            return;
        }

        var fromType = StringToUnit(outerTokens[0]);
        var toType = StringToUnit(outerTokens[1]);
        m_currentCategory = StringToCategory(outerTokens[2]);

        // Only restore from the saved units if they are valid in the current available units.
        if (m_categoryToUnits.TryGetValue(m_currentCategory.Id, out var curUnits))
        {
            if (curUnits.Contains(fromType)) //(find(curUnits.begin(), curUnits.end(), fromType) != curUnits.end())
            {
                m_fromType = fromType;
            }

            if (curUnits.Contains(toType)) //(find(curUnits.begin(), curUnits.end(), toType) != curUnits.end())
            {
                m_toType = toType;
            }
        }
    }

    /// <summary>
    /// Serializes the Category and Associated Units in the converter and returns it as a string
    /// </summary>
    public wstring SaveUserPreferences()
    {
        var delimiter = ";";
        var pipe = "|";
        return UnitToString(m_fromType, delimiter)
               + (pipe)
               + (UnitToString(m_toType, delimiter))
               + (pipe)
               + (CategoryToString(m_currentCategory, delimiter))
               + (pipe);
    }

    /// <summary>
    /// Sanitizes the input string, escape quoting any symbols we rely on for our delimiters, and returns the sanitized string.
    /// </summary>
    /// <param name="s">wstring_view to be sanitized</param>
    public string Quote(string s)
    {
        if (s is null)
        {
            throw new ArgumentNullException(nameof(s));
        }

        string quotedString = "";

        // Iterate over the delimiter characters we need to quote
        foreach (var ch in s)
        {
            if (quoteConversions.TryGetValue(ch, out var val))
            {
                quotedString += val;
            }
            else
            {
                quotedString += ch;
            }
        }

        return quotedString;
    }

    /// <summary>
    /// Unsanitizes the sanitized input string, returning it to its original contents before we had quoted it.
    /// </summary>
    /// <param name="s">wstring_view to be unsanitized</param>
    public string Unquote(string s)
    {
        if (s is null)
        {
            throw new ArgumentNullException(nameof(s));
        }

        string quotedSubString;
        string unquotedString = "";
        int cursor = 0;

        while (cursor < s.Length)
        {
            if (s[cursor] == LEFTESCAPECHAR)
            {
                quotedSubString = "";
                while (cursor < s.Length && s[cursor] != RIGHTESCAPECHAR)
                {
                    quotedSubString += s[cursor];
                    cursor++;
                }

                if (cursor == s.Length)
                {
                    // Badly formatted
                    break;
                }
                else
                {
                    quotedSubString += s[cursor];
                    unquotedString += unquoteConversions[quotedSubString];
                }
            }
            else
            {
                unquotedString += s[cursor];
            }

            cursor++;
        }

        return unquotedString;
    }

    /// <summary>
    /// Handles inputs to the converter from the view-model, corresponding to a given button or keyboard press
    /// </summary>
    /// <param name="command">Command enum representing the command that was entered</param>
    public void SendCommand(Command command)
    {
        if (!CheckLoad())
        {
            return;
        }

        // TODO: Localization of characters
        bool clearFront = false;
        bool clearBack = false;
        InitializeClearFlags(command, ref clearFront, ref clearBack);

        switch (command)
        {
            case Command.Zero:
                m_currentDisplay += '0';
                break;

            case Command.One:
                m_currentDisplay += '1';
                break;

            case Command.Two:
                m_currentDisplay += '2';
                break;

            case Command.Three:
                m_currentDisplay += '3';
                break;

            case Command.Four:
                m_currentDisplay += '4';
                break;

            case Command.Five:
                m_currentDisplay += '5';
                break;

            case Command.Six:
                m_currentDisplay += '6';
                break;

            case Command.Seven:
                m_currentDisplay += '7';
                break;

            case Command.Eight:
                m_currentDisplay += '8';
                break;

            case Command.Nine:
                m_currentDisplay += '9';
                break;

            case Command.DecimalSeparator:
                clearFront = false;
                clearBack = false;
                ProcessDecimalSeparatorCommand();
                break;

            case Command.Backspace:
                clearFront = false;
                clearBack = false;
                ProcessBackspaceCommand();
                break;

            case Command.Negate:
                clearFront = false;
                clearBack = false;
                ProcessNegateCommand();
                break;

            case Command.Clear:
                clearFront = false;
                clearBack = false;
                ClearValues();
                break;

            case Command.Reset:
                clearFront = false;
                clearBack = false;
                ClearValues();
                ResetCategoriesAndRatios();
                break;

            default:
                break;
        }

        if (clearFront)
        {
            m_currentDisplay = m_currentDisplay[1..]; // Remove the first character
        }

        if (clearBack)
        {
            m_currentDisplay = m_currentDisplay[..^1];
            NotifyMaxDigitsReached();
        }

        Calculate();
    }

    void InitializeClearFlags(Command command, ref bool clearFront, ref bool clearBack)
    {
        if (command != Command.Negate && m_switchedActive)
        {
            ClearValues();
            m_switchedActive = false;
            clearFront = true;
            clearBack = false;
        }
        else
        {
            clearFront = (m_currentDisplay == "0");
            clearBack =
                ((m_currentHasDecimal && m_currentDisplay.Length - 1 >= MAXIMUMDIGITSALLOWED)
                 || (!m_currentHasDecimal && m_currentDisplay.Length >= MAXIMUMDIGITSALLOWED));
        }
    }

    void ProcessDecimalSeparatorCommand()
    {
        if (!m_currentHasDecimal)
        {
            m_currentDisplay += '.';
            m_currentHasDecimal = true;
        }
    }

    void ProcessBackspaceCommand()
    {
        if ((m_currentDisplay.First() != '-' && m_currentDisplay.Length > 1) || m_currentDisplay.Length > 2)
        {
            if (m_currentDisplay.Last() == '.')
            {
                m_currentHasDecimal = false;
            }

            m_currentDisplay = m_currentDisplay[..^1];
        }
        else
        {
            m_currentDisplay = "0";
            m_currentHasDecimal = false;
        }
    }

    void ProcessNegateCommand()
    {
        if (m_currentCategory.SupportsNegative)
        {
            if (m_currentDisplay.First() == '-')
            {
                m_currentDisplay = m_currentDisplay[1..]; // Remove the first character
            }
            else
            {
                m_currentDisplay = m_currentDisplay.Insert(0, "-");
            }
        }
    }

    /// <summary>
    /// Sets the callback interface to send display update calls to
    /// </summary>
    /// <param name="newCallback">instance of IDisplayCallback interface that receives our update calls</param>
    public void SetViewModelCallback(IUnitConverterVMCallback newCallback)
    {
        m_vmCallback = newCallback;
        if (CheckLoad())
        {
            UpdateViewModel();
        }
    }

    public void SetViewModelCurrencyCallback(IViewModelCurrencyCallback newCallback)
    {
        m_vmCurrencyCallback = newCallback;

        var currencyDataLoader = GetCurrencyConverterDataLoader();
        if (currencyDataLoader != null)
        {
            currencyDataLoader.SetViewModelCallback(newCallback);
        }
    }

    public async Task<(bool, string)> RefreshCurrencyRatios()
    {
        ICurrencyConverterDataLoader? currencyDataLoader = GetCurrencyConverterDataLoader();
        Task<bool> loadDataResult;

        if (currencyDataLoader != null)
        {
            loadDataResult = currencyDataLoader.TryLoadDataFromWebOverrideAsync();
        }
        else
        {
            loadDataResult = Task.FromResult(false);
        }

        bool didLoad = await loadDataResult.ConfigureAwait(false);
        string timestamp = "";

        if (currencyDataLoader != null)
        {
            timestamp = currencyDataLoader.GetCurrencyTimestamp();
        }

        return (didLoad, timestamp);
    }

    ICurrencyConverterDataLoader? GetCurrencyConverterDataLoader()
    {
        return (ICurrencyConverterDataLoader?)(m_currencyDataLoader);
    }

    /// <summary>
    /// Converts a double value into another unit type
    /// </summary>
    /// <param name="value">double input value to convert</param>
    /// <param name="conversionData">offset and ratio to use</param>
    static double Convert(double value, ConversionData conversionData)
    {
        if (conversionData.OffsetFirst)
        {
            return (value + conversionData.Offset) * conversionData.Ratio;
        }
        else
        {
            return (value * conversionData.Ratio) + conversionData.Offset;
        }
    }

    /// <summary>
    /// Calculates the suggested values for the current display value and returns them as a vector
    /// </summary>
    List<(wstring, Unit)> CalculateSuggested()
    {
        if (m_currencyDataLoader != null && m_currencyDataLoader.SupportsCategory(m_currentCategory))
        {
            return new();
        }

        List<(wstring, Unit)> returnVector = [];
        List<SuggestedValueIntermediate> intermediateVector = [];
        List<SuggestedValueIntermediate> intermediateWhimsicalVector = [];
        var ratios = m_ratioMap.GetOrAdd(m_fromType);
        // Calculate converted values for every other unit type in this category, along with their magnitude
        foreach (var cur in ratios)
        {
            if (cur.Key != m_fromType && cur.Key != m_toType)
            {
                double convertedValue = Convert(double.Parse(m_currentDisplay, CultureInfo.InvariantCulture), cur.Value);
                var newEntry = new SuggestedValueIntermediate();
                newEntry.Magnitude = Math.Log10(convertedValue);
                newEntry.Value = convertedValue;
                newEntry.Type = cur.Key;
                if (newEntry.Type.IsWhimsical)
                    intermediateWhimsicalVector.Add(newEntry);
                else
                    intermediateVector.Add(newEntry);
            }
        }

        // Sort the resulting list by absolute magnitude, breaking ties by choosing the positive value

        intermediateVector.Sort((first, second) =>
        {
            if (Math.Abs(first.Magnitude) == Math.Abs(second.Magnitude))
            {
                return second.Magnitude.CompareTo(first.Magnitude); // Descending
            }
            else
            {
                return Math.Abs(first.Magnitude).CompareTo(Math.Abs(second.Magnitude)); // Ascending
            }
        });

        // Now that the list is sorted, iterate over it and populate the return vector with properly rounded and formatted return strings
        foreach (var entry in intermediateVector)
        {
            wstring roundedString;
            if (Math.Abs(entry.Value) < 100)
            {
                roundedString = NumberFormattingUtils.RoundSignificantDigits(entry.Value, 2U);
            }
            else if (Math.Abs(entry.Value) < 1000)
            {
                roundedString = NumberFormattingUtils.RoundSignificantDigits(entry.Value, 1U);
            }
            else
            {
                roundedString = NumberFormattingUtils.RoundSignificantDigits(entry.Value, 0U);
            }

            if (double.Parse(roundedString, CultureInfo.InvariantCulture) != 0.0 || m_currentCategory.SupportsNegative)
            {
                NumberFormattingUtils.TrimTrailingZeros(ref roundedString);
                returnVector.Add((roundedString, entry.Type));
            }
        }

        // The Whimsicals are determined differently
        // Sort the resulting list by absolute magnitude, breaking ties by choosing the positive value
        intermediateWhimsicalVector.Sort((first, second) =>
        {
            if (Math.Abs(first.Magnitude) == Math.Abs(second.Magnitude))
            {
                return second.Magnitude.CompareTo(first.Magnitude);
            }
            else
            {
                return Math.Abs(first.Magnitude).CompareTo(Math.Abs(second.Magnitude));
            }
        });

        // Now that the list is sorted, iterate over it and populate the return vector with properly rounded and formatted return strings
        List<(wstring, Unit)> whimsicalReturnVector = [];

        foreach (var entry in intermediateWhimsicalVector)
        {
            wstring roundedString;
            if (Math.Abs(entry.Value) < 100)
            {
                roundedString = NumberFormattingUtils.RoundSignificantDigits(entry.Value, 2U);
            }
            else if (Math.Abs(entry.Value) < 1000)
            {
                roundedString = NumberFormattingUtils.RoundSignificantDigits(entry.Value, 1U);
            }
            else
            {
                roundedString = NumberFormattingUtils.RoundSignificantDigits(entry.Value, 0U);
            }

            // How to work out which is the best whimsical value to add to the vector?
            if (double.Parse(roundedString, CultureInfo.InvariantCulture) != 0.0)
            {
                NumberFormattingUtils.TrimTrailingZeros(ref roundedString);
                whimsicalReturnVector.Add((roundedString, entry.Type));
            }
        }

        // Pickup the 'best' whimsical value - currently the first one
        if (whimsicalReturnVector.Count != 0)
        {
            returnVector.Add(whimsicalReturnVector.First());
        }

        return returnVector;
    }

    /// <summary>
    /// Resets categories and ratios
    /// </summary>
    public void ResetCategoriesAndRatios()
    {
        m_switchedActive = false;
        m_categories = m_dataLoader.GetOrderedCategories();
        if (m_categories.Count == 0)
        {
            return;
        }

        m_currentCategory = m_categories[0];

        m_categoryToUnits.Clear();
        m_ratioMap.Clear();
        bool readyCategoryFound = false;
        foreach (Category category in m_categories)
        {
            IConverterDataLoader activeDataLoader = GetDataLoaderForCategory(category);
            if (activeDataLoader == null)
            {
                // The data loader is different depending on the category, e.g. currency data loader
                // is different from the static data loader.
                // If there is no data loader for this category, continue.
                continue;
            }

            IList<Unit> units = activeDataLoader.GetOrderedUnits(category);
            m_categoryToUnits.Add(category.Id, units);

            // Just because the units are empty, doesn't mean the user can't select this category,
            // we just want to make sure we don't let an unready category be the default.
            if (units.Count != 0)
            {
                foreach (Unit u in units)
                {
                    m_ratioMap.Set(u, activeDataLoader.LoadOrderedRatios(u));
                }

                if (!readyCategoryFound)
                {
                    m_currentCategory = category;
                    readyCategoryFound = true;
                }
            }
        }

        InitializeSelectedUnits();
    }

    /// <summary>
    /// Sets the active data loader based on the input category.
    /// </summary>
    IConverterDataLoader GetDataLoaderForCategory(Category category)
    {
        if (m_currencyDataLoader != null && m_currencyDataLoader.SupportsCategory(category))
        {
            return m_currencyDataLoader;
        }
        else
        {
            return m_dataLoader;
        }
    }

    /// <summary>
    /// Sets the initial values for m_fromType and m_toType.
    /// This is an internal helper method as opposed to SetCurrentUnits
    /// which is for external use by clients.
    /// If we fail to set units, we will fallback to the EMPTY_UNIT.
    /// </summary>
    void InitializeSelectedUnits()
    {
        if (m_categoryToUnits.Count == 0)
        {
            return;
        }

        if (!m_categoryToUnits.TryGetValue(m_currentCategory.Id, out var itr))
        {
            return;
        }

        IList<Unit> curUnits = itr;
        if (curUnits.Count != 0)
        {
            // Units may already have been initialized through RestoreUserPreferences().
            // Check if they have been, and if so, do not override restored units.
            bool isFromUnitValid = m_fromType != Unit.EmptyUnit && curUnits.Contains(m_fromType);
            bool isToUnitValid = m_toType != Unit.EmptyUnit && curUnits.Contains(m_toType);

            if (isFromUnitValid && isToUnitValid)
            {
                return;
            }

            bool conversionSourceSet = false;
            bool conversionTargetSet = false;
            foreach (Unit cur in curUnits)
            {
                if (!conversionSourceSet && cur.IsConversionSource && !isFromUnitValid)
                {
                    m_fromType = cur;
                    conversionSourceSet = true;
                }

                if (!conversionTargetSet && cur.IsConversionTarget && !isToUnitValid)
                {
                    m_toType = cur;
                    conversionTargetSet = true;
                }

                if (conversionSourceSet && conversionTargetSet)
                {
                    return;
                }
            }
        }

        m_fromType = Unit.EmptyUnit;
        m_toType = Unit.EmptyUnit;
    }

    /// <summary>
    /// Resets the value fields to 0
    /// </summary>
    void ClearValues()
    {
        m_currentHasDecimal = false;
        m_returnHasDecimal = false;
        m_currentDisplay = "0";
    }

    /// <summary>
    /// Checks if either unit is EMPTY_UNIT.
    /// </summary>
    bool AnyUnitIsEmpty()
    {
        return m_fromType == Unit.EmptyUnit || m_toType == Unit.EmptyUnit;
    }

    /// <summary>
    /// Calculates a new return value based on the current display value
    /// </summary>
    public void Calculate()
    {
        if (AnyUnitIsEmpty())
        {
            m_returnDisplay = m_currentDisplay;
            m_returnHasDecimal = m_currentHasDecimal;
            NumberFormattingUtils.TrimTrailingZeros(ref m_returnDisplay);
            UpdateViewModel();
            return;
        }

        var conversionTable = m_ratioMap.GetOrAdd(m_fromType);
        ConversionData targetConversion = conversionTable[m_toType];
        if (targetConversion.Ratio == 1.0 && targetConversion.Offset == 0.0)
        {
            m_returnDisplay = m_currentDisplay;
            m_returnHasDecimal = m_currentHasDecimal;
            NumberFormattingUtils.TrimTrailingZeros(ref m_returnDisplay);
        }
        else
        {
            double currentValue = double.Parse(m_currentDisplay, CultureInfo.InvariantCulture);
            double returnValue = Convert(currentValue, conversionTable[m_toType]);

            var isCurrencyConverter = m_currencyDataLoader != null &&
                                      m_currencyDataLoader.SupportsCategory(this.m_currentCategory);
            if (isCurrencyConverter)
            {
                // We don't need to trim the value when it's a currency.
                m_returnDisplay = NumberFormattingUtils.RoundSignificantDigits(returnValue, MAXIMUMDIGITSALLOWED);
                NumberFormattingUtils.TrimTrailingZeros(ref m_returnDisplay);
            }
            else
            {
                uint numPreDecimal = NumberFormattingUtils.GetNumberDigitsWholeNumberPart(returnValue);
                if (numPreDecimal > MAXIMUMDIGITSALLOWED ||
                    (returnValue != 0 && Math.Abs(returnValue) < MINIMUMDECIMALALLOWED))
                {
                    m_returnDisplay = NumberFormattingUtils.ToScientificNumber(returnValue);
                }
                else
                {
                    uint currentNumberSignificantDigits = NumberFormattingUtils.GetNumberDigits(m_currentDisplay);
                    uint precision;
                    if (Math.Abs(returnValue) < OPTIMALDECIMALALLOWED)
                    {
                        precision = MAXIMUMDIGITSALLOWED;
                    }
                    else
                    {
                        // Fewer digits are needed following the decimal if the number is large,
                        // we calculate the number of decimals necessary based on the number of digits in the integer part.
                        var numberDigits = Math.Max(OPTIMALDIGITSALLOWED,
                            Math.Min(MAXIMUMDIGITSALLOWED, currentNumberSignificantDigits));
                        precision = numberDigits > numPreDecimal ? numberDigits - numPreDecimal : 0;
                    }

                    m_returnDisplay = NumberFormattingUtils.RoundSignificantDigits(returnValue, precision);
                    NumberFormattingUtils.TrimTrailingZeros(ref m_returnDisplay);
                }

                m_returnHasDecimal = (m_returnDisplay.IndexOf('.') != -1);
            }
        }

        UpdateViewModel();
    }

    void UpdateCurrencySymbols()
    {
        if (m_currencyDataLoader != null && m_vmCurrencyCallback != null)
        {
            var currencyDataLoader = (ICurrencyConverterDataLoader)m_currencyDataLoader;
            var currencySymbols = currencyDataLoader.GetCurrencySymbols(m_fromType, m_toType);
            var currencyRatios = currencyDataLoader.GetCurrencyRatioEquality(m_fromType, m_toType);

            m_vmCurrencyCallback.CurrencySymbolsCallback(currencySymbols.Item1, currencySymbols.Item2);
            m_vmCurrencyCallback.CurrencyRatiosCallback(currencyRatios.Item1, currencyRatios.Item2);
        }
    }

    void NotifyMaxDigitsReached()
    {
        m_vmCallback?.MaxDigitsReached();
    }

    void UpdateViewModel()
    {
        m_vmCallback?.DisplayCallback(m_currentDisplay, m_returnDisplay);
        m_vmCallback?.SuggestedValueCallback(CalculateSuggested());
    }
}
