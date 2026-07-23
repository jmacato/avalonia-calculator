// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Direct managed port of microsoft-ui-xaml's SwipeItem API and invocation
// behavior from SwipeControl.idl and SwipeItem.cpp at
// commit 3cae15f071f1ab8565f9a7592dbf27f04bafe651.

namespace CalculatorApp.Controls;

public enum SwipeBehaviorOnInvoked
{
    Auto,
    Close,
    RemainOpen,
}
