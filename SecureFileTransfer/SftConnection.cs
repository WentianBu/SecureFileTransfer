using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SecureFileTransfer
{
    internal class SftConnection
    {
        public TcpClient TheTcpClient { get; set; }
        public SslStream TheSslStream { get; set; }

        public bool IsAuthenticated { get; set; } = false;
        //public bool IsMainConnection { get; set; } = false; 


        private static bool ValidateServerCertificate(
              object? sender,
              X509Certificate? certificate,
              X509Chain? chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            Console.WriteLine("Certificate error: {0}", sslPolicyErrors);

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }

        internal SftConnection(TcpClient tcpClient, string trustedServerCertName)
        {
            TheTcpClient = tcpClient;
            TheSslStream = new SslStream(TheTcpClient.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);
            try
            {
                TheSslStream.AuthenticateAsClient(trustedServerCertName);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                TheSslStream.Close();
                TheTcpClient.Close();
                return;
            }
        }

        internal SftConnection(TcpClient tcpClient, X509Certificate2 serverCert)
        {
            TheTcpClient = tcpClient;
            TheSslStream = new SslStream(TheTcpClient.GetStream(), false);
            try
            {
                TheSslStream.AuthenticateAsServer(serverCert, clientCertificateRequired: false, checkCertificateRevocation: true);
                // Display the properties and settings for the authenticated stream.
                DisplaySecurityLevel(TheSslStream);
                DisplaySecurityServices(TheSslStream);
                DisplayCertificateInformation(TheSslStream);
                DisplayStreamProperties(TheSslStream);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                TheSslStream.Close();
                TheTcpClient.Close();
                return;
            }
        }


        internal void WritePacket(SftPacket pkt)
        {
            TheSslStream.Write(pkt.ConvertToBytes());
        }

        internal SftPacket ReadPacket()
        {
            SftPacket inPkt;
            try
            {
                inPkt = new(TheSslStream);
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
                if (ex.InnerException != null)
                    Console.WriteLine(ex.InnerException.Message);
                TheSslStream.Close();
                TheTcpClient.Close();
                throw ex;
            }
            
            return inPkt;
        }

        internal void Close()
        {
            TheSslStream.Close();
            TheTcpClient.Close();
        }

        /// <summary>
        /// Check whether the SftConnection authenticated.
        /// If not, send UnAuth Packet
        /// </summary>
        /// <param name="reqId"></param>
        /// <returns></returns>
        internal bool CheckAuthStatus(ushort reqId)
        {
            if (IsAuthenticated) return true;
            SftPacket sftPacket = new(SftCmdType.UnAuth, 0, reqId, null);
            WritePacket(sftPacket);
            return false;
        }

        internal void SendReset(ushort clientId, ushort reqId)
        {
            SftPacket sftPacket = new(SftCmdType.Reset, clientId, reqId, null);
            WritePacket(sftPacket);
            return;
        }

        internal void SendBye(ushort clientId, ushort reqId)
        {
            SftPacket sftPacket = new(SftCmdType.Bye, clientId, reqId, null);
            WritePacket(sftPacket);
            return;
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

    }
}
