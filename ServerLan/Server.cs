using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Drawing;

namespace ServerLan
{
    public class Server
    {
        private Socket server_socket;
        private List<Socket> clients_connected = new List<Socket>();

        public event onClientDisconnect onclientdisconnect;
        public event onReceiveText onreceivedata;
        public event onReceiveBitmap onreceivebitmap;

        public Server(string ip, int port)
        {
            server_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            initializeServer(new IPEndPoint(IPAddress.Parse(ip), port));
        }

        public Server(IPAddress ip, int port)
        {
            server_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            initializeServer(new IPEndPoint(ip, port));
        }

        private void initializeServer(IPEndPoint ipe)
        {
            try
            {
                Output.sendMsg("Server", MessageType.Process, "Starting server...");
                server_socket.Bind(ipe);
                server_socket.Listen(100);
                Output.sendMsg("Server", MessageType.Process, "Server started.");

                server_socket.BeginAccept(new AsyncCallback(acceptCallback), server_socket);
                Output.sendMsg("Server", MessageType.Process, "Waiting for connection...");
            }
            catch (ArgumentNullException ae)
            {
                Output.sendMsg("Server", MessageType.Error, "ArgumentNullException : " + ae.ToString());
            }
            catch (SocketException se)
            {
                Output.sendMsg("Server", MessageType.Error, "SocketException : " + se.ToString());
            }
            catch (Exception e)
            {
                Output.sendMsg("Server", MessageType.Error, "Unexpected exception : " + e.ToString());
            }
        }

        private void acceptCallback(IAsyncResult ar)
        {
            Socket handler = server_socket.EndAccept(ar);
            clients_connected.Add(handler);

            Output.sendMsg("Server", MessageType.Process, "Client connected(" + handler.LocalEndPoint.ToString() + ")");

            //Create the state object.  
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(readCallback), state);
            Output.sendMsg("Server", MessageType.Process, "Starting to receive data from the new client...");

            server_socket.BeginAccept(new AsyncCallback(acceptCallback), server_socket);
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
                            Output.sendMsg("Server", MessageType.Error, "Exeption: " + e.ToString());
                        }
                    }


                    //Add realcontent to dadabuilder buffer
                    databuilder.addPackage(realContent);
                }

                Output.sendMsg("Server", MessageType.Process, "Received " + read_bytes + " bits from the client(" + handler.LocalEndPoint.ToString() + ")");

                if (databuilder.receivedAllData)
                {
                    Output.sendMsg("Server", MessageType.Process, "All data received from the client(" + handler.LocalEndPoint.ToString() + ")");

                    if (databuilder.datatype == DataType.text)
                    {
                        //Convert data to string
                        string message = Encoding.ASCII.GetString(databuilder.buffer);
                        Output.sendMsg("Server", MessageType.ContentMessage, "Received all text: " + message);

                        if (onreceivedata != null)
                            onreceivedata(message, databuilder.socket);
                    }
                    else if (databuilder.datatype == DataType.image)
                    {
                        Output.sendMsg("Server", MessageType.ContentMessage, "Received all Bitmap");

                        //Convert data to bitmap
                        Bitmap bitmap = ImageManager.byteToImage(databuilder.buffer);

                        if (onreceivebitmap != null)
                            onreceivebitmap(bitmap, databuilder.socket);
                    }
                }

                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(readCallback), state);
            }
            else
            {
                clients_connected.Remove(handler);
                Output.sendMsg("Server", MessageType.Process, "Client disconnected(" + handler.LocalEndPoint.ToString() + ")");

                if (onclientdisconnect != null)
                    onclientdisconnect(handler);

                handler.Close();
            }
        }

        private void sendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                Output.sendMsg("Server", MessageType.Process, "Sent " + bytesSent + " bytes to client");
            }
            catch (ArgumentNullException ae)
            {
                Output.sendMsg("Server", MessageType.Error, "ArgumentNullException : " + ae.ToString());
            }
            catch (SocketException se)
            {
                Output.sendMsg("Server", MessageType.Error, "SocketException : " + se.ToString());
            }
            catch (Exception e)
            {
                Output.sendMsg("Server", MessageType.Error, "Unexpected exception : " + e.ToString());
            }
        }

        public void send(Socket client, string data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            send(byteData, client, DataType.text);
        }

        public void send(Socket client, Bitmap bitmap)
        {
            // Convert the bitmap to byte data.  
            byte[] byteData = ImageManager.imageToByte(bitmap);

            send(byteData, client, DataType.image);
        }

        private void send(byte[] data, Socket client, DataType type)
        {
            byte[] byteStartTag = new byte[0];
            if (type == DataType.image)
            {
                Output.sendMsg("Server", MessageType.Process, "Starting to send a bitmap to client(+" + client.LocalEndPoint.ToString() + "+)...");
                byteStartTag = MessageTags.bImageStartTag;
            }
            else if (type == DataType.text)
            {
                Output.sendMsg("Server", MessageType.Process, "Starting to send a message to client(+" + client.LocalEndPoint.ToString() + "+)...");
                byteStartTag = MessageTags.bTextStartTag;
            }

            byte[] byteEndTag = Encoding.ASCII.GetBytes(MessageTags.EndTag);

            client.BeginSend(byteStartTag, 0, byteStartTag.Length, 0, new AsyncCallback(sendCallback), client);
            client.BeginSend(data, 0, data.Length, 0, new AsyncCallback(sendCallback), client);
            client.BeginSend(byteEndTag, 0, byteEndTag.Length, 0, new AsyncCallback(sendCallback), client);
        }

        public void sendAll(string data)
        {
            List<Socket> to_remove = new List<Socket>();

            foreach (Socket socket in clients_connected)
                if (socket.Connected)
                    send(socket, data);
                else
                    to_remove.Add(socket);

            foreach (Socket socket in to_remove)
                clients_connected.Remove(socket);

        }
    }
}