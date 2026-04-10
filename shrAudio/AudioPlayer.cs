// NuGet dependencies: MP3Sharp (1.0.5) and Silk.NET.OpenAL (2.23.0)

using Silk.NET.OpenAL;
using MP3Sharp;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace shrAudio
{
    public static class AudioPlayer
    {
        private enum States
        {
            Initial = 0,
            Playing = 1,

            Stop = 2
        }

        private static readonly ConcurrentQueue<string> _queue = new();
        private static long _state;

        public static void Enqueue(string filename)
        {
            _queue.Enqueue(filename);
        }

        public static void ClearAndStop()
        {
            _queue.Clear();

            Interlocked.CompareExchange(ref _state, (long)States.Stop, (long)States.Playing);
        }

        public static async Task ProcessAsync(int delayInterval, CancellationToken token)
        {
            AL al = null;
            ALContext alc = null;
            IntPtr device = IntPtr.Zero;
            IntPtr context = IntPtr.Zero;

            try
            {
                InitAudio(ref al, ref alc, ref device, ref context);

                int channels, sampleRate, bitsPerSample;
                byte[] pcmData;
                BufferFormat format;
                int state;

                while (!token.IsCancellationRequested)
                {
                    if (!_queue.TryDequeue(out string filename))
                    {
                        await Task.Delay(delayInterval, token);

                        continue;
                    }

                    uint buffer = 0;
                    uint source = 0;

                    try
                    {
                        // tries to load the audio data

                        string ext = Path.GetExtension(filename).ToLowerInvariant();

                        if (ext.Equals(".wav", StringComparison.Ordinal))
                            LoadWav(filename, out channels, out sampleRate, out bitsPerSample, out pcmData);
                        else if (ext.Equals(".mp3", StringComparison.Ordinal))
                            LoadMp3(filename, out channels, out sampleRate, out bitsPerSample, out pcmData);
                        else
                            throw new NotSupportedException("Only WAV and MP3 are supported.");

                        if (channels == 1 && bitsPerSample == 8)
                            format = BufferFormat.Mono8;
                        else if (channels == 1 && bitsPerSample == 16)
                            format = BufferFormat.Mono16;
                        else if (channels == 2 && bitsPerSample == 8)
                            format = BufferFormat.Stereo8;
                        else if (channels == 2 && bitsPerSample == 16)
                            format = BufferFormat.Stereo16;
                        else
                            throw new NotSupportedException($"Unsupported audio format: {channels}ch, {bitsPerSample}bit");

                        // creates and sets the buffer

                        buffer = al.GenBuffer();

                        SetBuffer(al, sampleRate, pcmData, format, buffer);

                        // creates and sets the source

                        source = al.GenSource();

                        al.SetSourceProperty(source, SourceInteger.Buffer, (int)buffer);

                        // plays the source

                        al.SourcePlay(source);

                        Interlocked.Exchange(ref _state, (long)States.Playing);

                        do
                        {
                            long opcode = Interlocked.Read(ref _state);

                            if ((opcode & (long)States.Stop) != 0L)
                                al.SourcePause(source); // avoids spikes?

                            await Task.Delay(delayInterval, token);

                            al.GetSourceProperty(source, GetSourceInteger.SourceState, out state);
                        }
                        while ((SourceState)state == SourceState.Playing);

                        al.SourceStop(source);

                        Interlocked.Exchange(ref _state, (long)States.Initial);
                    }
                    catch (Exception)
                    { }
                    finally
                    {
                        // tries to delete the source

                        if (source != 0)
                            al.DeleteSource(source);

                        // tries to delete the buffer

                        if (buffer != 0)
                            al.DeleteBuffer(buffer);
                    }
                }
            }
            catch (Exception)
            { }
            finally
            {
                EndAudio(al, alc, device, context);
            }
        }

        private static unsafe void InitAudio(ref AL al, ref ALContext alc, ref IntPtr device, ref IntPtr context)
        {
            al = AL.GetApi();
            alc = ALContext.GetApi();

            device = (IntPtr)alc.OpenDevice(null);
            context = (IntPtr)alc.CreateContext((Device*)device, null);

            alc.MakeContextCurrent(context);
        }

        private static unsafe void EndAudio(AL al, ALContext alc, IntPtr device, IntPtr context)
        {
            if (alc != null)
            {
                if (context != IntPtr.Zero)
                {
                    alc.MakeContextCurrent(IntPtr.Zero);
                    alc.DestroyContext((Context*)context);
                }

                if (device != IntPtr.Zero)
                    alc.CloseDevice((Device*)device);

                alc.Dispose();
            }

            al?.Dispose();
        }

        private static unsafe void SetBuffer(AL al, int sampleRate, byte[] pcmData, BufferFormat format, uint buffer)
        {
            fixed (byte* data = pcmData)
                al.BufferData(buffer, format, data, pcmData.Length, sampleRate);
        }

        private static void LoadWav(string filename, out int channels, out int sampleRate, out int bitsPerSample, out byte[] pcmData)
        {
            byte[] wav = File.ReadAllBytes(filename);

            channels = BitConverter.ToInt16(wav, 22);
            sampleRate = BitConverter.ToInt32(wav, 24);
            bitsPerSample = BitConverter.ToInt16(wav, 34);

            const int dataOffset = 44;

            pcmData = new byte[wav.Length - dataOffset];

            Buffer.BlockCopy(wav, dataOffset, pcmData, 0, pcmData.Length);
        }

        private static void LoadMp3(string filename, out int channels, out int sampleRate, out int bitsPerSample, out byte[] pcmData)
        {
            using MP3Stream mp3 = new(filename);

            channels = mp3.ChannelCount;
            sampleRate = mp3.Frequency;
            bitsPerSample = 16; // MP3Stream always outputs 16‑bit PCM

            MemoryStream ms = new();

            mp3.CopyTo(ms);

            pcmData = ms.ToArray();
        }
    }
}