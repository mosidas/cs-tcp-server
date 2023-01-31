using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace tcp_server
{
    internal class TcpSocketServer
    {
        public delegate byte[] ReceiveMessageCallback(byte[] message, IPEndPoint endPoint);
        public event ReceiveMessageCallback ReceiveAction;
        public delegate bool AcceptTcpClientCallback(IPEndPoint endPoint);
        public event AcceptTcpClientCallback LoginAction;

        private readonly IPEndPoint _endPoint;
        private Socket _listener;

        internal TcpSocketServer(IPEndPoint ep)
        {
            _endPoint = ep;
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 255);
        }

        /// <summary>
        /// tcpクライアントの接続の受付を開始する。(LISTEN)
        /// </summary>
        public void StartListening()
        {
            Debug.WriteLine($"start listening...   ip address:{_endPoint.Address} port:{_endPoint.Port}");
            _listener.Bind(_endPoint);

            _ = Task.Run(() => Listen());
        }

        /// <summary>
        /// tcpクライアントの接続の受付を開始する。(CLOSED)
        /// </summary>
        public void StopListning()
        {
            Debug.WriteLine("stop listening");
            _listener.Close();
            _listener = null;
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// tcpクライアントの接続を待機
        /// </summary>
        /// <returns></returns>
        public async void Listen()
        {
            Debug.WriteLine("listen start");
            
            _listener.Listen(int.MaxValue);
            Socket client;
            while (true)
            {
                Debug.WriteLine($"listening thread id:{Thread.CurrentThread.ManagedThreadId}");
                try
                {
                    client = await _listener.AcceptAsync();
                    if (!LoginAction((IPEndPoint)client.RemoteEndPoint))
                    {
                        Debug.WriteLine($"{((IPEndPoint)client.RemoteEndPoint).Address}:{((IPEndPoint)client.RemoteEndPoint).Port} is not logined");
                        client.Close();
                        continue;
                    }
                }
                catch(Exception)
                {
                    Debug.WriteLine("listen end");
                    return;
                }

                _ = Task.Run(() =>
                {
                    var ip = ((IPEndPoint)client.RemoteEndPoint).Address;
                    var port = ((IPEndPoint)client.RemoteEndPoint).Port;
                    var result = ReceiveMessage(client);
                    Debug.WriteLine($"receive end {ip}:{port} - {result}");

                });
            }
        }

        /// <summary>
        /// tcpクライアントのメッセージを受信し、何らかのアクションをする。
        /// 何らかのアクションは、DoActionに登録する。
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private bool ReceiveMessage(Socket client)
        {
            Debug.WriteLine($"receive start thread id:{Thread.CurrentThread.ManagedThreadId} - {((IPEndPoint)client.RemoteEndPoint).Address}:{((IPEndPoint)client.RemoteEndPoint).Port}");
            try
            {
                while (true)
                {
                    // Read
                    var receivedMessage = Read(client);

                    if (!ClientIsConnected(client))
                    {
                        return true;
                    }

                    // Responce
                    Responce(client, receivedMessage);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
            finally
            {
                client.Close();
            }

        }
        private byte[] Read(Socket client)
        {
            byte[] receivedMessage = null;
            byte[] buffer = new byte[client.ReceiveBufferSize];
            using (var ms = new System.IO.MemoryStream())
            {
                // read
                var stream = new NetworkStream(client, true);
                int readSize = stream.Read(buffer, 0, buffer.Length);
                ms.Write(buffer, 0, readSize);
                receivedMessage = ms.ToArray();
            }

            // debug 
            string str = "";
            for (int i = 0; i < receivedMessage.Length; i++)
            {
                str += string.Format("{0:X2}", receivedMessage[i]);
            }
            var ip = ((IPEndPoint)client.RemoteEndPoint).Address;
            var port = ((IPEndPoint)client.RemoteEndPoint).Port;
            Debug.WriteLine($"from {ip}:{port} - received massage: {str}");

            return receivedMessage;
        }

        private void Responce(Socket client, byte[] receivedMessage)
        {
            // get responce message
            var responce = ReceiveAction(receivedMessage, (IPEndPoint)client.RemoteEndPoint);

            if (responce.Length > 0)
            {
                // responce
                var stream = new NetworkStream(client, true);
                stream.Write(responce, 0, responce.Length);

                // debug
                var str = "";
                for (int i = 0; i < responce.Length; i++)
                {
                    str += string.Format("{0:X2}", responce[i]);
                }
                var ip = ((IPEndPoint)client.RemoteEndPoint).Address;
                var port = ((IPEndPoint)client.RemoteEndPoint).Port;
                Debug.WriteLine($"to {ip}:{port} - responce massage: {str}");
            }
        }

        /// <summary>
        /// ref: https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c
        /// </summary>
        /// <param name="client">tcp client</param>
        /// <returns>true: connected, false: disconnected</returns>
        private bool ClientIsConnected(Socket client)
        {
            var a = client.Poll(100, SelectMode.SelectRead);
            var b = (client.Available == 0);
            if (a && b)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        
    }
}
