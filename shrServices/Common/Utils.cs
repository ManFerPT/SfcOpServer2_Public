using shrNet;

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Unicode;

namespace shrServices
{
    public static class Utils
    {
        public static readonly char[] HexChars = "0123456789abcdef".ToCharArray();

        // path functions

        public static string LowerCasePath(string directoryOrFilename)
        {
            if (directoryOrFilename.Contains('\\'))
                directoryOrFilename = directoryOrFilename.Replace('\\', '/');

            while (directoryOrFilename.Contains("//", StringComparison.Ordinal))
                directoryOrFilename = directoryOrFilename.Replace("//", "/", StringComparison.Ordinal);

            return directoryOrFilename.ToLowerInvariant();
        }

        // hash and hex functions

        public static string GetHash(string text)
        {
            Span<byte> b = stackalloc byte[text.Length];

            Encoding.ASCII.GetBytes(text.AsSpan(), b);

            Span<byte> h = stackalloc byte[MD5.HashSizeInBytes];

            MD5.TryHashData(b, h, out _);

            return DuplexMessage.GetHex(h);
        }

        public static uint HexToByte(byte[] buffer, int index)
        {
            uint h1 = buffer[index];
            uint h0 = buffer[index + 1];

            h1 = h1 - 48 - (h1 >> 6) * 7 - (h1 / 97 << 5);
            h0 = h0 - 48 - (h0 >> 6) * 7 - (h0 / 97 << 5);

            return (h1 << 4) + h0;
        }

        public static void AppendHex(this StringBuilder sb, byte u8)
        {
            sb.Append(HexChars[u8 >> 4]);
            sb.Append(HexChars[u8 & 15]);
        }

        public static void AppendHex(this StringBuilder sb, uint u32)
        {
            for (int i = 0; i < 4; i++)
            {
                sb.Append(HexChars[(u32 >> 4) & 15]);
                sb.Append(HexChars[u32 & 15]);

                u32 >>= 8;
            }
        }

        // span functions

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append(ref Span<byte> destination, ReadOnlySpan<byte> value)
        {
            value.CopyTo(destination);

            destination = destination[value.Length..];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append(ref Span<byte> destination, string value)
        {
            destination = destination[Encoding.UTF8.GetBytes(value.AsSpan(), destination)..];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append(ref Span<byte> destination, int value)
        {
            Utf8Formatter.TryFormat(value, destination, out int bytesWritten);

            destination = destination[bytesWritten..];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Append(ref Span<byte> destination, byte value)
        {
            destination[0] = value;

            destination = destination[1..];
        }

        public static bool TryGetValue(Span<byte> source, ReadOnlySpan<byte> key, byte delimiter, out string value)
        {
            int i = source.IndexOf(key);

            if (i >= 0)
            {
                source = source[(i + key.Length)..];

                i = source.IndexOf(delimiter);

                if (i >= 0)
                    source = source[..i];

                if (source.Length > 0)
                {
                    value = Encoding.UTF8.GetString(source);

                    return true;
                }
            }

            value = null;

            return false;
        }

        public static bool TryGetValue(Span<byte> source, ReadOnlySpan<byte> key, byte delimiter, out int value)
        {
            int i = source.IndexOf(key);

            if (i >= 0)
            {
                source = source[(i + key.Length)..];

                i = source.IndexOf(delimiter);

                if (i >= 0)
                    source = source[..i];

                if (source.Length > 0)
                    return Utf8Parser.TryParse(source, out value, out _);
            }

            value = 0;

            return false;
        }

        public static bool TryConvert(StringBuilder source, Span<byte> destination)
        {
            Contract.Assert(source.Length > 0 && source.Length <= destination.Length);

            Span<byte> span = destination;

            foreach (ReadOnlyMemory<char> chunk in source.GetChunks())
            {
                OperationStatus status = Utf8.FromUtf16(chunk.Span, span, out int charsRead, out int bytesWritten, false, false);

                if (status != OperationStatus.Done || charsRead != bytesWritten)
                    return false;

                span = span[..bytesWritten];
            }

            return true;
        }

        public static bool TryConvert(string source, Span<byte> destination)
        {
            Contract.Assert(source.Length > 0 && source.Length <= destination.Length);

            OperationStatus status = Utf8.FromUtf16(source.AsSpan(), destination, out int charsRead, out int bytesWritten, false, false);

            return status == OperationStatus.Done && charsRead == bytesWritten;
        }

        // IO functions

        public static void Read(BinaryReader r, bool allowNullString, out string t)
        {
            int c = r.ReadInt32();

            if (c > 0)
            {
                Span<byte> b = stackalloc byte[c];

                if (r.Read(b) == c)
                {
                    t = Encoding.UTF8.GetString(b);

                    return;
                }
            }
            else
            {
                if (c == 0)
                {
                    t = string.Empty;

                    return;
                }

                if (c == -1 && allowNullString)
                {
                    t = null;

                    return;
                }
            }

            throw new NotSupportedException();
        }

        public static void Write(BinaryWriter w, bool allowNullString, string t)
        {
            if (t != null)
            {
                int c = t.Length;

                w.Write(c);

                if (c == 0)
                    return;

                Span<byte> b = stackalloc byte[c];

                if (Encoding.UTF8.GetBytes(t.AsSpan(), b) == c)
                {
                    w.Write(b);

                    return;
                }
            }
            else if (allowNullString)
            {
                w.Write(-1);

                return;
            }

            throw new NotSupportedException();
        }

#if DEBUG
        public static void DebugUtf8Array(byte[] buffer, int offset)
        {
            StringBuilder t = new(32768);

            for (int i = offset; i < buffer.Length; i++)
            {
                int j = buffer[i];

                if (j > 32 && j <= 127)
                    t.Append(char.ConvertFromUtf32(j));
                else
                    t.Append(' ');
            }

            Debug.WriteLine(t.ToString());
        }

        public static void DebugUnicodeArray(byte[] buffer, int offset)
        {
            StringBuilder t = new(32768);

            for (int i = offset; (i + 1) < buffer.Length; i += 2)
            {
                int j = BitConverter.ToUInt16(buffer, i);

                if (j > 32 && j <= 127)
                    t.Append(char.ConvertFromUtf32(j));
                else
                    t.Append(' ');
            }

            Debug.WriteLine(t.ToString());
        }
#endif

    }
}
