using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace tcp_server
{
    public class SslTcpServer
    {
        public delegate byte[] ReceiveMessageCallback(byte[] message, IPEndPoint endPoint);
        public event ReceiveMessageCallback ReceiveAction;
        public delegate bool AcceptTcpClientCallback(IPEndPoint endPoint);
        public event AcceptTcpClientCallback LoginAction;

        private readonly IPEndPoint _endPoint;
        private readonly TcpListener _listener;
        private readonly X509Certificate _serverCertificate;

        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="ep">IPアドレス、ポート</param>
        /// <param name="certFilePath">証明書ファイルのパス</param>
        public SslTcpServer(IPEndPoint ep, string certFilePath)
        {
            _endPoint = ep;
            _serverCertificate = X509Certificate.CreateFromCertFile(certFilePath);
            _listener = new TcpListener(_endPoint);
            _listener.Server.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 255);
        }

        /// <summary>
        /// tcpクライアントの接続の受付を開始する。
        /// </summary>
        public void StartListening()
        {
            Debug.WriteLine($"start listening...   ip address:{_endPoint.Address} port:{_endPoint.Port}");
            _listener.Start();
            _ = Task.Run(() => Listen());
        }

        /// <summary>
        /// tcpクライアントの接続の受付を終了する。
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
                    var result = ReceiveMessage(client);
                    Debug.WriteLine($"receive end {ip}:{port} - {result}");
                });
            }
        }

        /// <summary>
        /// receive message from client and responce to cilent.
        /// </summary>
        /// <param name="client">client</param>
        /// <returns>true: connection end(no error) false: connection end(error exists)</returns>
        private bool ReceiveMessage(TcpClient client)
        {
            Debug.WriteLine($"receive start thread id:{Thread.CurrentThread.ManagedThreadId} - {((IPEndPoint)client.Client.RemoteEndPoint).Address}:{((IPEndPoint)client.Client.RemoteEndPoint).Port}");
            using (var sslStream = new SslStream(client.GetStream(), false))
            {
                try
                {
                    // Authenticate: SslProtocols = TLS1.2
                    sslStream.AuthenticateAsServer(_serverCertificate,
                        clientCertificateRequired: false,
                        enabledSslProtocols: SslProtocols.Tls12,
                        checkCertificateRevocation: true);

                    // Display the properties and settings for the authenticated stream.
                    SslStreamDebugger.DisplaySecurityLevel(sslStream);
                    SslStreamDebugger.DisplaySecurityServices(sslStream);
                    SslStreamDebugger.DisplayCertificateInformation(sslStream);
                    SslStreamDebugger.DisplayStreamProperties(sslStream);
                }
                catch (AuthenticationException aex)
                {
                    Debug.WriteLine("authentication failed - closing the connection.");
                    sslStream.Close();
                    client.Close();
                    Debug.WriteLine(aex.Message);
                    return false;
                }

                try
                {
                    // 2 minutes
                    sslStream.ReadTimeout = 2 * 60 * 1000;

                    while (true)
                    {
                        // Read
                        var receivedMessage = Read(client, sslStream);

                        if (!ClientIsConnected(client))
                        {
                            return true;
                        }

                        // Responce
                        Responce(client, sslStream, receivedMessage);   
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return false;
                }
                finally
                {
                    sslStream.Close();
                    client.Close();
                }
            }
        }

        /// <summary>
        /// ref: https://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c
        /// </summary>
        /// <param name="client">tcp client</param>
        /// <returns>true: connected, false: disconnected</returns>
        private bool ClientIsConnected(TcpClient client)
        {
            var a = client.Client.Poll(100, SelectMode.SelectRead);
            var b = (client.Client.Available == 0);
            if(a && b)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private byte[] Read(TcpClient client, SslStream sslStream)
        {
            byte[] receivedMessage = null;
            byte[] buffer = new byte[client.ReceiveBufferSize];
            using (var ms = new System.IO.MemoryStream())
            {
                // read
                // 16355byteごとに読み込む。(.net framework4.8)
                int readSize = sslStream.Read(buffer, 0, buffer.Length);
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

        private void Responce(TcpClient client, SslStream sslStream, byte[] receivedMessage)
        {
            // get responce message
            var responce = ReceiveAction(receivedMessage, (IPEndPoint)client.Client.RemoteEndPoint);

            if (responce.Length > 0)
            {
                // responce
                sslStream.Write(responce, 0, responce.Length);

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

        /// <summary>
        /// keep alive用。今のところ未使用。
        /// </summary>
        /// <param name="client"></param>
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
