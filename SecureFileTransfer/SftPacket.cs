using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SecureFileTransfer
{
    public class Common
    {
        public const int SFT_HEADER_SIZE = 8;
    }


    public enum SftCmdType : byte
    {
        // handshake
        ClientHello = 0x01,
        ServerHello = 0x02,
        Login = 0x03,
        Auth = 0x04,
        Welcome = 0x05,
        Reject = 0x06,
        Bye = 0x07,
        Reset = 0x08,
        // connection
        DataConn = 0x11,
        CloseConn = 0x12,
        // directory and file cmds
        List = 0x21,
        Pwd = 0x22,
        Cwd = 0x23,
        Rename = 0x24,
        Delete = 0x25,
        Upload = 0x26,
        Download = 0x27,
        // responses
        OK = 0xA1,
        Fail = 0xA2,
        DataTrans = 0xA3
    };

    public struct SftPacketHeader
    {
        public byte leading;
        public SftCmdType cmdType;
        public ushort clientId; // assigned by server when welcome
        public ushort reqId;
        public ushort DataLen;

        public SftPacketHeader(byte[] data)
        {
            leading = data[0];
            cmdType = (SftCmdType)data[1];
            byte[] cIdDomain = data[2..4], rIdDomain = data[4..6], pLDomain = data[6..8];
            //if (BitConverter.IsLittleEndian)
            //{
            //    Array.Reverse(cIdDomain);
            //    Array.Reverse(rIdDomain);
            //    Array.Reverse(pLDomain);
            //}
            clientId = BitConverter.ToUInt16(cIdDomain);
            reqId = BitConverter.ToUInt16(rIdDomain);   
            DataLen = BitConverter.ToUInt16(pLDomain); 
        }

        public byte[] Serialize()
        {
            byte[] bytes = new byte[8];
            bytes[0] = 0x01;
            bytes[1] = (byte)cmdType;
            BitConverter.GetBytes(clientId).CopyTo(bytes, 2);
            BitConverter.GetBytes(reqId).CopyTo(bytes, 4);
            BitConverter.GetBytes(DataLen).CopyTo(bytes, 6);
            return bytes;
        }

        public void Display()
        {
            Console.WriteLine($@"========= Packet Header ========= 
Leading: {(int)leading:X2}
CmdType: {cmdType}
ClientId: {clientId}
RequestId: {reqId}
DataLen: {DataLen}
================================");
        }
    }
    public class SftPacket
    {
        public SftPacketHeader header;
        public byte[]? serializedData = null;



        public SftPacket(SftPacketHeader header, byte[] serializedData)
        {
            this.header = header;
            this.serializedData = serializedData;
        }

        public SftPacket(SftCmdType cmdType, ushort clientId, ushort reqId, byte[]? data)
        {
            header = new SftPacketHeader
            {
                cmdType = cmdType,
                clientId = clientId,
                reqId = reqId
            };
            serializedData = data;
            header.DataLen = (ushort)(serializedData?.Length ?? 0);

        }

        public byte[] ConvertToBytes()
        {
            byte[] headerBytes = header.Serialize();
            byte[] buffer = new byte[headerBytes.Length + (serializedData?.Length ?? 0)];
            headerBytes.CopyTo(buffer, 0);
            serializedData?.CopyTo(buffer, headerBytes.Length);
            return buffer;
            /*
            if (escape)
            {
                // escape rules: 0x01 -> 0x1B, 0x02; 0x1B -> 0x1B, 0x03
                List<byte> bytesList = buffer.ToList();
                for (int i = 0; i < bytesList.Count-1; i++)
                {
                    if (bytesList[i] == 0x01)
                    {
                        bytesList[i] = 0x1B;
                        bytesList.Insert(i + 1, 0x02);
                    }
                    else if(bytesList[i] ==0x1B)
                    {
                        bytesList.Insert(i + 1, 0x03);
                    } 
                }
                ret = bytesList.ToArray();
                
            } 
            else
            {
                ret = buffer;
                
            }
            ret[0] = 0x01;*/
        }


    }

    

}