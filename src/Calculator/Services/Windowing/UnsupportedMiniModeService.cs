// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.Services.Windowing;

public sealed class UnsupportedMiniModeService : IMiniModeService
{
    public static UnsupportedMiniModeService Instance { get; } = new();

    private UnsupportedMiniModeService()
    {
    }

    public bool IsSupported => false;

    public bool IsActive => false;

    public bool TryEnter() => false;

    public bool TryExit() => false;
}
