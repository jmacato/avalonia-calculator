/**
 * CalcEngine_CWrapper.h
 * C-compatible wrapper for the Microsoft Calculator Engine
 */

#ifndef CALC_ENGINE_C_WRAPPER_H
#define CALC_ENGINE_C_WRAPPER_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

#ifdef _WIN32
#ifdef CALCENGINE_EXPORTS
    #define CALC_API __declspec(dllexport)
  #else
    #define CALC_API __declspec(dllimport)
  #endif
#else
#define CALC_API
#endif

/**
 * Opaque handles for C++ objects
 */
typedef struct CalcManager_t* CalcManagerHandle;
typedef struct CalcRational_t* CalcRationalHandle;
typedef struct CalcHistoryItem_t* CalcHistoryItemHandle;
typedef struct CalcHistoryItems_t* CalcHistoryItemsHandle;
typedef struct CalcCommandSnapshot_t* CalcCommandSnapshotHandle;

/**
 * Error codes - maps to ResultCode from CalcErr.h
 */
typedef enum {
    CALC_ERR_SUCCESS = 0,
    CALC_ERR_INVALID_PARAM = 0x80070057,
    CALC_ERR_OUT_OF_MEMORY = 0x8007000E,
    CALC_ERR_DIVIDE_BY_ZERO = 0x80000000,
    CALC_ERR_DOMAIN = 0x80000001,
    CALC_ERR_UNDEFINED = 0x80000002,
    CALC_ERR_POSITIVE_INFINITY = 0x80000003,  // New: Maps to CALC_E_POSINFINITY
    CALC_ERR_NEGATIVE_INFINITY = 0x80000004,  // New: Maps to CALC_E_NEGINFINITY
    CALC_ERR_INVALID_RANGE = 0x80000006,      // New: Maps to CALC_E_INVALIDRANGE
    CALC_ERR_OVERFLOW = 0x80000008,
    CALC_ERR_NO_RESULT = 0x80000009           // New: Maps to CALC_E_NORESULT
} CalcError;

/**
 * Calculator modes
 */
typedef enum {
    CALC_MODE_STANDARD = 0,
    CALC_MODE_SCIENTIFIC = 1,
    CALC_MODE_PROGRAMMER = 2
} CalcMode;

/**
 * Angle types
 */
typedef enum {
    CALC_ANGLE_DEGREES = 0,
    CALC_ANGLE_RADIANS = 1,
    CALC_ANGLE_GRADIANS = 2
} CalcAngleType;

/**
 * Radix types
 */
typedef enum {
    CALC_RADIX_HEX = 0,
    CALC_RADIX_DECIMAL = 1,
    CALC_RADIX_OCTAL = 2,
    CALC_RADIX_BINARY = 3
} CalcRadixType;

/**
 * Number format
 */
typedef enum {
    CALC_NUM_FORMAT_FLOAT = 0,
    CALC_NUM_FORMAT_SCIENTIFIC = 1,
    CALC_NUM_FORMAT_ENGINEERING = 2
} CalcNumberFormat;

struct ExpressionToken {
    char* text;
    int type;
};

// Structure for command serialization
struct ExpressionCommand {
    int commandType;
    char* token;
};

/**
 * Callback function types - updated to use UTF-8 strings
 */
typedef void (*CalcDisplayCallback)(const char* displayString, bool isError, void* context);
typedef void (*CalcHistoryCallback)(uint32_t addedItemIndex, void* context);
typedef char* (*ResourceProviderCallback)(const char* resourceId, void* context);
typedef void (*CalcSetIsInErrorCallback)(bool isError, void* context);
typedef void (*CalcSetExpressionDisplayCallback)(ExpressionToken* tokens, long tokenCount, void* context);
typedef void (*CalcSetParenthesisNumberCallback)(unsigned int count, void* context);
typedef void (*CalcOnNoRightParenAddedCallback)(void* context);
typedef void (*CalcMaxDigitsReachedCallback)(void* context);
typedef void (*CalcBinaryOperatorReceivedCallback)(void* context);
typedef void (*CalcSetMemorizedNumbersCallback)(const char** numbers, int count, void* context);
typedef void (*CalcMemoryItemChangedCallback)(unsigned int indexOfMemory, void* context);
typedef void (*CalcInputChangedCallback)(void* context);

/*****************************************************************************
 * Calculator Manager Functions
 *****************************************************************************/

/**
 * Create a calculator manager instance
 *
 * @param displayCallback Callback function for display updates
 * @param historyCallback Callback function for history updates
 * @param resourceCallback Callback function for resource string lookups
 * @param context User context pointer passed to callbacks
 * @return Handle to the calculator manager or NULL on error
 */
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
        void* context);

/**
 * Destroy a calculator manager instance and free resources
 *
 * @param handle Handle to the calculator manager
 */
