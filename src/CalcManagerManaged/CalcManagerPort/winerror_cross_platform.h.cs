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

public partial class RatPak
{
    private const uint32_t E_FAIL = 0x80004005; // #define E_IN// #define E_OUTOFMEMORY 0x8007000E
    private const uint32_t E_POINTER = 0x80004003;
    private const uint32_t E_UNEXPECTED = 0x8000FFFF;
    private const uint32_t E_BOUNDS = 0x8000000B;
    private const uint32_t S_OK = 0x0;
    private const uint32_t S_FALSE = 0x1;

    private bool SUCCEEDED(ResultCode hr) => hr >= 0;
    private bool FAILED(ResultCode hr) => hr < 0;
    public uint32_t SCODE_CODE(uint32_t sc) => sc & 0xFFFF;
}

//
// #endif
