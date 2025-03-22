// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#define TESTING

#include <gtest/gtest.h>
#include <memory>
#include <vector>
#include <string>

// You'll need to include the necessary headers here
#include "CalculatorManager.h"
#include "UnitConverter.h"
#include "CalculatorHistory.h"
#include "ResourceProvider.h"


using namespace UnitConversionManager;
using namespace std;

namespace UnitConverterUnitTests
{
    void SetUnitParams(Unit* type, int id, wstring name, wstring abbreviation, bool conversionSource, bool conversionTarget, bool isWhimsical)
    {
        type->id = id;
        type->name = name;
        type->abbreviation = abbreviation;
        type->isConversionSource = conversionSource;
        type->isConversionTarget = conversionTarget;
        type->isWhimsical = isWhimsical;
    }

    void SetCategoryParams(Category* type, int id, wstring name, bool supportsNegative)
    {
        type->id = id;
        type->name = name;
        type->supportsNegative = supportsNegative;
    }

    void SetConversionDataParams(ConversionData* type, double ratio, double offset, bool offsetFirst)
    {
        type->ratio = ratio;
        type->offset = offset;
        type->offsetFirst = offsetFirst;
    }

    class TestUnitConverterConfigLoader : public IConverterDataLoader
    {
    public:
        TestUnitConverterConfigLoader()
                : m_loadDataCallCount(0)
        {
            Category c1, c2;
            SetCategoryParams(&c1, 1, L"Length", true);
            SetCategoryParams(&c2, 2, L"Weight", false);
            m_categories.push_back(c1);
            m_categories.push_back(c2);

            Unit u1, u2, u3, u4;
            SetUnitParams(&u1, 1, L"Inches", L"In", true, true, false);
            SetUnitParams(&u2, 2, L"Feet", L"Ft", false, false, false);
            SetUnitParams(&u3, 3, L"Pounds", L"Lb", true, true, false);
            SetUnitParams(&u4, 4, L"Kilograms", L"Kg", false, false, false);

            vector<Unit> c1units = vector<Unit>();
            vector<Unit> c2units = vector<Unit>();
            c1units.push_back(u1);
            c1units.push_back(u2);
            c2units.push_back(u3);
            c2units.push_back(u4);

            m_units[c1.id] = c1units;
            m_units[c2.id] = c2units;

            unordered_map<Unit, ConversionData, UnitHash> unit1Map = unordered_map<Unit, ConversionData, UnitHash>();
            unordered_map<Unit, ConversionData, UnitHash> unit2Map = unordered_map<Unit, ConversionData, UnitHash>();
            unordered_map<Unit, ConversionData, UnitHash> unit3Map = unordered_map<Unit, ConversionData, UnitHash>();
            unordered_map<Unit, ConversionData, UnitHash> unit4Map = unordered_map<Unit, ConversionData, UnitHash>();

            ConversionData conversion1, conversion2, conversion3, conversion4, conversion5;
            SetConversionDataParams(&conversion1, 1.0, 0, false);
            SetConversionDataParams(&conversion2, 0.08333333333333333333333333333333, 0, false);
            SetConversionDataParams(&conversion3, 12.0, 0, false);
            SetConversionDataParams(&conversion4, 0.453592, 0, false);
            SetConversionDataParams(&conversion5, 2.20462, 0, false);

            // Setting the conversion ratios for testing
            unit1Map[u1] = conversion1;
            unit1Map[u2] = conversion2;

            unit2Map[u1] = conversion3;
            unit2Map[u2] = conversion1;

            unit3Map[u3] = conversion1;
            unit3Map[u4] = conversion4;

            unit4Map[u3] = conversion5;
            unit4Map[u4] = conversion1;

            m_ratioMaps[u1] = unit1Map;
            m_ratioMaps[u2] = unit2Map;
            m_ratioMaps[u3] = unit3Map;
            m_ratioMaps[u4] = unit4Map;
        }

        void LoadData()
        {
            m_loadDataCallCount++;
        }

        vector<Category> GetOrderedCategories()
        {
            return m_categories;
        }

        vector<Unit> GetOrderedUnits(const Category& category)
        {
            return m_units[category.id];
        }

        unordered_map<Unit, ConversionData, UnitHash> LoadOrderedRatios(const Unit& u)
        {
            return m_ratioMaps[u];
        }

        bool SupportsCategory(const Category& /*target*/)
        {
            return true;
        }

