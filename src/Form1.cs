using System;
using System.Drawing;
using System.Net;
using System.Windows.Forms;

namespace tcp_server
{
    public partial class Form1 : Form
    {
        //private readonly TcpServer _tcpServer;
        private readonly SslTcpServer _tcpServer;
        //private readonly int _port = 11111;
        private readonly int _port = 44333;
        public Form1()
        {
            InitializeComponent();
            //_tcpServer = new TcpServer(new IPEndPoint(IPAddress.Any, _port));
            _tcpServer = new SslTcpServer(new IPEndPoint(IPAddress.Any, _port),
                @"C:\tmp0\localhost.crt");
            _tcpServer.DoAction += WriteReceivedMessage;
            label_status.Text = "closed";
            label_status.BackColor = Color.LightGray;
        }

        private void WriteReceivedMessage(byte[] s)
        {
            if (InvokeRequired)
            {
                _ = Invoke(new Action(delegate
                {
                    var message = System.Text.Encoding.UTF8.GetString(s);
                    text_log.Text += "message:" + message + Environment.NewLine;
                }));
            }
            else
            {
                var message = System.Text.Encoding.UTF8.GetString(s);
                text_log.Text += "message:" + message + Environment.NewLine;
            }


        }

        private void Button_start_Click(object sender, EventArgs e)
        {
            _tcpServer.StartListening();
            label_status.Text = $"listening port:{_port}";
            label_status.BackColor = Color.Lime;
        }

        private void Button_stop_Click(object sender, EventArgs e)
        {
            _tcpServer.StopListning();
            label_status.Text = "closed";
            label_status.BackColor = Color.LightGray;
        }
    }
}
