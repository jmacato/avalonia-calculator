/**
 * CalcEngine_CWrapper.cpp
 * Implementation of C-compatible wrapper for the Microsoft Calculator Engine
 */

#include "CalcEngine_CWrapper.h"
#include "CalculatorManager.h"
#include "Command.h"
#include "Header Files/Rational.h"
#include "Header Files/RationalMath.h"
#include "CalculatorResource.h"
#include <stdexcept>
#include <vector>
#include <memory>
#include <string>
#include <iostream>
#include <codecvt>
#include <locale>

using namespace CalculationManager;
using namespace CalcEngine;

// UTF-8 conversion helpers
std::string WideToUtf8(const std::wstring& wide) {
    std::wstring_convert<std::codecvt_utf8<wchar_t>, wchar_t> converter;
    return converter.to_bytes(wide);
}

std::wstring Utf8ToWide(const std::string& utf8) {
    std::wstring_convert<std::codecvt_utf8<wchar_t>, wchar_t> converter;
    return converter.from_bytes(utf8);
}

// Define the resource provider callback type
typedef char* (*ResourceProviderCallback)(const char* resourceId, void* context);

// Wrapper structs to hold C++ objects
struct CalcManager_t {
    std::unique_ptr<CalculatorManager> manager;
    CalcDisplayCallback displayCallback;
    CalcHistoryCallback historyCallback;
    ResourceProviderCallback resourceCallback;
    CalcSetIsInErrorCallback isInErrorCallback;
    CalcSetExpressionDisplayCallback expressionDisplayCallback;
    CalcSetParenthesisNumberCallback parenthesisNumberCallback;
    CalcOnNoRightParenAddedCallback noRightParenCallback;
    CalcMaxDigitsReachedCallback maxDigitsCallback;
    CalcBinaryOperatorReceivedCallback binaryOpCallback;
    CalcSetMemorizedNumbersCallback memorizedNumbersCallback;
    CalcMemoryItemChangedCallback memoryItemChangedCallback;
    CalcInputChangedCallback inputChangedCallback;
    void* context;
};

struct CalcRational_t {
    Rational value;
};

struct CalcHistoryItem_t {
    std::shared_ptr<HISTORYITEM> item;
};

struct CalcHistoryItems_t {
    std::vector<std::shared_ptr<HISTORYITEM>> items;
};

struct CalcCommandSnapshot_t {
    // Store our own copy of the command vector since GetDisplayCommandsSnapshot returns by value
    std::vector<std::shared_ptr<IExpressionCommand>> commands;
};

class DisplayCallbackAdapter : public ICalcDisplay {
public:
    // Define callback types for each method
    typedef void (*SetIsInErrorCallback)(bool isError, void* context);
    typedef void (*SetExpressionDisplayCallback)(ExpressionToken* tokens, long tokenCount, void* context);

    typedef void (*SetParenthesisNumberCallback)(unsigned int count, void* context);
    typedef void (*OnNoRightParenAddedCallback)(void* context);
    typedef void (*MaxDigitsReachedCallback)(void* context);
    typedef void (*BinaryOperatorReceivedCallback)(void* context);
    typedef void (*SetMemorizedNumbersCallback)(const char** numbers, int count, void* context);
    typedef void (*MemoryItemChangedCallback)(unsigned int indexOfMemory, void* context);
    typedef void (*InputChangedCallback)(void* context);

    // Constructor with all callbacks
    DisplayCallbackAdapter(
            CalcDisplayCallback displayCallback,
            CalcHistoryCallback historyCallback,
            SetIsInErrorCallback isInErrorCallback,
            SetExpressionDisplayCallback expressionDisplayCallback,
            SetParenthesisNumberCallback parenthesisNumberCallback,
            OnNoRightParenAddedCallback noRightParenCallback,
            MaxDigitsReachedCallback maxDigitsCallback,
            BinaryOperatorReceivedCallback binaryOpCallback,
            SetMemorizedNumbersCallback memorizedNumbersCallback,
            MemoryItemChangedCallback memoryItemCallback,
            InputChangedCallback inputChangedCallback,
            void* context)
            : m_displayCallback(displayCallback)
            , m_historyCallback(historyCallback)
            , m_isInErrorCallback(isInErrorCallback)
            , m_expressionDisplayCallback(expressionDisplayCallback)
            , m_parenthesisNumberCallback(parenthesisNumberCallback)
            , m_noRightParenCallback(noRightParenCallback)
            , m_maxDigitsCallback(maxDigitsCallback)
            , m_binaryOpCallback(binaryOpCallback)
            , m_memorizedNumbersCallback(memorizedNumbersCallback)
            , m_memoryItemCallback(memoryItemCallback)
            , m_inputChangedCallback(inputChangedCallback)
            , m_context(context) {}

