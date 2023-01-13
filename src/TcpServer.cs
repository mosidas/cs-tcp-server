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
        public delegate byte[] ReceiveMessageCallback(byte[] message, IPEndPoint endPoint);
        public event ReceiveMessageCallback ReceiveAction;
        public delegate bool AcceptTcpClientCallback(IPEndPoint endPoint);
        public event AcceptTcpClientCallback LoginAction;

        private readonly IPEndPoint _endPoint;
        private readonly TcpListener _listener;

        public TcpServer(IPEndPoint ep)
        {
            _endPoint = ep;
            _listener = new TcpListener(_endPoint);
        }

        /// <summary>
        /// tcpクライアントの接続の受付を開始する。(LISTEN)
        /// </summary>
        public void StartListening()
        {
            Debug.WriteLine($"start listening...   ip address:{_endPoint.Address} port:{_endPoint.Port}" );
            _listener.Start();

            _ = Task.Run(() => Listen());
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
            Debug.WriteLine("listen start");
            while (true)
            {
                Debug.WriteLine($"listening thread id:{Thread.CurrentThread.ManagedThreadId}");

                TcpClient client;
                try
                {
                    // tcpクライアントの接続を待機
                    client = await _listener.AcceptTcpClientAsync();
                    if (!LoginAction((IPEndPoint)client.Client.RemoteEndPoint))
                    {
                        Debug.WriteLine($"{((IPEndPoint)client.Client.RemoteEndPoint).Address}:{((IPEndPoint)client.Client.RemoteEndPoint).Port} is not logined");
                        client.Close();
                        continue;
                    }
                }
                catch (ObjectDisposedException)
                {
                    Debug.WriteLine("listen end");
                    return;
                }

                // メッセージ受信
                _ = Task.Run(() => {
                    var ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                    var port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
                    ReceiveMessage(client);
                    Debug.WriteLine($"receive end {ip}:{port}");
                });
            }
        }

        /// <summary>
        /// tcpクライアントのメッセージを受信し、何らかのアクションをする。
        /// 何らかのアクションは、DoActionに登録する。
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private void ReceiveMessage(TcpClient client)
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
                        int result_size = ns.Read(result_bytes, 0, result_bytes.Length);

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
                ReceiveAction(message, (IPEndPoint)client.Client.RemoteEndPoint);

                ReceiveMessage(client);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return;
            }
            finally
            {
                client.Close();
            }
        }

        private byte[] Read(TcpClient client)
        {
            byte[] receivedMessage = null;
            byte[] buffer = new byte[client.ReceiveBufferSize];
            using (var ms = new System.IO.MemoryStream())
            {
                // read
                int readSize = client.GetStream().Read(buffer, 0, buffer.Length);
                ms.Write(buffer, 0, readSize);
                receivedMessage = ms.ToArray();
            }

            // debug 
            string str = "";
            for (int i = 0; i < receivedMessage.Length; i++)
            {
                str += string.Format("{0:X2}", receivedMessage[i]);
            }
            var ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
            var port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
            Debug.WriteLine($"from {ip}:{port} - received massage: {str}");

            return receivedMessage;
        }

        /// <summary>
        /// ref: https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c
        /// </summary>
        /// <param name="client">tcp client</param>
        /// <returns>true: connected, false: disconnected</returns>
        private bool ClientConnected(TcpClient client)
        {
            var a = client.Client.Poll(100, SelectMode.SelectRead);
            var b = (client.Client.Available == 0);
            if (a && b)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void Responce(TcpClient client, byte[] receivedMessage)
        {
            // get responce message
            var responce = ReceiveAction(receivedMessage, (IPEndPoint)client.Client.RemoteEndPoint);

            if (responce.Length > 0)
            {
                // responce
                client.GetStream().Write(responce, 0, responce.Length);

                // debug
                var str = "";
                for (int i = 0; i < responce.Length; i++)
                {
                    str += string.Format("{0:X2}", responce[i]);
                }
                var ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                var port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
                Debug.WriteLine($"to {ip}:{port} - responce massage: {str}");
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
