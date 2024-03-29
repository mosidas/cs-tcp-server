﻿using System;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace tcp_server
{
    public partial class Form1 : Form
    {
        private readonly SslTcpServer _tcpServer;
        //private readonly TcpServer _tcpServer;
        //private readonly TcpSocketServer _tcpServer;
        private readonly int _port = 44333;
        private readonly string _certFilePath = @"C:\tmp0\localhost.crt";

        public Form1()
        {
            InitializeComponent();
            _tcpServer = new SslTcpServer(_port, _certFilePath);
            //_tcpServer = new TcpServer(new IPEndPoint(IPAddress.Any, _port));
            //_tcpServer = new TcpSocketServer(new IPEndPoint(IPAddress.Any, _port));
            _tcpServer.ReceiveAction += WriteReceivedMessageInvoke;

            label_status.Text = "closed";
            label_status.BackColor = Color.LightGray;
            ThreadPool.GetMaxThreads(out int workerThreads, out int portThreads);
            Console.WriteLine("Worker threads={0}, Completion port threads={1}", workerThreads, portThreads);
        }

        private byte[] WriteReceivedMessageInvoke(byte[] s, IPEndPoint endPoint)
        {
            if (InvokeRequired)
            {
                return (byte[])Invoke(new Func<byte[]>(() =>
                {
                    return WriteReceivedMessage(s, endPoint);
                }));
            }
            else
            {
                return WriteReceivedMessage(s, endPoint);
            }
        }

        private byte[] WriteReceivedMessage(byte[] s, IPEndPoint endPoint)
        {
            var message = System.Text.Encoding.UTF8.GetString(s);
            text_log.Text += $"from: {endPoint.Address}:{endPoint.Port} - message: {message}" + Environment.NewLine;
            text_log.Text += $"to  : {endPoint.Address}:{endPoint.Port} - message: OK" + Environment.NewLine;

            return System.Text.Encoding.UTF8.GetBytes("OK");
        }

        private void Button_start_Click(object sender, EventArgs e)
        {
            _tcpServer.StartListening();
            label_status.Text = $"listening port:{_port}";
            label_status.BackColor = Color.Lime;
        }

        private void Button_stop_Click(object sender, EventArgs e)
        {
            _tcpServer.StopListening();
            label_status.Text = "closed";
            label_status.BackColor = Color.LightGray;
        }
    }
}
