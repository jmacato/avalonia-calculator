// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using CalcEngine;

namespace CalculationManager
{
    // Numbering continues from the Enum Command from Command.h
    // with some gap to ensure there is no overlap of these ids
    // when static_cast<unsigned char> is performed on these ids
    // they shouldn't fall in any number range greater than 80. So never
    // make the memory command ids go below 330
    enum MemoryCommand
    {
        MemorizeNumber = 330,
        MemorizedNumberLoad = 331,
        MemorizedNumberAdd = 332,
        MemorizedNumberSubtract = 333,
        MemorizedNumberClearAll = 334,
        MemorizedNumberClear = 335
    };
}