        uint m_loadDataCallCount;

    private:
        vector<Category> m_categories;
        CategoryToUnitVectorMap m_units;
        UnitToUnitToConversionDataMap m_ratioMaps;
    };

    class TestUnitConverterVMCallback : public IUnitConverterVMCallback
    {
    public:
        void Reset()
        {
            m_maxDigitsReachedCallCount = 0;
        }

        void DisplayCallback(const wstring& from, const wstring& to) override
        {
            m_lastFrom = from;
            m_lastTo = to;
        }

        void SuggestedValueCallback(const vector<tuple<wstring, Unit>>& suggestedValues) override
        {
            m_lastSuggested = suggestedValues;
        }

        void MaxDigitsReached() override
        {
            m_maxDigitsReachedCallCount++;
        }

        int GetMaxDigitsReachedCallCount()
        {
            return m_maxDigitsReachedCallCount;
        }

        bool CheckDisplayValues(wstring from, wstring to)
        {
            return (from == m_lastFrom && to == m_lastTo);
        }

        bool CheckSuggestedValues(vector<tuple<wstring, Unit>> suggested)
        {
            if (suggested.size() != m_lastSuggested.size())
            {
                return false;
            }
            bool returnValue = true;
            for (unsigned int i = 0; i < suggested.size(); i++)
            {
                if (suggested[i] != m_lastSuggested[i])
                {
                    returnValue = false;
                    break;
                }
            }
            return returnValue;
        }

        wstring m_lastTo;
    private:
        wstring m_lastFrom;
        vector<tuple<wstring, Unit>> m_lastSuggested;
        int m_maxDigitsReachedCallCount = 0;
    };

    // Declare this class as a test fixture
    class UnitConverterTest : public ::testing::Test
    {
    protected:
        void SetUp() override
        {
            s_testVMCallback = std::make_shared<TestUnitConverterVMCallback>();
            s_xmlLoader = std::make_shared<TestUnitConverterConfigLoader>();
            s_unitConverter = std::make_shared<UnitConverter>(s_xmlLoader);
            s_unitConverter->SetViewModelCallback(s_testVMCallback);
            SetCategoryParams(&s_testLength, 1, L"Length", true);
            SetCategoryParams(&s_testWeight, 2, L"Weight", false);
            SetUnitParams(&s_testInches, 1, L"Inches", L"In", true, true, false);
            SetUnitParams(&s_testFeet, 2, L"Feet", L"Ft", false, false, false);
            SetUnitParams(&s_testPounds, 3, L"Pounds", L"Lb", true, true, false);
            SetUnitParams(&s_testKilograms, 4, L"Kilograms", L"Kg", false, false, false);
        }

        void TearDown() override
        {
            s_unitConverter->SendCommand(Command::Reset);
            s_testVMCallback->Reset();
        }

        void ExecuteCommands(vector<Command> commands)
        {
            for (size_t i = 0; i < commands.size() && commands[i] != Command::None; i++)
            {
                s_unitConverter->SendCommand(commands[i]);
            }
        }

        static shared_ptr<UnitConverter> s_unitConverter;
        static shared_ptr<TestUnitConverterConfigLoader> s_xmlLoader;
        static shared_ptr<TestUnitConverterVMCallback> s_testVMCallback;
        static Category s_testLength;
        static Category s_testWeight;
        static Unit s_testInches;
        static Unit s_testFeet;
        static Unit s_testPounds;
        static Unit s_testKilograms;
    };

    // Initialize static members
    shared_ptr<UnitConverter> UnitConverterTest::s_unitConverter;
    shared_ptr<TestUnitConverterConfigLoader> UnitConverterTest::s_xmlLoader;
    shared_ptr<TestUnitConverterVMCallback> UnitConverterTest::s_testVMCallback;
    Category UnitConverterTest::s_testLength;
    Category UnitConverterTest::s_testWeight;
    Unit UnitConverterTest::s_testInches;
    Unit UnitConverterTest::s_testFeet;
    Unit UnitConverterTest::s_testPounds;
    Unit UnitConverterTest::s_testKilograms;

    // Test ctor/initialization states
    TEST_F(UnitConverterTest, UnitConverterTestInit)
    {
        EXPECT_EQ((uint)0, s_xmlLoader->m_loadDataCallCount); // shouldn't have initialized the loader yet
        s_unitConverter->Initialize();
        EXPECT_EQ((uint)1, s_xmlLoader->m_loadDataCallCount); // now we should have loaded
    }

