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
        public static byte[] Serialize<T>(T obj) where T: class
        {
            return JsonSerializer.SerializeToUtf8Bytes<T>(obj);
        }

        public static T Deserialize<T>(byte[] data) where T : class
        {
            var utf8Reader = new Utf8JsonReader(data);
            return JsonSerializer.Deserialize<T>(ref utf8Reader)!;
        }

        public static void DisplayData<T>(T obj) where T: class
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
        public SftClientHelloData(string msg)
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
        public string? ClientIPString { get; set; }

        public SftServerHelloData()
        {
            Banner = null;
            ClientIPString = null;
        }

        public SftServerHelloData(string b, IPAddress cIp)
        {
            Banner= b;
            ClientIPString= cIp.ToString();
        }

    }

    public class SftLoginData : SftPacketData
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public SftLoginData(string u, string p) { UserName= u; Password= p; }

    }

}
