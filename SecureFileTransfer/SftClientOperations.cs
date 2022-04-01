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

        public bool List(string path)
        {
            Console.WriteLine("Send list command.");
            if (mainConnection == null)
                throw new InvalidOperationException("Main connection unavailable, must login!");
            if (!path.StartsWith("/"))
            {
                throw new ArgumentException("Path in LIST command must starts from root(/)!", nameof(path));
            }
            Tuple<bool, SftPacketData?> listResult = SendList(mainConnection!, path);
            if (listResult.Item1)
            {
                SftMetaData? sftMetaData = (SftMetaData?)listResult.Item2;
                if (sftMetaData == null)
                {
                    throw new ArgumentNullException("Metadata packet is null.");
                }
                SftPacketData.DisplayData(sftMetaData);
            }
            else
            {
                SftFailData? sftFailData = (SftFailData?)listResult.Item2;
                Console.WriteLine(sftFailData?.Message);
            }
            return listResult.Item1;
        }

        public void Bye()
        {
            if (mainConnection == null)
                throw new InvalidOperationException("Main connection unavailable.");
            SendBye(mainConnection);
        }
    }
}