    void SetPrimaryDisplay(const std::wstring& displayString, bool isError) override {
        if (m_displayCallback) {
            // Convert wide string to UTF-8
            std::string utf8DisplayString = WideToUtf8(displayString);
            m_displayCallback(utf8DisplayString.c_str(), isError, m_context);
        }
    }

    void SetIsInError(bool isError) override {
        if (m_isInErrorCallback) {
            m_isInErrorCallback(isError, m_context);
        }
    }

    void SetExpressionDisplay(
            std::shared_ptr<std::vector<std::pair<std::wstring, int>>> const& tokens,
            std::shared_ptr<std::vector<std::shared_ptr<IExpressionCommand>>> const& commands) override {
        if (m_expressionDisplayCallback && tokens && !tokens->empty()) {
            // Create array of tokens for C# to consume
            std::vector<ExpressionToken> tokenArray;
            tokenArray.reserve(tokens->size());

            // Keep copies of strings so they don't get destroyed
            std::vector<std::unique_ptr<char[]>> stringCopies;
            stringCopies.reserve(tokens->size());

            for (const auto& token : *tokens) {
                ExpressionToken t;

                // Convert the wide string to UTF-8
                std::string utf8String = WideToUtf8(token.first);
                size_t len = utf8String.length();
                auto strCopy = std::make_unique<char[]>(len + 1);

                std::copy(utf8String.c_str(), utf8String.c_str() + len + 1, strCopy.get());

                t.text = strCopy.get();
                t.type = token.second;

                tokenArray.push_back(t);
                stringCopies.push_back(std::move(strCopy));
            }

            // Call the callback with the array
            m_expressionDisplayCallback(tokenArray.data(), tokenArray.size(), m_context);

            // stringCopies will be destroyed after function returns
        }
    }

    void SetParenthesisNumber(unsigned int count) override {
        if (m_parenthesisNumberCallback) {
            m_parenthesisNumberCallback(count, m_context);
        }
    }

    void OnNoRightParenAdded() override {
        if (m_noRightParenCallback) {
            m_noRightParenCallback(m_context);
        }
    }

    void MaxDigitsReached() override {
        if (m_maxDigitsCallback) {
            m_maxDigitsCallback(m_context);
        }
    }

    void BinaryOperatorReceived() override {
        if (m_binaryOpCallback) {
            m_binaryOpCallback(m_context);
        }
    }

    void OnHistoryItemAdded(unsigned int addedItemIndex) override {
        if (m_historyCallback) {
            m_historyCallback(addedItemIndex, m_context);
        }
    }


    void SetMemorizedNumbers(const std::vector<std::wstring>& memorizedNumbers) override {
        if (m_memorizedNumbersCallback) {
            // Convert all wide strings to UTF-8 strings and keep them alive
            std::vector<std::string> utf8Strings;
            std::vector<const char*> charPtrs;

            for (const auto& num : memorizedNumbers) {
                utf8Strings.push_back(WideToUtf8(num));
                charPtrs.push_back(utf8Strings.back().c_str());
            }

            // Create an array of pointers to newly allocated strings that will be freed by the C# side
            const char** stringArray = new const char*[utf8Strings.size()];
            for (size_t i = 0; i < utf8Strings.size(); i++) {
                size_t len = utf8Strings[i].length();
                char* strCopy = new char[len + 1];
                std::memcpy(strCopy, utf8Strings[i].c_str(), len);
                strCopy[len] = '\0';
                stringArray[i] = strCopy;
            }

            // Call the callback
            m_memorizedNumbersCallback(stringArray, static_cast<int>(utf8Strings.size()), m_context);

            // Clean up the array (but not the strings themselves - they'll be freed by C#)
            delete[] stringArray;
        }
    }

