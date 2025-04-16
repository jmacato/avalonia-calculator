using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

namespace CalculatorApp.ViewModel.Common.Automation
{
    public sealed class NarratorNotifier : DependencyObject
    {
        private static DependencyProperty s_announcementProperty;
        private TextBlock m_announcementElement;
        static NarratorNotifier()
        {
            RegisterDependencyProperties();
        }

        public NarratorNotifier()
        {
        }

        public void Announce(NarratorAnnouncement announcement)
        {
            if (NarratorAnnouncement.IsValid(announcement))
            {
                if (m_announcementElement == null)
                {
                    m_announcementElement = new TextBlock();
                }

                var peer = FrameworkElementAutomationPeer.FromElement(m_announcementElement);
                if (peer != null)
                {
                    peer.RaiseNotificationEvent(
                        announcement.Kind,
                        announcement.Processing,
                        announcement.Announcement,
                        announcement.ActivityId);
                }
            }
        }

        public static void RegisterDependencyProperties()
        {
            s_announcementProperty = DependencyProperty.Register(
                "Announcement",
                typeof(NarratorAnnouncement),
                typeof(NarratorNotifier),
                new PropertyMetadata(
                    null,
                    OnAnnouncementChanged));
        }

        private static void OnAnnouncementChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var instance = dependencyObject as NarratorNotifier;
            if (instance != null)
            {
                instance.Announce(e.NewValue as NarratorAnnouncement);
            }
        }

        public NarratorAnnouncement Announcement
        {
            get { return (NarratorAnnouncement)GetValue(AnnouncementProperty); }
            set { SetValue(AnnouncementProperty, value); }
        }

        public static DependencyProperty AnnouncementProperty
        {
            get { return s_announcementProperty; }
        }

        public static NarratorAnnouncement GetAnnouncement(DependencyObject element)
        {
            return (NarratorAnnouncement)element.GetValue(s_announcementProperty);
        }

        public static void SetAnnouncement(DependencyObject element, NarratorAnnouncement value)
        {
            element.SetValue(s_announcementProperty, value);
        }
    }
}
