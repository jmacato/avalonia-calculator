// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma  once
// #include  "NavCategory.h"
// #include  "CalculatorButtonUser.h"
// A trace logging provider can only be instantiated and registered once per module.
// This class implements a singleton model ensure that only one instance is created.

namespace CalculatorApp.ViewModel.Common;

internal readonly record struct ButtonLog(
    CalculatorButtonId Button,
    ViewMode Mode,
    int Count = 1);
