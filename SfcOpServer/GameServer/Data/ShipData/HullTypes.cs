#pragma warning disable IDE0130

namespace SfcOpServer
{
    public enum HullTypes
    {
        kFF,
        kDD,
        kCL,
        kCA,
        kDN,
        kF,

        kSB, // star base
        kBS, // base station
        kBT, // battle station
        kHullAsteroidBase,    // mining station
        kHullListeningPost,   // BASE
        kHullDefensePlatform, // BASE: weapons platform
        kHullStarDock,        // fleet repair dock

        kHullAstroMiner,
        kHullSunGlider,
        kHullDoomsdayMachine,
        kHullLivingCage,
        kHullM_Eater,
        kHullSpaceShell,
        kHullIntruder,

        kBox,
        kMineHull,
        kFighter, // pseudo fighter
        kShuttle,

        // total ship hull types

        kNumberOfShipHullTypes,

        // hull schematic names

        kHullPlanet = kNumberOfShipHullTypes,
        kHullMoon,
        kHullStar,
        kHullAsteroid,

        kHullPlasmaTorpedo,
        kHullDrones,

        kHullBodies,
        kHullFissure,
        kHullBlackHole,
        kHullWormHole,

        kHullFedShuttle,
        kHullKlingShuttle,
        kHullRomShuttle,
        kHullLyranShuttle,
        kHullHydranShuttle,
        kHullGornShuttle,
        kHullISCShuttle,
        kHullMirakShuttle,
        kHullOrionShuttle,

        kUnknownHull,

        // total hulls

        kNumTotalHulls,
    };
}
