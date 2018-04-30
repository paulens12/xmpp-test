using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
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
        public ObservableCollection<Contact> Contacts { get; set; }

        private Xmpp.Connection connection;
        public ChatPage()
        {
            this.InitializeComponent();

            Contacts = new ObservableCollection<Contact>();
            Messages = new ObservableCollection<Message>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Xmpp.Connection)
                connection = (Xmpp.Connection)e.Parameter;

            XmlDocument doc = new XmlDocument();
            XmlNode query = doc.CreateElement("query", "jabber:iq:roster");
            rosterId = Guid.NewGuid().ToString();

            connection.NewIq += FillRoster;
            await connection.SendIq(rosterId, "get", query);
            
            base.OnNavigatedTo(e);
        }

        private static string rosterId;

        private async void FillRoster(XmlNode data)
        {
            if (!data.Attributes["id"].Value.Equals(rosterId))
                return;

            XmlNode query = null;
            foreach (XmlNode node in data.ChildNodes)
                if (node.Name.Equals("query") && node.NamespaceURI.Equals("jabber:iq:roster"))
                    query = node;
            if (query == null)
                return;

            foreach(XmlNode item in query)
            {
                if (!item.Name.Equals("item"))
                    continue;
                string jid = item.Attributes["jid"].Value;
                string name = item.Attributes["name"].Value;
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    Contacts.Add(new Contact(name, jid));
                });
            }
        }

        private void MessagesList_ItemClick(object sender, ItemClickEventArgs e)
        {
            Debug.WriteLine(((Message) e.ClickedItem).Content);

            Messages.Add(new Message(Guid.NewGuid().ToString(), Contacts[0], DateTime.Now));
            MessagesList.UpdateLayout();
            MessagesList.ScrollIntoView(Messages.Last());
        }

        private async void AddContactButton_Click(object sender, RoutedEventArgs e)
        {
            string jid = AddContactTextbox.Text;
            if (String.IsNullOrEmpty(jid))
                return;

            AddContactButton.IsEnabled = false;
            AddContactProgress.IsActive = true;
            try
            {
                await AddContact(jid);
            }
            finally
            {
                AddContactProgress.IsActive = false;
                AddContactButton.IsEnabled = true;
            }
        }

        private async Task AddContact(string jid)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode query = doc.CreateElement("query", "jabber:iq:roster");
            rosterId = Guid.NewGuid().ToString();

            XmlNode item = doc.CreateElement("item");
            XmlAttribute jidA = doc.CreateAttribute("jid");
            jidA.Value = jid;
            XmlAttribute nameA = doc.CreateAttribute("name");
            nameA.Value = jid.Split('@')[0];
            item.Attributes.Append(jidA);
            item.Attributes.Append(nameA);

            query.AppendChild(item);

            bool waiting = true;
            Action<XmlNode> verify = (x) =>
            {
                if(x.Attributes["id"].Value.Equals(rosterId))
                {
                    if(x.Attributes["type"].Value.Equals("result"))
                        Contacts.Add(new Contact(nameA.Value, jid));

                    waiting = false;
                }
            };

            connection.NewIq += verify;
            await connection.SendIq(rosterId, "get", query);
            
            while (waiting)
                await Task.Delay(100);
            connection.NewIq -= verify;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
