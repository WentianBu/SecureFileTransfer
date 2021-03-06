using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using SecureFileTransfer.Data;

namespace SecureFileTransfer.Server
{
    public class SftServerConfig
    {
        public IPAddress ListenAddress { get; set; } = IPAddress.Any;
        public int Port { get; set; } = 9090;
        public string CertPemFilePath { get; set; } = null!;
        public string KeyPemFilePath { get; set; } = null!;
        public string RootDirPath { get; set; } = null!;
        public string UserInfoFile { get; set; } = null!;

    }



    internal class SftRemoteClient
    {
        public ushort ClientId { get; set; }
        public ushort ReqId { get; set; } = 0;
        public IPAddress ClientIp { get; set; }
        public string AuthToken { get; set; }

        public SftConnection MainConnection { get; set; } = null!;
        public Dictionary<ushort, SftConnection> DataConnections { get; set; } = new Dictionary<ushort, SftConnection>();


        public SftRemoteClient(IPAddress ip, Dictionary<ushort, SftRemoteClient> d)
        {
            ClientIp = ip;
            Random rd = new();
            ushort i = (ushort)rd.Next(1, 65535);
            while (d.ContainsKey(i))
            {
                i = (ushort)rd.Next(1, 65535);
            }
            ClientId = i;

            // generate cryptographically sound token
            char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            int tokenSize = 64;
            byte[] data = RandomNumberGenerator.GetBytes(4 * tokenSize);
            StringBuilder result = new(tokenSize);
            for (int j = 0; j < tokenSize; j++)
            {
                var rnd = BitConverter.ToUInt32(data, j * 4);
                var idx = rnd % chars.Length;
                result.Append(chars[idx]);
            }
            AuthToken = result.ToString();
        }
    }
    public class SftServer
    {
        private X509Certificate2 serverCert = null!;
        private SftServerConfig config = null!;
        internal Dictionary<ushort, SftRemoteClient> clientDict = new(65536);
        private string[] userInfoDb = null;

        public SftServer(SftServerConfig c)
        {
            config = c;
            // a bug in the windows: pem must be exported to pkcs12
            serverCert = new X509Certificate2(
                X509Certificate2.CreateFromPemFile(config.CertPemFilePath, config.KeyPemFilePath).Export(X509ContentType.Pkcs12));
            userInfoDb = File.ReadAllLines(c.UserInfoFile);
        }

        private void PostTransferTask(SftTransferTask tsk)
        {
            tsk.Connection!.DataConnectionBusy = false;
            Console.WriteLine("File transfer task finished.");
        }

        public void Start()
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = false;
                Console.WriteLine("Server exit.");
            };

