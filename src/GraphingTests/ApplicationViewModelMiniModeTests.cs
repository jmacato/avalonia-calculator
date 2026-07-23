using Avalonia.Headless.XUnit;
using CalculatorApp.Services.Settings;
using CalculatorApp.Services.Windowing;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace GraphingTests;

public sealed class ApplicationViewModelMiniModeTests
{
    [AvaloniaFact]
    public void UnsupportedPlatformsDoNotExposeMiniMode()
    {
        var service = new TestMiniModeService(isSupported: false);
        using var application = CreateApplication(service);

        Assert.False(application.DisplayNormalAlwaysOnTopOption);
        application.EnterMiniModeCommand.Execute(null);
        Assert.False(application.IsAlwaysOnTop);
    }

    [AvaloniaFact]
    public void EnteringAndExitingMiniModeUpdatesCalculatorState()
    {
        var service = new TestMiniModeService(isSupported: true);
        using var application = CreateApplication(service);
        application.IsNavigationPaneOpen = true;

        Assert.True(application.DisplayNormalAlwaysOnTopOption);
        application.EnterMiniModeCommand.Execute(null);

        Assert.True(service.IsActive);
        Assert.True(application.IsAlwaysOnTop);
        Assert.True(application.CalculatorViewModel?.IsAlwaysOnTop);
        Assert.False(application.CalculatorViewModel?.HistoryVM.AreHistoryShortcutsEnabled);
        Assert.False(application.IsNavigationPaneOpen);
        Assert.False(application.DisplayNormalAlwaysOnTopOption);

        application.ExitMiniModeCommand.Execute(null);

        Assert.False(service.IsActive);
        Assert.False(application.IsAlwaysOnTop);
        Assert.False(application.CalculatorViewModel?.IsAlwaysOnTop);
        Assert.True(application.CalculatorViewModel?.HistoryVM.AreHistoryShortcutsEnabled);
        Assert.True(application.DisplayNormalAlwaysOnTopOption);
    }

    [AvaloniaFact]
    public void FailedPlatformTransitionDoesNotChangeViewModelState()
    {
        var service = new TestMiniModeService(isSupported: true)
        {
            AllowTransitions = false
        };
        using var application = CreateApplication(service);

        application.EnterMiniModeCommand.Execute(null);

        Assert.False(service.IsActive);
        Assert.False(application.IsAlwaysOnTop);
        Assert.False(application.CalculatorViewModel?.IsAlwaysOnTop);
        Assert.True(application.CalculatorViewModel?.HistoryVM.AreHistoryShortcutsEnabled);
    }

    private static ApplicationViewModel CreateApplication(
        IMiniModeService miniModeService)
    {
        var application = new ApplicationViewModel(
            new InMemorySettingsStore(),
            miniModeService: miniModeService);
        application.Initialize(ViewMode.Standard);
        return application;
    }

}
