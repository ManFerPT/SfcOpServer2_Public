#pragma warning disable IDE0130

using shrNet;
using System.Net.Sockets;

namespace SfcOpServer
{
    // 'dataMaxSize' must match the value on 'SfcOpClient.Client27001'
    public sealed class Client27001(Socket socket) : DuplexClientTransport(socket, inboundQueue: null, dataMinSize: 4, dataMaxSize: Client27000.MaximumBufferSize, dataDelimiter: null)
    {
        public int Address;

        // references 

        public int ClientId;
    }
}
