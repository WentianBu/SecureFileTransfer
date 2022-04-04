using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureFileTransfer
{
    public class SftUnexpectedResponse : ApplicationException
    {
        public SftPacket? ExPkt { get; set; }
        public SftUnexpectedResponse() : base() { }

        public SftUnexpectedResponse(SftPacket pkt) : base()
        {
            ExPkt = pkt;
        }
        public SftUnexpectedResponse(SftPacket pkt, string? message) : base(message)
        {
            ExPkt = pkt;
        }
    }

    internal class SftFailedTransferTask : ApplicationException
    {
        public SftTransferTask? ExTask { get; set; }

        public SftFailedTransferTask() :base() { }

        public SftFailedTransferTask(SftTransferTask? exTask) : base()
        {
            ExTask = exTask;
        }

        public SftFailedTransferTask(SftTransferTask? exTask, string? message) : base(message)
        {
            ExTask = exTask;
        }
    }
}
