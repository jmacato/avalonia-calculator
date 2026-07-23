// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.Services.Windowing;

public interface IMiniModeService
{
    bool IsSupported { get; }

    bool IsActive { get; }

    bool TryEnter();

    bool TryExit();
}
