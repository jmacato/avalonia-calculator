// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

// this to 2^32 after solving scaling problems with
// overflow detection esp. in mul

public enum NumberFormat
{
    FloatingPoint, // returns floating point, or exponential if number is too big
    Scientific, // always returns scientific notation
    Engineering // always returns engineering notation such that exponent is a multiple of 3
};