    void MemoryItemChanged(unsigned int indexOfMemory) override {
        if (m_memoryItemCallback) {
            m_memoryItemCallback(indexOfMemory, m_context);
        }
    }

    void InputChanged() override {
        if (m_inputChangedCallback) {
            m_inputChangedCallback(m_context);
        }
    }

private:
    CalcDisplayCallback m_displayCallback;
    CalcHistoryCallback m_historyCallback;
    SetIsInErrorCallback m_isInErrorCallback;
    SetExpressionDisplayCallback m_expressionDisplayCallback;
    SetParenthesisNumberCallback m_parenthesisNumberCallback;
    OnNoRightParenAddedCallback m_noRightParenCallback;
    MaxDigitsReachedCallback m_maxDigitsCallback;
    BinaryOperatorReceivedCallback m_binaryOpCallback;
    SetMemorizedNumbersCallback m_memorizedNumbersCallback;
    MemoryItemChangedCallback m_memoryItemCallback;
    InputChangedCallback m_inputChangedCallback;
    void* m_context;
};

class ResourceProviderAdapter : public IResourceProvider {
public:
    ResourceProviderAdapter(ResourceProviderCallback callback, void* context)
            : m_callback(callback), m_context(context) {}

    std::wstring GetCEngineString(std::wstring_view id) override {
        if (!m_callback) {
            // Default implementation if no callback is provided
            return std::wstring(id);
        }

        // Convert wide id to UTF-8
        std::string utf8Id = WideToUtf8(std::wstring(id));

        // Call the managed callback to get the resource string
        char* result = m_callback(utf8Id.c_str(), m_context);

        if (result) {
            // Convert the returned UTF-8 string back to wide
            std::wstring value = Utf8ToWide(std::string(result));

            // Free the string that was allocated by the managed code
            Calc_FreeString(result);
            return value;
        }

        // Return id if callback returned null
        return std::wstring(id);
    }

private:
    ResourceProviderCallback m_callback;
    void* m_context;
};

// Add string memory freeing function
CALC_API void Calc_FreeString(char* str)
{
    if (str) {
        delete[] str;
    }
}

// Helper function to convert C++ exceptions to appropriate error codes
template<typename Func>
CalcError TryCatch(Func&& func) {
    try {
        func();
        return CALC_ERR_SUCCESS;
    }
    catch (const std::bad_alloc&) {
        return CALC_ERR_OUT_OF_MEMORY;
    }
    catch (const std::invalid_argument&) {
        return CALC_ERR_INVALID_PARAM;
    }
    // The calculator throws error codes directly as uint32_t values
    catch (const uint32_t& errorCode) {
        // Map the native error codes to our C API error codes
        switch (errorCode) {
            case CALC_E_DIVIDEBYZERO:
                return CALC_ERR_DIVIDE_BY_ZERO;
            case CALC_E_DOMAIN:
                return CALC_ERR_DOMAIN;
            case CALC_E_INDEFINITE:
                return CALC_ERR_UNDEFINED;
            case CALC_E_NORESULT:
                return CALC_ERR_NO_RESULT;
            case CALC_E_OVERFLOW:
                return CALC_ERR_OVERFLOW;
            case CALC_E_INVALIDRANGE:
                return CALC_ERR_INVALID_RANGE;
            case CALC_E_POSINFINITY:
                return CALC_ERR_POSITIVE_INFINITY;
            case CALC_E_NEGINFINITY:
                return CALC_ERR_NEGATIVE_INFINITY;
            case CALC_E_OUTOFMEMORY:
                return CALC_ERR_OUT_OF_MEMORY;
            default:
                return CALC_ERR_UNDEFINED;
        }
    }
    catch (const std::runtime_error& e) {
        // For any other runtime errors, check the message to see if we can determine the error type
        std::string message = e.what();
        
        // Diagnostic logging to see if this block gets hit
        std::cerr << "TryCatch: Caught std::runtime_error with message: " << message << std::endl;
        
        if (message.find("divide by zero") != std::string::npos) {
            std::cerr << "TryCatch: Categorized as CALC_ERR_DIVIDE_BY_ZERO" << std::endl;
            return CALC_ERR_DIVIDE_BY_ZERO;
        }
        else if (message.find("domain") != std::string::npos) {
            std::cerr << "TryCatch: Categorized as CALC_ERR_DOMAIN" << std::endl;
            return CALC_ERR_DOMAIN;
        }
        else if (message.find("overflow") != std::string::npos) {
            std::cerr << "TryCatch: Categorized as CALC_ERR_OVERFLOW" << std::endl;
            return CALC_ERR_OVERFLOW;
        }
        
        std::cerr << "TryCatch: No specific error identified, returning CALC_ERR_UNDEFINED" << std::endl;
        return CALC_ERR_UNDEFINED;
    }
    catch (const std::exception&) {
        return CALC_ERR_UNDEFINED;
    }
    catch (...) {
        return CALC_ERR_UNDEFINED;
    }
}

