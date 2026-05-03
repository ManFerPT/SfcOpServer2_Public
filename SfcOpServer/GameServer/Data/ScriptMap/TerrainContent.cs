#pragma warning disable IDE0130

using System;
using System.IO;

namespace SfcOpServer
{
    public struct TerrainContent
    {
        public double Space;
        public double Asteroids;
        public double DustClouds;
        public double IonStorms;

        public bool Nebulas;
        public int BlackHoles;

        public TerrainContent(double space, double asteroids, double dustClouds, double ionStorms, bool nebulas, int blackHoles)
        {
            double n = 1.0 / (space + asteroids + dustClouds + ionStorms);

            Space = space * n;
            Asteroids = asteroids * n;
            DustClouds = dustClouds * n;
            IonStorms = ionStorms * n;

            Nebulas = nebulas;
            BlackHoles = blackHoles;
        }

        public void ReadFrom(BinaryReader r)
        {
            Space = r.ReadDouble();
            Asteroids = r.ReadDouble();
            DustClouds = r.ReadDouble();
            IonStorms = r.ReadDouble();

            Nebulas = r.ReadBoolean();
            BlackHoles = r.ReadInt32();
        }

        public readonly void WriteTo(BinaryWriter w)
        {
            w.Write(Space);
            w.Write(Asteroids);
            w.Write(DustClouds);
            w.Write(IonStorms);

            w.Write(Nebulas);
            w.Write(BlackHoles);
        }

        public void Normalize()
        {
            double sum = Space + Asteroids + DustClouds + IonStorms;

            Space = Math.Round(Space / sum, 2, MidpointRounding.AwayFromZero);
            Asteroids = Math.Round(Asteroids / sum, 2, MidpointRounding.AwayFromZero);
            DustClouds = Math.Round(DustClouds / sum, 2, MidpointRounding.AwayFromZero);
            IonStorms = Math.Round(IonStorms / sum, 2, MidpointRounding.AwayFromZero);
        }
    }
}
