namespace FluentAvalonia.Core;

/// <summary>
/// Provides settings related to the behavior of UI elements, like animation, etc.
/// </summary>
public static class FAUISettings
{
    /// <summary>
    /// Checks whether animations are enabled or have been disabled
    /// </summary>
    public static bool AreAnimationsEnabled()
    {
        return Volatile.Read(ref s_animationsEnabled) != 0;
    }

    /// <summary>
    /// Enables or disables animations for the current application
    /// </summary>
    public static void SetAnimationsEnabledAtAppLevel(bool isEnabled)
    {
        Volatile.Write(ref s_animationsEnabled, isEnabled ? 1 : 0);
    }

    private static int s_animationsEnabled = 1;
}
