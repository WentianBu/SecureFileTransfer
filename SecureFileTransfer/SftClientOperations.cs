using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecureFileTransfer;
using SecureFileTransfer.Data;

namespace SecureFileTransfer.Client
{
    public partial class SftClient
    {
        public bool Login(string? username, string? password)
        {
            if (mainConnection == null)
                CreateConnection();
            SftServerHelloData? sftServerHelloData = SendHello(mainConnection!, "Hello from client!");
            Console.WriteLine(sftServerHelloData?.Banner);
            Tuple<bool, SftPacketData?> loginResp = SendLogin(mainConnection!, username, password);
            if (loginResp.Item1) // login succeed
            {
                Console.WriteLine("Login succeeded.");
                return true;
            }
            else
            {
                Console.Write("Login failed. Server message: ");
                Console.WriteLine(((SftRejectData?)loginResp.Item2)?.RejectMessage);
                return false;
            }
        }

        public Tuple<bool, SftPacketData?> List(string path)
        {
            Console.WriteLine("Send list command.");
            if (mainConnection == null)
                throw new InvalidOperationException("Main connection unavailable, must login!");
            if (!path.StartsWith("/"))
            {
                throw new ArgumentException("Path in LIST command must starts from root(/)!", nameof(path));
            }
            return SendList(mainConnection, path);
            
        }


        public void Bye()
        {
            if (mainConnection == null)
                throw new InvalidOperationException("Main connection unavailable.");
            foreach (var conn in dataConnections)
            {
                SendBye(conn);
            }
            SendBye(mainConnection);
        }

        public bool Upload(string localPath, string remotePath, long startPos = 0)
        {
            if (mainConnection == null)
                throw new InvalidOperationException("Main connection unavailable.");
            SftConnection? dataConnection = GetIdleDataConnection();
            if (dataConnection == null)
            {
                Console.WriteLine("No idle data connection!");
                return false;
            }
            if (!dataConnection.IsAuthenticated)
            {
                Tuple<bool, SftPacketData?> authResult = SendAuth(dataConnection);
                if (!authResult.Item1)
                {
                    Console.WriteLine((authResult.Item2 as SftRejectData)?.RejectMessage);
                    return false;
                }
            }
            return SendUpload(dataConnection, localPath, remotePath, startPos);
        }

        public bool Download(string localPath, string remotePath, long startPos = 0)
        {
            if (mainConnection == null)
                throw new InvalidOperationException("Main connection unavailable.");
            SftConnection? dataConnection = GetIdleDataConnection();
            if (dataConnection == null)
            {
                Console.WriteLine("No idle data connection!");
                return false;
            }
            if (!dataConnection.IsAuthenticated)
            {
                Tuple<bool, SftPacketData?> authResult = SendAuth(dataConnection);
                if (!authResult.Item1)
                {
                    Console.WriteLine((authResult.Item2 as SftRejectData)?.RejectMessage);
                    return false;
                }
            }
            return SendDownload(dataConnection, localPath, remotePath, startPos);
        }
    }
}
