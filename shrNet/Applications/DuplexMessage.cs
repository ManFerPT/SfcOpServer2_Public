using System;
using System.Buffers;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Text.Unicode;

namespace shrNet
{
    public readonly struct DuplexMessage
    {
        private static readonly byte[] _hexadecimals =
        [
            (byte)'0', (byte)'1', (byte)'2', (byte)'3',
            (byte)'4', (byte)'5', (byte)'6', (byte)'7',
            (byte)'8', (byte)'9', (byte)'a', (byte)'b',
            (byte)'c', (byte)'d', (byte)'e', (byte)'f'
        ];

        public readonly int Id;

        public readonly byte[] Buffer;
        public readonly int Length;

        public DuplexMessage(int id, int length)
        {
            Id = id;

            Buffer = ArrayPool<byte>.Shared.Rent(length);
            Length = length;
        }

        public DuplexMessage(int id, byte[] buffer, int length)
        {
            Contract.Assert(buffer != null);

            Id = id;

            Buffer = buffer;
            Length = length;
        }

        public void Release()
        {
            ArrayPool<byte>.Shared.Return(Buffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> AsSpan()
        {
            return new(Buffer, 0, Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<byte> AsReadOnlySpan()
        {
            return new(Buffer, 0, Length);
        }

        // static functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetHex(ref byte bytes, nint length)
        {
            if (length == 0)
                return string.Empty;

            char[] chars = ArrayPool<char>.Shared.Rent((int)(length << 1));

            ref uint destiny = ref Unsafe.As<char, uint>(ref MemoryMarshal.GetArrayDataReference(chars));
            ref byte hexadecimals = ref MemoryMarshal.GetArrayDataReference(_hexadecimals);

            for (nint i = 0; i < length; i++)
            {
                nint a = Unsafe.Add(ref bytes, i);

                uint b = Unsafe.Add(ref hexadecimals, a & 15);
                uint c = Unsafe.Add(ref hexadecimals, a >> 4);

                Unsafe.WriteUnaligned(ref Unsafe.As<uint, byte>(ref Unsafe.Add(ref destiny, i)), b << 16 | c);
            }

            string hex = new(chars, 0, (int)(length << 1));

            ArrayPool<char>.Shared.Return(chars);

            return hex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetHex(Span<byte> bytes)
        {
            return GetHex(ref MemoryMarshal.GetReference(bytes), bytes.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetHex(byte[] array, int start, int length)
        {
            return GetHex(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), start), length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetString(ReadOnlySpan<byte> bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
