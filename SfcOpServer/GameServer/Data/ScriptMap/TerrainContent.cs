#pragma warning disable IDE0130, IDE0290

using System;
using System.Diagnostics.Contracts;
using System.IO;

namespace SfcOpServer
{
    public struct TerrainContent
    {
        public const int MaxBlackHoles = 3;

        public double Space;
        public double Asteroids;
        public double DustClouds;
        public double IonStorms;

        public int BlackHoles;
        public bool Nebulas;

        private bool _isInitialized;

        public readonly bool IsInitialized => _isInitialized;

        public TerrainContent(double space, double asteroids, double dustClouds, double ionStorms, int blackHoles, bool nebulas)
        {
            Space = space;
            Asteroids = asteroids;
            DustClouds = dustClouds;
            IonStorms = ionStorms;

            BlackHoles = blackHoles;
            Nebulas = nebulas;

            _isInitialized = true;
        }

        public void ReadFrom(BinaryReader r)
        {
            Space = r.ReadDouble();
            Asteroids = r.ReadDouble();
            DustClouds = r.ReadDouble();
            IonStorms = r.ReadDouble();

            BlackHoles = r.ReadInt32();
            Nebulas = r.ReadBoolean();

            _isInitialized = r.ReadBoolean();
        }

        public readonly void WriteTo(BinaryWriter w)
        {
            w.Write(Space);
            w.Write(Asteroids);
            w.Write(DustClouds);
            w.Write(IonStorms);

            w.Write(BlackHoles);
            w.Write(Nebulas);

            w.Write(_isInitialized);
        }

        public void Normalize()
        {
            double space = Math.Max(0.0, Space);
            double asteroids = Math.Max(0.0, Asteroids);
            double dustClouds = Math.Max(0.0, DustClouds);
            double ionStorms = Math.Max(0.0, IonStorms);

            double n = 1.0 / (space + asteroids + dustClouds + ionStorms);

            asteroids = Math.Round(asteroids * n, 5, MidpointRounding.AwayFromZero);
            dustClouds = Math.Round(dustClouds * n, 5, MidpointRounding.AwayFromZero);
            ionStorms = Math.Round(ionStorms * n, 5, MidpointRounding.AwayFromZero);

            space = 1.0 - asteroids - dustClouds - ionStorms;

            Contract.Assert(space + asteroids + dustClouds + ionStorms == 1.0);

            Space = space;
            Asteroids = asteroids;
            DustClouds = dustClouds;
            IonStorms = ionStorms;

            BlackHoles = Math.Max(0, Math.Min(BlackHoles, MaxBlackHoles));
        }
    }
}