    // Verify a basic input command stream.'3', '2', '.', '0'
    TEST_F(UnitConverterTest, UnitConverterTestBasic)
    {
        tuple<wstring, Unit> test1[] = { tuple<wstring, Unit>(wstring(L"0.25"), s_testFeet) };
        tuple<wstring, Unit> test2[] = { tuple<wstring, Unit>(wstring(L"2.5"), s_testFeet) };

        s_unitConverter->SendCommand(Command::Three);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"3"), wstring(L"3")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test1), end(test1))));
        s_unitConverter->SendCommand(Command::Zero);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"30"), wstring(L"30")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test2), end(test2))));
        s_unitConverter->SendCommand(Command::Decimal);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"30."), wstring(L"30")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test2), end(test2))));
        s_unitConverter->SendCommand(Command::Zero);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"30.0"), wstring(L"30")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test2), end(test2))));
    }

    // Verify a basic copy paste steam. '20.43' with backspace button pressed
    TEST_F(UnitConverterTest, UnitConverterTestBackspaceBasic)
    {
        s_unitConverter->SendCommand(Command::Two);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Decimal);
        s_unitConverter->SendCommand(Command::Four);
        s_unitConverter->SendCommand(Command::Three);
        s_unitConverter->SendCommand(Command::Backspace);

        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"20.4"), wstring(L"20.4")));
        s_unitConverter->SendCommand(Command::Backspace);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"20."), wstring(L"20")));
        s_unitConverter->SendCommand(Command::Backspace);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"20"), wstring(L"20")));
        s_unitConverter->SendCommand(Command::Backspace);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"2"), wstring(L"2")));
        s_unitConverter->SendCommand(Command::Backspace);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"0"), wstring(L"0")));
    }

    // Verify a basic copy paste steam. '20.43' with clear button pressed
    TEST_F(UnitConverterTest, UnitConverterTestClear)
    {
        s_unitConverter->SendCommand(Command::Two);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Decimal);
        s_unitConverter->SendCommand(Command::Four);
        s_unitConverter->SendCommand(Command::Three);
        s_unitConverter->SendCommand(Command::Clear);

        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"0"), wstring(L"0")));
    }

    // Check the getter functions
    TEST_F(UnitConverterTest, UnitConverterTestGetters)
    {
        Category test1[] = { s_testLength, s_testWeight };
        Unit test2[] = { s_testInches, s_testFeet };

        EXPECT_TRUE(s_unitConverter->GetCategories() == vector<Category>(begin(test1), end(test1)));
        EXPECT_TRUE(get<0>(s_unitConverter->SetCurrentCategory(test1[0])) == vector<Unit>(begin(test2), end(test2)));
    }

    // Test getting category after it has been set.
    TEST_F(UnitConverterTest, UnitConverterTestGetCategory)
    {
        s_unitConverter->SetCurrentCategory(s_testWeight);
        EXPECT_TRUE(s_unitConverter->GetCurrentCategory() == s_testWeight);
    }

    // Test switching of unit types
    TEST_F(UnitConverterTest, UnitConverterTestUnitTypeSwitching)
    {
        // Enter 57 into the from field, then switch focus to the to field (making it the new from field)
        s_unitConverter->SendCommand(Command::Five);
        s_unitConverter->SendCommand(Command::Seven);
        s_unitConverter->SwitchActive(wstring(L"57"));
        // Now set unit conversion to go from kilograms to pounds
        s_unitConverter->SetCurrentCategory(s_testWeight);
        s_unitConverter->SetCurrentUnitTypes(s_testKilograms, s_testPounds);
        s_unitConverter->SendCommand(Command::Five);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"5"), wstring(L"11.0231")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>()));
    }

    // Test input escaping
    TEST_F(UnitConverterTest, UnitConverterTestQuote)
    {
        constexpr wstring_view input1 = L"Weight";
        constexpr wstring_view output1 = L"Weight";
        constexpr wstring_view input2 = L"{p}Weig;[ht|";
        constexpr wstring_view output2 = L"{lb}p{rb}Weig{sc}{lc}ht{p}";
        constexpr wstring_view input3 = L"{{{t;s}}},:]";
        constexpr wstring_view output3 = L"{lb}{lb}{lb}t{sc}s{rb}{rb}{rb}{cm}{co}{rc}";
        EXPECT_TRUE(UnitConverter::Quote(input1) == output1);
        EXPECT_TRUE(UnitConverter::Quote(input2) == output2);
        EXPECT_TRUE(UnitConverter::Quote(input3) == output3);
    }

    // Test output unescaping
    TEST_F(UnitConverterTest, UnitConverterTestUnquote)
    {
        constexpr wstring_view input1 = L"Weight";
        constexpr wstring_view input2 = L"{p}Weig;[ht|";
        constexpr wstring_view input3 = L"{{{t;s}}},:]";
        EXPECT_TRUE(UnitConverter::Unquote(input1) == input1);
        EXPECT_TRUE(UnitConverter::Unquote(UnitConverter::Quote(input1)) == input1);
        EXPECT_TRUE(UnitConverter::Unquote(UnitConverter::Quote(input2)) == input2);
        EXPECT_TRUE(UnitConverter::Unquote(UnitConverter::Quote(input3)) == input3);
    }

    // Test backspace commands
    TEST_F(UnitConverterTest, UnitConverterTestBackspace)
    {
        tuple<wstring, Unit> test1[] = { tuple<wstring, Unit>(wstring(L"13.66"), s_testKilograms) };
        tuple<wstring, Unit> test2[] = { tuple<wstring, Unit>(wstring(L"13.65"), s_testKilograms) };
        tuple<wstring, Unit> test3[] = { tuple<wstring, Unit>(wstring(L"13.61"), s_testKilograms) };
        tuple<wstring, Unit> test4[] = { tuple<wstring, Unit>(wstring(L"1.36"), s_testKilograms) };

        s_unitConverter->SetCurrentCategory(s_testWeight);
        s_unitConverter->SetCurrentUnitTypes(s_testPounds, s_testPounds);
        s_unitConverter->SendCommand(Command::Three);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Decimal);
        s_unitConverter->SendCommand(Command::One);
        s_unitConverter->SendCommand(Command::Two);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"30.12"), wstring(L"30.12")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test1), end(test1))));
        s_unitConverter->SendCommand(Command::Backspace);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"30.1"), wstring(L"30.1")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test2), end(test2))));
        s_unitConverter->SendCommand(Command::Backspace);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"30."), wstring(L"30")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test3), end(test3))));
        s_unitConverter->SendCommand(Command::Backspace);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"30"), wstring(L"30")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test3), end(test3))));
        s_unitConverter->SendCommand(Command::Backspace);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"3"), wstring(L"3")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test4), end(test4))));
        s_unitConverter->SendCommand(Command::Backspace);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"0"), wstring(L"0")));
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>()));
    }

    // Test large values
    TEST_F(UnitConverterTest, UnitConverterTestScientificInputs)
    {
        s_unitConverter->SetCurrentCategory(s_testWeight);
        s_unitConverter->SetCurrentUnitTypes(s_testPounds, s_testKilograms);
        s_unitConverter->SendCommand(Command::Decimal);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::Zero);
        s_unitConverter->SendCommand(Command::One);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"0.00000000000001"), wstring(L"4.535920e-15")));
        s_unitConverter->SwitchActive(wstring(L"4.535920e-15"));
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        s_unitConverter->SendCommand(Command::Nine);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"999999999999999"), wstring(L"2.204620e+15")));
        s_unitConverter->SwitchActive(wstring(L"2.20463e+15"));
        s_unitConverter->SendCommand(Command::One);
        s_unitConverter->SendCommand(Command::Two);
        s_unitConverter->SendCommand(Command::Three);
        s_unitConverter->SendCommand(Command::Four);
        s_unitConverter->SendCommand(Command::Five);
        s_unitConverter->SendCommand(Command::Six);
        s_unitConverter->SendCommand(Command::Seven);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"1234567"), wstring(L"559989.7")));
        s_unitConverter->SwitchActive(wstring(L"559989.7"));
        s_unitConverter->SendCommand(Command::One);
        s_unitConverter->SendCommand(Command::Two);
        s_unitConverter->SendCommand(Command::Three);
        s_unitConverter->SendCommand(Command::Four);
        s_unitConverter->SendCommand(Command::Five);
        s_unitConverter->SendCommand(Command::Six);
        s_unitConverter->SendCommand(Command::Seven);
        s_unitConverter->SendCommand(Command::Eight);
        EXPECT_TRUE(s_testVMCallback->CheckDisplayValues(wstring(L"12345678"), wstring(L"27217529")));
    }

    // Test large values
    TEST_F(UnitConverterTest, UnitConverterTestSupplementaryResultRounding)
    {
        tuple<wstring, Unit> test1[] = { tuple<wstring, Unit>(wstring(L"27.75"), s_testFeet) };
        tuple<wstring, Unit> test2[] = { tuple<wstring, Unit>(wstring(L"277.8"), s_testFeet) };
        tuple<wstring, Unit> test3[] = { tuple<wstring, Unit>(wstring(L"2778"), s_testFeet) };
        s_unitConverter->SendCommand(Command::Three);
        s_unitConverter->SendCommand(Command::Three);
        s_unitConverter->SendCommand(Command::Three);
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test1), end(test1))));
        s_unitConverter->SendCommand(Command::Three);
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test2), end(test2))));
        s_unitConverter->SendCommand(Command::Three);
        EXPECT_TRUE(s_testVMCallback->CheckSuggestedValues(vector<tuple<wstring, Unit>>(begin(test3), end(test3))));
    }

    TEST_F(UnitConverterTest, UnitConverterTestMaxDigitsReached)
    {
        ExecuteCommands({ Command::One,
                          Command::Two,
                          Command::Three,
                          Command::Four,
                          Command::Five,
                          Command::Six,
                          Command::Seven,
                          Command::Eight,
                          Command::Nine,
                          Command::One,
                          Command::Zero,
                          Command::One,
                          Command::One,
                          Command::One,
                          Command::Two });

        EXPECT_EQ(0, s_testVMCallback->GetMaxDigitsReachedCallCount());

        ExecuteCommands({ Command::One });

        EXPECT_EQ(1, s_testVMCallback->GetMaxDigitsReachedCallCount());
    }

    TEST_F(UnitConverterTest, UnitConverterTestMaxDigitsReached_LeadingDecimal)
    {
        ExecuteCommands({ Command::Zero,
                          Command::Decimal,
                          Command::One,
                          Command::Two,
                          Command::Three,
                          Command::Four,
                          Command::Five,
                          Command::Six,
                          Command::Seven,
                          Command::Eight,
                          Command::Nine,
                          Command::One,
                          Command::Zero,
                          Command::One,
                          Command::One,
                          Command::One });

        EXPECT_EQ(0, s_testVMCallback->GetMaxDigitsReachedCallCount());

        ExecuteCommands({ Command::Two });

        EXPECT_EQ(1, s_testVMCallback->GetMaxDigitsReachedCallCount());
    }

    TEST_F(UnitConverterTest, UnitConverterTestMaxDigitsReached_TrailingDecimal)
    {
        ExecuteCommands({ Command::One,
                          Command::Two,
                          Command::Three,
                          Command::Four,
                          Command::Five,
                          Command::Six,
                          Command::Seven,
                          Command::Eight,
                          Command::Nine,
                          Command::One,
                          Command::Zero,
                          Command::One,
                          Command::One,
                          Command::One,
                          Command::Two,
                          Command::Decimal });

        EXPECT_EQ(0, s_testVMCallback->GetMaxDigitsReachedCallCount());

        ExecuteCommands({ Command::One });

        EXPECT_EQ(1, s_testVMCallback->GetMaxDigitsReachedCallCount());
    }

    TEST_F(UnitConverterTest, UnitConverterTestMaxDigitsReached_MultipleTimes)
    {
        ExecuteCommands({ Command::One,
                          Command::Two,
                          Command::Three,
                          Command::Four,
                          Command::Five,
                          Command::Six,
                          Command::Seven,
                          Command::Eight,
                          Command::Nine,
                          Command::One,
                          Command::Zero,
                          Command::One,
                          Command::One,
                          Command::One,
                          Command::Two });

        EXPECT_EQ(0, s_testVMCallback->GetMaxDigitsReachedCallCount());

        for (auto count = 1; count <= 10; count++)
        {
            ExecuteCommands({ Command::Three });

            EXPECT_EQ(count, s_testVMCallback->GetMaxDigitsReachedCallCount());
        }
    }
} /* namespace UnitConverterUnitTests */


