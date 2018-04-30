using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Xml.Schema;

namespace xmpp_test.Xmpp
{
    public class Connection
    {
        private Socket socket;
        private Stream stream;
        private string domain;
        private CancellationTokenSource cancelAllTasks;
        private string JID = "kompTinkluKlientas";

        public event Action<XmlNode> NewData;
        public event Action<XmlNode> NewIq;
        public event Action StreamClosed;

        public static async Task<Connection> Open(string url, string port, string username, string password)
        {
            Connection conn = await Task.Run(() => { return new Connection(url, Int32.Parse(port)); });
            await conn.Authenticate(username, password);
            return conn;
        }

        private Connection(string url, int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            domain = url;
            socket.Connect(url, port);
            stream = new NetworkStream(socket);
            cancelAllTasks = new CancellationTokenSource();
        }

        private async Task Authenticate(string username, string password)
        {
            await Send(GetStreamHeader());
            XmlNode resp = null;
            XmlNode features = null;
            do
            {
                resp = GetNode("stream:stream", "stream:features");
                if (resp.Name.Equals("stream:stream"))
                {
                    foreach (XmlNode child in resp.ChildNodes)
                        if (child.Name == "stream:features")
                            features = child;
                }
                else
                    features = resp;
            }
            while (features == null);
            XmlNode starttls = null;
            foreach (XmlNode child in features.ChildNodes)
                if (child.Name == "starttls")
                    starttls = child;

            if (starttls == null)
                throw new XmppException("The server does not support SSL. Aborting.");

            await Send("<starttls xmlns='urn:ietf:params:xml:ns:xmpp-tls' />");
            resp = GetNode("proceed", "failure");
            if (resp.Name.Equals("failure"))
                throw new XmppException("Failed to initiate SSL.");

            stream = new SslStream(stream, true, (a, b, c, d) => { return true; });
            await ((SslStream)stream).AuthenticateAsClientAsync(domain);

            Task.Factory.StartNew(() => ReceiveLoop(cancelAllTasks.Token), TaskCreationOptions.LongRunning);

            bool waiting = true;
            Action<XmlNode> waitAction = (x) =>
            {
                if (x.Name.Equals("stream:features"))
                {
                    resp = x;
                    waiting = false;
                }

                if(x.ChildNodes.Count != 0)
                {
                    foreach(XmlNode node in x.ChildNodes)
                        if(node.Name.Equals("stream:features"))
                        {
                            resp = node;
                            waiting = false;
                        }
                }
            };
            NewData += waitAction;

            await Send(GetStreamHeader());
            while (waiting)
                await Task.Delay(100);
            NewData -= waitAction;

            XmlNode mechanisms = null;
            foreach (XmlNode child in resp.ChildNodes)
                if (child.Name == "mechanisms")
                    mechanisms = child;

            string supported = "PLAIN";

            foreach(XmlNode mechanism in mechanisms)
            {
                if(supported.Equals(mechanism.InnerXml))
                {
                    await AuthenticateSASL(username, password);
                    break;
                }
            }

            waiting = true;
            waitAction = async (x) =>
            {
                waiting = !await BindStream(x);
            };
            NewData += waitAction;
            await Send(GetStreamHeader());
            while (waiting)
                await Task.Delay(100);
            NewData -= waitAction;

            Send("<presence/>");
        }

