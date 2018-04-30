using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xmpp_test
{
    public class Message
    {
        public string Content { get; private set; }
        public Contact Sender { get; private set; }
        public string Date { get; private set; }

        public Message(string content, Contact sender, DateTime date)
        {
            Content = content;
            Sender = sender;
            Date = date.ToString("G");
        }
    }
}
