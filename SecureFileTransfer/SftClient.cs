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
    public class SftClientConfig
    {
        public string TrustedServerCertificateName { get; set; } = "SecureFileTransferProtocol Server";
        public uint SingleServerMaxConnections { get; set; } = 10;
        public int ClientReadTimeout { get; set; } = 10000;

    }


    public partial class SftClient
    {
        private ushort clientId = 0;
        private ushort reqId = 0;
        private ushort nextConnectionId = 0;
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

        private void PostTransferTask(SftTransferTask tsk)
        {
            tsk.Connection!.DataConnectionBusy = false;
            Console.WriteLine("File transfer task finished.");
        }

        private SftConnection CreateConnection()
        {
            if (serverName == null) 
                throw new ArgumentNullException(nameof(serverName));
            TcpClient tcpClient = new(serverName, serverPort);
            SftConnection sftConnection = new(tcpClient, config.TrustedServerCertificateName, nextConnectionId++);
            //sftConnection.TheSslStream.ReadTimeout = config.ClientReadTimeout;
            if (mainConnection == null)
            {
                mainConnection = sftConnection;
                mainConnection.IsMainConnection = true;
            }
            else
            {
                sftConnection.DataConnectionBusy = false;
                dataConnections.Add(sftConnection);
            }
            return sftConnection;
        }

        private SftConnection? GetIdleDataConnection()
        {
            foreach (var conn in dataConnections)
            {
                if (!conn.IsMainConnection && !conn.DataConnectionBusy)
                    return conn;
            }
            if (dataConnections.Count < config.SingleServerMaxConnections)
            {
                SftConnection newConn = CreateConnection();
                return CreateConnection();
            }
            else
            {
                return null;
            }
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
            SftLoginData loginData = new(username, password, sftConnection.ConnectionId);
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
            SftAuthData sftAuthData = new(authToken, sftConnection.ConnectionId);
            Console.WriteLine("Connection Id: {0}", sftConnection.ConnectionId);
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

        private bool SendUpload(SftConnection dataConnection, string localPath, string remotePath, long startPos=0)
        {
            SftClientTransferTask sftClientTransferTask;
            try
            {
                sftClientTransferTask = new SftClientTransferTask(clientId, reqId, dataConnection, SftTaskDirection.Upload, localPath, startPos);
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine("Local file not found!");
                Console.WriteLine(e.Message);
                return false;
            }
            if (!remotePath.StartsWith("/"))
                throw new ArgumentException(nameof(remotePath), "Invalid remote path!");
            SftUploadData sftUploadData = new SftUploadData(remotePath, startPos, dataConnection.ConnectionId);
            SftPacket outPkt = new(SftCmdType.Upload, clientId, reqId++, SftPacketData.Serialize(sftUploadData));
            dataConnection.WritePacket(outPkt);
            SftPacket inPkt = dataConnection.ReadPacket();
            if (inPkt.header.cmdType==SftCmdType.OK)
            {
                sftClientTransferTask.Start(PostTransferTask);
                return true;
            }
            else
            {
                SftFailData sftFailData = SftPacketData.Deserialize<SftFailData>(inPkt.serializedData)!;
                SftPacketData.DisplayData(sftFailData);
                return false;
            }
        }

        private bool SendDownload(SftConnection dataConnection, string localPath, string remotePath, long startPos = 0)
        {
            SftClientTransferTask sftClientTransferTask = new(clientId, reqId, dataConnection, SftTaskDirection.Download, localPath, startPos);
            if (!remotePath.StartsWith("/"))
                throw new ArgumentException(nameof(remotePath), "Invalid remote path!");
            SftDownloadData sftDownloadData = new(remotePath, startPos, dataConnection.ConnectionId);
            SftPacket outPkt = new(SftCmdType.Download, clientId, reqId++, SftPacketData.Serialize(sftDownloadData));
            dataConnection.WritePacket(outPkt);
            SftPacket inPkt = dataConnection.ReadPacket();
            if (inPkt.header.cmdType == SftCmdType.OK)
            {
                sftClientTransferTask.Start(PostTransferTask);
                inPkt.header.Display();
                return true;
            }
            else
            {
                SftFailData sftFailData = SftPacketData.Deserialize<SftFailData>(inPkt.serializedData)!;
                SftPacketData.DisplayData(sftFailData);
                return false;
            }
        }

        
    }
}
