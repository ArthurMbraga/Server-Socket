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
    public class Client
    {
        Socket client_socket;

        public event onReceiveData onreceivedata;
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

                if (client_socket.Connected)
                    Output.sendMsg("Client", MessageType.Process, "Connected");

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

        private void readCallback(IAsyncResult ar)
        {
            if (client_socket.Connected)
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                // Read data from the client socket.  

                int read = handler.EndReceive(ar);
                byte[] dataBuf = new byte[read];

                // Data was read from the client socket.  
                if (read > 0)
                {
                    string message = Encoding.ASCII.GetString(state.buffer, 0, read);
                    Output.sendMsg("Client", MessageType.Process, "Received " + message.Length + " bits from the server");
                    Output.sendMsg("Client", MessageType.ContentMessage, "Message data: " + message);                   

                    if(onreceivedata != null)
                        onreceivedata(message);

                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(readCallback), state);
                }
                else
                    handler.Close();
            }
        }

        public void send(string data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            Output.sendMsg("Client", MessageType.Process, "Starting to send a message to the server...");
            // Begin sending the data to the remote device.  
            client_socket.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(sendCallback), client_socket);
        }

        private static void sendCallback(IAsyncResult ar)
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
