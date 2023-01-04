using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace tcp_server
{
    public class TcpServer
    {
        public delegate void ReceiveMessageCallback(string message);
        public event ReceiveMessageCallback WriteText;

        private readonly IPEndPoint _endPoint;
        private TcpListener _listener;

        public TcpServer(IPEndPoint ep)
        {
            _endPoint = ep;
        }
        public void StartListening()
        {
            Debug.WriteLine("StartListening");
            _listener = new TcpListener(_endPoint);
            _listener.Start();

            _ = Listen();
        }

        public void StopListning()
        {
            Debug.WriteLine("StopListning");
            _listener.Stop();
        }

        private async Task Listen()
        {
            Debug.WriteLine("listening...");
            while (true)
            {
                Debug.WriteLine($"waiting thread id:{Thread.CurrentThread.ManagedThreadId}");
                TcpClient client;
                try
                {
                    // クライアントの接続を待機
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch(ObjectDisposedException)
                {
                    return;
                }

                // メッセージ受信
                Debug.WriteLine("ReceiveMessage");
                _ = ReceiveMessage(client);
 
            }
        }

        private async Task ReceiveMessage(TcpClient client)
        {
            try
            {
                string message = "";

                var ns = client.GetStream();
                byte[] result_bytes = new byte[256];

                using (var ms = new System.IO.MemoryStream())
                {
                    do
                    {
                        int result_size = await ns.ReadAsync(result_bytes, 0, result_bytes.Length);

                        if (result_size == 0)
                        {
                            Debug.WriteLine("receive end");
                            return;
                        }

                        ms.Write(result_bytes, 0, result_size);
                    } while (ns.DataAvailable);

                    message = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                }

                Debug.WriteLine($"received massage:{message}");
                WriteText(message);

                _ = ReceiveMessage(client);
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                throw;
            }
        }

        private void SetKeepAlive(TcpClient client)
        {
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            byte[] tcp_keepalive = new byte[12];
            BitConverter.GetBytes((Int32)1).CopyTo(tcp_keepalive, 0);//onoffスイッチ.
            BitConverter.GetBytes((Int32)2000).CopyTo(tcp_keepalive, 4);//wait time.(ms)
            BitConverter.GetBytes((Int32)1000).CopyTo(tcp_keepalive, 8);//interval.(ms)
            client.Client.IOControl(IOControlCode.KeepAliveValues, tcp_keepalive, null);
        }
    }
}
