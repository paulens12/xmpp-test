using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace xmpp_test
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ChatPage : Page
    {
        public ObservableCollection<Message> Messages { get; set; }
        public ObservableCollection<string> Participants { get; set; }
        public ChatPage()
        {
            this.InitializeComponent();
            Messages = new ObservableCollection<Message>();
            Messages.Add(new Message("message 1", "sender", DateTime.Now));

            Participants = new ObservableCollection<string>();
            Participants.Add("user");
            Participants.Add("user1");
            Participants.Add("user2");
            Participants.Add("user3");
            Participants.Add("user4");
            Participants.Add("user5");
            Participants.Add("user6");
            Participants.Add("user7");
        }

        private void MessagesList_ItemClick(object sender, ItemClickEventArgs e)
        {
            Debug.WriteLine(((Message) e.ClickedItem).Content);
            Messages.Add(new Message(Guid.NewGuid().ToString(), "some sender", DateTime.Now));
            MessagesList.UpdateLayout();
            MessagesList.ScrollIntoView(Messages.Last());
        }

        private void UsersList_ItemClick(object sender, ItemClickEventArgs e)
        {
            Debug.WriteLine(e.ClickedItem);
        }
    }
}
