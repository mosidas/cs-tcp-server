using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace tcp_server
{
    public partial class Form1 : Form
    {
        //new IPEndPoint(IPAddress.Any, port);
        private TcpServer tcpServer;

        public Form1()
        {
            InitializeComponent();
            tcpServer = new TcpServer(new IPEndPoint(IPAddress.Any, 11111));
            tcpServer.WriteText += WriteReceivedMessage;
            label_status.Text = "Closed";
            label_status.BackColor = Color.LightGray;
        }

        private void WriteReceivedMessage(string s)
        {
            text_log.Text += "---" + Environment.NewLine + s + Environment.NewLine;
        }

        private void Button_start_Click(object sender, EventArgs e)
        {
            tcpServer.StartListening();
            label_status.Text = "Listening port:11111";
            label_status.BackColor = Color.Lime;
        }

        private void Button_stop_Click(object sender, EventArgs e)
        {
            tcpServer.StopListning();
            label_status.Text = "Closed";
            label_status.BackColor = Color.LightGray;
        }
    }
}