        private async Task<bool> BindStream(XmlNode node)
        {
            if(node.Name.Equals("stream:stream"))
            {
                foreach(XmlNode child in node.ChildNodes)
                {
                    if (child.Name.Equals("stream:features"))
                    {
                        node = child;
                        break;
                    }
                }
            }
            if(node.Name.Equals("stream:features"))
            {
                foreach(XmlNode child in node.ChildNodes)
                {
                    if(child.Name.Equals("bind"))
                    {
                        string iqIdStr = Guid.NewGuid().ToString();
                        XmlDocument doc = new XmlDocument();
                        XmlNode iq = doc.CreateElement("iq");
                        XmlAttribute iqId = doc.CreateAttribute("id");
                        iqId.Value = iqIdStr;
                        iq.Attributes.Append(iqId);
                        XmlAttribute iqType = doc.CreateAttribute("type");
                        iqType.Value = "set";
                        iq.Attributes.Append(iqType);

                        XmlNode bind = doc.CreateElement("bind", "urn:ietf:params:xml:ns:xmpp-bind");

                        XmlNode jid = doc.CreateElement("jid");
                        jid.InnerText = this.JID;
                        
                        //bind.AppendChild(jid);

                        iq.AppendChild(bind);

                        bool waiting = true;
                        bool success = false;
                        Action<XmlNode> waitAction = (x) =>
                        {
                            if(x.Attributes["id"].Value.Equals(iqIdStr))
                            {
                                success = FinishBind(x);
                                waiting = false;
                            }
                        };
                        NewIq += waitAction;

                        //await SendXml(iq);

                        await Send(String.Format("<iq id=\"{0}\" type=\"set\"><bind xmlns=\"urn:ietf:params:xml:ns:xmpp-bind\"/></iq>", iqIdStr));

                        while (waiting)
                            await Task.Delay(100);
                        NewIq -= waitAction;

                        if (!success)
                            throw new XmppException("Failed to bind stream.");
                    }
                }
                return true;
            }
            return false;
        }

        private bool FinishBind(XmlNode node)
        {
            if (node.Attributes["type"].Value.Equals("error"))
                return false;

            XmlNode bind = null;
            foreach(XmlNode x in node.ChildNodes)
                if (x.Name.Equals("bind"))
                    bind = x;
            if (bind == null || bind.ChildNodes.Count == 0)
                return false;

            XmlNode jid = null;
            foreach (XmlNode x in node.ChildNodes)
                if (x.Name.Equals("bind"))
                    jid = x;
            if (jid == null)
                return false;

            this.JID = jid.InnerText;
            return true;
        }

        private async Task AuthenticateSASL(string username, string password)
        {
            byte[] initial = Encoding.UTF8.GetBytes("\0" + username + "\0" + password);
            string initStr = Convert.ToBase64String(initial);
            
            bool waiting = true;
            bool success = true;
            Action<XmlNode> waitAction = (x) =>
            {
                if (x.Name.Equals("success"))
                {
                    success = true;
                    waiting = false;
                }
                else
                {
                    success = false;
                    waiting = false;
                }
            };
            NewData += waitAction;
            await Send(String.Format("<auth xmlns=\"urn:ietf:params:xml:ns:xmpp-sasl\" mechanism=\"PLAIN\">{0}</auth>", initStr));
            while (waiting)
                await Task.Delay(100);
            NewData -= waitAction;

            if (!success)
                throw new XmppException("Wrong username or password.");
        }

        private XmlNode GetNode(params string[] names)
        {
            while(true)
            {
                XmlDocument doc = new XmlDocument();
                XmlSchema schema = new XmlSchema();
                schema.Namespaces.Add("stream", "http://etherx.jabber.org/streams");
                doc.Schemas.Add(schema);
                string resp = GetNextMessage();
                bool wtf = false;
                if (resp.IndexOf("<stream:stream") != -1)
                {
                    wtf = true;
                    resp += "</stream:stream>";
                }
                else
                {
                    resp = GetStreamHeader() + resp + "</stream:stream>";
                }
                doc.LoadXml(resp);

                XmlNodeList nodes = null;
                if (wtf)
                    nodes = doc.ChildNodes;
                else
                    nodes = doc.LastChild.ChildNodes;

                foreach (XmlNode x in nodes)
                {
                    foreach(string name in names)
                        if(string.IsNullOrEmpty(name) || x.Name.Equals(name))
                        {
                            return x;
                        }
                }
            }
        }

