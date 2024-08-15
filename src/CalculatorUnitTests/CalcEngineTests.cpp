#define TESTING

#include <gtest/gtest.h>
#include <memory>
#include <vector>
#include <string>

// You'll need to include the necessary headers here
#include "CalculatorManager.h"
#include "CalculatorHistory.h"
#include "ResourceProvider.h"

using namespace std;
// Uncomment these when you have the actual headers
using namespace CalculationManager;

static constexpr size_t MAX_HISTORY_SIZE = 20;

class CalcEngineTests : public ::testing::Test {
protected:
    void SetUp() override {
        m_resourceProvider = make_shared<ResourceProvider>();
        m_history = make_shared<CalculatorHistory>(MAX_HISTORY_SIZE);
        CCalcEngine::InitialOneTimeOnlySetup(*(m_resourceProvider.get()));
        m_calcEngine = make_unique<CCalcEngine>(
                false /* Respect Order of Operations */,
                false /* Set to Integer Mode */,
                m_resourceProvider.get(),
                nullptr,
                m_history);
    }

    void TearDown() override {
        m_resourceProvider = nullptr;
        m_history = nullptr;
        m_calcEngine = nullptr;
    }

    unique_ptr<CCalcEngine> m_calcEngine;
    shared_ptr<IResourceProvider> m_resourceProvider;
    shared_ptr<CalculatorHistory> m_history;
};

TEST_F(CalcEngineTests, TestGroupDigitsPerRadix) {
    // Empty/Error cases
    EXPECT_TRUE(m_calcEngine->GroupDigitsPerRadix(L"", 10).empty()) << "Verify grouping empty string returns empty string.";
    EXPECT_EQ(L"12345678", m_calcEngine->GroupDigitsPerRadix(L"12345678", 9)) << "Verify grouping on invalid base returns original string";

    // Octal
    EXPECT_EQ(L"1 234 567", m_calcEngine->GroupDigitsPerRadix(L"1234567", 8)) << "Verify grouping in octal.";
    EXPECT_EQ(L"123", m_calcEngine->GroupDigitsPerRadix(L"123", 8)) << "Verify minimum grouping in octal.";

    // Binary/Hexadecimal
    EXPECT_EQ(L"12 3456 7890", m_calcEngine->GroupDigitsPerRadix(L"1234567890", 2)) << "Verify grouping in binary.";
    EXPECT_EQ(L"1234", m_calcEngine->GroupDigitsPerRadix(L"1234", 2)) << "Verify minimum grouping in binary.";
    EXPECT_EQ(L"12 3456 7890", m_calcEngine->GroupDigitsPerRadix(L"1234567890", 16)) << "Verify grouping in hexadecimal.";
    EXPECT_EQ(L"1234", m_calcEngine->GroupDigitsPerRadix(L"1234", 16)) << "Verify minimum grouping in hexadecimal.";

    // Decimal
    EXPECT_EQ(L"1,234,567,890", m_calcEngine->GroupDigitsPerRadix(L"1234567890", 10)) << "Verify grouping in base10.";
    EXPECT_EQ(L"1,234,567.89", m_calcEngine->GroupDigitsPerRadix(L"1234567.89", 10)) << "Verify grouping in base10 with decimal.";
    EXPECT_EQ(L"1,234,567e89", m_calcEngine->GroupDigitsPerRadix(L"1234567e89", 10)) << "Verify grouping in base10 with exponent.";
    EXPECT_EQ(L"1,234,567.89e5", m_calcEngine->GroupDigitsPerRadix(L"1234567.89e5", 10)) << "Verify grouping in base10 with decimal and exponent.";
    EXPECT_EQ(L"-123,456,789", m_calcEngine->GroupDigitsPerRadix(L"-123456789", 10)) << "Verify grouping in base10 with negative.";
}


