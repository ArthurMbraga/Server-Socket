using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Drawing;

namespace ServerLan
{
    public class Client
    {
        Socket client_socket;

        public event onReceiveText onreceivedata;
        public event onReceiveBitmap onreceivebitmap;
        public event onConnect onconnect;
        
        public Client(string server_ip, int port)
        {
            client_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress address = IPAddress.Parse(server_ip);
            IPEndPoint ipe = new IPEndPoint(address, port);
            connect(ipe);
        }

        public Client(IPAddress host_ip, int port)
        {
            client_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint ipe = new IPEndPoint(host_ip, port);
            connect(ipe);
        }

        public void disconnect()
        {
            Output.sendMsg("Client", MessageType.Process, "Disconnecting...");
            client_socket.Shutdown(SocketShutdown.Both);
            client_socket.Close();
            Output.sendMsg("Client", MessageType.Process, "Disconnected");
        }    

        private void connect(IPEndPoint ipe)
        {
            try
            {
                Output.sendMsg("Client", MessageType.Process, "Connecting to the server...");
                client_socket.BeginConnect(ipe, new AsyncCallback(connectCallback), client_socket);                
            }
            catch (ArgumentNullException ae)
            {
                Output.sendMsg("Client", MessageType.Error, "ArgumentNullException : " + ae.ToString());
            }
            catch (SocketException se)
            {
                Output.sendMsg("Client", MessageType.Error, "SocketException : " + se.ToString());
            }
            catch (Exception e)
            {
                Output.sendMsg("Client", MessageType.Error, "Unexpected exception : " + e.ToString());
            }
        }

        private void connectCallback(IAsyncResult ar)
        {
            try
            {
                client_socket.EndConnect(ar);

              
                Output.sendMsg("Client", MessageType.Process, "Connected");

                if(onconnect != null)
                    onconnect();

                StateObject state = new StateObject();
                state.workSocket = client_socket;

                client_socket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(readCallback), state);
                Output.sendMsg("Client", MessageType.Process, "Starting to receive data from the server...");
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }    

        List<DataBuilder> readqueue = new List<DataBuilder>();

        private void readCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            //Read data from the client socket.  
            int read_bytes = handler.EndReceive(ar);

            // Data was read from the client socket.  
            if (read_bytes > 0)
            {
                DataBuilder databuilder = null;

                List<DataBuilder> to_remove = new List<DataBuilder>();

                //Check if it's in the queue
                foreach (DataBuilder db in readqueue)
                    if (db == null)
                        to_remove.Add(db);
                    else
                    if (db.socket == handler)
                        databuilder = db;

                foreach (DataBuilder db in to_remove)
                    readqueue.Remove(db);


                //Variable that receives the buffer without tags
                byte[] realContent;

                if (databuilder == null)
                {
                    realContent = new byte[read_bytes - MessageTags.tagLength];

                    //Get inicial tag
                    byte[] start = new byte[MessageTags.tagLength];
                    Array.Copy(state.buffer, start, MessageTags.tagLength);

                    DataType type;
                    if (start.SequenceEqual(MessageTags.bTextStartTag))
                        type = DataType.text;
                    else if (start.SequenceEqual(MessageTags.bImageStartTag))
                        type = DataType.image;
                    else
                        throw new Exception("Invalid data");

                    databuilder = new DataBuilder(handler, type);
                    readqueue.Add(databuilder);

                    //Remove inicial tag and zeros
                    Array.Copy(state.buffer, MessageTags.tagLength - 1, realContent, 0, read_bytes - MessageTags.tagLength);
                }
                else
                {
                    realContent = new byte[read_bytes];
                    //Remove zeros
                    Array.Copy(state.buffer, 0, realContent, 0, read_bytes);
                }

                if (realContent.Length > 0)
                {
                    byte[] end = new byte[MessageTags.tagLength];
                    Array.Copy(realContent, realContent.Length - MessageTags.tagLength, end, 0, MessageTags.tagLength);

                    //Check if it contains the end tag
                    if (end.SequenceEqual(MessageTags.bEndTag))
                    {
                        //Remove end tag
                        byte[] newcontent = new byte[realContent.Length - MessageTags.tagLength];

                        Array.Copy(realContent, 0, newcontent, 0, newcontent.Length);

                        realContent = newcontent;
                        databuilder.receivedAllData = true;

                        try
                        {
                            if (readqueue.Contains(databuilder))
                                readqueue.Remove(databuilder);
                        }
                        catch (Exception e)
                        {
                            Output.sendMsg("Client", MessageType.Error, "Exeption: " + e.ToString());
                        }
                    }


                    //Add realcontent to dadabuilder buffer
                    databuilder.addPackage(realContent);
                }

                Output.sendMsg("Client", MessageType.Process, "Received " + read_bytes + " bits from the client(" + handler.LocalEndPoint.ToString() + ")");

                if (databuilder.receivedAllData)
                {
                    Output.sendMsg("Client", MessageType.Process, "All data received from the client(" + handler.LocalEndPoint.ToString() + ")");

                    if (databuilder.datatype == DataType.text)
                    {
                        //Convert data to string
                        string message = Encoding.ASCII.GetString(databuilder.buffer);
                        Output.sendMsg("Client", MessageType.ContentMessage, "Received all text: " + message);

                        if (onreceivedata != null)
                            onreceivedata(message, databuilder.socket);
                    }
                    else if (databuilder.datatype == DataType.image)
                    {
                        Output.sendMsg("Client", MessageType.ContentMessage, "Received all Bitmap");

                        //Convert data to bitmap
                        Bitmap bitmap = ImageManager.byteToImage(databuilder.buffer);

                        if (onreceivebitmap != null)
                            onreceivebitmap(bitmap, databuilder.socket);
                    }
                }

                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(readCallback), state);
            }
            else
                handler.Close();
        }

        public void send(string data)
        {
            // Convert the string data to byte data using ASCII encoding.              
            byte[] byteData = Encoding.ASCII.GetBytes(data);
            send(byteData, DataType.text);
        }

        public void send(Bitmap bitmap)
        {   
            byte[] byteData = ImageManager.imageToByte(bitmap);
            send(byteData,DataType.image);               
        }

        private void send(byte[] data, DataType type)
        {
            byte[] byteStartTag = new byte[0];
            if (type == DataType.image)
                byteStartTag = MessageTags.bImageStartTag;
            else if (type == DataType.text)
                byteStartTag = MessageTags.bTextStartTag;


            byte[] byteEndTag = Encoding.ASCII.GetBytes(MessageTags.EndTag);

            Output.sendMsg("Client", MessageType.Process, "Starting to send data to the server...");
            client_socket.BeginSend(byteStartTag, 0, byteStartTag.Length, 0, new AsyncCallback(sendCallback), client_socket);
            client_socket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(sendCallback), client_socket);
            client_socket.BeginSend(byteEndTag, 0, byteEndTag.Length, 0, new AsyncCallback(sendCallback), client_socket);
        }

        private void sendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);
                Output.sendMsg("Client", MessageType.Process, "Sent " + bytesSent + " bytes to the server.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