        private string GetStreamHeader()
        {
            return String.Format("<?xml version='1.0'?><stream:stream xmlns=\"jabber:client\" version=\"1.0\" xmlns:stream=\"http://etherx.jabber.org/streams\" to=\"{0}\" xml:lang=\"en\">", domain);
            /*
            XmlDocument xmlDoc = new XmlDocument();

            XmlNode ver = xmlDoc.CreateXmlDeclaration("1.0", null, null);
            xmlDoc.AppendChild(ver);

            XmlNode node = xmlDoc.CreateElement("stream", "stream", "jabber:client");
            XmlAttribute attr = xmlDoc.CreateAttribute("version");
            attr.Value = "1.0";
            node.Attributes.Append(attr);

            attr = xmlDoc.CreateAttribute("xmlns:stream");
            attr.Value = "http://etherx.jabber.org/streams";
            node.Attributes.Append(attr);

            attr = xmlDoc.CreateAttribute("to");
            attr.Value = domain;
            node.Attributes.Append(attr);

            attr = xmlDoc.CreateAttribute("xml:lang");
            attr.Value = "en";
            node.Attributes.Append(attr);

            xmlDoc.AppendChild(node);
            return xmlDoc;
            */
        }

        public async Task Send(string request)
        {
            //request = request.Replace(" xmlns=\"\"", "");
            byte[] msg = Encoding.ASCII.GetBytes(request);
            await stream.WriteAsync(msg, 0, request.Length);
            Debug.WriteLine("Sent: " + request);
        }

        private async Task SendXml(XmlNode xml)
        {
            await Send(xml.OuterXml);
        }

        public async Task SendIq(string id, string type, XmlNode childXml)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode iq = doc.CreateElement("iq");
            XmlAttribute idAttr = doc.CreateAttribute("id");
            idAttr.Value = id;
            iq.Attributes.Append(idAttr);
            XmlAttribute tAttr = doc.CreateAttribute("type");
            tAttr.Value = type;
            iq.Attributes.Append(tAttr);
            XmlAttribute from = doc.CreateAttribute("from");
            from.Value = JID;
            iq.Attributes.Append(from);

            XmlNode imported = doc.ImportNode(childXml, true);
            iq.AppendChild(imported);
            await SendXml(iq);
        }

        private void ReceiveLoop(CancellationToken ct)
        {
            while(!ct.IsCancellationRequested)
            {
                string msg = GetNextMessage();
                if (msg == "")
                    continue;
                XmlDocument doc = new XmlDocument();
                XmlSchema schema = new XmlSchema();
                schema.Namespaces.Add("stream", "http://etherx.jabber.org/streams");
                doc.Schemas.Add(schema);

                //quit if the stream has been closed
                if (msg.IndexOf("</stream:stream>") != -1)
                {
                    StreamClosed?.Invoke();
                    return;
                }
                bool wtf = false;
                // Correct XML errors: stream opening should be the only case in which there is an open tag
                if (msg.IndexOf("<stream:stream") != -1)
                {
                    msg += "</stream:stream>";
                    wtf = true;
                }
                else
                {
                    msg = GetStreamHeader() + msg + "</stream:stream>";
                }
                
                doc.LoadXml(msg);
                XmlNodeList nodes = null;
                if (wtf)
                    nodes = doc.ChildNodes;
                else
                    nodes = doc.LastChild.ChildNodes;

                foreach (XmlNode node in nodes)
                {
                    NewData?.Invoke(node);
                    if (node.Name.Equals("iq"))
                        NewIq?.Invoke(node);
                }
            }
        }

        private string GetNextMessage()
        {
            byte[] buffer = new byte[100000];
            int length = stream.Read(buffer, 0, 100000);
            string message = Encoding.ASCII.GetString(buffer, 0, length);
            Debug.WriteLine("Received: " + message);
            return message;
        }
    }
}