namespace UnitConverterUnitTests
{
    // Helper struct to hold conversion test parameters
    struct ConversionTestCase {
        wstring inputValue;
        wstring expectedFromDisplay;
        wstring expectedToDisplay;
        wstring fromUnitName;
        wstring toUnitName;
    };

    // Main test fixture that supports parameterized tests
    class UnitConverterParameterizedTest : public ::testing::TestWithParam<ConversionTestCase>
    {
    protected:
        void SetUp() override
        {
            m_testVMCallback = std::make_shared<TestUnitConverterVMCallback>();
            m_xmlLoader = std::make_shared<TestUnitConverterConfigLoader>();
            m_unitConverter = std::make_shared<UnitConverter>(m_xmlLoader);
            m_unitConverter->SetViewModelCallback(m_testVMCallback);
            SetCategoryParams(&m_testLength, 1, L"Length", true);
            SetCategoryParams(&m_testWeight, 2, L"Weight", false);
            SetUnitParams(&m_testInches, 1, L"Inches", L"In", true, true, false);
            SetUnitParams(&m_testFeet, 2, L"Feet", L"Ft", false, false, false);
            SetUnitParams(&m_testPounds, 3, L"Pounds", L"Lb", true, true, false);
            SetUnitParams(&m_testKilograms, 4, L"Kilograms", L"Kg", false, false, false);
        }

