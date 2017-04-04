using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Drawing;

namespace ServerLan
{
    public delegate void onReceiveText(string text, Socket client);
    public delegate void onReceiveBitmap(Bitmap bitmap, Socket client);
    public delegate void onClientConnect(Socket client);
    public delegate void onConnect();
    public delegate void onClientDisconnect(Socket client);

    // State object for reading client data asynchronously  
    public class StateObject
    {
        // Client  socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 4095;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];


    }

    public static class MessageTags
    {
        public const string textStartTag = "tx$>";
        public const string ImageStartTag = "im$>";
        public const string EndTag = "<$$$";
        public const int tagLength = 4;

        public readonly static byte[] bTextStartTag;
        public readonly static byte[] bImageStartTag;
        public readonly static byte[] bEndTag;


        static MessageTags()
        {
            bTextStartTag = Encoding.ASCII.GetBytes(textStartTag);
            bImageStartTag = Encoding.ASCII.GetBytes(ImageStartTag);
            bEndTag = Encoding.ASCII.GetBytes(EndTag);
        }
    }

    public class DataBuilder
    {
        public byte[] buffer = new byte[0];
        public Socket socket;
        public bool receivedAllData = false;
        public DataType datatype;

        public DataBuilder(Socket socket, DataType datatype)
        {
            this.socket = socket;
            this.datatype = datatype;
        }

        public void addPackage(byte[] package)
        {
            buffer = buffer.Concat(package).ToArray();
        }
    }

    public static class ImageManager
    {
        static ImageConverter converter = new ImageConverter();

        public static byte[] imageToByte(Bitmap bitmap)
        {
            return (byte[])converter.ConvertTo(bitmap, typeof(byte[]));
        }

        public static Bitmap byteToImage(byte[] bytes)
        {
            Image img = (Image)converter.ConvertFrom(bytes);
            return new Bitmap(img);
        }
    }

    public enum DataType
    {
        image,
        text
    }
}