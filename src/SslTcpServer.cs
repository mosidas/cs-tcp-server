using System;
using System.Collections.Generic;
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

        private readonly IPEndPoint _endPoint;
        private readonly TcpListener _listener;
        private readonly X509Certificate _serverCertificate;

        private List<TcpClient> clients { get; set; } = new List<TcpClient>();

        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="ep">IPアドレス、ポート</param>
        /// <param name="certFilePath">証明書ファイルのパス</param>
        public SslTcpServer(int port, string certFilePath)
        {
            _endPoint = new IPEndPoint(IPAddress.Any, port);
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
            _ = Task.Run(() => ConnectAsync());
        }

        /// <summary>
        /// tcpクライアントの接続の受付を終了する。
        /// </summary>
        public void StopListening(bool close = false)
        {
            Debug.WriteLine("stop listening");
            if (close)
            {
                clients.ForEach(c => c.Close());
            }
            _listener.Stop();
        }

        /// <summary>
        /// tcpクライアントの接続を待機して接続
        /// </summary>
        /// <returns></returns>
        private async Task ConnectAsync()
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
                    clients.Add(client);
                }
                catch (ObjectDisposedException)
                {
                    Debug.WriteLine("listen end");
                    return;
                }

                // メッセージ受信開始
                _ = Task.Run(() => {
                    var ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                    var port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
                    var result = StartConnection(client);
                    Debug.WriteLine($"receive end {ip}:{port} - {result}");
                });
            }
        }

        /// <summary>
        /// receive message from client and responce to cilent.
        /// </summary>
        /// <param name="client">client</param>
        /// <returns>true: connection end(no error) false: connection end(error exists)</returns>
        private bool StartConnection(TcpClient client)
        {
            Debug.WriteLine($"receive start thread id:{Thread.CurrentThread.ManagedThreadId} - {((IPEndPoint)client.Client.RemoteEndPoint).Address}:{((IPEndPoint)client.Client.RemoteEndPoint).Port}");
            using (var sslStream = new SslStream(client.GetStream(), false))
            {
                if (!StartTls(sslStream))
                {
                    Close(client, sslStream);
                    return false;
                }

                try
                {
                    // 2 minutes
                    sslStream.ReadTimeout = 2 * 60 * 1000;

                    while (true)
                    {
                        // Receive
                        var receivedMessage = Receive(client, sslStream);

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
                    Close(client, sslStream);
                }
            }
        }

        private bool StartTls(SslStream sslStream)
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

                return true;
            }
            catch (AuthenticationException aex)
            {
                Debug.WriteLine("authentication failed - closing the connection.");
                Debug.WriteLine(aex.Message);
                return false;
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

        private byte[] Receive(TcpClient client, SslStream sslStream)
        {

            byte[] buffer = new byte[client.ReceiveBufferSize];
            sslStream.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        private void Responce(TcpClient client, SslStream sslStream, byte[] receivedMessage)
        {
            // get responce message
            var responce = ReceiveAction(receivedMessage, (IPEndPoint)client.Client.RemoteEndPoint);

            if (responce.Length > 0)
            {
                // responce
                sslStream.Write(responce, 0, responce.Length);
            }
        }

        private bool Close(TcpClient client, SslStream sslStream)
        {
            try
            {
                sslStream.Close();
                client.Close();
                return true;
            }
            catch { return false; }
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
