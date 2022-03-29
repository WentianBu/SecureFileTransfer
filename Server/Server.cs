using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SecureFileTransfer;
using SecureFileTransfer.Data;

namespace Server // Note: actual namespace depends on the project name.
{
    internal class Server
    {
        static X509Certificate2 serverCert = null;
        public static void RunServer(string certPemFilePath, string keyPemFilePath)
        {
            // a bug in the windows: pem must be exported to pkcs12
            serverCert = new X509Certificate2(
                X509Certificate2.CreateFromPemFile(certPemFilePath, keyPemFilePath).Export(X509ContentType.Pkcs12));
            //return new X509Certificate2(sslCert.Export(X509ContentType.Pkcs12));
            TcpListener listener = new TcpListener(IPAddress.Any, 9090);
            listener.Start();
            while (true)
            {
                Console.WriteLine("Waiting for a client to connect...");
                TcpClient client = listener.AcceptTcpClient();
                ProcessClient(client);
            }

        }
        static void ProcessClient(TcpClient client)
        {
            SslStream sslStream = new SslStream(client.GetStream(), false);
            try
            {
                sslStream.AuthenticateAsServer(serverCert, clientCertificateRequired: false, checkCertificateRevocation: true);

                // Display the properties and settings for the authenticated stream.
                DisplaySecurityLevel(sslStream);
                DisplaySecurityServices(sslStream);
                DisplayCertificateInformation(sslStream);
                DisplayStreamProperties(sslStream);

                Console.WriteLine("Waiting for client message...");
                ReadMessage(sslStream);
                //Console.WriteLine("Received: {0}", messageData);

                SftServerHelloData sftServerHelloData = new(
                    "Hello, this is SFT server.",
                    ((IPEndPoint)client.Client.RemoteEndPoint).Address);
                SftPacket sftPacket = new(SftCmdType.ServerHello, 0, 1, SftPacketData.Serialize(sftServerHelloData));
                //byte[] message = Encoding.UTF8.GetBytes("Hello from the server.<EOF>");
                Console.WriteLine("Sending hello message.");
                sslStream.Write(sftPacket.ConvertToBytes());

            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                sslStream.Close();
                client.Close();
                return;
            }
            finally
            {
                sslStream.Close();
                client.Close();
            }
        }
        static void ReadMessage(SslStream sslStream)
        {
            byte[] buffer = new byte[2048];
            
            //StringBuilder messageData = new StringBuilder();
            int bytes = sslStream.Read(buffer, 0, Common.SFT_HEADER_SIZE);
            while (bytes!=0)
            {
                SftPacketHeader header = new(buffer);
                header.Display();
                if (header.DataLen <= 0)
                {
                    continue;
                }
                Array.Clear(buffer, 0, buffer.Length);
                bytes = sslStream.Read(buffer, 0, header.DataLen);
                if (header.cmdType == SftCmdType.ClientHello)
                {
                    SftClientHelloData sftClientHelloData = SftPacketData.Deserialize<SftClientHelloData>(buffer);
                    SftPacketData.DisplayData(sftClientHelloData);
                }

                Array.Clear(buffer);
                break;
                bytes = sslStream.Read(buffer, 0, Common.SFT_HEADER_SIZE);
                
            }
            return;
        }

        /*
            do
            {
                bytes = sslStream.Read(buffer, 0, buffer.Length);
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                if (messageData.ToString().IndexOf("<EOF>") != -1)
                {
                    break;
                }
            } while (bytes != 0);
            return messageData.ToString();
        }*/
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
        private static void DisplayUsage()
        {
            Console.WriteLine("To start the server specify:");
            Console.WriteLine("serverSync certificateFile.cer");
            Environment.Exit(1);
        }
        static int Main(string[] args)
        {
            //Console.WriteLine("Hello World!");
            string certPemFilePath = @"C:\Users\Wentian Bu\certs\server.cer";
            string keyPemFilePath = @"C:\Users\Wentian Bu\certs\server.key";
            //if (args==null||args.Length<1)
            //{
            //    DisplayUsage();
            //}
            RunServer(certPemFilePath, keyPemFilePath);
            return 0;
        }
    }
}
