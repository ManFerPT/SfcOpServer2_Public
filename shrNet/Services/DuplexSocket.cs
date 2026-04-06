using System.Diagnostics.Contracts;
using System.Net.Sockets;

namespace shrNet
{
    public static class DuplexSocket
    {
        public static void Initialize(Socket socket, int receiveTimeout, int sendTimout)
        {
            Initialize(socket);

            socket.ReceiveTimeout = receiveTimeout;
            socket.SendTimeout = sendTimout;
        }

        public static void Initialize(Socket socket)
        {
            Contract.Assert(
                socket.Blocking &&
                socket.DontFragment &&
                socket.ReceiveBufferSize == 65536 &&
                socket.SendBufferSize == 65536
            );

            socket.NoDelay = true;
        }
    }
}
