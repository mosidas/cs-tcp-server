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
        public event ReceiveMessageCallback DoAction;

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
        }

        /// <summary>
        /// tcpクライアントの接続の受付を開始する。(LISTEN)
        /// </summary>
        public void StartListening()
        {
            Debug.WriteLine($"start listening...   ip address:{_endPoint.Address} port:{_endPoint.Port}");
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
            Debug.WriteLine("listen start");
            while (true)
            {
                Debug.WriteLine($"listening thread id:{Thread.CurrentThread.ManagedThreadId}");

                TcpClient client;
                try
                {
                    // tcpクライアントの接続を待機
                    client = await _listener.AcceptTcpClientAsync();
                }
                catch (ObjectDisposedException)
                {
                    Debug.WriteLine("listen end");
                    return;
                }

                // メッセージ受信
                _ = Task.Run(async () => { 
                    var result = await ReceiveMessage(client);
                    Debug.WriteLine($"receive end: {result}");
                });
            }
        }

        /// <summary>
        /// tcpクライアントのメッセージを受信し、何らかのアクションをする。
        /// 何らかのアクションは、DoActionに登録する。
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task<bool> ReceiveMessage(TcpClient client)
        {
            Debug.WriteLine("receive start");
            using (var sslStream = new SslStream(client.GetStream(), false))
            {
                try
                {
                    sslStream.AuthenticateAsServer(_serverCertificate,
                        clientCertificateRequired: false,
                        enabledSslProtocols: SslProtocols.Tls12,
                        checkCertificateRevocation: true);

                    // デバッグ表示
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
                    while (true)
                    {
                        if (!client.Connected)
                        {
                            return true;
                        }

                        byte[] receivedMessage = null;
                        byte[] result_bytes = new byte[client.ReceiveBufferSize];
                        int bytes = -1;
                        using (var ms = new System.IO.MemoryStream())
                        {
                            // メッセージを読み取る。
                            bytes = await sslStream.ReadAsync(result_bytes, 0, result_bytes.Length);
                            ms.Write(result_bytes, 0, bytes);
                            receivedMessage = ms.ToArray();
                        }

                        if (bytes == 0)
                        {
                            Debug.WriteLine("receive end");
                            return true;
                        }

                        string str = "";
                        for (int i = 0; i < receivedMessage.Length; i++)
                        {
                            str += string.Format("{0:X2}", receivedMessage[i]);
                        }
                        var ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                        var port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
                        Debug.WriteLine($"from {ip}:{port} - received massage: {str}");

                        // 受信データに対して何らかの処理をする。
                        var responce = DoAction(receivedMessage, (IPEndPoint)client.Client.RemoteEndPoint);

                        // 応答
                        await sslStream.WriteAsync(responce, 0, responce.Length);

                        str = "";
                        for (int i = 0; i < responce.Length; i++)
                        {
                            str += string.Format("{0:X2}", responce[i]);
                        }
                        Debug.WriteLine($"to {ip}:{port} - responce massage: {str}");
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