// Implementation of the C API functions
CALC_API CalcManagerHandle Calc_CreateManager(
        CalcDisplayCallback displayCallback,
        CalcHistoryCallback historyCallback,
        ResourceProviderCallback resourceCallback,
        CalcSetIsInErrorCallback isInErrorCallback,
        CalcSetExpressionDisplayCallback expressionDisplayCallback,
        CalcSetParenthesisNumberCallback parenthesisNumberCallback,
        CalcOnNoRightParenAddedCallback noRightParenCallback,
        CalcMaxDigitsReachedCallback maxDigitsCallback,
        CalcBinaryOperatorReceivedCallback binaryOpCallback,
        CalcSetMemorizedNumbersCallback memorizedNumbersCallback,
        CalcMemoryItemChangedCallback memoryItemCallback,
        CalcInputChangedCallback inputChangedCallback,
        void* context)
{
    try {
        auto manager = new CalcManager_t;
        auto displayAdapter = std::make_unique<DisplayCallbackAdapter>(
                displayCallback,
                historyCallback,
                isInErrorCallback,
                expressionDisplayCallback,
                parenthesisNumberCallback,
                noRightParenCallback,
                maxDigitsCallback,
                binaryOpCallback,
                memorizedNumbersCallback,
                memoryItemCallback,
                inputChangedCallback,
                context);

        auto resourceProvider = std::make_unique<ResourceProviderAdapter>(resourceCallback, context);

        manager->manager = std::make_unique<CalculatorManager>(
                displayAdapter.release(), resourceProvider.release());

        // Store all callbacks in the manager
        manager->displayCallback = displayCallback;
        manager->historyCallback = historyCallback;
        manager->resourceCallback = resourceCallback;
        manager->isInErrorCallback = isInErrorCallback;
        manager->expressionDisplayCallback = expressionDisplayCallback;
        manager->parenthesisNumberCallback = parenthesisNumberCallback;
        manager->noRightParenCallback = noRightParenCallback;
        manager->maxDigitsCallback = maxDigitsCallback;
        manager->binaryOpCallback = binaryOpCallback;
        manager->memorizedNumbersCallback = memorizedNumbersCallback;
        manager->memoryItemChangedCallback = memoryItemCallback;
        manager->inputChangedCallback = inputChangedCallback;
        manager->context = context;

        return manager;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API void Calc_DestroyManager(CalcManagerHandle handle)
{
    if (handle) {
        delete handle;
    }
}

CALC_API CalcError Calc_SendCommand(CalcManagerHandle handle, int commandId)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->SendCommand(static_cast<Command>(commandId));
    });
}

CALC_API CalcError Calc_SetMode(CalcManagerHandle handle, CalcMode mode)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        switch (mode) {
            case CALC_MODE_STANDARD:
                handle->manager->SetStandardMode();
                break;
            case CALC_MODE_SCIENTIFIC:
                handle->manager->SetScientificMode();
                break;
            case CALC_MODE_PROGRAMMER:
                handle->manager->SetProgrammerMode();
                break;
            default:
                throw std::invalid_argument("Invalid calculator mode");
        }
    });
}

