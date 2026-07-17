// #pragma once
//
// #if defined(_WIN32) && defined(_MSC_VER)
//
// #include <winerror.h>
//
// #else
//
// #include "Ratpack/CalcErr.h"

using uint32_t = System.UInt32;
using ResultCode = System.Int32;

namespace CalcEngine;

internal sealed partial class RatPak
{
    private const uint32_t S_OK = 0x0;

    private static bool SUCCEEDED(ResultCode hr) => hr >= 0;
    private static bool FAILED(ResultCode hr) => hr < 0;
    public static uint32_t SCODE_CODE(uint32_t sc) => sc & 0xFFFF;
}

//
// #endif
