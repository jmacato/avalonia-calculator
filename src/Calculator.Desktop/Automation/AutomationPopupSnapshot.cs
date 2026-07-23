// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Media.Imaging;

namespace CalculatorApp.Automation;

internal sealed record AutomationPopupSnapshot(Task<Bitmap> Snapshot, Point Position);
