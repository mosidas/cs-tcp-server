using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace tcp_server
{
    public class SslTcpServer
    {
        public delegate void ReceiveMessageCallback(byte[] message);
        public event ReceiveMessageCallback DoAction;

        private readonly IPEndPoint _endPoint;
        private TcpListener _listener;

        private static X509Certificate _serverCertificate = null;

        public SslTcpServer(IPEndPoint ep, string certFilePath)
        {
            _endPoint = ep;
            _serverCertificate = X509Certificate.CreateFromCertFile(certFilePath);

            Debug.WriteLine($"start listening...   ip address:{_endPoint.Address} port:{_endPoint.Port}");
            _listener = new TcpListener(_endPoint);
        }

        /// <summary>
        /// tcpクライアントの接続の受付を開始する。(LISTEN)
        /// </summary>
        public void StartListening()
        {
            Debug.WriteLine($"start listening...   ip address:{_endPoint.Address} port:{_endPoint.Port}");
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
                catch (ObjectDisposedException)
                {
                    return;
                }

                // メッセージ受信
                Debug.WriteLine("receive start");
                _ = Task.Run(() => { _ = ReceiveMessage(client); });

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
            SslStream sslStream = new SslStream(client.GetStream(), false);

            try
            {
                sslStream.AuthenticateAsServer(_serverCertificate,
                    clientCertificateRequired: false,
                    enabledSslProtocols: SslProtocols.Tls12,
                    checkCertificateRevocation: true);

                // Display the properties and settings for the authenticated stream.
                DisplaySecurityLevel(sslStream);
                DisplaySecurityServices(sslStream);
                DisplayCertificateInformation(sslStream);
                DisplayStreamProperties(sslStream);
            }
            catch (AuthenticationException aex)
            {
                Debug.WriteLine("authentication failed - closing the connection.");
                sslStream.Close();
                client.Close();
                Debug.WriteLine(aex.ToString());
                return;
            }

            try
            {
                while (true)
                {
                    if (!client.Connected)
                    {
                        break;
                    }

                    byte[] message = null;
                    byte[] result_bytes = new byte[client.ReceiveBufferSize];
                    int bytes = -1;
                    using (var ms = new System.IO.MemoryStream())
                    {
                        bytes = await sslStream.ReadAsync(result_bytes, 0, result_bytes.Length);
                        ms.Write(result_bytes, 0, bytes);
                        message = ms.ToArray();
                    }

                    if (bytes == 0)
                    {
                        Debug.WriteLine("receive end");
                        return;
                    }

                    string str = "";
                    for (int i = 0; i < message.Length; i++)
                    {
                        str += string.Format("{0:X2}", message[i]);
                    }

                    Debug.WriteLine($"received massage:{str}");

                    // 受信データに対して何らかの処理をする。
                    DoAction(message);
                }
            }
            
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                throw;
            }
        }

        static void DisplaySecurityLevel(SslStream stream)
        {
            Console.WriteLine("Cipher: {0} strength {1}", stream.CipherAlgorithm, stream.CipherStrength);
            Console.WriteLine("Hash: {0} strength {1}", stream.HashAlgorithm, stream.HashStrength);
            Console.WriteLine("Key exchange: {0} strength {1}", stream.KeyExchangeAlgorithm, stream.KeyExchangeStrength);
            Console.WriteLine("Protocol: {0}", stream.SslProtocol);
        }
        static void DisplaySecurityServices(SslStream stream)
        {
            Console.WriteLine("Is authenticated: {0} as server? {1}", stream.IsAuthenticated, stream.IsServer);
            Console.WriteLine("IsSigned: {0}", stream.IsSigned);
            Console.WriteLine("Is Encrypted: {0}", stream.IsEncrypted);
        }
        static void DisplayStreamProperties(SslStream stream)
        {
            Console.WriteLine("Can read: {0}, write {1}", stream.CanRead, stream.CanWrite);
            Console.WriteLine("Can timeout: {0}", stream.CanTimeout);
        }
        static void DisplayCertificateInformation(SslStream stream)
        {
            Console.WriteLine("Certificate revocation list checked: {0}", stream.CheckCertRevocationStatus);

            X509Certificate localCertificate = stream.LocalCertificate;
            if (stream.LocalCertificate != null)
            {
                Console.WriteLine("Local cert was issued to {0} and is valid from {1} until {2}.",
                    localCertificate.Subject,
                    localCertificate.GetEffectiveDateString(),
                    localCertificate.GetExpirationDateString());
            }
            else
            {
                Console.WriteLine("Local certificate is null.");
            }
            // Display the properties of the client's certificate.
            X509Certificate remoteCertificate = stream.RemoteCertificate;
            if (stream.RemoteCertificate != null)
            {
                Console.WriteLine("Remote cert was issued to {0} and is valid from {1} until {2}.",
                    remoteCertificate.Subject,
                    remoteCertificate.GetEffectiveDateString(),
                    remoteCertificate.GetExpirationDateString());
            }
            else
            {
                Console.WriteLine("Remote certificate is null.");
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
