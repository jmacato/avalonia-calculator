//using CalculatorApp.ViewModel.Common.Automation;

//x// Copyright (c) Microsoft Corporation. All rights reserved.
//// Licensed under the MIT License.

//// Declaration of the NarratorNotifier class.

//  // #pragma  once
//  // #include  "NarratorAnnouncement.h"

//namespace CalculatorApp.ViewModel.Common.Automation
//{
//    public
//        ref class NarratorNotifier sealed : Microsoft.UI.Xaml.DependencyObject
//    {
//    public:
//        NarratorNotifier();

//    void Announce(NarratorAnnouncement announcement);

//    property NarratorAnnouncement^ Announcement
//        {
//            NarratorAnnouncement^ get() { return GetAnnouncement(this); }
//    void set(NarratorAnnouncement^ value)
//    {
//        SetAnnouncement(this, value);
//    }
//}

//static void RegisterDependencyProperties();

//static property Microsoft.UI.Xaml.DependencyProperty

//    AnnouncementProperty { Microsoft.UI.Xaml.DependencyProperty get() { return s_announcementProperty; } }

//static NarratorAnnouncement
//GetAnnouncement(
//    Microsoft.UI.Xaml.DependencyObject element)
//{ return (NarratorAnnouncement) (element.GetValue(s_announcementProperty)); }

//static void SetAnnouncement(Microsoft.UI.Xaml.DependencyObject element, NarratorAnnouncement value)
//{
//    element.SetValue(s_announcementProperty, value);
//}

//private:
//        static void OnAnnouncementChanged(
//             Microsoft.UI.Xaml.DependencyObject dependencyObject,
//             Microsoft.UI.Xaml.DependencyPropertyChangedEventArgs eventArgs);

//static Microsoft.UI.Xaml.DependencyProperty s_announcementProperty;

//private:
//        Microsoft.UI.Xaml.UIElement m_announcementElement;
//    };
//}
