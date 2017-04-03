using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLan
{
    public delegate void onReceiveMessage(string sender_name, MessageType type, string message);

    public static class Output
    {
        public static event onReceiveMessage onreceivemessage;

        public static void sendMsg(string sender_name, MessageType type, string message)
        {
            if (onreceivemessage != null)
                onreceivemessage(sender_name, type, message);
        }
    }

    public enum MessageType
    {
        ContentMessage,
        Error,
        Process
    }
}
