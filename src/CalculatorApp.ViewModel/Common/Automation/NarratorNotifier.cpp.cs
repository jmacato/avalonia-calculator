using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

namespace CalculatorApp.ViewModel.Common.Automation
{
    public sealed class NarratorNotifier : DependencyObject
    {
        private TextBlock? m_announcementElement;

        public NarratorNotifier()
        {
        }

        public void Announce(NarratorAnnouncement? announcement)
        {
            if (announcement is not null && NarratorAnnouncement.IsValid(announcement))
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

        private static void OnAnnouncementChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            if (dependencyObject is NarratorNotifier instance)
            {
                instance.Announce(e.NewValue as NarratorAnnouncement);
            }
        }

        public NarratorAnnouncement? Announcement
        {
            get { return GetValue(AnnouncementProperty) as NarratorAnnouncement; }
            set { SetValue(AnnouncementProperty, value); }
        }

        public static DependencyProperty AnnouncementProperty { get; } = DependencyProperty.Register(
            nameof(Announcement),
            typeof(NarratorAnnouncement),
            typeof(NarratorNotifier),
            new PropertyMetadata(null, OnAnnouncementChanged));

        public static NarratorAnnouncement? GetAnnouncement(DependencyObject element)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            return element.GetValue(AnnouncementProperty) as NarratorAnnouncement;
        }

        public static void SetAnnouncement(DependencyObject element, NarratorAnnouncement? value)
        {
            if (element is null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            element.SetValue(AnnouncementProperty, value);
        }
    }
}
