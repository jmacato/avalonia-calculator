// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma  once
// #include  "NavCategory.h"
// #include  "CalculatorButtonUser.h"
// A trace logging provider can only be instantiated and registered once per module.
// This class implements a singleton model ensure that only one instance is created.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace CalculatorApp.ViewModel.Common;

public readonly record struct ButtonLog
{
    public ButtonLog(CalculatorButtonId button, ViewMode mode)
    {
        Button = button;
        Mode = mode;
        Count = 1;
    }

    public int Count { get; }
    public CalculatorButtonId Button { get; }
    public ViewMode Mode { get; }
};
