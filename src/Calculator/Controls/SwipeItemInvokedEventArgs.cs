// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Ported from microsoft-ui-xaml SwipeControl.idl at
// commit 3cae15f071f1ab8565f9a7592dbf27f04bafe651.
namespace CalculatorApp.Controls;

public sealed class SwipeItemInvokedEventArgs : EventArgs
{
    internal SwipeItemInvokedEventArgs(SwipeControl swipeControl)
    {
        SwipeControl = swipeControl;
    }

    public SwipeControl SwipeControl { get; }
}
