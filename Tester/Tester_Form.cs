using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ServerLan;
using System.Net.Sockets;

namespace Tester
{
    public partial class Tester : Form
    {
        Server server;
        Client client1;
        Client client2;
        string ip = "25.145.99.147";

        public Tester()
        {        
            InitializeComponent();            
        }

        private void Output_onreceivemessage(string sender_name, MessageType type, string message)
        {
            Invoke((MethodInvoker)delegate
            {
                richTextBox5.Text = richTextBox5.Text + "\n[" + sender_name + "][" + type.ToString() + "]: " + message;
            });
        }

        private void button1_Click(object sender, EventArgs e)
        {
            client2.send("Client 2");
        }

        private void button2_Click(object sender, EventArgs e)
        {
            client1.send("Client 1");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            server.sendAll("Host");
        }

        private void aloo(Socket socket)
        {
            Invoke((MethodInvoker)delegate
            {
                richTextBox4.Text = richTextBox4.Text + "Disconectou: " + socket.LocalEndPoint.ToString();
            });
        }

        private void client1_r(string m, Socket socket)
        {
            Invoke((MethodInvoker)delegate
            {
                richTextBox2.Text = richTextBox2.Text + "\n" + m;
            });
        }

        private void client2_r(string m, Socket socket)
        {
            Invoke((MethodInvoker)delegate
            {
                richTextBox3.Text = richTextBox3.Text + "\n" + m;
            });
        }

        private void server_r(string m, Socket socket)
        {
            Invoke((MethodInvoker)delegate
            {
                richTextBox1.Text = richTextBox1.Text + "\n" + m;
            });
            
        }

        private void button5_Click(object sender, EventArgs e)
        {
            client2.disconnect();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            client2 = new Client(ip, 500);

            client2.onreceivedata += client2_r;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            client1.send(Properties.Resources.Test);
        }

        private void Tester_Load(object sender, EventArgs e)
        {
            server = new Server(ip, 500);
            client1 = new Client(ip, 500);
            client2 = new Client(ip, 500);

            client1.onreceivedata += client1_r;
            client2.onreceivedata += client2_r;
            server.onreceivedata += server_r;
            server.onclientdisconnect += aloo;
            server.onreceivebitmap += Server_onreceivebitmap;
            Output.onreceivemessage += Output_onreceivemessage;
        }

        private void Server_onreceivebitmap(Bitmap bitmap, Socket client)
        {
            panel1.BackgroundImage = bitmap;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            client1.send(Properties.Resources.Test);
            client2.send("Client 2");
        }
    }
}
