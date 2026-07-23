using CalculatorApp.Services.Windowing;

namespace GraphingTests;

internal sealed class TestMiniModeService(bool isSupported) : IMiniModeService
{
    public bool AllowTransitions { get; init; } = true;

    public bool IsSupported { get; } = isSupported;

    public bool IsActive { get; private set; }

    public bool TryEnter()
    {
        if (!AllowTransitions || !IsSupported || IsActive)
        {
            return false;
        }

        IsActive = true;
        return true;
    }

    public bool TryExit()
    {
        if (!AllowTransitions || !IsActive)
        {
            return false;
        }

        IsActive = false;
        return true;
    }
}
