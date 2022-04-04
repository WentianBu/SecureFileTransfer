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
            sftClient.Download(@"C:\Users\Wentian Bu\SFTTest\clientroot\新建文本文档.txt", "/newsss/新建文本文档.txt");
            sftClient.Upload(@"C:\Users\Wentian Bu\SFTTest\clientroot\video.mp4", "/newsss/newvideo.mp4");
            Thread.Sleep(10000);
            sftClient.Bye();


            return 0;
        }
    }
}