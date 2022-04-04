using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecureFileTransfer.Data;

namespace SecureFileTransfer
{
    internal delegate void SftTaskFinishCallBack(SftTransferTask tsk);
    internal enum SftTaskDirection
    {
        Upload,
        Download
    }

    internal enum SftTaskType
    {
        Unstarted,
        Stopped,
        Running,
        Pause
    }
    internal abstract class SftTransferTask
    {
        
        public ushort ClientId { get; set; }
        public ushort ReqId { get; set; }
        public SftTaskDirection Direction { get; set; }
        public string LocalPath { get; set; } = null!;
        //public string RemotePath { get; set; } = null!;
        public FileStream? LocalStream { get; set; } = null;
        public long FileLength { get; set; }
        public long StartPos { get; set; }
        public SftConnection? Connection { get; set; } = null;
        //public Thread? WorkThread { get; set; } = null;
        protected ManualResetEvent pauseSignal = new ManualResetEvent(true);



        protected void SendFileData()
        {
            if (LocalStream == null)
                throw new InvalidOperationException("LocalStream is null!");
            if (Connection == null)
                throw new InvalidOperationException("Connection is null!");
            if (StartPos < 0 || StartPos > LocalStream.Length)
                throw new InvalidOperationException("Invalid startPos!");
            // Send StartTrans and waiting for OK, then start transferring
            SftStartTransData sftStartTransData = new(FileLength, StartPos);
            SftPacket outStartTransPkt = new(SftCmdType.StartTrans, ClientId, ReqId, SftPacketData.Serialize(sftStartTransData));
            Connection.WritePacket(outStartTransPkt);

            SftPacket inOkPkt = Connection.ReadPacket();
            if (inOkPkt.header.cmdType != SftCmdType.OK)
            {
                // task fail
                Connection.SendReset(ClientId, ReqId);
                // cancel task
                Console.WriteLine(inOkPkt.header.cmdType);
                throw new SftFailedTransferTask(this, "StartTransfer got no OK");
            }

            LocalStream.Position = StartPos;
            byte[] buffer = new byte[4096];
            int readBytes = LocalStream.Read(buffer, 0, buffer.Length);
            while (readBytes > 0)
            {
                SftPacket outPkt;
                if (readBytes < buffer.Length)
                {
                    outPkt = new(SftCmdType.DataTrans, ClientId, ReqId, buffer.Take(readBytes).ToArray());
                }
                else
                {
                    outPkt = new(SftCmdType.DataTrans, ClientId, ReqId, buffer);
                }
                Connection.WritePacket(outPkt);
                pauseSignal.WaitOne();
                readBytes = LocalStream.Read(buffer, 0, buffer.Length);
            }

            // send FinishTransfer
            SftPacket outFinishTransferPkt = new(SftCmdType.FinishTrans, ClientId, ReqId, null);
            Connection.WritePacket(outFinishTransferPkt);

            //LocalStream.Flush();
            LocalStream.Close();
            Connection.DataConnectionBusy = false;
        }

        protected void ReceiveFileData()
        {
            if (LocalStream == null)
                throw new InvalidOperationException("LocalStream is null!");
            if (Connection == null)
                throw new InvalidOperationException("Connection is null!");

            // Wait StartTrans and send OK, then start receiving
            SftPacket inStartTransPkt = Connection.ReadPacket();
            if (inStartTransPkt.header.cmdType != SftCmdType.StartTrans)
            {
                Connection.SendReset(ClientId, ReqId);
                throw new SftFailedTransferTask(this, "did not get StartTransfer.");
            }
            if (inStartTransPkt.serializedData == null)
            {
                Connection.SendReset(ClientId, ReqId);
                throw new SftFailedTransferTask(this, "Bad StartTrans Packet.");
            }

            SftStartTransData? sftStartTransData = SftPacketData.Deserialize<SftStartTransData>(inStartTransPkt.serializedData);
            if (sftStartTransData == null)
            {
                Connection.SendReset(ClientId, ReqId);
                throw new ArgumentNullException(nameof(sftStartTransData));
            }

            FileLength = sftStartTransData.Length;
            StartPos = sftStartTransData.StartPos;
            if (StartPos < 0 || StartPos > LocalStream.Length)
                throw new InvalidOperationException("Invalid startPos!");
            LocalStream.Position = StartPos;

            // send OK
            SftPacket outOkPkt = new(SftCmdType.OK, ClientId, ReqId, null);
            Connection.WritePacket(outOkPkt);

            while (true)
            {
                pauseSignal.WaitOne();
                SftPacket inPkt = Connection.ReadPacket();
                if (inPkt.header.cmdType == SftCmdType.DataTrans)
                {
                    if (inPkt.serializedData == null)
                        throw new ArgumentNullException(nameof(inPkt.serializedData), "File data in the packet is null!");
                    LocalStream.Write(inPkt.serializedData, 0, inPkt.serializedData.Length);
                }
                else if (inPkt.header.cmdType == SftCmdType.FinishTrans)
                {
                    // finish
                    break;
                }
                else
                {
                    // error
                    Connection.SendReset(ClientId, ReqId);
                    Connection.Close();
                    // notification
                    break;
                }
            }

            //LocalStream.Flush();
            LocalStream.Close();
            Connection.DataConnectionBusy = false;


        }

