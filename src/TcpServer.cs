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
        public delegate void ReceiveMessageCallback(byte[] message);
        public event ReceiveMessageCallback DoAction;

        private readonly IPEndPoint _endPoint;
        private TcpListener _listener;

        public TcpServer(IPEndPoint ep)
        {
            _endPoint = ep;
        }

        /// <summary>
        /// tcpクライアントの接続の受付を開始する。(LISTEN)
        /// </summary>
        public void StartListening()
        {
            Debug.WriteLine($"start listening...   ip address:{_endPoint.Address} port:{_endPoint.Port}" );
            _listener = new TcpListener(_endPoint);
            _listener.Start();

            _ = Listen();
        }

        /// <summary>
        /// tcpクライアントの接続の受付を開始する。(CLOSED)
        /// </summary>
        public void StopListning()
        {
            Debug.WriteLine("stop listening");
            _listener.Stop();
        }

        /// <summary>
        /// tcpクライアントの接続を待機
        /// </summary>
        /// <returns></returns>
        private async Task Listen()
        {
            Debug.WriteLine("listening...");
            while (true)
            {
                Debug.WriteLine($"listening thread id:{Thread.CurrentThread.ManagedThreadId}");
                TcpClient client;
                try
                {
                    // tcpクライアントの接続を待機
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch(ObjectDisposedException)
                {
                    return;
                }

                // メッセージ受信
                Debug.WriteLine("receive start");
                _ = ReceiveMessage(client);
 
            }
        }

        /// <summary>
        /// tcpクライアントのメッセージを受信し、何らかのアクションをする。
        /// 何らかのアクションは、DoActionに登録する。
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task ReceiveMessage(TcpClient client)
        {
            try
            {
                byte[] message = null;

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

                    message = ms.ToArray();
                }

                string str = "";
                for (int i = 0; i < message.Length; i++)
                {
                    str += string.Format("{0:X2}", message[i]);
                }

                Debug.WriteLine($"received massage:{str}");

                // 受信データに対して何らかの処理をする。
                DoAction(message);

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
