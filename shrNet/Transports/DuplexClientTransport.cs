using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace shrNet
{
    public class DuplexClientTransport : DuplexApplication
    {
        private readonly DuplexQueue _inboundQueue;
        private readonly DuplexQueue _outboundQueue;
        private readonly int _dataMinSize;
        private readonly int _dataMaxSize;
        private readonly byte[] _dataDelimiter;

        public EndPoint RemoteEndPoint => _socket.RemoteEndPoint;

        public DuplexClientTransport(Socket socket, DuplexQueue inboundQueue, int dataMinSize, int dataMaxSize, byte[] dataDelimiter) : base(socket)
        {
            _inboundQueue = inboundQueue;
            _outboundQueue = new(isShared: false);

            Contract.Assert((dataMinSize == 0 || dataMinSize == 2 || dataMinSize == 4) && (dataMinSize < dataMaxSize));

            _dataMinSize = dataMinSize;
            _dataMaxSize = dataMaxSize;

            Contract.Assert(
                (dataDelimiter == null && (dataMinSize == 2 || dataMinSize == 4)) ||
                (dataDelimiter != null && dataDelimiter.Length != 0 && dataMinSize == 0)
            );

            _dataDelimiter = dataDelimiter;
        }

        public override async Task StartAsync()
        {
            Task inboundReadTask = InboundReadAsync();
            Task inboundWriteTask = InboundWriteAsync();

            Task outboundReadTask = OutboundReadAsync();
            Task outboundWriteTask = OutboundWriteAsync();

            await Task.WhenAll(inboundReadTask, inboundWriteTask, outboundReadTask, outboundWriteTask);

            Dispose();

#if VERBOSE
            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " <Client" + _id + " disposed>");
#endif

        }

        /// <summary>
        /// When the function returns <see langword="true" />, it is the caller’s responsibility to return the buffer. Use <see cref="DuplexMessage.Release()"/>.
        /// </summary>
        public bool TryRead(out DuplexMessage msg)
        {
            return _inboundQueue.TryDequeue(out msg);
        }

        public bool TryWrite(Span<byte> span)
        {
            Contract.Assert(!span.IsEmpty);

            DuplexMessage msg = new(_id, span.Length);

            span.CopyTo(msg.AsSpan());

            return _outboundQueue.TryEnqueue(msg);
        }

        protected async Task OutboundReadAsync()
        {
            PipeReader reader = _outboundPipe.Input;

            try
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync();

                    ReadOnlySequence<byte> buffer = result.Buffer;
                    SequencePosition consumed = buffer.End;

                    try
                    {
                        if (result.IsCanceled)
                            break;

                        if (!buffer.IsEmpty)
                        {
                            try
                            {
                                InboundEnqueue(buffer, ref consumed);
                            }
                            catch (Exception)
                            {
                                break;
                            }
                        }
                        else if (result.IsCompleted)
                            break;
                    }
                    finally
                    {
                        reader.AdvanceTo(consumed, buffer.End);
                    }
                }
            }
            catch (Exception)
            { }
            finally
            {
                reader.Complete();

                Close();
            }
        }

        protected async Task OutboundWriteAsync()
        {
            PipeWriter writer = _outboundPipe.Output;

            try
            {
                while (!IsDisposing)
                {
                    if (_outboundQueue.TryDequeue(out DuplexMessage msg))
                    {
                        try
                        {
                            msg.AsSpan().CopyTo(writer.GetMemory(msg.Length).Span);

#if VERBOSE
                            OutboundDebug(msg);
#endif

                        }
                        catch (Exception)
                        {
                            break;
                        }
                        finally
                        {
                            msg.Release();
                        }

                        writer.Advance(msg.Length);

                        FlushResult result = await writer.FlushAsync();

                        if (result.IsCanceled || result.IsCompleted)
                            break;
                    }
                    else
                        await Task.Delay(1);
                }
            }
            catch (Exception)
            { }
            finally
            {
                writer.Complete();

                Close();
            }
        }

        protected override void OnClose()
        {
            base.OnClose();

            _outboundPipe.Input.CancelPendingRead();
            _outboundPipe.Output.CancelPendingFlush();
        }

        protected override void OnDispose()
        {
            base.OnDispose();

            if (_inboundQueue != null && !_inboundQueue.IsShared)
                _inboundQueue.Dispose();

            if (!_outboundQueue.IsShared)
                _outboundQueue.Dispose();
        }

        private void InboundEnqueue(ReadOnlySequence<byte> buffer, ref SequencePosition consumed)
        {
            SequenceReader<byte> reader = new(buffer);

            if (_dataMinSize == 4)
            {
                while (reader.TryReadLittleEndian(out int size))
                {
                    if (size <= 4 || size > _dataMaxSize)
                        goto outOfRange;

                    reader.Rewind(4L);

                    if (!reader.TryReadExact(size, out ReadOnlySequence<byte> sequence))
                        break;

                    InboundEnqueue(sequence, size);
                }
            }
            else if (_dataMinSize == 2)
            {
                while (reader.TryReadLittleEndian(out short temp))
                {
                    int size = (ushort)temp;

                    if (size <= 2 || size > _dataMaxSize)
                        goto outOfRange;

                    reader.Rewind(2L);

                    if (!reader.TryReadExact(size, out ReadOnlySequence<byte> sequence))
                        break;

                    InboundEnqueue(sequence, size);
                }
            }
            else
            {
                int maxSize = _dataMaxSize - _dataDelimiter.Length;
                ReadOnlySpan<byte> delimiter = new(_dataDelimiter);

                while (reader.TryReadTo(out ReadOnlySequence<byte> sequence, delimiter, advancePastDelimiter: true))
                {
                    int size = (int)sequence.Length;

                    if (size <= 0 || size > maxSize)
                        goto outOfRange;

                    InboundEnqueue(sequence, size);
                }
            }

            consumed = reader.Position;

            return;

        outOfRange:

            throw new InvalidDataException(nameof(buffer));
        }

        private void InboundEnqueue(ReadOnlySequence<byte> sequence, int size)
        {
            DuplexMessage msg = new(_id, size);

            sequence.CopyTo(msg.AsSpan());

            bool enqueuedMsg = _inboundQueue.TryEnqueue(msg);

#if VERBOSE
            if (enqueuedMsg)
                InboundDebug(msg);
#endif

            Contract.Assert(enqueuedMsg);
        }

