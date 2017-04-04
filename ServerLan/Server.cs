using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace ServerLan
{
    public class Server
    {
        private Socket server_socket;
        private List<Socket> clients_connected = new List<Socket>();

        public event onClientDisconnect onclientdisconnect;
        public event onReceiveData onreceivedata;

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

        private void readCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            //Read data from the client socket.  
            int read = handler.EndReceive(ar);
            byte[] dataBuf = new byte[read];

            // Data was read from the client socket.  
            if (read > 0)
            {
                string message = Encoding.ASCII.GetString(state.buffer, 0, read);
                Output.sendMsg("Server", MessageType.Process, "Received " + message.Length + " bits from the client(" + handler.LocalEndPoint.ToString() + ")");
                Output.sendMsg("Server", MessageType.ContentMessage, "Message data: " + message);

                if (onreceivedata != null)
                    onreceivedata(message);

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

            Output.sendMsg("Server", MessageType.Process, "Starting to send a message to client(+" + client.LocalEndPoint.ToString() + "+)...");
            // Begin sending the data to the remote device.  
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(sendCallback), client);
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