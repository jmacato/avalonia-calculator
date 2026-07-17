// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Direct managed port of microsoft-ui-xaml's SwipeItems API and collection
// validation from SwipeControl.idl and SwipeItems.cpp at
// commit 3cae15f071f1ab8565f9a7592dbf27f04bafe651.
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;

namespace CalculatorApp.Controls;

public enum SwipeMode
{
    Reveal,
    Execute,
}