        protected void Pause() { pauseSignal.Reset(); }
        protected void Resume() { pauseSignal.Set(); }

        internal abstract void Start(SftTaskFinishCallBack sftTaskFinishCallBack);
        

        //public SftTaskType GetTaskState()
        //{
        //    if (WorkThread == null)
        //        return SftTaskType.Stopped;
        //    switch (WorkThread.ThreadState)
        //    {
        //        case ThreadState.Running:
        //        case ThreadState.Background:
        //            break;

        //        case ThreadState.Suspended:
        //        case ThreadState.SuspendRequested:
        //            break;

        //        case ThreadState.Unstarted:
        //        case ThreadState.StopRequested:
        //        case ThreadState.Stopped:
        //            break;
        //        case ThreadState.WaitSleepJoin:
        //            break;

        //        case ThreadState.AbortRequested:
        //            break;
        //        case ThreadState.Aborted:
        //            break;
        //        default:
        //            break;
        //    }
        //}
    }
    internal class SftClientTransferTask : SftTransferTask
    {
        internal SftClientTransferTask(ushort clientId, ushort reqId, SftConnection dataConnection, SftTaskDirection direction, string clientPath/*, string serverPath*/, long startPos)
        {
            ClientId = clientId;
            ReqId = reqId;
            Direction = direction;
            LocalPath = clientPath;
            StartPos = startPos;
            //RemotePath = serverPath;
            Connection = dataConnection;
            if (Direction == SftTaskDirection.Upload)
            {
                FileInfo localFileInfo = new(LocalPath);
                if (!localFileInfo.Exists)
                {
                    throw new FileNotFoundException(localFileInfo.FullName);
                }
                FileLength = localFileInfo.Length;
            }
            LocalStream = new FileStream(LocalPath,
                (Direction == SftTaskDirection.Upload) ? FileMode.Open : FileMode.Create,
                (Direction == SftTaskDirection.Upload) ? FileAccess.Read : FileAccess.Write);
        }

        protected void BindConnection(SftConnection sftConnection)
        {
            Connection = sftConnection;
        }

        internal override void Start(SftTaskFinishCallBack sftTaskFinishCallBack)
        {
            Console.WriteLine("File transfer task start...");
            if (Connection == null)
                throw new InvalidOperationException("Must bind to a connection before start!");
            if (Direction == SftTaskDirection.Upload) // Upload
            {
                Thread thread = new(SendFileData);
                thread.Start();
            }
            else // Download
            {
                Thread thread = new(ReceiveFileData);
                thread.Start();
            }
            sftTaskFinishCallBack(this);
        }
    }

    internal class SftServerTransferTask : SftTransferTask
    {
        internal SftServerTransferTask(ushort clientId, ushort reqId, SftUploadData sftUploadData, SftConnection sftConnection, string rootDirPath)
        {
            ClientId = clientId;
            ReqId = reqId;
            string queryRelativePath = Path.GetFullPath(sftUploadData.Path).Remove(0, Path.GetFullPath("/").Length);
            LocalPath = Path.Combine(rootDirPath, queryRelativePath);
            Direction = SftTaskDirection.Upload;
            StartPos = sftUploadData.StartPos;
            LocalStream = new FileStream(LocalPath, FileMode.Create, FileAccess.Write);
            Connection = sftConnection;
        }

        internal SftServerTransferTask(ushort clientId, ushort reqId, SftDownloadData sftDownloadData, SftConnection sftConnection, string rootDirPath)
        {
            ClientId = clientId;
            ReqId = reqId;
            string queryRelativePath = Path.GetFullPath(sftDownloadData.Path).Remove(0, Path.GetFullPath("/").Length);
            LocalPath = Path.Combine(rootDirPath, queryRelativePath);
            Direction = SftTaskDirection.Download;
            FileInfo localFileInfo = new(LocalPath);
            if (!localFileInfo.Exists)
            {
                throw new FileNotFoundException(localFileInfo.FullName);
            }
            FileLength = localFileInfo.Length;
            StartPos = sftDownloadData.StartPos;
            LocalStream = new FileStream(LocalPath, FileMode.Open, FileAccess.Read);
            Connection = sftConnection;
        }

        internal override void Start(SftTaskFinishCallBack sftTaskFinishCallBack)
        {
            Console.WriteLine("File transfer task start...");
            if (Connection == null)
                throw new InvalidOperationException("Must bind to a connection before start!");
            if (Direction == SftTaskDirection.Upload) // Upload
            {
                ReceiveFileData();
                //Thread thread = new(ReceiveFileData);
                //thread.Start();
            }
            else // Download
            {
                SendFileData();
                //Thread thread = new(SendFileData);
                //thread.Start();
            }
            sftTaskFinishCallBack(this);
        }
    }
}