CALC_API void Calc_DestroyManager(CalcManagerHandle handle);

/**
 * Send a command to the calculator
 *
 * @param handle Handle to the calculator manager
 * @param commandId Command ID (from Command.h)
 * @return Error code
 */
CALC_API CalcError Calc_SendCommand(CalcManagerHandle handle, int commandId);

/**
 * Set the calculator mode
 *
 * @param handle Handle to the calculator manager
 * @param mode Calculator mode
 * @return Error code
 */
CALC_API CalcError Calc_SetMode(CalcManagerHandle handle, CalcMode mode);

/**
 * Reset the calculator state
 *
 * @param handle Handle to the calculator manager
 * @param clearMemory Whether to clear memory
 * @return Error code
 */
CALC_API CalcError Calc_Reset(CalcManagerHandle handle, bool clearMemory);

/**
 * Set the radix type (number base)
 *
 * @param handle Handle to the calculator manager
 * @param radixType Radix type
 * @return Error code
 */
CALC_API CalcError Calc_SetRadix(CalcManagerHandle handle, CalcRadixType radixType);

/**
 * Set the precision for calculations
 *
 * @param handle Handle to the calculator manager
 * @param precision Precision value
 * @return Error code
 */
CALC_API CalcError Calc_SetPrecision(CalcManagerHandle handle, int32_t precision);

/**
 * Get the current display string
 *
 * @param handle Handle to the calculator manager
 * @return Pointer to a newly allocated string or NULL on error.
 *         The string must be freed with Calc_FreeString when no longer needed.
 */
CALC_API char* Calc_GetDisplayString(CalcManagerHandle handle);

/**
 * Check if calculator is in error state
 *
 * @param handle Handle to the calculator manager
 * @param isError Output parameter to receive error state
 * @return Error code
 */
CALC_API CalcError Calc_IsInError(CalcManagerHandle handle, bool* isError);

/**
 * Check if calculator input is empty
 *
 * @param handle Handle to the calculator manager
 * @param isEmpty Output parameter to receive empty state
 * @return Error code
 */
CALC_API CalcError Calc_IsInputEmpty(CalcManagerHandle handle, bool* isEmpty);

/**
 * Free a string that was allocated by the library
 *
 * @param str String to free
 */
CALC_API void Calc_FreeString(char* str);

/*****************************************************************************
 * Memory Functions
 *****************************************************************************/

/**
 * Memorize current number
 *
 * @param handle Handle to the calculator manager
 * @return Error code
 */
CALC_API CalcError Calc_MemorizeNumber(CalcManagerHandle handle);

/**
 * Load memorized number
 *
 * @param handle Handle to the calculator manager
 * @param memoryIndex Index of the memory slot
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberLoad(CalcManagerHandle handle, uint32_t memoryIndex);

/**
 * Add current value to memory
 *
 * @param handle Handle to the calculator manager
 * @param memoryIndex Index of the memory slot
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberAdd(CalcManagerHandle handle, uint32_t memoryIndex);

/**
 * Subtract current value from memory
 *
 * @param handle Handle to the calculator manager
 * @param memoryIndex Index of the memory slot
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberSubtract(CalcManagerHandle handle, uint32_t memoryIndex);

/**
 * Clear a specific memory slot
 *
 * @param handle Handle to the calculator manager
 * @param memoryIndex Index of the memory slot
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberClear(CalcManagerHandle handle, uint32_t memoryIndex);

/**
 * Clear all memory slots
 *
 * @param handle Handle to the calculator manager
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberClearAll(CalcManagerHandle handle);


/*****************************************************************************
 * History Functions
 *****************************************************************************/

/**
 * Get the history items
 *
 * @param handle Handle to the calculator manager
 * @param mode Calculator mode
 * @return Handle to history items or NULL on error
 */
CALC_API CalcHistoryItemsHandle Calc_GetHistoryItems(
        CalcManagerHandle handle,
        CalcMode mode);

/**
 * Get the number of history items
 *
 * @param historyItems Handle to history items
 * @param count Output parameter to receive the count
 * @return Error code
 */
CALC_API CalcError Calc_GetHistoryItemCount(
        CalcHistoryItemsHandle historyItems,
        uint32_t* count);

/**
 * Get a specific history item
 *
 * @param historyItems Handle to history items
 * @param index Index of the history item
 * @return Handle to the history item or NULL on error
 */
CALC_API CalcHistoryItemHandle Calc_GetHistoryItem(
        CalcHistoryItemsHandle historyItems,
        uint32_t index);

/**
 * Get the expression from a history item
 *
 * @param historyItem Handle to the history item
 * @return Pointer to a newly allocated string or NULL on error.
 *         The string must be freed with Calc_FreeString when no longer needed.
 */
CALC_API char* Calc_GetHistoryItemExpression(CalcHistoryItemHandle historyItem);

