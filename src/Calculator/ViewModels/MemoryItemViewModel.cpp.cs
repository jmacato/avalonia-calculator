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
        m_calcVM.OnMemoryClear(Position);
    }

    public void MemoryAdd()
    {
        m_calcVM.OnMemoryAdd(Position);
    }


    public void MemorySubtract()
    {
        m_calcVM.OnMemorySubtract(Position);
    }

}
