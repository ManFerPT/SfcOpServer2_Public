//#define USE_CHANNELS

using shrNet;
using shrServices;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace shrWire
{

#if USE_CHANNELS
    internal sealed class WireClient
    {
        private const int socketSize = 65536;

        private static int _nextId;

        private readonly int _id;

        public WireClient()
        {
            _id = Interlocked.Increment(ref _nextId);
        }

        public async Task StartAsync(TcpClient localClient)
        {
            IPEndPoint localEP = (IPEndPoint)localClient.Client.LocalEndPoint;

            using TcpClient remoteClient = new(localEP.Address.ToString(), localEP.Port + 1);

            DuplexSocket.Initialize(localClient.Client);
            DuplexSocket.Initialize(remoteClient.Client);

            UnboundedChannelOptions options = new()
            {
                SingleReader = true,
                SingleWriter = true,
            };

            Channel<byte[]> localChannel = Channel.CreateUnbounded<byte[]>(options);
            Channel<byte[]> remoteChannel = Channel.CreateUnbounded<byte[]>(options);

            using CancellationTokenSource cts = new();

            Task localSendTask = SendAsync(remoteClient.Client, localChannel.Reader, "Client", 0, cts.Token);
            Task remoteSendTask = SendAsync(localClient.Client, remoteChannel.Reader, "Server", 0, cts.Token);

            Task localReceiveTask = ReceiveAsync(localClient.Client, localChannel.Writer, cts.Token);
            Task remoteReceiveTask = ReceiveAsync(remoteClient.Client, remoteChannel.Writer, cts.Token);

            Console.WriteLine("The man-in-the-middle was created");

            await Task.WhenAny(localSendTask, remoteSendTask, localReceiveTask, remoteReceiveTask);

            cts.Cancel();

            await Task.WhenAll(localSendTask, remoteSendTask, localReceiveTask, remoteReceiveTask);

            Complete(localChannel);
            Complete(remoteChannel);

            localClient.Close();
            remoteClient.Close();

            Console.WriteLine("The man-in-the-middle was closed");
        }

        private static void Complete(Channel<byte[]> channel)
        {
            ChannelReader<byte[]> reader = channel.Reader;

            while (reader.TryRead(out byte[] msg))
                Return(ref msg);
        }

        private static void Return(ref byte[] msg)
        {
            if (msg != null)
            {
                ArrayPool<byte>.Shared.Return(msg);

                msg = null;
            }
        }

        private async Task ReceiveAsync(Socket socket, ChannelWriter<byte[]> writer, CancellationToken token)
        {
            byte[] msg = null;

            try
            {
                while (true)
                {
                    const int length = 4 + socketSize;

                    msg = ArrayPool<byte>.Shared.Rent(length);

                    int offset = 4;

                tryReceive:

                    int c = await socket.ReceiveAsync(new Memory<byte>(msg, offset, length - offset), SocketFlags.None, token).ConfigureAwait(false);

                    if (c == 0)
                        break;

                    offset += c;

                    int i = 4;

                    do
                    {
                        if (i + 4 > offset)
                            goto tryReceive; // header missing

                        c = Unsafe.ReadUnaligned<int>(ref msg[i]);
                        i += c;

                        if (i > offset)
                            goto tryReceive; // data missing
                    }
                    while (i < offset);

                    Unsafe.WriteUnaligned(ref msg[0], offset);

                    await writer.WriteAsync(msg, token).ConfigureAwait(false);

                    msg = null;
                }
            }
            catch (Exception)
            {
                Return(ref msg);
            }
        }

        private async Task SendAsync(Socket socket, ChannelReader<byte[]> reader, string name, int delay, CancellationToken token)
        {
            StringBuilder log = new(2048 + (socketSize << 1));
            byte[] msg = null;

            while (true)
            {
                try
                {
                    msg = await reader.ReadAsync(token).ConfigureAwait(false);

                    int offset = 4;
                    int length = Unsafe.ReadUnaligned<int>(ref msg[0]);

                    if (delay > 0)
                        await Task.Delay(delay, token).ConfigureAwait(false);

                    int c = await socket.SendAsync(new ReadOnlyMemory<byte>(msg, offset, length - offset), SocketFlags.None, token).ConfigureAwait(false);

                    Contract.Assert(c == length - offset);

                    const string format0 = "HH:mm:ss.fff";
                    const string format1 = "            ";

                    log.Append(DateTime.Now.ToString(format0, CultureInfo.InvariantCulture));

                    while (true)
                    {
                        log.Append(" [");
                        log.Append(name);
                        log.Append(_id);
                        log.Append("] ");

                        for (c = offset + Unsafe.ReadUnaligned<int>(ref msg[offset]); offset < c; offset++)
                        {
                            log.Append(Utils.HexChars[msg[offset] >> 4]);
                            log.Append(Utils.HexChars[msg[offset] & 15]);
                        }

                        log.AppendLine();

                        if (offset >= length)
                            break;

                        log.Append(format1);
                    }

                    Debug.Write(log.ToString());

                    log.Clear();
                }
                catch (Exception)
                {
                    break;
                }
                finally
                {
                    Return(ref msg);
                }
            }
        }
    }
#else
    internal sealed class WireClient
    {
        private const int socketSize = 1048576;

        private static int _nextId;

        private readonly int _id;

        public WireClient()
        {
            _id = Interlocked.Increment(ref _nextId);
        }

        public async Task StartAsync(TcpClient localClient)
        {
            IPEndPoint localEP = (IPEndPoint)localClient.Client.LocalEndPoint;

            using TcpClient remoteClient = new(localEP.Address.ToString(), localEP.Port + 1);

            DuplexSocket.Initialize(localClient.Client);
            DuplexSocket.Initialize(remoteClient.Client);

            using CancellationTokenSource cts = new();

            Task localTask = BridgeAsync(localClient.Client, remoteClient.Client, "Client", 1000, cts.Token);
            Task remoteTask = BridgeAsync(remoteClient.Client, localClient.Client, "Server", 0, cts.Token);

            Console.WriteLine("The man-in-the-middle was created");

            await Task.WhenAny(localTask, remoteTask);

            cts.Cancel();

            await Task.WhenAll(localTask, remoteTask);

            localClient.Close();
            remoteClient.Close();

            Console.WriteLine("The man-in-the-middle was closed");
        }

        private async Task BridgeAsync(Socket receiver, Socket sender, string name, int delay, CancellationToken token)
        {
            try
            {
                byte[] msg = new byte[socketSize];
                StringBuilder log = new(512 + (socketSize << 1));

                while (true)
                {
                    if (delay > 0)
                        await Task.Delay(delay, token).ConfigureAwait(false);

                    int length = 0;

                tryReceive:

                    int c = await receiver.ReceiveAsync(new Memory<byte>(msg, length, socketSize - length), SocketFlags.None, token).ConfigureAwait(false);

                    if (c == 0)
                        break;

                    length += c;

                    // checks the integrity of the messages received

                    int i = 0;

                    do
                    {
                        if (i + 4 > length)
                            goto tryReceive; // header missing

                        c = Unsafe.ReadUnaligned<int>(ref msg[i]);
                        i += c;

                        if (i > length)
                            goto tryReceive; // data missing
                    }
                    while (i < length);

                    // tries to send the messages received

                    c = await sender.SendAsync(new ReadOnlyMemory<byte>(msg, 0, length), SocketFlags.None, token).ConfigureAwait(false);

                    Contract.Assert(c == length);

                    // outputs the messages sent

                    const string format0 = "HH:mm:ss.fff";
                    const string format1 = "            ";

                    log.Append(DateTime.Now.ToString(format0, CultureInfo.InvariantCulture));

                    i = 0;

                    while (true)
                    {
                        log.Append(" [");
                        log.Append(name);
                        log.Append(_id);
                        log.Append("] ");

                        for (c = i + Unsafe.ReadUnaligned<int>(ref msg[i]); i < c; i++)
                        {
                            log.Append(Utils.HexChars[msg[i] >> 4]);
                            log.Append(Utils.HexChars[msg[i] & 15]);
                        }

                        log.AppendLine();

                        if (i >= length)
                            break;

                        log.Append(format1);
                    }

                    Debug.Write(log.ToString());

                    log.Clear();
                }
            }
            catch (Exception)
            { }
        }
    }
#endif

}
