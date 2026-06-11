// derived from https://github.com/imneme/pcg-cpp

#pragma warning disable IDE0079

#pragma warning disable CA1515, CA1810
#pragma warning disable IDE1006

using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace shrPcg
{
    public sealed class clsPcg
    {
        public static readonly clsPcg Shared;

#if NET9_0_OR_GREATER
        private static readonly Lock _lock;
#else
        private static readonly object _lock;
#endif

        private static readonly UInt128 _lcgMultiplier;
        private static UInt128 _lcgState;

        private ulong _pcgState;

        [SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveOptimization)]
        static clsPcg()
        {
            Span<byte> data = stackalloc byte[16];

            RandomNumberGenerator.Fill(data);

            _lock = new();

            _lcgMultiplier = new(0x0fc94e3bf4e9ab32, 0x866458cd56f5e605);
            _lcgState = Unsafe.ReadUnaligned<UInt128>(ref MemoryMarshal.GetReference(data)) | UInt128.One;

            Contract.Assert(UInt128.IsOddInteger(_lcgState));

            Shared = new();

#if DEBUG
            Shared.Seed(0uL);

            // NextUInt64()

            ReadOnlySpan<ulong> u64 = [
                4073575256423186656, 4976277049109602808, 1131369595235443094, 8483047370033139638, 15929478269862897795, 16801080801056684117, 13846753628636920712, 37468615611691582,
                7, 5, 20, 12, 87, 99, 98, 204, 99, 1317, 2936, 1221, 20313, 55673, 102935, 105808
            ];

            for (int i = 0; i < 8; i++)
                Contract.Assert(Shared.NextUInt64() == u64[i]);

            for (int i = 0; i < 16; i++)
                Contract.Assert(Shared.NextUInt64(8uL << i) == u64[i + 8]);

            // NextDouble()

            Contract.Assert((long)(ulong.MaxValue >> 11) * (1.0 / (1L << 53)) < 1.0);

            // NextSingle()

            Contract.Assert((int)(ulong.MaxValue >> 40) * (1f / (1 << 24)) < 1.0f);

            Shared.Seed();
#endif

        }

        // Constructors

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public clsPcg()
        {
            Seed();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public clsPcg(ulong seed)
        {
            Seed(seed);
        }

        // Seed

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Seed()
        {
            /*
                // Today's 64-bit Minimal Standard: A 128-bit Truncated LCG

                uint128_t state = 1; // any odd number

                uint64_t next()
                {
                    state *= 0x0fc94e3bf4e9ab32866458cd56f5e605;

                    return state >> 64;
                }
            */

            UInt128 state;

            lock (_lock)
                state = _lcgState *= _lcgMultiplier;

            Seed((ulong)(state >> 64));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Seed(ulong state)
        {
            /*
                #define PCG_STATE_ONESEQ_64_INITIALIZER 0x4d595df4d0f33173ull
            */

            _pcgState = state != 0uL ? state : 0x4d595df4d0f33173uL;
        }

        // UInt64

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong NextUInt64()
        {
            /*
                inline uint64_t pcg_oneseq_64_rxs_m_xs_64_random_r(struct pcg_state_64* rng)
                {
                    uint64_t oldstate = rng->state;

                    pcg_oneseq_64_step_r(rng);

                    return pcg_output_rxs_m_xs_64_64(oldstate);
                }

                inline void pcg_oneseq_64_step_r(struct pcg_state_64* rng)
                {
                    rng->state = rng->state * 6364136223846793005ull + 1442695040888963407ull;
                }

                inline uint64_t pcg_output_rxs_m_xs_64_64(uint64_t state)
                {
                    uint64_t word = ((state >> ((state >> 59u) + 5u)) ^ state) * 12605985483714917081ull;

                    return (word >> 43u) ^ word;
                }
            */

            ulong state = _pcgState;

            _pcgState = _pcgState * 6364136223846793005uL + 1442695040888963407uL;

            ulong word = ((state >> (int)((state >> 59) + 5uL)) ^ state) * 12605985483714917081uL;

            return (word >> 43) ^ word;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong NextUInt64(ulong bound)
        {
            /*
                inline uint64_t pcg_oneseq_64_rxs_m_xs_64_boundedrand_r(struct pcg_state_64* rng, uint64_t bound)
                {
                    uint64_t threshold = -bound % bound;

                    for (;;) {
                        uint64_t r = pcg_oneseq_64_rxs_m_xs_64_random_r(rng);

                        if (r >= threshold)
                            return r % bound;
                    }
                } 
            */

            ulong threshold = (ulong)-(long)bound % bound;

            while (true)
            {
                ulong r = NextUInt64();

                if (r >= threshold)
                    return r % bound;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong NextUInt64(ulong minValue, ulong maxValue)
        {
            Contract.Assert(minValue <= maxValue);

            return NextUInt64(maxValue - minValue + 1uL) + minValue;
        }

        // Int64

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long NextInt64()
        {
            return (long)NextUInt64();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long NextInt64(long bound)
        {
            Contract.Assert(bound >= 0L);

            return (long)NextUInt64((ulong)bound);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long NextInt64(long minValue, long maxValue)
        {
            Contract.Assert(minValue <= maxValue);

            return (long)NextUInt64((ulong)(maxValue - minValue + 1L)) + minValue;
        }

        // Double

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double NextDouble()
        {
            return (long)(NextUInt64() >> 11) * (1.0 / (1L << 53));
        }

        // UInt32

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt32()
        {
            return (uint)(NextUInt64() >> 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt32(uint bound)
        {
            return (uint)NextUInt64(bound);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt32(uint minValue, uint maxValue)
        {
            Contract.Assert(minValue <= maxValue);

            return (uint)NextUInt64(maxValue - minValue + 1u) + minValue;
        }

        // Int32

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt32()
        {
            return (int)(NextUInt64() >> 32);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt32(int bound)
        {
            Contract.Assert(bound >= 0);

            return (int)NextUInt64((uint)bound);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int NextInt32(int minValue, int maxValue)
        {
            Contract.Assert(minValue <= maxValue);

            return (int)NextUInt64((uint)(maxValue - minValue + 1)) + minValue;
        }

        // Float

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float NextSingle()
        {
            return (int)(NextUInt64() >> 40) * (1f / (1 << 24));
        }
    }
}
