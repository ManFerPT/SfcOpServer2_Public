#pragma warning disable IDE0079
#pragma warning disable CA1416

using shrAudio;
using shrNet;

using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SfcOpClient
{
    public class Client27001(string localPath, IPEndPoint remoteEP) : IDisposable
    {
        public const string ScriptPath = "Assets/Scripts/Met_Common.ini";

        private const int minimumBufferSize = 13;
        private const int maximumBufferSize = 262144;

        private ref struct Message
        {
            public int Size;
            public byte Opcode;
            public ReadOnlySpan<byte> PathBuffer;
            public ReadOnlySpan<byte> FileBuffer;

            public Message(byte[] buffer)
            {
                Size = BitConverter.ToInt32(buffer, 0);
                Opcode = buffer[4];

                int logSize = BitConverter.ToInt32(buffer, 5);

                if (logSize > 0)
                    PathBuffer = new(buffer, 9, logSize);
                else
                    PathBuffer = [];

                int fileSize = BitConverter.ToInt32(buffer, logSize + 9);

                if (fileSize > 0)
                    FileBuffer = new(buffer, logSize + 13, fileSize);
                else
                    FileBuffer = [];
            }
        };

        private readonly string _localPath = localPath;
        private readonly IPEndPoint _remoteEP = remoteEP;
        private readonly StringBuilder _log = new();

        private CancellationTokenSource _cts;
        private Socket _socket;

        public async Task StartAsync()
        {
            _cts = new();
            _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            Task clientTask = ProcessAsync(_cts.Token);
            Task audioTask = AudioPlayer.ProcessAsync(delayInterval: 40, _cts.Token);

            await Task.WhenAny(clientTask, audioTask);

            Close();

            await Task.WhenAll(clientTask, audioTask);
        }

        public void Dispose()
        {
            Close();

            _socket.Dispose();
            _cts.Dispose();

            GC.SuppressFinalize(this);
        }

        private void Close()
        {
            _cts.Cancel();

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            { }

            AudioPlayer.ClearAndStop();
        }

        private async Task ProcessAsync(CancellationToken token)
        {
            byte[] buffer = null;

            try
            {
                // tries to initialize the socket

                DuplexSocket.Initialize(_socket);

                await _socket.ConnectAsync(_remoteEP, token);

                Log($"The client is connected to {_remoteEP}");

                // tries to initialize the buffer

                buffer = ArrayPool<byte>.Shared.Rent(maximumBufferSize);

                while (!token.IsCancellationRequested)
                {
                    int offset = 0;

                continueReading:

                    Memory<byte> remainingMemory = new(buffer, offset, maximumBufferSize - offset);

                    int bytesRead = await _socket.ReceiveAsync(remainingMemory, SocketFlags.None, token);

                    if (bytesRead == 0)
                        break;

                    offset += bytesRead;

                    if (offset < 4)
                        goto continueReading;

                getSize:

                    int size = BitConverter.ToInt32(buffer, 0);

                    if (size < minimumBufferSize || size > maximumBufferSize)
                        break; // out of bounds

                    if (offset < size)
                        goto continueReading;

                    int result = Process(buffer);

                    if (result == 0)
                        break; // something went wrong

                    if (offset == size)
                        continue;

                    Contract.Assert(offset > size);

                    offset -= size;

                    Buffer.BlockCopy(buffer, size, buffer, 0, offset);

                    goto getSize;
                }
            }
            catch (Exception)
            { }
            finally
            {
                if (buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);

                    Log($"The client closed its connection to {_remoteEP}");
                }
                else
                    Log($"An error occurred while connecting to {_remoteEP}");
            }
        }

        private int Process(byte[] buffer)
        {
            void Enqueue(ref ReadOnlySpan<byte> span)
            {
                string filename = Path.Combine(_localPath, Encoding.UTF8.GetString(span));

                if (File.Exists(filename))
                    AudioPlayer.Enqueue(filename);
            }

            try
            {
                Message msg = new(buffer);

                // checks if the message size is valid

                if (msg.Size != msg.PathBuffer.Length + msg.FileBuffer.Length + minimumBufferSize)
                    throw new NotSupportedException();

                // tries to get the file name

                string filename;

                switch (msg.Opcode)
                {
                    case 0:
                        Contract.Assert(msg.PathBuffer.Length == 0);

                        filename = Path.Combine(_localPath, ScriptPath);

                        break;

                    case 1:
                        Contract.Assert(msg.PathBuffer.Length != 0);

                        filename = Path.Combine(_localPath, Encoding.UTF8.GetString(msg.PathBuffer));
                        msg.PathBuffer = [];

                        break;

                    case 2:
                        Contract.Assert(msg.FileBuffer.Length == 0);

                        if (msg.PathBuffer.Length != 0)
                            Enqueue(ref msg.PathBuffer);
                        else
                            AudioPlayer.ClearAndStop();

                        return 1;

                    case 3:
                        Contract.Assert(msg.FileBuffer.Length == 0);

                        AudioPlayer.ClearAndStop();

                        if (msg.PathBuffer.Length != 0)
                            Enqueue(ref msg.PathBuffer);

                        return 1;

                    default:
                        throw new NotSupportedException();
                }

                // tries to write the file

                try
                {
                    File.WriteAllBytes(filename, msg.FileBuffer);

                    Log("The client received an update");

                    return 1;
                }
                catch (Exception)
                {
                    Log("The client got an IO error!");
                }
            }
            catch (Exception)
            {
                Log("The client received invalid data!");
            }

            return 0;
        }

        private void Log(string t)
        {
            DateTime d = DateTime.Now;

            Contract.Assert(_log.Length == 0);

            _log.Append(d.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
            _log.Append(" - ");
            _log.Append(t);
            _log.AppendLine();

            Console.Write(_log.ToString());

            _log.Clear();
        }
    }
}
