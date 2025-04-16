// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Microsoft.UI.Xaml.Automation.Peers;

namespace CalculatorApp.ViewModel.Common.Automation
{
public partial  class NarratorAnnouncement
    {
    //public:
        //property string
        //    Announcement { string get(); }

        //    property string
        //    ActivityId { string get(); }

        //    property AutomationNotificationKind Kind
        //{
        //    AutomationNotificationKind get();
        //}

        //property AutomationNotificationProcessing Processing
        //{
        //    AutomationNotificationProcessing get();
        //}

        //static bool IsValid(NarratorAnnouncement announcement);
          string m_announcement;
       string m_activityId;
        AutomationNotificationKind m_kind;
        AutomationNotificationProcessing m_processing;

    //internal:
    //    NarratorAnnouncement(
    //        string announcement,
    //        string activityId,
    //        AutomationNotificationKind kind,
    //        AutomationNotificationProcessing processing);
    };

    // CalculatorAnnouncement is intended to contain only static methods
    // that return announcements made for the Calculator app.
//public
//    ref class CalculatorAnnouncement sealed
//    {
//    public:
//        static NarratorAnnouncement GetDisplayUpdatedAnnouncement(string announcement);
//        static NarratorAnnouncement GetMaxDigitsReachedAnnouncement(string announcement);

//        static NarratorAnnouncement GetMemoryClearedAnnouncement(string announcement);
//        static NarratorAnnouncement GetMemoryItemChangedAnnouncement(string announcement);
//        static NarratorAnnouncement GetMemoryItemAddedAnnouncement(string announcement);

//        static NarratorAnnouncement GetHistoryClearedAnnouncement(string announcement);
//        static NarratorAnnouncement GetHistorySlotClearedAnnouncement(string announcement);

//        static NarratorAnnouncement GetCategoryNameChangedAnnouncement(string announcement);

//        static NarratorAnnouncement GetUpdateCurrencyRatesAnnouncement(string announcement);

//        static NarratorAnnouncement GetDisplayCopiedAnnouncement(string announcement);

//        static NarratorAnnouncement GetOpenParenthesisCountChangedAnnouncement(string announcement);
//        static NarratorAnnouncement GetNoRightParenthesisAddedAnnouncement(string announcement);

//        static NarratorAnnouncement GetGraphModeChangedAnnouncement(string announcement);
//        static NarratorAnnouncement GetGraphViewChangedAnnouncement(string announcement);
//        static NarratorAnnouncement GetGraphViewBestFitChangedAnnouncement(string announcement);

//        static NarratorAnnouncement GetFunctionRemovedAnnouncement(string announcement);

//        static NarratorAnnouncement GetAlwaysOnTopChangedAnnouncement(string announcement);

//        static NarratorAnnouncement GetBitShiftRadioButtonCheckedAnnouncement(string announcement);

//        static NarratorAnnouncement GetSettingsPageOpenedAnnouncement(string announcement);
//    };
}