        void TearDown() override
        {
            m_unitConverter->SendCommand(Command::Reset);
            m_testVMCallback->Reset();
        }

        // Helper method to handle potential differences in string representation
        wstring NormalizeOutput(const wstring& output)
        {
            // Check if the string contains a decimal point
            if (output.find(L'.') != wstring::npos)
            {
                // Try to parse as a double
                try {
                    double value = stod(output);

                    // For values close to whole numbers, the display might round them
                    if (fabs(value - round(value)) < 1e-10)
                    {
                        return to_wstring(static_cast<long long>(round(value)));
                    }

                    // For very small or large values, handle differently
                    if (fabs(value) < 1e-10 || fabs(value) > 1e10)
                    {
                        // In C++, format this with scientific notation
                        wchar_t buffer[50];
                        swprintf(buffer, 50, L"%.6e", value);
                        return wstring(buffer);
                    }
                }
                catch (...) {
                    // If parsing fails, return the original
                }
            }

            // Return the original string if no normalization applied
            return output;
        }

        // Helper method to check if two values are close enough accounting for rounding
        wstring CloseEnough(const wstring& expectedValue)
        {
            try {
                double value = stod(expectedValue);
                // Allow for slight rounding differences
                double roundedValue = round(value * 100000.0) / 100000.0; // Round to 5 decimal places
                return to_wstring(roundedValue);
            }
            catch (...) {
                // If parsing fails, return the original
                return expectedValue;
            }
        }

