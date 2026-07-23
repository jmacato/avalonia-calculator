// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculationManager;

[Flags]
public enum CalculatorPrecision
{
    None = 0,
    StandardModePrecision = 16,
    ScientificModePrecision = 32,
    ProgrammerModePrecision = 64
};
