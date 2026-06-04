//#define TEST_ASSETS

#pragma warning disable CA1416, CA1822, IDE1006

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace shrQ3
{
    public sealed class clsQ3
    {
        // public enumerations

        public enum IndexingMethods
        {
            None, // this flag, when saving, makes sure each sprite is assigned its own bitmap

            HashKey,
            FileOffset,

            Total
        }

        // public constants

        public const int DefaultBufferSize = 1 << 26;  // 64MB

        // transparent color (argb)

        public const byte TransparentColorAlpha = 255;
        public const byte TransparentColorRed = 239;
        public const byte TransparentColorGreen = 0;
        public const byte TransparentColorBlue = 239;

        // 8 bit transparent color

        public const byte Transparent8BitColor = 0;

        // 16 bit transparent color

        public const ushort Transparent16BitColor = (ushort)(
            (uint)TransparentColorBlue >> 3 << 0 |
            (uint)TransparentColorGreen >> 3 << 5 |
            (uint)TransparentColorRed >> 3 << 10
        );

        // 32 bit transparent color

        public const uint Transparent32BitColor =
            (uint)TransparentColorBlue << 0 |
            (uint)TransparentColorGreen << 8 |
            (uint)TransparentColorRed << 16 |
            (uint)TransparentColorAlpha << 24
        ;

        // 8 bit decoding\encoding masks

        private const nint lenMask8Bit = 63;
        private const nint runMask8Bit = 64;
        private const nint transparentMask8Bit = 128;

        // 16 bit decoding\encoding masks

        private const nint lenMask16Bit = 16383;
        private const nint runMask16Bit = 16384;
        private const nint transparentMask16Bit = 32768;

        // static variables

        private readonly static ushort[] _palette16;
        private readonly static uint[] _palette32;

        private readonly static char[] _hexChars;

        // private variables

        private readonly tFile _files;
        private readonly Dictionary<string, tSprite> _sprites;
        private readonly tDirectory _directories;
        private readonly Dictionary<int, tAsset> _assets;

        private readonly Dictionary<string, int> _names;

        private readonly byte[] _buffer;

        // public fields

        public Dictionary<string, tSprite> Sprites => _sprites;
        public Dictionary<int, tAsset> Assets => _assets;

        public Dictionary<string, int> Names => _names;

        public bool IsEmpty => _sprites.Count + _assets.Count + _names.Count == 0;

        public byte[] DefaultBuffer => _buffer;

        // public constructors\destructors

        static clsQ3()
        {
            _palette16 = new ushort[256];
            _palette32 = new uint[256];

            ushort c16 = 0b_0_11111_11111_11111;
            uint c32 = 0b_11111111_11111111_11111111_11111111;

            for (int i = 0; i < 256; i += 8)
            {
                for (int j = i; j < i + 8; j++)
                {
                    _palette16[j] = c16;
                    _palette32[j] = c32;
                }

                c16 -= 0b_0_00001_00001_00001;
                c32 -= 0b_00000000_00001000_00001000_00001000;
            }

            _palette16[0] = Transparent16BitColor;
            _palette32[0] = Transparent32BitColor;

            _hexChars = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'];
        }

        public clsQ3(int bufferSize = DefaultBufferSize)
        {
            _files = new();
            _sprites = [];
            _directories = new();
            _assets = [];

            _names = [];

            if (bufferSize > 0)
                _buffer = new byte[bufferSize];
        }

        // tries to clear this

        public void Clear()
        {
            _files.DirectoryOffset = 0;

            _sprites.Clear();

            _directories.DirectorySize = 0;
            _directories.AssetsCount = 0;

            _assets.Clear();

            _names.Clear();
        }

        // tries to load this

        public bool Load(string filename, IndexingMethods indexingMethod)
        {
            FileStream f = null;
            BinaryReader r = null;

            try
            {
                f = new(filename, FileMode.Open, FileAccess.Read, FileShare.None);
                r = new(f, Encoding.UTF8, leaveOpen: true);

                Load(r, indexingMethod);

                return true;
            }
            catch (Exception)
            {
                Clear();

                return false;
            }
            finally
            {
                r?.Dispose();
                f?.Dispose();
            }
        }

        public void Load(BinaryReader r, IndexingMethods indexingMethod)
        {
            Contract.Assert(IsEmpty);

            // tries to load the files

            _files.ReadFrom(r);

            // tries to load the directories

            Stream s = r.BaseStream;

            s.Seek(_files.DirectoryOffset, SeekOrigin.Begin);

            _directories.ReadFrom(r);

            // tries to load the assets and sprites

            for (int id = 1; id <= _directories.AssetsCount; id++)
            {
                tAsset.eType assetType = (tAsset.eType)r.PeekChar();

                long position;
                tAsset asset;

#if TEST_ASSETS
                position = s.Position;

                tUnknown unknown = new(r);

                s.Seek(position, SeekOrigin.Begin);
#endif

                switch (assetType)
                {
                    case tAsset.eType.Bmp:
                        tBmpAsset bitmap = new(r);

                        position = s.Position;

                        s.Seek(bitmap.FileOffset, SeekOrigin.Begin);

                        tSprite sprite = new(r);

                        s.Seek(position, SeekOrigin.Begin);

                        Contract.Assert(bitmap.FileSize == sprite.Length);

                        bitmap.HashKey = indexingMethod switch
                        {
                            IndexingMethods.HashKey => GetHashKey(sprite),
                            IndexingMethods.FileOffset => bitmap.FileOffset.ToString(CultureInfo.InvariantCulture),
                            _ => throw new NotSupportedException()
                        };

                        _sprites.TryAdd(bitmap.HashKey, sprite);

                        asset = bitmap;
                        break;

                    case tAsset.eType.Scene:
                        asset = new tVisualGroupAsset(r);
                        break;

                    case tAsset.eType.Button:
                        asset = new tPushBtnAsset(r);
                        break;

                    case tAsset.eType.TextEdit:
                        asset = new tTextEditAsset(r);
                        break;

                    case tAsset.eType.StateBtn:
                        asset = new tValueBtnAsset(r);
                        break;

                    case tAsset.eType.Slider:
                        asset = new tSliderAsset(r);
                        break;

                    case tAsset.eType.ScrollBox:
                        asset = new tScrollBoxAsset(r);
                        break;

                    case tAsset.eType.RadioGroup:
                        asset = new tRadioGroupAsset(r);
                        break;

                    case tAsset.eType.TextRect:
                        asset = new tStaticTextAsset(r);
                        break;

                    case tAsset.eType.GenericRect:
                        asset = new tGenericRectAsset(r);
                        break;

                    default:
                        throw new InvalidDataException();
                }

#if TEST_ASSETS
                Contract.Assert(unknown.Length == asset.Length);
#endif

                _assets.Add(id, asset);
            }

            Contract.Assert(s.Position == _files.DirectoryOffset + _directories.DirectorySize);
        }

        public unsafe string GetHashKey(tSprite sprite)
        {
            string hashKey;

            fixed (byte* p = _buffer)
            {
                int c = sprite.Data.Length;

                // writes the sprite header

                *(short*)p = sprite.SizeX;
                *(short*)(p + 2) = sprite.SizeY;
                *(short*)(p + 4) = sprite.RefX;
                *(short*)(p + 6) = sprite.RefY;
                *(ushort*)(p + 8) = sprite.Reserved;
                *(short*)(p + 10) = sprite.BitDepth;
                *(short*)(p + 12) = sprite.Opacity;
                *(short*)(p + 14) = (short)sprite.Compression;
                *(int*)(p + 16) = c;

                // tries to write the sprite data

                if (c > 0)
                    Buffer.MemoryCopy(Unsafe.AsPointer(ref sprite.Data[0]), p + tSprite.HeaderSize, c, c);

                // gets the hash bytes

                c += tSprite.HeaderSize;

                byte* b = p + c;
                char* h = (char*)(b + SHA256.HashSizeInBytes);

                SHA256.TryHashData(new ReadOnlySpan<byte>(p, c), new Span<byte>(b, SHA256.HashSizeInBytes), out _);

                for (char* h0 = h; b < h0; b++, h += 2)
                {
                    c = *b;

                    *h = _hexChars[c >> 4];
                    *(h + 1) = _hexChars[c & 15];
                }

                hashKey = new string((char*)b, 0, SHA256.HashSizeInBytes << 1);
            }

            return hashKey;
        }

        // tries to save this

        public bool Save(string filename, IndexingMethods indexingMethod, bool updateSprites = true)
        {
            FileStream f = null;
            BinaryWriter w = null;

            try
            {
                f = new(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                w = new(f, Encoding.UTF8, leaveOpen: true);

                Save(w, indexingMethod, updateSprites);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                w?.Dispose();
                f?.Dispose();
            }
        }

        public void Save(BinaryWriter w, IndexingMethods indexingMethod, bool updateSprites)
        {
            Contract.Assert(indexingMethod >= IndexingMethods.None && indexingMethod < IndexingMethods.Total);

            Dictionary<string, int> offsets = null;

            MemoryStream m1 = null;
            BinaryWriter w1 = null;

            MemoryStream m2 = null;
            BinaryWriter w2 = null;

            try
            {
                if (updateSprites)
                {
                    offsets = new(_sprites.Count);

                    m1 = new(1 << 27); // 128MB
                    w1 = new(m1, Encoding.UTF8, leaveOpen: true);
                }

                m2 = new(1 << 20); // 1MB
                w2 = new(m2, Encoding.UTF8, leaveOpen: true);

                foreach (KeyValuePair<int, tAsset> p in _assets)
                {
                    tAsset asset = p.Value;

                    if (updateSprites && asset.Type == tAsset.eType.Bmp)
                    {
                        tBmpAsset bitmap = (tBmpAsset)asset;
                        tSprite sprite = _sprites[bitmap.HashKey];

                        bitmap.FileSize = sprite.Length;

                        if (indexingMethod == IndexingMethods.None)
                        {
                            // creates an unique sprite for each bitmap

                            bitmap.FileOffset = (int)(m1.Position + _files.Length);

                            sprite.WriteTo(w1);
                        }
                        else if (!offsets.TryGetValue(bitmap.HashKey, out bitmap.FileOffset))
                        {
                            bitmap.FileOffset = (int)(m1.Position + _files.Length);

                            offsets.Add(bitmap.HashKey, bitmap.FileOffset);

                            sprite.WriteTo(w1);
                        }
                    }

                    asset.WriteTo(w2);
                }

                if (updateSprites)
                {
                    // writes the files

                    _files.DirectoryOffset = (int)(m1.Position + _files.Length);

                    _files.WriteTo(w);

                    // writes the sprites

                    m1.SetLength(m1.Position);
                    m1.WriteTo(w.BaseStream);
                }
                else
                {
                    // advances to the last known directory offset
                    // (we assume here that the sprites didn't changed)

                    w.Seek(_files.DirectoryOffset, SeekOrigin.Begin);
                }

                // writes the directories

                _directories.DirectorySize = (int)(m2.Position + _directories.Length);
                _directories.AssetsCount = _assets.Count;

                _directories.WriteTo(w);

                // writes the assets

                m2.SetLength(m2.Position);
                m2.WriteTo(w.BaseStream);

                // flushes the file

                w.BaseStream.SetLength(w.BaseStream.Position);
                w.BaseStream.Flush();
            }
            finally
            {
                w2?.Dispose();
                m2?.Dispose();

                w1?.Dispose();
                m1?.Dispose();
            }
        }

        // updates this

        public void Update()
        {
            _names.Clear();

            if (_assets.Count > 0)
            {
                Dictionary<string, int> d = new(_assets.Count);

                foreach (KeyValuePair<int, tAsset> p in _assets)
                {
                    tAsset asset = p.Value;

                    if (!d.TryAdd(asset.Name.Value, asset.Id))
                        d[asset.Name.Value] = 0;
                }

                foreach (KeyValuePair<string, int> p in d)
                {
                    if (p.Value != 0)
                        _names.Add(p.Key, p.Value);
                }
            }
        }

        // tries to get a asset by name

        public bool TryGetAsset(string name, out tAsset asset)
        {
            Contract.Assert(name != null && name.Length > 0);

            if (_names.TryGetValue(name, out int id))
            {
                asset = _assets[id];

                return true;
            }

            asset = null;

            return false;
        }

        public tAsset GetAsset(string name)
        {
            Contract.Assert(name != null && name.Length > 0);

            return _assets[_names[name]];
        }

        // tries to convert a sprite into a bitmap

        public static unsafe bool TryConvert(tSprite sprite, out Bitmap bitmap)
        {
            if ((sprite == null) || (sprite.Data == null) || (sprite.SizeX <= 0) || (sprite.SizeY <= 0) || (sprite.Compression != tSprite.eCompression.Raw && sprite.Compression != tSprite.eCompression.Runlen))
                goto notSupported;

            PixelFormat bitmapFormat;

            if (sprite.BitDepth == 8)
                bitmapFormat = PixelFormat.Format8bppIndexed;
            else if (sprite.BitDepth == 16)
                bitmapFormat = PixelFormat.Format16bppRgb555;
            else
                goto notSupported;

            bitmap = new Bitmap(sprite.SizeX, sprite.SizeY, bitmapFormat);

            if (bitmapFormat == PixelFormat.Format8bppIndexed)
            {
                ColorPalette temp = bitmap.Palette;

                for (int i = 0; i < 256; i++)
                    temp.Entries[i] = Color.FromArgb((int)_palette32[i]);

                bitmap.Palette = temp;
            }

            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmapFormat);

            Decode((byte*)Unsafe.AsPointer(ref sprite.Data[0]), sprite.SizeX, sprite.SizeY, sprite.BitDepth, sprite.Compression, (byte*)bitmapData.Scan0, out nint bytesDecoded);

            Contract.Assert(bytesDecoded == bitmapData.Height * bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return true;

        notSupported:

            bitmap = null;

            return false;
        }

        // tries to convert a bitmap into a sprite

        public bool TryConvert(Bitmap bitmap, out tSprite sprite)
        {
            return TryConvert(bitmap, out sprite, tSprite.eCompression.Runlen, ref _buffer[0]);
        }

        public static unsafe bool TryConvert(Bitmap bitmap, out tSprite sprite, tSprite.eCompression spriteCompression, ref byte spriteData0)
        {
            if ((bitmap == null) || (spriteCompression != tSprite.eCompression.Raw && spriteCompression != tSprite.eCompression.Runlen))
                goto notSupported;

            short bitDepth;

            if (bitmap.PixelFormat == PixelFormat.Format8bppIndexed)
                bitDepth = 8;
            else if (bitmap.PixelFormat == PixelFormat.Format16bppRgb555)
                bitDepth = 16;
            else
                goto notSupported;

            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
            byte* spriteData = (byte*)Unsafe.AsPointer(ref spriteData0);

            Encode((byte*)bitmapData.Scan0, bitmapData.Width, bitmapData.Height, spriteData, bitDepth, spriteCompression, out nint bytesEncoded);

            Contract.Assert(bytesEncoded != 0);

            bitmap.UnlockBits(bitmapData);

            sprite = new()
            {
                SizeX = (short)(bitmap.Width),
                SizeY = (short)(bitmap.Height),
                RefX = (short)(bitmap.Width >> 1),
                RefY = (short)(bitmap.Height >> 1),

                BitDepth = bitDepth,
                Opacity = 255,
                Compression = spriteCompression,
                Data = new byte[bytesEncoded]
            };

            Buffer.MemoryCopy(spriteData, (byte*)Unsafe.AsPointer(ref sprite.Data[0]), bytesEncoded, bytesEncoded);

            return true;

        notSupported:

            sprite = null;

            return false;
        }

        // decoders

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void Decode(byte* spriteData, nint spriteSizeX, nint spriteSizeY, nint spriteBitDepth, tSprite.eCompression spriteCompression, byte* bitmapScan, out nint bytesDecoded)
        {
            if (spriteBitDepth == 8)
            {
                if (spriteCompression == tSprite.eCompression.Raw)
                    DecodeRaw8to8(spriteData, spriteSizeX, spriteSizeY, bitmapScan, out bytesDecoded);
                else if (spriteCompression == tSprite.eCompression.Runlen)
                    DecodeRunlen8to8(spriteData, spriteSizeX, spriteSizeY, bitmapScan, out bytesDecoded);
                else
                    goto notSupported;
            }
            else if (spriteBitDepth == 16)
            {
                if (spriteCompression == tSprite.eCompression.Raw)
                    DecodeRaw16to16(spriteData, spriteSizeX, spriteSizeY, bitmapScan, out bytesDecoded);
                else if (spriteCompression == tSprite.eCompression.Runlen)
                    DecodeRunlen16to16(spriteData, spriteSizeX, spriteSizeY, bitmapScan, out bytesDecoded);
                else
                    goto notSupported;
            }
            else
                goto notSupported;

            return;

        notSupported:

            bytesDecoded = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void DecodeRaw8to8(byte* spriteData, nint spriteSizeX, nint spriteSizeY, byte* bitmapScan, out nint bytesDecoded)
        {
            nint bitmapStride = (spriteSizeX * 8 + 31 & ~31) >> 3;

            bytesDecoded = spriteSizeY * bitmapStride;

            byte* eof = bitmapScan + bytesDecoded;

            spriteData += (nint)512;

            spriteSizeY = bitmapStride;
            bitmapStride -= 4;

            do
            {
                *(uint*)(bitmapScan + bitmapStride) = 0;

                Buffer.MemoryCopy(spriteData, bitmapScan, spriteSizeX, spriteSizeX);

                spriteData += spriteSizeX;
                bitmapScan += spriteSizeY;
            }
            while (bitmapScan < eof);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void DecodeRunlen8to8(byte* spriteData, nint spriteSizeX, nint spriteSizeY, byte* bitmapScan, out nint bytesDecoded)
        {
            nint bitmapStride = (spriteSizeX * 8 + 31 & ~31) >> 3;

            bytesDecoded = spriteSizeY * bitmapStride;

            byte* eof = bitmapScan + bytesDecoded;

            spriteData += 512 + (spriteSizeY << 2);

            bitmapStride -= 4;

            do
            {
                byte* eol = bitmapScan + spriteSizeX;

                *(uint*)(bitmapScan + bitmapStride) = 0;

                do
                {
                    spriteSizeY = *spriteData;

                    spriteData++;

                    if ((spriteSizeY & runMask8Bit) != 0)
                    {
                        if ((spriteSizeY & transparentMask8Bit) != 0)
                        {
                            spriteSizeY &= lenMask8Bit;

                            Unsafe.InitBlockUnaligned(bitmapScan, Transparent8BitColor, (uint)spriteSizeY);
                        }
                        else
                        {
                            spriteSizeY &= lenMask8Bit;

                            Unsafe.InitBlockUnaligned(bitmapScan, *spriteData, (uint)spriteSizeY);

                            spriteData++;
                        }
                    }
                    else
                    {
                        Buffer.MemoryCopy(spriteData, bitmapScan, spriteSizeY, spriteSizeY);

                        spriteData += spriteSizeY;
                    }

                    bitmapScan += spriteSizeY;
                }
                while (bitmapScan < eol);

                bitmapScan += -(nint)bitmapScan & 3;
            }
            while (bitmapScan < eof);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void DecodeRaw16to16(byte* spriteData, nint spriteSizeX, nint spriteSizeY, byte* bitmapScan, out nint bytesDecoded)
        {
            nint bitmapStride = (spriteSizeX * 16 + 31 & ~31) >> 3;

            bytesDecoded = spriteSizeY * bitmapStride;

            byte* eof = bitmapScan + bytesDecoded;

            spriteSizeX <<= 1;

            spriteSizeY = bitmapStride;
            bitmapStride -= 4;

            do
            {
                *(uint*)(bitmapScan + bitmapStride) = 0;

                Buffer.MemoryCopy(spriteData, bitmapScan, spriteSizeX, spriteSizeX);

                spriteData += spriteSizeX;
                bitmapScan += spriteSizeY;
            }
            while (bitmapScan < eof);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void DecodeRunlen16to16(byte* spriteData, nint spriteSizeX, nint spriteSizeY, byte* bitmapScan, out nint bytesDecoded)
        {
            nint bitmapStride = (spriteSizeX * 16 + 31 & ~31) >> 3;

            bytesDecoded = spriteSizeY * bitmapStride;

            byte* eof = bitmapScan + bytesDecoded;

            spriteData += spriteSizeY << 2;
            spriteSizeX <<= 1;

            bitmapStride -= 4;

            do
            {
                byte* eol = bitmapScan + spriteSizeX;

                *(uint*)(bitmapScan + bitmapStride) = 0;

                do
                {
                    spriteSizeY = *(ushort*)spriteData;

                    spriteData += (nint)2;

                    if ((spriteSizeY & runMask16Bit) != 0)
                    {
                        if ((spriteSizeY & transparentMask16Bit) != 0)
                        {
                            spriteSizeY &= lenMask16Bit;

                            new Span<ushort>(bitmapScan, (int)spriteSizeY).Fill(Transparent16BitColor);

                            spriteSizeY <<= 1;
                        }
                        else
                        {
                            spriteSizeY &= lenMask16Bit;

                            new Span<ushort>(bitmapScan, (int)spriteSizeY).Fill(*(ushort*)spriteData);

                            spriteSizeY <<= 1;
                            spriteData += (nint)2;
                        }
                    }
                    else
                    {
                        spriteSizeY <<= 1;

                        Buffer.MemoryCopy(spriteData, bitmapScan, spriteSizeY, spriteSizeY);

                        spriteData += spriteSizeY;
                    }

                    bitmapScan += spriteSizeY;
                }
                while (bitmapScan < eol);

                bitmapScan += -(nint)bitmapScan & 3;
            }
            while (bitmapScan < eof);
        }

        // encoders

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void Encode(byte* bitmapScan, nint bitmapWidth, nint bitmapHeight, byte* spriteData, nint spriteBitDepth, tSprite.eCompression spriteCompression, out nint bytesEncoded)
        {
            if (spriteBitDepth == 8)
            {
                if (spriteCompression == tSprite.eCompression.Raw)
                    EncodeRaw8to8(bitmapScan, bitmapWidth, bitmapHeight, spriteData, out bytesEncoded);
                else if (spriteCompression == tSprite.eCompression.Runlen)
                    EncodeRunlen8to8(bitmapScan, bitmapWidth, bitmapHeight, spriteData, out bytesEncoded);
                else
                    goto notSupported;
            }
            else if (spriteBitDepth == 16)
            {
                if (spriteCompression == tSprite.eCompression.Raw)
                    EncodeRaw16to16(bitmapScan, bitmapWidth, bitmapHeight, spriteData, out bytesEncoded);
                else if (spriteCompression == tSprite.eCompression.Runlen)
                    EncodeRunlen16to16(bitmapScan, bitmapWidth, bitmapHeight, spriteData, out bytesEncoded);
                else
                    goto notSupported;
            }
            else
                goto notSupported;

            return;

        notSupported:

            bytesEncoded = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void EncodeRaw8to8(byte* bitmapScan, nint bitmapWidth, nint bitmapHeight, byte* spriteData, out nint bytesEncoded)
        {
            bytesEncoded = (nint)spriteData;

            nint bitmapStride = (bitmapWidth * 8 + 31 & ~31) >> 3;

            Buffer.MemoryCopy(Unsafe.AsPointer(ref _palette16[0]), spriteData, 512L, 512L);

            spriteData += (nint)512;

            byte* eof = spriteData + bitmapHeight * bitmapWidth;

            do
            {
                Buffer.MemoryCopy(bitmapScan, spriteData, bitmapWidth, bitmapWidth);

                bitmapScan += bitmapStride;
                spriteData += bitmapWidth;
            }
            while (spriteData < eof);

            bytesEncoded = (nint)spriteData - bytesEncoded;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void EncodeRunlen8to8(byte* bitmapScan, nint bitmapWidth, nint bitmapHeight, byte* spriteData, out nint bytesEncoded)
        {
            bytesEncoded = (nint)spriteData;

            Buffer.MemoryCopy(Unsafe.AsPointer(ref _palette16[0]), spriteData, 512L, 512L);

            spriteData += (nint)512;

            uint* ptr0 = (uint*)spriteData;

            spriteData += bitmapHeight << 2;

            uint* ptr1 = (uint*)spriteData;

            do
            {
                *ptr0 = (uint)((nint)spriteData - (nint)ptr1);

                ptr0++;

                byte* eol = bitmapScan + bitmapWidth;

                do
                {
                    byte* ptr = bitmapScan;
                    nint value = *bitmapScan;

                    do
                        bitmapScan++;
                    while (bitmapScan < eol && *bitmapScan == value);

                    bitmapHeight = (nint)bitmapScan - (nint)ptr;

                    if (value == Transparent8BitColor)
                        goto encodeTransparentRun;

                    if (bitmapHeight > 2)
                        goto encodeRun;

                    if (bitmapScan >= eol || *bitmapScan == Transparent8BitColor)
                        goto encode0;

                    while (true)
                    {
                        bitmapScan++;

                        if (bitmapScan >= eol || *bitmapScan == Transparent8BitColor)
                            goto encode1;

                        if (
                            *bitmapScan == *(bitmapScan - (nint)1) &&
                            *bitmapScan == *(bitmapScan - (nint)2)
                        )
                            goto encode2;
                    }

                encodeTransparentRun:

                    if (bitmapHeight > lenMask8Bit)
                    {
                        do
                        {
                            *spriteData = (byte)(lenMask8Bit | runMask8Bit | transparentMask8Bit);

                            spriteData++;

                            bitmapHeight -= lenMask8Bit;
                        }
                        while (bitmapHeight > lenMask8Bit);
                    }

                    *spriteData = (byte)(bitmapHeight | (runMask8Bit | transparentMask8Bit));

                    spriteData++;

                    continue;

                encodeRun:

                    if (bitmapHeight > lenMask8Bit)
                    {
                        do
                        {
                            *(spriteData + (nint)0) = (byte)(lenMask8Bit | runMask8Bit);
                            *(spriteData + (nint)1) = (byte)value;

                            spriteData += (nint)2;

                            bitmapHeight -= lenMask8Bit;
                        }
                        while (bitmapHeight > lenMask8Bit);
                    }

                    *(spriteData + (nint)0) = (byte)(bitmapHeight | runMask8Bit);
                    *(spriteData + (nint)1) = (byte)value;

                    spriteData += (nint)2;

                    continue;

                encode2:

                    bitmapScan -= (nint)2;

                encode1:

                    bitmapHeight = (nint)bitmapScan - (nint)ptr;

                encode0:

                    if (bitmapHeight > lenMask8Bit)
                    {
                        do
                        {
                            *spriteData = (byte)lenMask8Bit;

                            spriteData++;

                            Buffer.MemoryCopy(ptr, spriteData, lenMask8Bit, lenMask8Bit);

                            ptr += lenMask8Bit;
                            spriteData += lenMask8Bit;

                            bitmapHeight -= lenMask8Bit;
                        }
                        while (bitmapHeight > lenMask8Bit);
                    }

                    *spriteData = (byte)bitmapHeight;

                    spriteData++;

                    Buffer.MemoryCopy(ptr, spriteData, bitmapHeight, bitmapHeight);

                    spriteData += bitmapHeight;
                }
                while (bitmapScan < eol);

                bitmapScan += -(nint)bitmapScan & 3;
            }
            while (ptr0 < ptr1);

            bytesEncoded = (nint)spriteData - bytesEncoded;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void EncodeRaw16to16(byte* bitmapScan, nint bitmapWidth, nint bitmapHeight, byte* spriteData, out nint bytesEncoded)
        {
            bytesEncoded = (nint)spriteData;

            nint bitmapStride = (bitmapWidth * 16 + 31 & ~31) >> 3;

            bitmapWidth <<= 1;

            byte* eof = spriteData + bitmapHeight * bitmapWidth;

            do
            {
                Buffer.MemoryCopy(bitmapScan, spriteData, bitmapWidth, bitmapWidth);

                bitmapScan += bitmapStride;
                spriteData += bitmapWidth;
            }
            while (spriteData < eof);

            bytesEncoded = (nint)spriteData - bytesEncoded;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void EncodeRunlen16to16(byte* bitmapScan, nint bitmapWidth, nint bitmapHeight, byte* spriteData, out nint bytesEncoded)
        {
            bytesEncoded = (nint)spriteData;

            bitmapWidth <<= 1;

            uint* ptr0 = (uint*)spriteData;

            spriteData += bitmapHeight << 2;

            uint* ptr1 = (uint*)spriteData;

            do
            {
                *ptr0 = (uint)((nint)spriteData - (nint)ptr1 >> 1);

                ptr0++;

                byte* eol = bitmapScan + bitmapWidth;

                do
                {
                    byte* ptr = bitmapScan;
                    nint value = *(ushort*)bitmapScan;

                    do
                        bitmapScan += (nint)2;
                    while (bitmapScan < eol && *(ushort*)bitmapScan == value);

                    bitmapHeight = (nint)bitmapScan - (nint)ptr;

                    if (value == Transparent16BitColor)
                        goto encodeTransparentRun;

                    if (bitmapHeight > 4)
                        goto encodeRun;

                    if (bitmapScan >= eol || *(ushort*)bitmapScan == Transparent16BitColor)
                        goto encode0;

                    while (true)
                    {
                        bitmapScan += (nint)2;

                        if (bitmapScan >= eol || *(ushort*)bitmapScan == Transparent16BitColor)
                            goto encode1;

                        if (
                            *(ushort*)bitmapScan == *(ushort*)(bitmapScan - (nint)2) &&
                            *(ushort*)bitmapScan == *(ushort*)(bitmapScan - (nint)4)
                        )
                            goto encode2;
                    }

                encodeTransparentRun:

                    if (bitmapHeight > (lenMask16Bit << 1))
                    {
                        do
                        {
                            *(ushort*)spriteData = (ushort)(lenMask16Bit | runMask16Bit | transparentMask16Bit);

                            spriteData += (nint)2;

                            bitmapHeight -= lenMask16Bit << 1;
                        }
                        while (bitmapHeight > (lenMask16Bit << 1));
                    }

                    *(ushort*)spriteData = (ushort)((bitmapHeight >> 1) | (runMask16Bit | transparentMask16Bit));

                    spriteData += (nint)2;

                    continue;

                encodeRun:

                    if (bitmapHeight > (lenMask16Bit << 1))
                    {
                        do
                        {
                            *(ushort*)(spriteData + (nint)0) = (ushort)(lenMask16Bit | runMask16Bit);
                            *(ushort*)(spriteData + (nint)2) = (ushort)value;

                            spriteData += (nint)4;

                            bitmapHeight -= lenMask16Bit << 1;
                        }
                        while (bitmapHeight > (lenMask16Bit << 1));
                    }

                    *(ushort*)(spriteData + (nint)0) = (ushort)((bitmapHeight >> 1) | runMask16Bit);
                    *(ushort*)(spriteData + (nint)2) = (ushort)value;

                    spriteData += (nint)4;

                    continue;

                encode2:

                    bitmapScan -= (nint)4;

                encode1:

                    bitmapHeight = (nint)bitmapScan - (nint)ptr;

                encode0:

                    if (bitmapHeight > (lenMask16Bit << 1))
                    {
                        do
                        {
                            *(ushort*)spriteData = (ushort)lenMask16Bit;

                            spriteData += (nint)2;

                            Buffer.MemoryCopy(ptr, spriteData, lenMask16Bit << 1, lenMask16Bit << 1);

                            ptr += lenMask16Bit << 1;
                            spriteData += lenMask16Bit << 1;

                            bitmapHeight -= lenMask16Bit << 1;
                        }
                        while (bitmapHeight > (lenMask16Bit << 1));
                    }

                    *(ushort*)spriteData = (ushort)(bitmapHeight >> 1);

                    spriteData += (nint)2;

                    Buffer.MemoryCopy(ptr, spriteData, bitmapHeight, bitmapHeight);

                    spriteData += bitmapHeight;
                }
                while (bitmapScan < eol);

                bitmapScan += -(nint)bitmapScan & 3;
            }
            while (ptr0 < ptr1);

            bytesEncoded = (nint)spriteData - bytesEncoded;
        }
    }
}