        shared_ptr<UnitConverter> m_unitConverter;
        shared_ptr<TestUnitConverterConfigLoader> m_xmlLoader;
        shared_ptr<TestUnitConverterVMCallback> m_testVMCallback;
        Category m_testLength;
        Category m_testWeight;
        Unit m_testInches;
        Unit m_testFeet;
        Unit m_testPounds;
        Unit m_testKilograms;
    };

    // Parameterized test for basic unit conversion logic
    TEST_P(UnitConverterParameterizedTest, TestUnitConversionLogic)
    {
        auto params = GetParam();

        // Setup - determine which category we're testing based on the units
        Category category;
        Unit fromUnit;
        Unit toUnit;

        if (params.fromUnitName == L"Inches" || params.fromUnitName == L"Feet")
        {
            category = m_testLength;
            fromUnit = params.fromUnitName == L"Inches" ? m_testInches : m_testFeet;
            toUnit = params.toUnitName == L"Inches" ? m_testInches : m_testFeet;
        }
        else
        {
            category = m_testWeight;
            fromUnit = params.fromUnitName == L"Pounds" ? m_testPounds : m_testKilograms;
            toUnit = params.toUnitName == L"Pounds" ? m_testPounds : m_testKilograms;
        }

        // Reset calculator state from any previous tests
        m_unitConverter->SendCommand(Command::Reset);
        m_testVMCallback->Reset();

        // Set the category and unit types
        m_unitConverter->SetCurrentCategory(category);
        m_unitConverter->SetCurrentUnitTypes(fromUnit, toUnit);

        // Start with a clean slate to ensure proper testing
        m_unitConverter->SendCommand(Command::Clear);

        // Enter the input value by switching active (which simulates pasting a value)
        m_unitConverter->SwitchActive(params.inputValue);

        // Calculate if needed (the SwitchActive should trigger calculation automatically)
        m_unitConverter->Calculate();

        // Verify the display values are as expected
        EXPECT_TRUE(m_testVMCallback->CheckDisplayValues(
                params.expectedFromDisplay,
                params.expectedToDisplay));
    }