TEST_F(CalcEngineTests, TestIsNumberInvalid) {
    // Binary Number Checks
    vector<wstring> validBinStrs{ L"0", L"1", L"0011", L"1100" };
    vector<wstring> invalidBinStrs{ L"2", L"A", L"0.1" };
    for (wstring const& str : validBinStrs) {
        EXPECT_EQ(0, m_calcEngine->IsNumberInvalid(str, 0, 0, 2 /* Binary */));
    }
    for (wstring const& str : invalidBinStrs) {
        EXPECT_EQ(IDS_ERR_UNK_CH, m_calcEngine->IsNumberInvalid(str, 0, 0, 2 /* Binary */));
    }

    // Octal Number Checks
    vector<wstring> validOctStrs{ L"0", L"7", L"01234567", L"76543210" };
    vector<wstring> invalidOctStrs{ L"8", L"A", L"0.7" };
    for (wstring const& str : validOctStrs) {
        EXPECT_EQ(0, m_calcEngine->IsNumberInvalid(str, 0, 0, 8 /* Octal */));
    }
    for (wstring const& str : invalidOctStrs) {
        EXPECT_EQ(IDS_ERR_UNK_CH, m_calcEngine->IsNumberInvalid(str, 0, 0, 8 /* Octal */));
    }

    // Hexadecimal Number Checks
    vector<wstring> validHexStrs{ L"0", L"F", L"0123456789ABCDEF", L"FEDCBA9876543210" };
    vector<wstring> invalidHexStrs{ L"G", L"abcdef", L"x", L"0.1" };
    for (wstring const& str : validHexStrs) {
        EXPECT_EQ(0, m_calcEngine->IsNumberInvalid(str, 0, 0, 16 /* Hex */));
    }
    for (wstring const& str : invalidHexStrs) {
        EXPECT_EQ(IDS_ERR_UNK_CH, m_calcEngine->IsNumberInvalid(str, 0, 0, 16 /* Hex */));
    }

    // Decimal Number Checks
    // Special case errors: long exponent, long mantissa
    wstring longExp(L"1e12345");
    EXPECT_EQ(0, m_calcEngine->IsNumberInvalid(longExp, 5 /* Max exp length */, 100, 10 /* Decimal */));
    EXPECT_EQ(IDS_ERR_INPUT_OVERFLOW, m_calcEngine->IsNumberInvalid(longExp, 4 /* Max exp length */, 100, 10 /* Decimal */));

    vector<wstring> longMantStrs{ L"10000", L"10.000", L"0000012345", L"123.45", L"0.00123", L"0.12345", L"-123.45e678" };
    for (wstring const& str : longMantStrs) {
        EXPECT_EQ(0, m_calcEngine->IsNumberInvalid(str, 100, 5 /* Max mantissa length */, 10 /* Decimal */));
    }
    for (wstring const& str : longMantStrs) {
        EXPECT_EQ(IDS_ERR_INPUT_OVERFLOW, m_calcEngine->IsNumberInvalid(str, 100, 4 /* Max mantissa length */, 10 /* Decimal */));
    }

    // Regex matching
    vector<wstring> validDecStrs{ L"+1", L"-1", L"1", L"-", L"", L"1234567890", L"1.0", L"-.", L"1.",
                                  L"0.0", L"0.123456", L"1e", L"1.e", L"-e", L"1e+12345", L"1e-12345",
                                  L"1e123", L"-123.456e+789" };
    vector<wstring> invalidDecStrs{ L"x123", L"123-", L"1e1.2", L"1-e2" };
    for (wstring const& str : validDecStrs) {
        EXPECT_EQ(0, m_calcEngine->IsNumberInvalid(str, 100, 100, 10 /* Dec */));
    }
    for (wstring const& str : invalidDecStrs) {
        EXPECT_EQ(IDS_ERR_UNK_CH, m_calcEngine->IsNumberInvalid(str, 100, 100, 10 /* Dec */));
    }
}