            TcpListener listener = new(config.ListenAddress, config.Port);
            listener.Start();
            while (true)
            {
                Console.WriteLine("Waiting for a client to connect...");
                TcpClient client = listener.AcceptTcpClient();
                SftConnection sftConnection = new(client, serverCert);
                Thread thread = new(HandleConnection);
                thread.Start(sftConnection);

            }
        }

        // obj must be a TcpClient
        private void HandleConnection(object? obj)
        {
            if (obj == null) { throw new ArgumentNullException(nameof(obj)); }
            SftConnection sftConnection = (SftConnection)obj;
            Console.WriteLine("Connection ID: {0}", sftConnection.ConnectionId);
            SftPacket sftPacket;
            bool keepHandle = true;
            while (keepHandle)
            {
                try
                {
                    sftPacket = sftConnection.ReadPacket();
                }
                catch (IOException e)
                {
                    Console.WriteLine(e.Message);
                    sftConnection.Close();
                    return;
                }
                IPEndPoint? _rEndp = sftConnection.TheTcpClient.Client.RemoteEndPoint as IPEndPoint;
                if (_rEndp == null) throw new ArgumentNullException(nameof(sftConnection.TheTcpClient.Client.RemoteEndPoint));
                IPAddress clientIp = _rEndp.Address;

                // check the SftPacket type and do different things
                switch (sftPacket.header.cmdType)
                {
                    case SftCmdType.ClientHello:
                        HandleClientHello(sftPacket, sftConnection, clientIp); break;
                    case SftCmdType.Login:
                        HandleLogin(sftPacket, sftConnection, clientIp); break;
                    case SftCmdType.Auth:
                        HandleAuth(sftPacket, sftConnection); break;
                    case SftCmdType.Bye:
                        // bye and reset should close all the connections
                        HandleBye(sftPacket, sftConnection);
                        keepHandle = false;
                        break;
                    case SftCmdType.Reset:
                        // bye and reset should close all the connections
                        HandleReset(sftConnection);
                        keepHandle = false;
                        break;
                    case SftCmdType.List:
                        HandleList(sftPacket, sftConnection); break;
                    case SftCmdType.Upload:
                        HandleUpload(sftPacket, sftConnection); break;
                    case SftCmdType.Download:
                        HandleDownload(sftPacket, sftConnection); break;

                    default:
                        break;
                }
            }

        }

        private void HandleClientHello(SftPacket inPkt, SftConnection sftConnection, IPAddress cIp)
        {
            if (inPkt.serializedData == null)
            {
                sftConnection.SendReset(0, 0);
                sftConnection.Close();
                return;
            }

            SftClientHelloData? sftClientHelloData =
                SftPacketData.Deserialize<SftClientHelloData>(inPkt.serializedData);
            if (sftClientHelloData == null)
            {
                sftConnection.SendReset(0, 0);
                sftConnection.Close();
                return;
            }
            SftPacketData.DisplayData(sftClientHelloData);


            SftServerHelloData sftServerHelloData = new("Hello, this is SFT server.", cIp);
            SftPacket sftPacket = new(SftCmdType.ServerHello, 0, 0, SftPacketData.Serialize(sftServerHelloData));
            sftConnection.WritePacket(sftPacket);
        }

        private void HandleLogin(SftPacket inPkt, SftConnection sftConnection, IPAddress cIp)
        {
            SftPacket sftPacket;
            if (inPkt.serializedData == null)
            {
                SftRejectData sftRejectData = new("Must provide username and password!");
                sftPacket = new(SftCmdType.Reject, 0, 0, SftPacketData.Serialize(sftRejectData));
            }
            else
            {
                SftLoginData? sftLoginData = SftPacketData.Deserialize<SftLoginData>(inPkt.serializedData);
                Console.WriteLine("UserName: {0}\nPassword: {1}\nConnectionId: {2}",
                    sftLoginData?.UserName, sftLoginData?.Password, sftLoginData?.ConnectionId);

                bool isLoginSuccess = false;
                if (sftLoginData?.UserName != null && sftLoginData.Password != null)
                {
                    var query =
                    from user in userInfoDb
                    where sftLoginData?.UserName == user.Split('|')[0]
                    select user;

                    foreach (var u in query)
                    {
                        string passwdMd5 = u.Split('|')[1];
                        string userInputPwdMd5;
                        using (MD5 md5 = MD5.Create())
                        {
                            byte[] userInputPwdMd5Bytes = md5.ComputeHash(Encoding.ASCII.GetBytes(sftLoginData.Password));
                            userInputPwdMd5 = Convert.ToHexString(userInputPwdMd5Bytes);
                            Console.WriteLine("UserInput Pwd Md5: {0}", userInputPwdMd5);
                        }
                        
                        if (passwdMd5 == userInputPwdMd5)
                        {
                            isLoginSuccess = true;
                            break;
                        }
                    }
                }
                
                // temp test
                if (isLoginSuccess)
                {
                    // authenticate passed
                    sftConnection.IsAuthenticated = true;
                    sftConnection.ConnectionId = sftLoginData.ConnectionId;
                    sftConnection.IsMainConnection = true;
                    SftRemoteClient remoteClient = new(cIp, clientDict);
                    remoteClient.MainConnection = sftConnection;
                    clientDict.Add(remoteClient.ClientId, remoteClient);
                    SftWelcomeData sftWelcomeData = new(remoteClient.ClientId, remoteClient.AuthToken, null, null, "Welcome!");
                    sftPacket = new(SftCmdType.Welcome, remoteClient.ClientId, 0, SftPacketData.Serialize(sftWelcomeData));
                }
                else
                {
                    // authenticate failed
                    SftRejectData sftRejectData = new("Login failed.");
                    sftPacket = new(SftCmdType.Reject, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftRejectData));
                }

            }
            sftConnection.WritePacket(sftPacket);
        }

        private void HandleAuth(SftPacket inPkt, SftConnection sftConnection)
        {
            SftAuthData? sftAuthData = SftPacketData.Deserialize<SftAuthData>(inPkt.serializedData);
            if (sftAuthData == null)
            {
                SftRejectData sftRejectData = new("Must provide the auth token!");
                SftPacket outPkt = new(SftCmdType.Reject, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftRejectData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            if (inPkt.header.clientId == 0)
            {
                SftRejectData sftRejectData = new("Must specify the client ID!");
                SftPacket outPkt = new(SftCmdType.Reject, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftRejectData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            if (clientDict.TryGetValue(inPkt.header.clientId, out SftRemoteClient? sftRemoteClient))
            {
                // client id exists
                if (sftRemoteClient.AuthToken == sftAuthData.AuthToken)
                {
                    // auth passed
                    sftConnection.IsAuthenticated = true;
                    sftConnection.ConnectionId = sftAuthData.ConnectionId;
                    sftConnection.IsMainConnection = false;
                    sftRemoteClient.DataConnections.Add(sftConnection.ConnectionId, sftConnection);
                    SftWelcomeData sftWelcomeData = new(inPkt.header.clientId, sftAuthData.AuthToken, null, null, "Auth suceeded.");
                    SftPacket outPkt = new(SftCmdType.Welcome, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftWelcomeData));
                    sftConnection.WritePacket(outPkt);
                    return;
                }
                else
                {
                    // auth failed
                    SftRejectData sftRejectData = new("Authentication failed.");
                    SftPacket outPkt = new(SftCmdType.Reject, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftRejectData));
                    sftConnection.WritePacket(outPkt);
                    return;
                }
            }
            else
            {
                // client id not exists
                SftRejectData sftRejectData = new("Authentication failed.");
                SftPacket outPkt = new(SftCmdType.Reject, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftRejectData));
                sftConnection.WritePacket(outPkt);
                return;
            }

        }

        private void HandleBye(SftPacket inPkt, SftConnection sftConnection)
        {
            SftPacket sftPacket = new(SftCmdType.Bye, inPkt.header.clientId, inPkt.header.reqId, null);
            sftConnection.WritePacket(sftPacket);
            sftConnection.Close();
            Console.WriteLine("Bye received.");
        }

        private void HandleReset(SftConnection sftConnection)
        {
            // don't need response
            sftConnection.Close();
        }

        private void HandleList(SftPacket inPkt, SftConnection sftConnection)
        {
            if (!sftConnection.CheckAuthStatus(inPkt.header.reqId))
                return;
            SftListData? sftListData = SftPacketData.Deserialize<SftListData>(inPkt.serializedData);
            if (sftListData?.Path == null)
            {
                SftFailData sftFailData = new("List command must specify the path!");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            if (!sftListData.Path.StartsWith("/"))
            {
                SftFailData sftFailData = new("List Path must start with '/'");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            string systemRoot = Path.GetFullPath("/");
            string queryFullPath = Path.GetFullPath(sftListData.Path);
            string queryRelativePath = queryFullPath.Remove(0, systemRoot.Length);
            string safeQueryFullPath = Path.Combine(config.RootDirPath, queryRelativePath);
            DirectoryInfo queryDir = new(safeQueryFullPath);
            if (!queryDir.Exists)
            {
                SftFailData sftFailData = new("Query directory does not exist!");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            else
            {
                SftMetaData sftMetaData = new(queryDir, "/" + queryRelativePath);
                SftPacket outPkt = new(SftCmdType.Meta, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftMetaData));
                sftConnection.WritePacket(outPkt);
                return;
            }
        }

        private void HandleUpload(SftPacket inPkt, SftConnection sftConnection)
        {
            if (!clientDict.TryGetValue(inPkt.header.clientId, out SftRemoteClient? sftRemoteClient))
            {
                // client invalid
                SftFailData sftFailData = new("Invalid clientID!");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            if (!sftConnection.CheckAuthStatus(inPkt.header.reqId))
                return;
            SftUploadData? sftUploadData = SftPacketData.Deserialize<SftUploadData>(inPkt.serializedData);
            if (sftUploadData == null)
            {
                SftFailData sftFailData = new("Bad Upload command!");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            

            if (sftUploadData?.ConnectionId == null 
                || sftUploadData.ConnectionId == sftRemoteClient.MainConnection.ConnectionId
                || !sftRemoteClient.DataConnections.TryGetValue(sftUploadData.ConnectionId, out SftConnection? dataConnection))
            {
                SftFailData sftFailData = new("Invalid Connection Id!");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }

            dataConnection.DataConnectionBusy = true;
            SftServerTransferTask sftServerTransferTask = new(inPkt.header.clientId, inPkt.header.reqId, sftUploadData, dataConnection, config.RootDirPath);
            // add to task list
            SftPacket okPkt = new(SftCmdType.OK, inPkt.header.clientId, inPkt.header.reqId, null);
            sftConnection.WritePacket(okPkt);
            sftServerTransferTask.Start(PostTransferTask);

            

        }

        private void HandleDownload(SftPacket inPkt, SftConnection sftConnection)
        {
            if (!clientDict.TryGetValue(inPkt.header.clientId, out SftRemoteClient? sftRemoteClient))
            {
                // client invalid
                SftFailData sftFailData = new("Invalid clientID!");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            if (!sftConnection.CheckAuthStatus(inPkt.header.reqId))
                return;
            SftDownloadData? sftDownloadData = SftPacketData.Deserialize<SftDownloadData>(inPkt.serializedData);
            if (sftDownloadData == null)
            {
                SftFailData sftFailData = new("Bad Download command!");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            if (sftDownloadData?.ConnectionId == null
                || sftDownloadData.ConnectionId == sftRemoteClient.MainConnection.ConnectionId
                || !sftRemoteClient.DataConnections.TryGetValue(sftDownloadData.ConnectionId, out SftConnection? dataConnection))
            {
                SftFailData sftFailData = new("Invalid Connection Id!");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }
            SftServerTransferTask sftServerTransferTask;
            try
            {
                sftServerTransferTask = new(inPkt.header.clientId, inPkt.header.reqId, sftDownloadData, dataConnection, config.RootDirPath);
                
            } 
            catch (FileNotFoundException e)
            {
                SftFailData sftFailData = new(e.Message + "not found!");
                SftPacket outPkt = new(SftCmdType.Fail, inPkt.header.clientId, inPkt.header.reqId, SftPacketData.Serialize(sftFailData));
                sftConnection.WritePacket(outPkt);
                return;
            }

            SftPacket okPkt = new(SftCmdType.OK, inPkt.header.clientId, inPkt.header.reqId, null);
            sftConnection.WritePacket(okPkt);
            sftServerTransferTask.Start(PostTransferTask);
        }









    }
}
