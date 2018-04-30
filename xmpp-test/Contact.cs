namespace xmpp_test
{
    public class Contact
    {
        public string Name { get; private set; }
        public string JID { get; private set; }

        public Contact(string name, string jid)
        {
            Name = name;
            JID = jid;
        }
    }
}