using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;

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
        UnAuth = 0xA3,
        DataTrans = 0xA4,
        Meta = 0xA5,
        StartTrans = 0xA6,
        FinishTrans = 0xA7
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
        public ushort fixedDataLength = 0;

        public SftPacket(SftPacketHeader h, byte[] d)
        {
            header = h;
            serializedData = d;
        }

        // Receive a SftPacket from SSL stream
        public SftPacket(SslStream sslStream)
        {
            byte[] headerBuffer = new byte[Common.SFT_HEADER_SIZE];
            int bytes;
            for (int readBytes = 0; readBytes < headerBuffer.Length; readBytes += bytes)
            {
                bytes = sslStream.Read(headerBuffer, readBytes, headerBuffer.Length - readBytes);
                if (bytes == 0)
                    throw new IOException("Connection closed by client");
            }
            
            //int bytes = sslStream.Read(headerBuffer, 0, headerBuffer.Length);
            
            header = new(headerBuffer);
            //header.Display();
            if (header.DataLen == 0)
                return;
            byte[] bodyBuffer = new byte[header.DataLen];
            for (int readBytes = 0; readBytes < bodyBuffer.Length; readBytes += bytes)
            {
                bytes = sslStream.Read(bodyBuffer, readBytes, bodyBuffer.Length - readBytes);
                if (bytes == 0)
                    throw new IOException("Connection closed by client");
            }

            //bytes = sslStream.Read(bodyBuffer, 0, header.DataLen);
            //if (bytes == 0) 
            //    throw new IOException("Client closed the stream before sending a complete packet.");
            serializedData = bodyBuffer;
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

        public SftPacket(SftCmdType cmdType, ushort clientId, ushort reqId, byte[]? data, ushort setDataLen)
        {
            header = new SftPacketHeader
            {
                cmdType = cmdType,
                clientId = clientId,
                reqId = reqId
            };
            serializedData = data;
            fixedDataLength = setDataLen;
            header.DataLen = (fixedDataLength==0) ? (ushort)(serializedData?.Length ?? 0) : fixedDataLength;
        }

        public byte[] ConvertToBytes()
        {
            byte[] headerBytes = header.Serialize();
            byte[] buffer = new byte[headerBytes.Length + header.DataLen];
            Array.Clear(buffer, 0, buffer.Length);
            headerBytes.CopyTo(buffer, 0);
            serializedData?.CopyTo(buffer, headerBytes.Length);
            return buffer;
        }


    }

    

}