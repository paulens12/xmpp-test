using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xmpp_test.Xmpp
{
    class XmppException : Exception
    {
        public XmppException() : base() { }
        public XmppException(string message) : base(message) { }
        public XmppException(string message, Exception innerException) : base(message, innerException) { }
    }
}
