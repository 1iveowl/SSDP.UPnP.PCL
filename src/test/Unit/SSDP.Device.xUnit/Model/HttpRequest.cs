using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HttpMachine;
using ISimpleHttpListener.Rx.Enum;
using ISimpleHttpListener.Rx.Model;

namespace SSDP.Device.xUnit.Model
{
    public class HttpRequest : IHttpRequestResponse
    {
        public bool IsEndOfRequest { get; internal set; }
        public bool IsRequestTimedOut { get; internal set; }
        public bool IsUnableToParseHttp { get; internal set; }
        public bool HasParsingErrors { get; internal set; }
        public RequestType RequestType { get; internal set; }
        public int MajorVersion { get; internal set; }
        public int MinorVersion { get; internal set; }
        public IDictionary<string, string> Headers { get; internal set; }
        public MemoryStream Body { get; internal set; }
        public IPEndPoint LocalIpEndPoint { get; internal set; }
        public IPEndPoint RemoteIpEndPoint { get; internal set; }
        public Stream ResponseStream { get; internal set; }
        public TcpClient TcpClient { get; internal set; }
        public bool ShouldKeepAlive { get; internal set; }
        public object UserContext { get; internal set; }
        public string Method { get; internal set; }
        public string RequestUri { get; internal set; }
        public string Path { get; internal set; }
        public string QueryString { get; internal set; }
        public string Fragment { get; internal set; }
        public int StatusCode { get; internal set; }
        public string ResponseReason { get; internal set; }
        public MessageType MessageType { get; internal set; }
    }
}
