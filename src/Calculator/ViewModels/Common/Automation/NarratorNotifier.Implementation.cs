// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Automation;

namespace CalculatorApp.ViewModel.Common.Automation;

public sealed partial class NarratorNotifier
{
    public NarratorNotifier()
    {
        Width = 1;
        Height = 1;
        Opacity = 0;
        IsHitTestVisible = false;
    }

    public void Announce(NarratorAnnouncement? announcement)
    {
        if (announcement is null || !NarratorAnnouncement.IsValid(announcement))
        {
            return;
        }

        AutomationLiveSetting liveSetting = announcement.Processing is
            AutomationNotificationProcessing.ImportantMostRecent
            or AutomationNotificationProcessing.CurrentThenMostRecent
                ? AutomationLiveSetting.Assertive
                : AutomationLiveSetting.Polite;
        AutomationProperties.SetLiveSetting(this, liveSetting);
        AutomationProperties.SetAutomationId(this, announcement.ActivityId);
        Text = announcement.Announcement;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == AnnouncementProperty)
        {
            Announce(change.GetNewValue<NarratorAnnouncement?>());
        }
    }
}