#if VERBOSE
        private const string timeStamp = "HH:mm:ss.fff";

        private readonly StringBuilder _outboundLog = new(262144 << 1);
        private readonly StringBuilder _inboundLog = new(262144 << 1);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void OutboundDebug(DuplexMessage msg)
        {
            Contract.Assert(_outboundLog.Length == 0);

            _outboundLog.Append(DateTime.Now.ToString(timeStamp, CultureInfo.InvariantCulture));

            int offset = 0;

            while (true)
            {
                _outboundLog.Append(" [Server");
                _outboundLog.Append(_id);
                _outboundLog.Append("] ");

                int size;

                if (_dataMinSize == 4)
                {
                    size = BitConverter.ToInt32(msg.Buffer, offset);

                    _outboundLog.Append(DuplexMessage.GetHex(msg.Buffer, offset, size));
                }
                else if (_dataMinSize == 2)
                {
                    size = BitConverter.ToInt16(msg.Buffer, offset);

                    _outboundLog.Append(DuplexMessage.GetHex(msg.Buffer, offset, size));
                }
                else
                {
                    size = new ReadOnlySpan<byte>(msg.Buffer, offset, msg.Length - offset).IndexOf(new ReadOnlySpan<byte>(_dataDelimiter));

                    Contract.Assert(size != -1);

                    size += _dataDelimiter.Length;

                    _outboundLog.Append(DuplexMessage.GetString(new ReadOnlySpan<byte>(msg.Buffer, offset, size)));
                }

                string message = _outboundLog.ToString();

                _outboundLog.Clear();

                if (message.EndsWith('\n'))
                    Debug.Write(message);
                else
                    Debug.WriteLine(message);

                offset += size;

                Contract.Assert(offset <= msg.Length);

                if (offset == msg.Length)
                    break;

                _outboundLog.Append(' ', timeStamp.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void InboundDebug(DuplexMessage msg)
        {
            Contract.Assert(_inboundLog.Length == 0);

            _inboundLog.Append(DateTime.Now.ToString(timeStamp, CultureInfo.InvariantCulture));
            _inboundLog.Append(" [Client");
            _inboundLog.Append(_id);
            _inboundLog.Append("] ");

            if (_dataMinSize != 0)
                _inboundLog.Append(DuplexMessage.GetHex(msg.Buffer, 0, msg.Length));
            else
            {
                _inboundLog.Append(DuplexMessage.GetString(msg.AsReadOnlySpan()));
                _inboundLog.Append(DuplexMessage.GetString(_dataDelimiter));
            }

            string message = _inboundLog.ToString();

            _inboundLog.Clear();

            if (message.EndsWith('\n'))
                Debug.Write(message);
            else
                Debug.WriteLine(message);
        }
#endif

    }
}