TEST_F(CalcEngineTests, TestDigitGroupingStringToGroupingVector) {
    const vector<uint32_t> emptyVector{};
    EXPECT_EQ(emptyVector, CCalcEngine::DigitGroupingStringToGroupingVector(L"")) << "Verify empty grouping";

    const vector<uint32_t> simpleGrouping{1};
    EXPECT_EQ(simpleGrouping, CCalcEngine::DigitGroupingStringToGroupingVector(L"1")) << "Verify simple grouping";

    const vector<uint32_t> standardGrouping{3, 0};
    EXPECT_EQ(standardGrouping, CCalcEngine::DigitGroupingStringToGroupingVector(L"3;0")) << "Verify standard grouping";

    const vector<uint32_t> expandedNonRepeatingGrouping{3, 0, 0};
    EXPECT_EQ(expandedNonRepeatingGrouping, CCalcEngine::DigitGroupingStringToGroupingVector(L"3;0;0")) << "Verify expanded non-repeating grouping";

    const vector<uint32_t> longGrouping{5, 3, 2, 4, 6};
    EXPECT_EQ(longGrouping, CCalcEngine::DigitGroupingStringToGroupingVector(L"5;3;2;4;6")) << "Verify long grouping";

    const vector<uint32_t> largeGrouping{15, 15, 15, 0};
    EXPECT_EQ(largeGrouping, CCalcEngine::DigitGroupingStringToGroupingVector(L"15;15;15;0")) << "Verify large grouping";

    const vector<uint32_t> oversizeGrouping{4, 7, 0};
    EXPECT_EQ(oversizeGrouping, CCalcEngine::DigitGroupingStringToGroupingVector(L"4;16;7;25;0")) << "Verify we ignore oversize grouping";

    constexpr wstring_view nonRepeatingGrouping = L"3;0;0";
    constexpr wstring_view repeatingGrouping = nonRepeatingGrouping.substr(0, 3);
    const vector<uint32_t> repeatingGroupingResult{3, 0};
    EXPECT_EQ(repeatingGroupingResult, CCalcEngine::DigitGroupingStringToGroupingVector(repeatingGrouping)) << "Verify we don't go past the end of wstring_view range";
}

TEST_F(CalcEngineTests, TestGroupDigits) {
    EXPECT_EQ(L"1234567", m_calcEngine->GroupDigits(L"", { 3, 0 }, L"1234567", false)) << "Verify handling of empty delimiter.";
    EXPECT_EQ(L"1234567", m_calcEngine->GroupDigits(L",", {}, L"1234567", false)) << "Verify handling of empty grouping.";
    EXPECT_EQ(L"1,234,567", m_calcEngine->GroupDigits(L",", { 3, 0 }, L"1234567", false)) << "Verify standard digit grouping.";
    EXPECT_EQ(L"1 234 567", m_calcEngine->GroupDigits(L" ", { 3, 0 }, L"1234567", false)) << "Verify delimiter change.";
    EXPECT_EQ(L"1|||234|||567", m_calcEngine->GroupDigits(L"|||", { 3, 0 }, L"1234567", false)) << "Verify long delimiter.";
    EXPECT_EQ(L"12,345e67", m_calcEngine->GroupDigits(L",", { 3, 0 }, L"12345e67", false)) << "Verify respect of exponent.";
    EXPECT_EQ(L"12,345.67", m_calcEngine->GroupDigits(L",", { 3, 0 }, L"12345.67", false)) << "Verify respect of decimal.";
    EXPECT_EQ(L"1,234.56e7", m_calcEngine->GroupDigits(L",", { 3, 0 }, L"1234.56e7", false)) << "Verify respect of exponent and decimal.";
    EXPECT_EQ(L"-1,234,567", m_calcEngine->GroupDigits(L",", { 3, 0 }, L"-1234567", true)) << "Verify negative number grouping.";

    // Test various groupings
    EXPECT_EQ(L"1234567890123456", m_calcEngine->GroupDigits(L",", { 0, 0 }, L"1234567890123456", false)) << "Verify no grouping.";
    EXPECT_EQ(L"1234567890123,456", m_calcEngine->GroupDigits(L",", { 3 }, L"1234567890123456", false)) << "Verify non-repeating grouping.";
    EXPECT_EQ(L"1234567890123,456", m_calcEngine->GroupDigits(L",", { 3, 0, 0 }, L"1234567890123456", false)) << "Verify expanded form non-repeating grouping.";
    EXPECT_EQ(L"12,34,56,78,901,23456", m_calcEngine->GroupDigits(L",", { 5, 3, 2, 0 }, L"1234567890123456", false)) << "Verify multigroup with repeating grouping.";
    EXPECT_EQ(L"1234,5678,9012,3456", m_calcEngine->GroupDigits(L",", { 4, 0 }, L"1234567890123456", false)) << "Verify repeating non-standard grouping.";
    EXPECT_EQ(L"123456,78,901,23456", m_calcEngine->GroupDigits(L",", { 5, 3, 2 }, L"1234567890123456", false)) << "Verify multigroup non-repeating grouping.";
    EXPECT_EQ(L"123456,78,901,23456", m_calcEngine->GroupDigits(L",", { 5, 3, 2, 0, 0 }, L"1234567890123456", false)) << "Verify expanded form multigroup non-repeating grouping.";
}