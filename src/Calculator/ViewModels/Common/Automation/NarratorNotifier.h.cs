// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;

namespace CalculatorApp.ViewModel.Common.Automation;

public sealed partial class NarratorNotifier : TextBlock
{
    public static readonly StyledProperty<NarratorAnnouncement?> AnnouncementProperty =
        AvaloniaProperty.Register<NarratorNotifier, NarratorAnnouncement?>(nameof(Announcement));

    public NarratorAnnouncement? Announcement
    {
        get => GetValue(AnnouncementProperty);
        set => SetValue(AnnouncementProperty, value);
    }
}
