using System.Collections;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SecureFileTransfer;
using SecureFileTransfer.Data;
using SecureFileTransfer.Client;

namespace ClientConsole
{
    public class ClientConsole
    {
        public static int Main(string[] args)
        {
            SftClientConfig sftClientConfig = new();
            SftClient sftClient = new(sftClientConfig, "127.0.0.1", 9090);
            sftClient.Login("wentianbu", "abcdefgh");
            sftClient.List("/newsss");
            sftClient.Bye();


            return 0;
        }
    }
}