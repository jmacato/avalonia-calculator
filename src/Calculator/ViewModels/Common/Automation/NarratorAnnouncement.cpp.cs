namespace CalculatorApp.ViewModel.Common.Automation
{
    public partial class NarratorAnnouncement
    {
        public NarratorAnnouncement(string announcement, string activityId, AutomationNotificationKind kind, AutomationNotificationProcessing processing)
        {
            m_announcement = (announcement);
            ;
            m_activityId = (activityId);
            m_kind = (kind);
            m_processing = (processing);
        }

        public string Announcement { get => m_announcement; }
        public string ActivityId { get => m_activityId; }
        public AutomationNotificationKind Kind { get => m_kind; }
        public AutomationNotificationProcessing Processing { get => m_processing; }

        public static bool IsValid(NarratorAnnouncement? announcement)
        {
            return announcement is not null && !string.IsNullOrEmpty(announcement.Announcement);
        }

        public static NarratorAnnouncement GetDisplayUpdatedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.DisplayUpdated, AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetMaxDigitsReachedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.MaxDigitsReached, AutomationNotificationKind.Other, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetMemoryClearedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.MemoryCleared, AutomationNotificationKind.ItemRemoved, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetMemoryItemChangedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.MemoryItemChanged, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.MostRecent);
        }

        public static NarratorAnnouncement GetMemoryItemAddedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.MemoryItemAdded, AutomationNotificationKind.ItemAdded, AutomationNotificationProcessing.MostRecent);
        }

        public static NarratorAnnouncement GetHistoryClearedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.HistoryCleared, AutomationNotificationKind.ItemRemoved, AutomationNotificationProcessing.MostRecent);
        }

        public static NarratorAnnouncement GetHistorySlotClearedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.HistorySlotCleared, AutomationNotificationKind.ItemRemoved, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetCategoryNameChangedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.CategoryNameChanged, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetUpdateCurrencyRatesAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.UpdateCurrencyRates, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetDisplayCopiedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.DisplayCopied, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetOpenParenthesisCountChangedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.OpenParenthesisCountChanged, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetNoRightParenthesisAddedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.NoParenthesisAdded, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetGraphModeChangedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.GraphModeChanged, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetGraphViewChangedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.GraphViewChanged, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.CurrentThenMostRecent);
        }

        public static NarratorAnnouncement GetFunctionRemovedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.FunctionRemoved, AutomationNotificationKind.ItemRemoved, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetGraphViewBestFitChangedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.GraphViewBestFitChanged, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.MostRecent);
        }

        public static NarratorAnnouncement GetAlwaysOnTopChangedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.AlwaysOnTop, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetBitShiftRadioButtonCheckedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.BitShiftRadioButtonContent, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent);
        }

        public static NarratorAnnouncement GetSettingsPageOpenedAnnouncement(string announcement)
        {
            return new NarratorAnnouncement(announcement, CalculatorActivityIds.SettingsPageOpened, AutomationNotificationKind.ActionCompleted, AutomationNotificationProcessing.ImportantMostRecent);
        }
    }
}