CALC_API CalcError Calc_Reset(CalcManagerHandle handle, bool clearMemory)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->Reset(clearMemory);
    });
}

CALC_API CalcError Calc_SetRadix(CalcManagerHandle handle, CalcRadixType radixType)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->SetRadix(static_cast<RadixType>(radixType));
    });
}

CALC_API CalcError Calc_SetPrecision(CalcManagerHandle handle, int32_t precision)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->SetPrecision(precision);
    });
}

CALC_API char* Calc_GetDisplayString(CalcManagerHandle handle)
{
    if (!handle || !handle->manager) {
        return nullptr;
    }

    try {
        std::wstring result = handle->manager->GetResultForRadix(10, 0, false);

        // Convert to UTF-8
        std::string utf8Result = WideToUtf8(result);
        size_t length = utf8Result.length();

        char* buffer = new char[length + 1];
        utf8Result.copy(buffer, length);
        buffer[length] = '\0';

        return buffer;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcError Calc_IsInError(CalcManagerHandle handle, bool* isError)
{
    if (!handle || !handle->manager || !isError) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        *isError =  handle->manager->m_currentCalculatorEngine->FInErrorState();
    });
}

CALC_API CalcError Calc_IsInputEmpty(CalcManagerHandle handle, bool* isEmpty)
{
    if (!handle || !handle->manager || !isEmpty) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        *isEmpty = handle->manager->IsInputEmpty();
    });
}


// History functions

CALC_API CalcHistoryItemsHandle Calc_GetHistoryItems(
        CalcManagerHandle handle,
        CalcMode mode)
{
    if (!handle || !handle->manager) {
        return nullptr;
    }

    try {
        auto historyItems = new CalcHistoryItems_t;
        historyItems->items = handle->manager->GetHistoryItems(static_cast<CalculatorMode>(mode));
        return historyItems;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcError Calc_GetHistoryItemCount(
        CalcHistoryItemsHandle historyItems,
        uint32_t* count)
{
    if (!historyItems || !count) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        *count = static_cast<uint32_t>(historyItems->items.size());
    });
}

