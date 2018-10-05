using System;
using System.Collections.Generic;
using System.Text;

namespace SSDP.UPnP.PCL.Model
{
    public class SSDPException : Exception
    {
        public SSDPException() : base() { }

        public SSDPException(string message) : base(message) { }

        public SSDPException(string message, Exception inner) : base(message, inner) { }
    }
}
