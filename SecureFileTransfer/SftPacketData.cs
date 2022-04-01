using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;

namespace SecureFileTransfer.Data
{

    public abstract class SftPacketData
    {
        public static byte[] Serialize<T>(T obj) where T : class
        {
            return JsonSerializer.SerializeToUtf8Bytes<T>(obj);
        }

        public static T? Deserialize<T>(byte[]? data) where T : class
        {
            if (data == null) return null;
            var utf8Reader = new Utf8JsonReader(data);
            return JsonSerializer.Deserialize<T>(ref utf8Reader)!;
        }

        public static void DisplayData<T>(T obj) where T : class
        {
            if (obj is null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            Console.WriteLine("============= Packet Data =============");
            Console.WriteLine(JsonSerializer.Serialize(obj, new JsonSerializerOptions() { WriteIndented = true }));
            Console.WriteLine("=======================================");
        }

    }

    public class SftClientHelloData : SftPacketData
    {
        public string? Message { get; set; }
        public DateTime ClientTime { get; set; }


        public SftClientHelloData()
        {
            Message = null;
            ClientTime = DateTime.Now;
        }
        public SftClientHelloData(string? msg)
        {
            Message = msg;
            ClientTime = DateTime.Now;
        }

        public SftClientHelloData(string msg, DateTime clientTime)
        { Message = msg; ClientTime = clientTime; }

    }

    public class SftServerHelloData : SftPacketData
    {
        public string? Banner { get; set; }
        public string? ClientIpString { get; set; }

        public SftServerHelloData()
        {
            Banner = null;
            ClientIpString = null;
        }

        public SftServerHelloData(string b, IPAddress? cIp)
        {
            Banner = b;
            ClientIpString = cIp?.ToString();
        }

    }

    public class SftLoginData : SftPacketData
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }

        public SftLoginData() { }
        public SftLoginData(string? u, string? p) { UserName = u; Password = p; }

    }

    public class SftWelcomeData : SftPacketData
    {
        public ushort ClientId { get; set; }
        public string? AuthToken { get; set; }
        public DateTime? LastLoginTime { get; set; } = null;
        public string? LastLoginIpString { get; set; } = null;
        public string? WelcomeMessage { get; set; } = null;

        public SftWelcomeData() { ClientId = 0; AuthToken = null; }

        public SftWelcomeData(ushort cId, string t, DateTime? lt, string? lip, string? m)
        {
            ClientId = cId;
            AuthToken = t;
            LastLoginTime = lt;
            LastLoginIpString = lip;
            WelcomeMessage = m;
        }


    }

    public class SftRejectData : SftPacketData
    {
        public string? RejectMessage { get; set; }
        public SftRejectData() { }
        public SftRejectData(string m) { RejectMessage = m; }
    }

    public class SftAuthData : SftPacketData
    {
        public string? AuthToken { get; set; }
        public SftAuthData() { }
        public SftAuthData(string t) { AuthToken = t; }
    }

    public class SftByeData : SftPacketData { }
    public class SftResetData : SftPacketData { }


    public class SftListData : SftPacketData
    {
        public string Path { get; set; }
        public SftListData() { Path = "/"; }
        public SftListData(string p) { Path = p; }
    }

    public class SftMetaData : SftPacketData
    {
        public class SftFileObject
        {
            public string? Name { get; set; }
            public bool IsReadOnly { get; set; }
            public DateTime LastWriteTime { get; set; }
            public long Length { get; set; }

            public SftFileObject() { }
            public SftFileObject(FileInfo f)
            {
                Name = f.Name;
                IsReadOnly = f.IsReadOnly;
                LastWriteTime = f.LastWriteTime;
                Length = f.Length;
            }
        }
        public class SftDirectoryObject
        {
            public string? Name { get; set; }
            public DateTime LastWriteTime { get; set; }

            public SftDirectoryObject() { }
            public SftDirectoryObject(DirectoryInfo d)
            {
                Name = d.Name;
                LastWriteTime = d.LastWriteTime;
            }
        }

        string? CurrentPath { get; set; }
        public IList<SftFileObject> FileList { get; set; } = new List<SftFileObject>();
        public IList<SftDirectoryObject> DirList { get; set; } = new List<SftDirectoryObject>();

        public SftMetaData() { }

        public SftMetaData(DirectoryInfo dirInfo, string path)
        {
            CurrentPath = path;
            DirectoryInfo[] subDirs = dirInfo.GetDirectories();
            foreach (var d in subDirs)
            {
                DirList.Add(new SftDirectoryObject(d));
            }
            FileInfo[] files = dirInfo.GetFiles();
            foreach (var f in files)
            {
                FileList.Add(new SftFileObject(f));
            }
        }
    }

    public class SftUnAuthData : SftPacketData { }
    public class SftFailData : SftPacketData
    {
        public string? Message { get; set; }
        public SftFailData() { }
        public SftFailData(string? message) { Message = message; }
    }




}