CALC_API CalcHistoryItemHandle Calc_GetHistoryItem(
        CalcHistoryItemsHandle historyItems,
        uint32_t index)
{
    if (!historyItems || index >= historyItems->items.size()) {
        return nullptr;
    }

    try {
        auto historyItem = new CalcHistoryItem_t;
        historyItem->item = historyItems->items[index];
        return historyItem;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API char* Calc_GetHistoryItemExpression(CalcHistoryItemHandle historyItem)
{
    if (!historyItem || !historyItem->item) {
        return nullptr;
    }

    try {
        const auto& expression = historyItem->item->historyItemVector.expression;

        // Convert to UTF-8
        std::string utf8Expression = WideToUtf8(expression);
        size_t length = utf8Expression.length();

        char* buffer = new char[length + 1];
        utf8Expression.copy(buffer, length);
        buffer[length] = '\0';

        return buffer;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API char* Calc_GetHistoryItemResult(CalcHistoryItemHandle historyItem)
{
    if (!historyItem || !historyItem->item) {
        return nullptr;
    }

    try {
        const auto& result = historyItem->item->historyItemVector.result;

        // Convert to UTF-8
        std::string utf8Result = WideToUtf8(result);
        size_t length = utf8Result.length();

        char* buffer = new char[length + 1];
        utf8Result.copy(buffer, length);
        buffer[length] = '\0';

        return buffer;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API void Calc_ReleaseHistoryItems(CalcHistoryItemsHandle historyItems)
{
    if (historyItems) {
        delete historyItems;
    }
}

CALC_API CalcError Calc_ClearHistory(CalcManagerHandle handle)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->ClearHistory();
    });
}

// Rational number functions

CALC_API CalcRationalHandle Calc_CreateRationalFromInt32(int32_t value)
{
    try {
        auto rational = new CalcRational_t;
        rational->value = Rational(value);
        return rational;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_CreateRationalFromString(
        const char* valueStr,
        uint32_t radix,
        int32_t precision)
{
    if (!valueStr) {
        return nullptr;
    }

    try {
        auto rational = new CalcRational_t;

        // Convert UTF-8 to wide string
        std::wstring wideValueStr = Utf8ToWide(std::string(valueStr));

        // Parse the string to create a Rational
        bool isNegative = false;
        std::wstring mantissa = wideValueStr;

        if (!mantissa.empty() && mantissa[0] == L'-') {
            isNegative = true;
            mantissa = mantissa.substr(1);
        }

        PRAT prat = StringToRat(isNegative, mantissa, false, L"", radix, precision);
        rational->value = Rational(prat);
        destroyrat(prat);

        return rational;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API void Calc_DestroyRational(CalcRationalHandle rational)
{
    if (rational) {
        delete rational;
    }
}

CALC_API char* Calc_RationalToString(
        CalcRationalHandle rational,
        uint32_t radix,
        CalcNumberFormat format,
        int32_t precision)
{
    if (!rational) {
        return nullptr;
    }

    try {
        std::wstring result = rational->value.ToString(
                radix, static_cast<NumberFormat>(format), precision);

        // Convert to UTF-8
        std::string utf8Result = WideToUtf8(result);
        size_t length = utf8Result.length();

        char* buffer = new char[length + 1];
        utf8Result.copy(buffer, length);
        buffer[length] = '\0';

        return buffer;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcError Calc_RationalToInt32(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision,
        int32_t* value)
{
    if (!rational || !value) {
        return CALC_ERR_INVALID_PARAM;
    }

    try {
        PRAT prat = rational->value.ToPRAT();
        *value = rattoi32(prat, radix, precision);
        destroyrat(prat);
        return CALC_ERR_SUCCESS;
    }
    catch (...) {
        return CALC_ERR_UNDEFINED;
    }
}

CALC_API CalcError Calc_RationalToUInt64(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision,
        uint64_t* value)
{
    if (!rational || !value) {
        return CALC_ERR_INVALID_PARAM;
    }

    try {
        PRAT prat = rational->value.ToPRAT();
        *value = rattoUi64(prat, radix, precision);
        destroyrat(prat);
        return CALC_ERR_SUCCESS;
    }
    catch (...) {
        return CALC_ERR_UNDEFINED;
    }
}

// Basic arithmetic operations

CALC_API CalcRationalHandle Calc_RationalAdd(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision)
{
    if (!a || !b) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = a->value + b->value;
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalSubtract(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision)
{
    if (!a || !b) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = a->value - b->value;
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalMultiply(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision)
{
    if (!a || !b) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = a->value * b->value;
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalDivide(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision)
{
    if (!a || !b) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = a->value / b->value;
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalMod(
        CalcRationalHandle a,
        CalcRationalHandle b)
{
    if (!a || !b) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = a->value % b->value;
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalNegate(CalcRationalHandle rational)
{
    if (!rational) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = -rational->value;
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

// Comparison operations

CALC_API CalcError Calc_RationalEquals(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision,
        bool* result)
{
    if (!a || !b || !result) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        *result = (a->value == b->value);
    });
}

CALC_API CalcError Calc_RationalCompare(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision,
        int* result)
{
    if (!a || !b || !result) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        if (a->value < b->value) {
            *result = -1;
        }
        else if (a->value > b->value) {
            *result = 1;
        }
        else {
            *result = 0;
        }
    });
}

// Mathematical functions

CALC_API CalcRationalHandle Calc_RationalSin(
        CalcRationalHandle rational,
        CalcAngleType angleType,
        uint32_t radix,
        int32_t precision)
{
    if (!rational) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = RationalMath::Sin(
                rational->value, static_cast<AngleType>(angleType));
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalCos(
        CalcRationalHandle rational,
        CalcAngleType angleType,
        uint32_t radix,
        int32_t precision)
{
    if (!rational) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = RationalMath::Cos(
                rational->value, static_cast<AngleType>(angleType));
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalTan(
        CalcRationalHandle rational,
        CalcAngleType angleType,
        uint32_t radix,
        int32_t precision)
{
    if (!rational) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = RationalMath::Tan(
                rational->value, static_cast<AngleType>(angleType));
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalSqrt(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision)
{
    if (!rational) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;

        // Create a rational with value 1/2 for the square root
        Number num1(1, 0, {1});
        Number num2(1, 0, {2});
        Rational half(num1, num2);
        result->value = RationalMath::Pow(rational->value, half);

        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalPow(
        CalcRationalHandle base,
        CalcRationalHandle exponent,
        uint32_t radix,
        int32_t precision)
{
    if (!base || !exponent) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = RationalMath::Pow(base->value, exponent->value);
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalRoot(
        CalcRationalHandle value,
        CalcRationalHandle root,
        uint32_t radix,
        int32_t precision)
{
    if (!value || !root) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = RationalMath::Root(value->value, root->value);
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalFact(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision)
{
    if (!rational) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = RationalMath::Fact(rational->value);
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalLn(
        CalcRationalHandle rational,
        int32_t precision)
{
    if (!rational) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = RationalMath::Log(rational->value);
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalLog10(
        CalcRationalHandle rational,
        int32_t precision)
{
    if (!rational) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = RationalMath::Log10(rational->value);
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcRationalHandle Calc_RationalExp(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision)
{
    if (!rational) {
        return nullptr;
    }

    try {
        auto result = new CalcRational_t;
        result->value = RationalMath::Exp(rational->value);
        return result;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API char Calc_GetDecimalSeparator(CalcManagerHandle handle)
{
    if (!handle || !handle->manager) {
        return '.';  // Default
    }

    try {
        // Convert wide character to UTF-8
        wchar_t wideChar = handle->manager->DecimalSeparator();
        std::wstring wideStr(1, wideChar);
        std::string utf8Str = WideToUtf8(wideStr);
        return utf8Str[0];  // Return first character of UTF-8 string
    }
    catch (...) {
        return '.';  // Default on error
    }
}

// Memory-related functions.

CALC_API CalcError Calc_MemorizeNumber(CalcManagerHandle handle)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->MemorizeNumber();
    });
}

CALC_API CalcError Calc_MemorizedNumberLoad(CalcManagerHandle handle, uint32_t memoryIndex)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->MemorizedNumberLoad(memoryIndex);
    });
}

CALC_API CalcError Calc_MemorizedNumberAdd(CalcManagerHandle handle, uint32_t memoryIndex)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->MemorizedNumberAdd(memoryIndex);
    });
}

CALC_API CalcError Calc_MemorizedNumberSubtract(CalcManagerHandle handle, uint32_t memoryIndex)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->MemorizedNumberSubtract(memoryIndex);
    });
}

CALC_API CalcError Calc_MemorizedNumberClear(CalcManagerHandle handle, uint32_t memoryIndex)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->MemorizedNumberClear(memoryIndex);
    });
}

CALC_API CalcError Calc_MemorizedNumberClearAll(CalcManagerHandle handle)
{
    if (!handle || !handle->manager) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        handle->manager->MemorizedNumberClearAll();
    });
}

CALC_API CalcError Calc_GetMemorizedNumbers(
        CalcManagerHandle handle,
        uint32_t* count,
        char** buffer,
        uint32_t bufferSize)
{
    if (!handle || !handle->manager || !count || !buffer || bufferSize == 0) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        // Get memorized numbers as strings
        std::vector<std::wstring> memorizedNumbers;
        handle->manager->SetMemorizedNumbersString();

        // We'll create our own vector to get the strings
        std::vector<std::wstring> displayStrings;
        auto radix = handle->manager->m_currentCalculatorEngine->GetCurrentRadix();

        for (const auto& memoryItem : handle->manager->m_memorizedNumbers) {
            std::wstring stringValue = handle->manager->m_currentCalculatorEngine->GetStringForDisplay(memoryItem, radix);
            if (!stringValue.empty()) {
                displayStrings.push_back(handle->manager->m_currentCalculatorEngine->GroupDigitsPerRadix(stringValue, radix));
            }
        }

        // Cap count to minimum of buffer size and actual items
        *count = std::min(static_cast<uint32_t>(displayStrings.size()), bufferSize);

        for (uint32_t i = 0; i < *count; i++) {
            // Convert wide string to UTF-8
            std::string utf8Value = WideToUtf8(displayStrings[i]);

            // Allocate memory and copy string with null terminator
            char* str = new char[utf8Value.length() + 1];
            std::copy(utf8Value.begin(), utf8Value.end(), str);
            str[utf8Value.length()] = '\0';

            buffer[i] = str;
        }
    });
}

// Command snapshot functions
CALC_API CalcCommandSnapshotHandle Calc_GetDisplayCommandsSnapshot(CalcManagerHandle handle)
{
    if (!handle || !handle->manager) {
        return nullptr;
    }

    try {
        auto snapshot = new CalcCommandSnapshot_t;
        
        // GetDisplayCommandsSnapshot returns a vector directly, not a pointer or shared_ptr
        snapshot->commands = handle->manager->GetDisplayCommandsSnapshot();
        
        return snapshot;
    }
    catch (...) {
        return nullptr;
    }
}

CALC_API CalcError Calc_GetCommandSnapshotSize(CalcCommandSnapshotHandle snapshot, uint32_t* count)
{
    if (!snapshot || !count) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        *count = static_cast<uint32_t>(snapshot->commands.size());
    });
}

CALC_API CalcError Calc_GetCommandFromSnapshot(
        CalcCommandSnapshotHandle snapshot, 
        uint32_t index, 
        ExpressionCommand* command)
{
    if (!snapshot || !command || index >= snapshot->commands.size()) {
        return CALC_ERR_INVALID_PARAM;
    }

    return TryCatch([&]() {
        auto& expressionCommand = snapshot->commands[index];
        
        if (!expressionCommand) {
            return;
        }
        
        // Get the command type using the command interface
        int commandType = static_cast<int>(expressionCommand->GetCommandType());
        command->commandType = commandType;
        
        // For the token string, we'll use a generic representation
        std::wstring token = L"Command" + std::to_wstring(index);
        
        // Try to extract a better string representation based on command type
        try {
            // If it's an operand command, try to get its token
            if (auto opndCommand = dynamic_cast<IOpndCommand*>(expressionCommand.get())) {
                token = opndCommand->GetToken(L'.'); // Use period as decimal symbol
            }
            // For binary command, we could extract the operator symbol
            else if (auto binaryCommand = dynamic_cast<IBinaryCommand*>(expressionCommand.get())) {
                int cmdId = binaryCommand->GetCommand();
                // Map some common binary operators
                switch (cmdId) {
                    case static_cast<int>(CalculationManager::Command::CommandADD):
                        token = L"+";
                        break;
                    case static_cast<int>(CalculationManager::Command::CommandSUB):
                        token = L"-";
                        break;
                    case static_cast<int>(CalculationManager::Command::CommandMUL):
                        token = L"×";
                        break;
                    case static_cast<int>(CalculationManager::Command::CommandDIV):
                        token = L"÷";
                        break;
                    default:
                        token = L"Op:" + std::to_wstring(cmdId);
                        break;
                }
            }
            // For parenthesis, determine if it's open or closed
            else if (auto parenCommand = dynamic_cast<IParenthesisCommand*>(expressionCommand.get())) {
                int cmdId = parenCommand->GetCommand();
                if (cmdId == static_cast<int>(CalculationManager::Command::CommandOPENP)) {
                    token = L"(";
                } else {
                    token = L")";
                }
            }
        }
        catch (...) {
            // If any errors, fall back to generic representation
            token = L"Command" + std::to_wstring(index);
        }
        
        // Convert token to UTF-8
        std::string utf8Token = WideToUtf8(token);
        
        // Allocate memory for the token
        char* tokenStr = new char[utf8Token.length() + 1];
        std::copy(utf8Token.begin(), utf8Token.end(), tokenStr);
        tokenStr[utf8Token.length()] = '\0';
        
        command->token = tokenStr;
    });
}

CALC_API void Calc_ReleaseCommandSnapshot(CalcCommandSnapshotHandle snapshot)
{
    if (snapshot) {
        delete snapshot;
    }
}