/**
 * Get the result from a history item
 *
 * @param historyItem Handle to the history item
 * @return Pointer to a newly allocated string or NULL on error.
 *         The string must be freed with Calc_FreeString when no longer needed.
 */
CALC_API char* Calc_GetHistoryItemResult(CalcHistoryItemHandle historyItem);

/**
 * Release a history items handle
 *
 * @param historyItems Handle to history items
 */
CALC_API void Calc_ReleaseHistoryItems(CalcHistoryItemsHandle historyItems);

/**
 * Clear all history
 *
 * @param handle Handle to the calculator manager
 * @return Error code
 */
CALC_API CalcError Calc_ClearHistory(CalcManagerHandle handle);

/*****************************************************************************
 * Rational Number Functions
 *****************************************************************************/

/**
 * Create a rational number from an integer
 *
 * @param value Integer value
 * @return Handle to the rational number or NULL on error
 */
CALC_API CalcRationalHandle Calc_CreateRationalFromInt32(int32_t value);

/**
 * Create a rational number from a string
 *
 * @param valueStr String representation of the number
 * @param radix Numeric base
 * @param precision Precision
 * @return Handle to the rational number or NULL on error
 */
CALC_API CalcRationalHandle Calc_CreateRationalFromString(
        const char* valueStr,
        uint32_t radix,
        int32_t precision);

/**
 * Destroy a rational number and free resources
 *
 * @param rational Handle to the rational number
 */
CALC_API void Calc_DestroyRational(CalcRationalHandle rational);

/**
 * Convert a rational number to a string
 *
 * @param rational Handle to the rational number
 * @param radix Numeric base
 * @param format Number format
 * @param precision Precision
 * @return Pointer to a newly allocated string or NULL on error.
 *         The string must be freed with Calc_FreeString when no longer needed.
 */
CALC_API char* Calc_RationalToString(
        CalcRationalHandle rational,
        uint32_t radix,
        CalcNumberFormat format,
        int32_t precision);

/**
 * Convert a rational number to a 32-bit integer
 *
 * @param rational Handle to the rational number
 * @param radix Numeric base
 * @param precision Precision
 * @param value Output parameter to receive the integer value
 * @return Error code
 */
CALC_API CalcError Calc_RationalToInt32(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision,
        int32_t* value);

/**
 * Convert a rational number to a 64-bit unsigned integer
 *
 * @param rational Handle to the rational number
 * @param radix Numeric base
 * @param precision Precision
 * @param value Output parameter to receive the integer value
 * @return Error code
 */
CALC_API CalcError Calc_RationalToUInt64(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision,
        uint64_t* value);

/**
 * Add two rational numbers
 *
 * @param a First operand
 * @param b Second operand
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalAdd(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision);

/**
 * Subtract two rational numbers
 *
 * @param a First operand
 * @param b Second operand
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalSubtract(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision);

/**
 * Multiply two rational numbers
 *
 * @param a First operand
 * @param b Second operand
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalMultiply(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision);

/**
 * Divide two rational numbers
 *
 * @param a First operand
 * @param b Second operand
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalDivide(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision);

/**
 * Compute the modulo of two rational numbers
 *
 * @param a First operand
 * @param b Second operand
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalMod(
        CalcRationalHandle a,
        CalcRationalHandle b);

/**
 * Negate a rational number
 *
 * @param rational Operand
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalNegate(CalcRationalHandle rational);

/**
 * Compare two rational numbers for equality
 *
 * @param a First operand
 * @param b Second operand
 * @param precision Precision
 * @param result Output parameter to receive the comparison result
 * @return Error code
 */
CALC_API CalcError Calc_RationalEquals(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision,
        bool* result);

/**
 * Compare two rational numbers
 *
 * @param a First operand
 * @param b Second operand
 * @param precision Precision
 * @param result Output parameter to receive the comparison result (-1 for less than, 0 for equal, 1 for greater than)
 * @return Error code
 */
CALC_API CalcError Calc_RationalCompare(
        CalcRationalHandle a,
        CalcRationalHandle b,
        int32_t precision,
        int* result);