    // Define test cases
    INSTANTIATE_TEST_SUITE_P(
            UnitConversionTests,
            UnitConverterParameterizedTest,
            ::testing::Values(
                  ConversionTestCase{L"1", L"1", L"12", L"Inches", L"Feet"},                   // 1 foot = 12 inches
                ConversionTestCase{L"12", L"12", L"1", L"Feet", L"Inches"},                  // 12 inches = 1 foot
                ConversionTestCase{L"0", L"0", L"0", L"Inches", L"Feet"},                    // Zero test
                 ConversionTestCase{L"1.5", L"1.5", L"18", L"Inches", L"Feet"},               // Decimal test
                   ConversionTestCase{L"100", L"100", L"8.333333", L"Feet", L"Inches"},          // Larger value test
                  ConversionTestCase{L"0.0833333", L"0.0833333", L"0.9999996", L"Inches", L"Feet"},        // Small value test
                   ConversionTestCase{L"1", L"1", L"2.20462", L"Pounds", L"Kilograms"},        // 1 pound = 0.453592 kg
                  ConversionTestCase{L"2.20462", L"2.20462", L"0.999998", L"Kilograms", L"Pounds"},   // 1 kg = 2.20462 pounds
                 ConversionTestCase{L"-10", L"-10", L"-22.0462", L"Pounds", L"Kilograms"},    // Negative value test for Weight
                   ConversionTestCase{L"-10", L"-10", L"-120", L"Inches", L"Feet"}              // Negative value test for Length
            )
    );

    // Test for extensive conversion scenarios
    TEST_F(UnitConverterParameterizedTest, TestExtensiveConversionScenarios)
    {
        // Test a range of values to ensure conversion works properly
        m_unitConverter->SetCurrentCategory(m_testWeight);
        m_unitConverter->SetCurrentUnitTypes(m_testKilograms,m_testPounds);

        // Test very small value
        m_unitConverter->SendCommand(Command::Clear);
        m_unitConverter->SwitchActive(L"0.0001");
        m_unitConverter->Calculate();
        EXPECT_TRUE(m_testVMCallback->CheckDisplayValues(L"0.0001", L"0.0000454"));

        // Test normal value
        m_unitConverter->SendCommand(Command::Reset);
        m_unitConverter->SwitchActive(L"100");
        EXPECT_TRUE(m_testVMCallback->CheckDisplayValues(L"100", L"45.3592"));

        // Test large value
        m_unitConverter->SendCommand(Command::Reset);
        m_unitConverter->SwitchActive(L"1000000");
        EXPECT_TRUE(m_testVMCallback->CheckDisplayValues(L"1000000", L"453592"));

        // Test compound conversion (chain multiple conversions)
        // Convert lb → kg then immediately kg → lb should give back original value (approximately)
        m_unitConverter->SendCommand(Command::Reset);
        m_unitConverter->SetCurrentUnitTypes(m_testPounds, m_testKilograms);
        m_unitConverter->SwitchActive(L"123.456");

        // Get the first conversion result
        wstring kgResult = m_testVMCallback->m_lastTo;

        // Now convert it back
        m_unitConverter->SetCurrentUnitTypes(m_testKilograms, m_testPounds);
        m_unitConverter->SwitchActive(kgResult);

        // The result should be close to the original value (accounting for rounding errors)
        wstring lbResult = m_testVMCallback->m_lastTo;
        double originalValue = 123.456;
        double finalValue = stod(lbResult);

        // Allow for a small error margin due to rounding in conversions
        EXPECT_TRUE(fabs(originalValue - finalValue) < 0.01)
                            << "Round-trip conversion error: " << originalValue << " → " << kgResult.c_str() << " → " << finalValue;

        // Test with length units to ensure multiple unit types work
        m_unitConverter->SendCommand(Command::Reset);
        m_unitConverter->SetCurrentCategory(m_testLength);
        m_unitConverter->SetCurrentUnitTypes(m_testInches, m_testFeet);
        m_unitConverter->SwitchActive(L"36");
        EXPECT_TRUE(m_testVMCallback->CheckDisplayValues(L"36", L"3"));
    }
} /* namespace UnitConverterUnitTests */