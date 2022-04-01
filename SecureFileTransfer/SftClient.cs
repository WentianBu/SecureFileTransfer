using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SecureFileTransfer;
using SecureFileTransfer.Data;

namespace SecureFileTransfer.Client
{
    public class SftUnexpectedResponse : ApplicationException
    {
        public SftPacket? ExPkt { get; set; }

        public SftUnexpectedResponse() : base() { }

        public SftUnexpectedResponse(SftPacket pkt) : base()
        {
            ExPkt = pkt;
        }
        public SftUnexpectedResponse(SftPacket pkt, string? message) : base(message)
        {
            ExPkt = pkt;
        }
    }
    public class SftClientConfig
    {
        public string TrustedServerCertificateName { get; set; } = "SecureFileTransferProtocol Server";
        public uint SingleServerMaxConnections { get; set; } = 1;
        public int ClientReadTimeout { get; set; } = 10000;

    }


    public partial class SftClient
    {
        private ushort clientId = 0;
        private ushort reqId = 0;
        private string? authToken = null;
        private SftClientConfig config = null!;
        private string? serverName = null;
        private int serverPort;
        private SftConnection? mainConnection = null;
        private List<SftConnection> dataConnections = new();

        public SftClient(SftClientConfig c, string server, int port)
        {
            config = c;
            serverName = server;
            serverPort = port;
        }

        private void CreateConnection()
        {
            if (serverName == null) 
                throw new ArgumentNullException(nameof(serverName));
            TcpClient tcpClient = new(serverName, serverPort);
            SftConnection sftConnection = new(tcpClient, config.TrustedServerCertificateName);
            sftConnection.TheSslStream.ReadTimeout = config.ClientReadTimeout;
            if (mainConnection == null)
                mainConnection = sftConnection;
            else
                dataConnections.Add(mainConnection);
        }

        private SftServerHelloData? SendHello(SftConnection sftConnection, string? helloMessage)
        {
            SftClientHelloData helloData = new(helloMessage);
            SftPacket outPkt = new(SftCmdType.ClientHello, clientId, reqId++, SftPacketData.Serialize(helloData));
            sftConnection.WritePacket(outPkt);
            SftPacket inPkt = sftConnection.ReadPacket()!;
            if (inPkt.header.cmdType != SftCmdType.ServerHello)
            {
                sftConnection.SendReset(clientId, reqId++);
            }
            return SftPacketData.Deserialize<SftServerHelloData>(inPkt.serializedData);
        }

        private Tuple<bool, SftPacketData?> SendLogin(SftConnection sftConnection, string? username, string? password)
        { 
            SftLoginData loginData = new(username, password);
            SftPacket outPkt = new(SftCmdType.Login, clientId, reqId++, SftPacketData.Serialize(loginData));
            sftConnection.WritePacket(outPkt);
            SftPacket inPkt = sftConnection.ReadPacket();
            if (inPkt.header.cmdType == SftCmdType.Welcome)
            {

                SftWelcomeData? sftWelcomData = SftPacketData.Deserialize<SftWelcomeData>(inPkt.serializedData);
                if (sftWelcomData == null)
                {
                    throw new ArgumentNullException(nameof(sftWelcomData), "Bad welcome packet.");
                }
                clientId = sftWelcomData.ClientId;
                authToken = sftWelcomData.AuthToken;
                sftConnection.IsAuthenticated = true;

                return new Tuple<bool, SftPacketData?>(true, sftWelcomData);

            }
            else if (inPkt.header.cmdType == SftCmdType.Reject)
            {
                SftRejectData? sftRejectData = SftPacketData.Deserialize<SftRejectData>(inPkt.serializedData);
                return new Tuple<bool, SftPacketData?>(false, sftRejectData);
            }
            else
            {
                throw new SftUnexpectedResponse(inPkt);
            }
        }

        private Tuple<bool, SftPacketData?> SendAuth(SftConnection sftConnection)
        {
            if (authToken == null)
                throw new ArgumentNullException(nameof(authToken));
            SftAuthData sftAuthData = new(authToken);
            SftPacket outPkt = new(SftCmdType.Auth, clientId, reqId++, SftPacketData.Serialize(sftAuthData));
            sftConnection.WritePacket(outPkt);
            SftPacket inPkt = sftConnection.ReadPacket();
            if (inPkt.header.cmdType == SftCmdType.Welcome)
            {

                SftWelcomeData? sftWelcomData = SftPacketData.Deserialize<SftWelcomeData>(inPkt.serializedData);
                if (sftWelcomData == null)
                {
                    throw new ArgumentNullException(nameof(sftWelcomData), "Bad welcome packet.");
                }
                clientId = sftWelcomData.ClientId;
                authToken = sftWelcomData.AuthToken;
                sftConnection.IsAuthenticated = true;

                return new Tuple<bool, SftPacketData?>(true, sftWelcomData);

            }
            else if (inPkt.header.cmdType == SftCmdType.Reject)
            {
                SftRejectData? sftRejectData = SftPacketData.Deserialize<SftRejectData>(inPkt.serializedData);
                return new Tuple<bool, SftPacketData?>(false, sftRejectData);
            }
            else
            {
                throw new SftUnexpectedResponse(inPkt);
            }
        }

        private void SendBye(SftConnection sftConnection)
        {
            SftPacket outPkt = new(SftCmdType.Bye, clientId, reqId++, null);
            sftConnection.WritePacket(outPkt);
            SftPacket inPkt = sftConnection.ReadPacket();
            if (inPkt.header.cmdType == SftCmdType.Bye)
            {
                sftConnection.Close();
            }
            else
            {
                throw new SftUnexpectedResponse(inPkt);
            }
        }

        private Tuple<bool, SftPacketData?> SendList(SftConnection sftConnection, string path)
        {
            SftListData sftListData = new(path);
            SftPacket outPkt = new(SftCmdType.List, clientId, reqId++, SftPacketData.Serialize(sftListData));
            sftConnection.WritePacket(outPkt);
            SftPacket inPkt = sftConnection.ReadPacket();
            if (inPkt.header.cmdType == SftCmdType.Meta)
            {
                SftMetaData? sftMetaData = SftPacketData.Deserialize<SftMetaData>(inPkt.serializedData);
                return new Tuple<bool, SftPacketData?>(true, sftMetaData);
            }
            else if (inPkt.header.cmdType == SftCmdType.Fail)
            {
                SftFailData? sftFailData = SftPacketData.Deserialize<SftFailData>(inPkt.serializedData);
                return new Tuple<bool, SftPacketData?>(false, sftFailData);
            }
            else
            {
                throw new SftUnexpectedResponse(inPkt);
            }
        }
    }
}
