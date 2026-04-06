using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace shrNet
{
    public class DuplexServiceTransport(DuplexService service, Socket socket) : DuplexApplication(socket)
    {
        private DuplexService _service = service;

        public IPAddress RemoteIPAddress => ((IPEndPoint)_socket.RemoteEndPoint)?.Address;

        public override async Task StartAsync()
        {
            Task inboundReadTask = InboundReadAsync();
            Task inboundWriteTask = InboundWriteAsync();

            Task outboundReadWriteTask = OutboundReadWriteAsync();

            await Task.WhenAll(inboundReadTask, inboundWriteTask, outboundReadWriteTask);

            Dispose();

#if VERBOSE
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " <Client" + _id + " disposed>");
#endif

        }

        public void Read(ReadOnlySequence<byte> sequence, byte[] buffer, out Span<byte> msg)
        {
            msg = new Span<byte>(buffer, 0, (int)sequence.Length);

            sequence.CopyTo(msg);

#if VERBOSE
            LogMessage(msg, isFromClient: true);
#endif

        }

        public void Write(Span<byte> msg)
        {
            int size = msg.Length;

            msg.CopyTo(_outboundPipe.Output.GetMemory(size).Span);
            _outboundPipe.Output.Advance(size);

#if VERBOSE
            LogMessage(msg, isFromClient: false);
#endif

        }

        protected override void OnDispose()
        {
            base.OnDispose();

            _service = null;
        }

        private async Task OutboundReadWriteAsync()
        {
            PipeReader reader = _outboundPipe.Input;
            PipeWriter writer = _outboundPipe.Output;

            try
            {
                int result;

                if (_service._handshakeClientMethod != null)
                {
                    result = await _service._handshakeClientMethod(this);

                    Contract.Assert(result >= 0);

                    if (result > 0)
                    {
                        FlushResult flushResult = await writer.FlushAsync();

                        if (flushResult.IsCanceled || flushResult.IsCompleted)
                            return;
                    }
                }

                bool running = true;

                while (running)
                {
                    ReadResult readResult = await reader.ReadAsync();

                    if (readResult.IsCanceled)
                        break;

                    ReadOnlySequence<byte> buffer = readResult.Buffer;
                    bool notEmpty = !buffer.IsEmpty;
                    int bytesToFlush = 0;

                    if (notEmpty)
                    {
                        try
                        {
                            while (true)
                            {
                                result = OutboundRead(ref buffer, out ReadOnlySequence<byte> sequence);

                                if (result == 0)
                                    break; // data missing

                                Contract.Assert(result > 0);

                                result = await _service._processMessageMethod(this, sequence);

                                if (result < 0)
                                {
                                    Contract.Assert(result == -1);

                                    running = false;

                                    break; // invalid data
                                }

                                bytesToFlush += result;

                                if (buffer.Length == 0)
                                    break; // no more data
                            }
                        }
                        catch (Exception)
                        {
                            running = false;
                        }
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);

                    if (bytesToFlush != 0)
                    {
                        Contract.Assert(bytesToFlush > 0);

                        FlushResult flushResult = await writer.FlushAsync();

                        if (flushResult.IsCanceled || flushResult.IsCompleted)
                            break;
                    }

                    if (notEmpty)
                        continue;

                    if (readResult.IsCompleted)
                        break;
                }
            }
            catch (Exception)
            { }
            finally
            {
                reader.Complete();
                writer.Complete();

                Close();
            }
        }

        private int OutboundRead(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> sequence)
        {
            SequenceReader<byte> reader = new(buffer);

            int size;

            if (_service._dataMinSize == 4)
            {
                if (!reader.TryReadLittleEndian(out size))
                    goto sizeMissing;

                if (size <= 4 || size > _service._dataMaxSize)
                    goto invalidSize;

                reader.Rewind(4L);

                if (!reader.TryReadExact(size, out sequence))
                    goto dataMissing;
            }
            else if (_service._dataMinSize == 2)
            {
                if (!reader.TryReadLittleEndian(out short size16))
                    goto sizeMissing;

                size = (ushort)size16;

                if (size <= 2 || size > _service._dataMaxSize)
                    goto invalidSize;

                reader.Rewind(2L);

                if (!reader.TryReadExact(size, out sequence))
                    goto dataMissing;
            }
            else
            {
                ReadOnlySpan<byte> delimiter = new(_service._dataDelimiter);

                if (!reader.TryReadTo(out sequence, delimiter, advancePastDelimiter: true))
                    goto dataMissing;

                size = (int)sequence.Length;

                if (size <= 0 || size > _service._dataMaxSize - delimiter.Length)
                    goto invalidSize;
            }

            buffer = buffer.Slice(reader.Position);

            return size;

        sizeMissing:

            sequence = default;

        dataMissing:

            return 0;

        invalidSize:

            throw new ArgumentOutOfRangeException(nameof(buffer));
        }

#if VERBOSE
        private readonly StringBuilder _log = new(2048 << 1);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void LogMessage(Span<byte> msg, bool isFromClient)
        {
            Contract.Assert(_log.Length == 0);

            _log.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));

            if (isFromClient)
                _log.Append(" [Client");
            else
                _log.Append(" [Server");

            _log.Append(_id);
            _log.Append("] ");

            if (_service._dataMinSize != 0)
                _log.Append(DuplexMessage.GetHex(msg));
            else
            {
                _log.Append(DuplexMessage.GetString(msg));

                if (isFromClient)
                    _log.Append(DuplexMessage.GetString(_service._dataDelimiter));
            }

            string message = _log.ToString();

            _log.Clear();

            if (message.EndsWith('\n'))
                Debug.Write(message);
            else
                Debug.WriteLine(message);
        }
#endif

    }
}
