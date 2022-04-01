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
            //Console.WriteLine("Hello World!");
            string certPemFilePath = @"C:\Users\Wentian Bu\certs\server2.cer";
            string keyPemFilePath = @"C:\Users\Wentian Bu\certs\server2.key";
            string RootWorkDir = @"C:\Users\Wentian Bu\SFTTest\serverroot\";
            
            SftServerConfig sftServerConfig = new()
            {
                CertPemFilePath = certPemFilePath,
                KeyPemFilePath = keyPemFilePath,
                ListenAddress = IPAddress.Any,
                Port = 9090,
                RootDirPath = RootWorkDir
            };
            SftServer sftServer = new(sftServerConfig);
            sftServer.Start();
            return 0;
        }
    }
}