/**
 * Calculate sine of a rational number
 *
 * @param rational Operand
 * @param angleType Angle type
 * @param radix Numeric base
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalSin(
        CalcRationalHandle rational,
        CalcAngleType angleType,
        uint32_t radix,
        int32_t precision);

/**
 * Calculate cosine of a rational number
 *
 * @param rational Operand
 * @param angleType Angle type
 * @param radix Numeric base
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalCos(
        CalcRationalHandle rational,
        CalcAngleType angleType,
        uint32_t radix,
        int32_t precision);

/**
 * Calculate tangent of a rational number
 *
 * @param rational Operand
 * @param angleType Angle type
 * @param radix Numeric base
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalTan(
        CalcRationalHandle rational,
        CalcAngleType angleType,
        uint32_t radix,
        int32_t precision);

/**
 * Calculate square root of a rational number
 *
 * @param rational Operand
 * @param radix Numeric base
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalSqrt(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision);

/**
 * Calculate power of a rational number
 *
 * @param base Base operand
 * @param exponent Exponent operand
 * @param radix Numeric base
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalPow(
        CalcRationalHandle base,
        CalcRationalHandle exponent,
        uint32_t radix,
        int32_t precision);

/**
 * Calculate root of a rational number
 *
 * @param value Value operand
 * @param root Root operand
 * @param radix Numeric base
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalRoot(
        CalcRationalHandle value,
        CalcRationalHandle root,
        uint32_t radix,
        int32_t precision);

/**
 * Calculate factorial of a rational number
 *
 * @param rational Operand
 * @param radix Numeric base
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalFact(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision);

/**
 * Calculate natural logarithm of a rational number
 *
 * @param rational Operand
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalLn(
        CalcRationalHandle rational,
        int32_t precision);

/**
 * Calculate base-10 logarithm of a rational number
 *
 * @param rational Operand
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalLog10(
        CalcRationalHandle rational,
        int32_t precision);

/**
 * Calculate exponential function of a rational number
 *
 * @param rational Operand
 * @param radix Numeric base
 * @param precision Precision
 * @return Handle to the result or NULL on error
 */
CALC_API CalcRationalHandle Calc_RationalExp(
        CalcRationalHandle rational,
        uint32_t radix,
        int32_t precision);

/**
 * Get the decimal separator character
 *
 * @param handle Handle to the calculator manager
 * @return Decimal separator character
 */
CALC_API char Calc_GetDecimalSeparator(CalcManagerHandle handle);

/**
 * Memorize current number
 *
 * @param handle Handle to the calculator manager
 * @return Error code
 */
CALC_API CalcError Calc_MemorizeNumber(CalcManagerHandle handle);

/**
 * Load memorized number
 *
 * @param handle Handle to the calculator manager
 * @param memoryIndex Index of the memory slot
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberLoad(CalcManagerHandle handle, uint32_t memoryIndex);

/**
 * Add current value to memory
 *
 * @param handle Handle to the calculator manager
 * @param memoryIndex Index of the memory slot
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberAdd(CalcManagerHandle handle, uint32_t memoryIndex);

/**
 * Subtract current value from memory
 *
 * @param handle Handle to the calculator manager
 * @param memoryIndex Index of the memory slot
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberSubtract(CalcManagerHandle handle, uint32_t memoryIndex);

/**
 * Clear a specific memory slot
 *
 * @param handle Handle to the calculator manager
 * @param memoryIndex Index of the memory slot
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberClear(CalcManagerHandle handle, uint32_t memoryIndex);

/**
 * Clear all memory slots
 *
 * @param handle Handle to the calculator manager
 * @return Error code
 */
CALC_API CalcError Calc_MemorizedNumberClearAll(CalcManagerHandle handle);

/**
 * Get the memorized numbers as strings
 *
 * @param handle Handle to the calculator manager
 * @param count Output parameter to receive the actual number of memory slots
 * @param buffer Array to receive the memory values as strings
 * @param bufferSize Size of the buffer array
 * @return Error code
 */
CALC_API CalcError Calc_GetMemorizedNumbers(
        CalcManagerHandle handle,
        uint32_t* count,
        char** buffer,
        uint32_t bufferSize);

/**
 * Get the display commands snapshot
 *
 * @param handle Handle to the calculator manager
 * @return Handle to command snapshot containing the display commands or NULL on error
 */
CALC_API CalcCommandSnapshotHandle Calc_GetDisplayCommandsSnapshot(CalcManagerHandle handle);

/**
 * Get the number of commands in a snapshot
 * 
 * @param snapshot Handle to command snapshot
 * @param count Output parameter to receive the count
 * @return Error code
 */
CALC_API CalcError Calc_GetCommandSnapshotSize(CalcCommandSnapshotHandle snapshot, uint32_t* count);

/**
 * Get a command from a snapshot
 * 
 * @param snapshot Handle to command snapshot
 * @param index Index of the command
 * @param command Output parameter to receive the command data
 * @return Error code
 */
CALC_API CalcError Calc_GetCommandFromSnapshot(
        CalcCommandSnapshotHandle snapshot, 
        uint32_t index, 
        ExpressionCommand* command);

/**
 * Release a command snapshot handle
 * 
 * @param snapshot Handle to the command snapshot
 */
CALC_API void Calc_ReleaseCommandSnapshot(CalcCommandSnapshotHandle snapshot);

#ifdef __cplusplus
}
#endif

#endif /* CALC_ENGINE_C_WRAPPER_H */