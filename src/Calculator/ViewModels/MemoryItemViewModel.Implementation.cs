// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//#include  "pch.h"
//#include  "MemoryItemViewModel.h"
//#include  "StandardCalculatorViewModel.h"

namespace CalculatorApp.ViewModel;

public partial class MemoryItemViewModel
{

    public void Clear()
    {
        calcVm.OnMemoryClear(Position);
    }

    public void MemoryAdd()
    {
        calcVm.OnMemoryAdd(Position);
    }


    public void MemorySubtract()
    {
        calcVm.OnMemorySubtract(Position);
    }

}
