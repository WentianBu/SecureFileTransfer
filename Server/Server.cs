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
using SecureFileTransfer.Server;

namespace Server // Note: actual namespace depends on the project name.
{
    internal class Server
    {
        static int Main(string[] args)
        {
            string certPemFilePath, keyPemFilePath, RootWorkDir, UserInfoFilePath;
            if (args.Length == 4)
            {
                certPemFilePath = args[0];
                keyPemFilePath = args[1];
                RootWorkDir = args[2];
                UserInfoFilePath = args[3];
            }
            else if (args.Length == 0)
            {
                //Console.WriteLine("Hello World!");
                certPemFilePath = @"C:\Users\Wentian Bu\certs\server2.cer";
                keyPemFilePath = @"C:\Users\Wentian Bu\certs\server2.key";
                RootWorkDir = @"C:\Users\Wentian Bu\SFTTest\serverroot\";
                UserInfoFilePath = @"C:\Users\Wentian Bu\source\repos\SecureFileTransfer\config\userinfo.txt";
            } else
            {
                Console.WriteLine("Usage: ./Server certPemFilePath keyPemFilePath serverRootDir");
                return 0;
            }
            
            SftServerConfig sftServerConfig = new()
            {
                CertPemFilePath = certPemFilePath,
                KeyPemFilePath = keyPemFilePath,
                ListenAddress = IPAddress.Any,
                Port = 9090,
                RootDirPath = RootWorkDir,
                UserInfoFile = UserInfoFilePath
            };
            SftServer sftServer = new(sftServerConfig);
            sftServer.Start();
            return 0;
        }
    }
}
