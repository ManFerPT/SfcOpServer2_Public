#pragma warning disable IDE0028

#if DEBUG
//#define DEBUG_SETTINGS
//#define RESET_VALIDADATED_CLIENT_FILES
#endif

using shrGF;
using shrPcg;
using shrServices;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace SfcOpServer
{
    public unsafe partial class GameServer
    {
        private const string defaultServerName = "New_Server";
        private const string defaultServerDescription = "This is the new server campaign.";

        private const int defaultDifficultyLevel = 2;  // 0 - 5
        private const int defaultStartingEra = 0; // 0 - 3

        private const int defaultInitialPopulation = 128;
        private const int defaultPopulationDistribution = 3;

        private const float defaultNeutralPopulationRatio = 2.5f;

        // messages

        private const string defaultBusyWarning = "The server is busy. Please try again in a few seconds.";
        private const string defaultClosingWarning = "The server is closing for maintenance.";
        private const string defaultMaintenanceWarning = "The server is currently closed for maintenance. Please try again later.";

        // characters

        public const int MaxFleetSize = 12; // needs to match the script

        private const int maxHumanFleetSize = 3;

        // cpu movement

        private const int defaultCpuMovementDelay = 3; // s

        private const int defaultCpuMovementMinRest = 120; // s
        private const int defaultCpuMovementMaxRest = 600; // s

        private const double cpuPressureMultiplier = 0.01;

        // human movement

        private const int defaultHumanMovementDelay = 1; // s 

        private const double humanPressureMultiplier = 0.001;

        // clock

        private const int smallTicksPerSecond = 25;

        // stardate

        private const int defaultBaseYear = 2263;

        private const int defaultEarlyYears = 0;
        private const int defaultMiddleYears = 10;
        private const int defaultLateYears = 20;
        private const int defaultAdvancedYears = 50;

        private const int defaultMediumMissileSpeedDate = 4; // needs to match the client's exe
        private const int defaultFastMissileSpeedDate = 17; // needs to match the client's exe

        // ships

        public const int ClassTypeIconMask =
            1 << (int)ClassTypes.kClassFreighter |
            1 << (int)ClassTypes.kClassFrigate |
            1 << (int)ClassTypes.kClassDestroyer |
            1 << (int)ClassTypes.kClassWarDestroyer |
            1 << (int)ClassTypes.kClassLightCruiser |
            1 << (int)ClassTypes.kClassHeavyCruiser |
            1 << (int)ClassTypes.kClassNewHeavyCruiser |
            1 << (int)ClassTypes.kClassHeavyBattlecruiser |
            1 << (int)ClassTypes.kClassCarrier |
            1 << (int)ClassTypes.kClassDreadnought |
            1 << (int)ClassTypes.kClassBattleship |
            1 << (int)ClassTypes.kClassMonster;

        // chat

        private const string defaultIrcChannel = "General";
        private const int defaultIrcPort = 6667;

        // IO

        private const string defaultMap = "H&S";
        private const string mapExtension = ".mvm";

        private const string savegameDirectory = "Savegames/";
        private const string savegameExtension = ".bin";

        private const int load0 = 2030;
        private const int load1 = 2020;
        private const int load2 = 2003;
        private const int load3 = 2000;

        private const int save0 = 1060;
        private const int save1 = 1040;
        private const int save2 = 1010;
        private const int save3 = 1000;

        // private properties

        private int CurrentEra
        {
            get
            {
                int year = _turn / _turnsPerYear;

                if (year >= _advancedYears)
                    return 3;

                if (year >= _lateYears)
                    return 2;

                if (year >= _middleYears)
                    return 1;

                return 0;
            }
        }

        private int CurrentYear
        {
            get
            {
                return _turn / _turnsPerYear;
            }
        }

        // private variables

        private clsPcg _rand;

        // clock

        private long _smallTicks;
        private long _seconds;

        // stack

        private byte[] _stack;
        private GCHandle _handle;
        private byte* _end;
        private byte* _head;
        private byte* _tail;

        // server status

        private string _administrator;
        private string _nickSuffix;

        private int _difficultyLevel;
        private int _startingEra;

        // server files

        private Dictionary<string, uint> _serverFiles;

        // data counter

        private int _dataCounter;

        // characters

        private long _lastLogin;
        private ConcurrentQueue<int> _logouts;

        private Dictionary<string, int> _ipAddresses;
        private SortedDictionary<string, int> _characterNames;
        private Dictionary<int, Character> _characters;

        private Ranks[] _startingRank;
        private int[] _startingPrestige;

        private Dictionary<int, long> _cpuMovements; // character id, tick limit
        private Dictionary<int, long> _humanMovements; // character id, tick limit

        private int _cpuMovementDelay;
        private int _cpuMovementMinRest;
        private int _cpuMovementMaxRest;

        private int _humanMovementDelay;

        // hex map

        private string _mapName;
        private int _mapWidth;
        private int _mapHeight;
        private MapHex[] _map;

        private int[][] _locationIncrements; // 0 current hex, 1-7 surrounding hexes

        private Location[][] _homeLocations;
        private PopulationCensus _census;

        // script map

        private TerrainContent[] _terrainContents;

        private List<MapTemplate> _genericMapTemplates;
        private Dictionary<int, MapTemplate> _indexedMapTemplates;

        private List<string> _spaceBackgrounds;

        // economy

        private double _expensesMultiplier;
        private double _maintenanceMultiplier;
        private double _productionMultiplier;

        private double[] _curBudget;
        private double[] _curExpenses;
        private double[] _curMaintenance;
        private double[] _curProduction;

        private int[] _curPopulation;
        private int[] _curSize;

        private List<double>[] _logBudget;
        private List<double>[] _logExpenses;
        private List<double>[] _logMaintenance;
        private List<double>[] _logProduction;

        private List<int>[] _logPopulation;
        private List<int>[] _logSize;

        // stardate

        private int _turnsPerYear;
        private int _millisecondsPerTurn;

        private int _baseYear;

        private int _earlyYears;
        private int _middleYears;
        private int _lateYears;
        private int _advancedYears;

        private int _turn;

        private int _mediumMissileSpeedDate; // in which turn they are available
        private int _fastMissileSpeedDate; // in which turn they are available

        // specs

        private Dictionary<string, ShipData> _shiplist;
        private Dictionary<string, FighterData> _ftrlist;

        private Dictionary<string, int>[] _supplyFtrList;

        private double _sparePartsMultiplier;

        private int _initialPopulation;
        private int _populationDistribution;
        private double _neutralPopulationRatio;

        private RaceMasks _theGood;
        private RaceMasks _theBad;
        private RaceMasks _theUgly;

        private RaceMasks[] _alliances;

        // ... MetaVerseMap.Planets
        private List<ShipData>[][] _homeWorlds;
        private List<ShipData>[][] _coreWorlds;
        private List<ShipData>[][] _colonies;
        private List<ShipData>[][] _orbitalStations; // listed as 'ClassTypes.kClassPlanets' and 'MetaVerseMap.AsteroidBase1-3', we assume it is a base orbiting an uninhabited world (ex: gas giant)

        // ... MetaVerseMap.Bases
        private List<ShipData>[] _starbases;
        private List<ShipData>[] _battleStations;
        private List<ShipData>[] _baseStations;
        private List<ShipData>[] _weaponPlatforms;
        private List<ShipData>[] _listeningPosts;

        // ships

        private Dictionary<int, Ship> _ships;

        private ClassTypes[] _minStartingClass;
        private ClassTypes[] _maxStartingClass;

        private OfficerRanks _cpuOfficerRank;
        private double _cpuPowerBoost;

        private double[] _classAverageBpv;
        private double[] _classCostRatio;

        private double _costRepair;
        private double _costTradeIn;

        private double _costMissiles;
        private double _costShuttles;
        private double _costMarines;
        private double _costMines;
        private double _costSpareParts;

        private double _cpuAutomaticRepairMultiplier;
        private double _cpuAutomaticResupplyMultiplier;

        // shipyard

        private Dictionary<int, BidItem>[] _bidItems;
        private Dictionary<string, int> _bidReplacements; // shipClassName, count

        private int _sellingAction;
        private int[] _turnsToClose;

        // chat

        private string[] _channels;
        private string _serverNick;

        // drafts

        private List<int> _availableMissions;

        private Dictionary<int, Draft> _drafts; // hex index, countdown (s)

        // maintenance

        private string _lastSavegame;
        private int _savegameState;

        // private functions

        private void InitializeData()
        {
            // initializes the local GF

            GFFile gf = new();

#if !DEBUG_SETTINGS
            string filename = $"{_root}SfcOpServer.gf";

            gf.Load(filename);
#endif
            // initializes the gamespy side

            _hostName = gf.GetValue("", "Name", defaultServerName);
            _gameType = gf.GetValue("", "Description", defaultServerDescription);

            _maxNumPlayers = 10000;
            _numPlayers = 0;

            _maxNumLoggedOnPlayers = 0;
            _numLoggedOnPlayers = 0;

            _raceList = (uint)RaceMasks.AllEmpires;

            if ((_raceList & ~(uint)RaceMasks.AllEmpires) != 0u)
                throw new NotSupportedException(); // only empires are supported. cartels fulfill a support role

            // initializes the server side

            _rand = new();

            // stack

            InitializeStack();

            // server status

            _difficultyLevel = gf.GetValue("", "DifficultyLevel", defaultDifficultyLevel);

            if (_difficultyLevel < 0 || _difficultyLevel > 5)
                throw new NotSupportedException();

            _startingEra = gf.GetValue("", "Era", defaultStartingEra);

            if (_startingEra < 0 || _startingEra > 3)
                throw new NotSupportedException();

            // server files

            _serverFiles = new(StringComparer.OrdinalIgnoreCase);

            // characters

            _logouts = new();

            _ipAddresses = [];
            _characterNames = [];
            _characters = [];

            _startingRank =
            [
                (Ranks)gf.GetValue("EarlyEra/Character", "Rank", (int)Ranks.LieutenantCommander),
                (Ranks)gf.GetValue("MiddleEra/Character", "Rank", (int)Ranks.Captain),
                (Ranks)gf.GetValue("LateEra/Character", "Rank", (int)Ranks.Commodore),
                (Ranks)gf.GetValue("AdvancedEra/Character", "Rank", (int)Ranks.RearAdmiral)
            ];

            if (
                _startingRank[0] < Ranks.LieutenantCommander ||
                _startingRank[3] > Ranks.FleetAdmiral ||

                _startingRank[0] > _startingRank[1] ||
                _startingRank[1] > _startingRank[2] ||
                _startingRank[2] > _startingRank[3]
            )
                throw new NotSupportedException();

            _startingPrestige =
            [
                gf.GetValue("EarlyEra/Character", "Prestige", 3000),
                gf.GetValue("MiddleEra/Character", "Prestige", 5000),
                gf.GetValue("LateEra/Character", "Prestige", 8000),
                gf.GetValue("AdvancedEra/Character", "Prestige", 13000)
            ];

            if (
                _startingPrestige[0] < 0 ||
                _startingPrestige[3] > 1000000 ||

                _startingPrestige[0] > _startingPrestige[1] ||
                _startingPrestige[1] > _startingPrestige[2] ||
                _startingPrestige[2] > _startingPrestige[3]
            )
                throw new NotSupportedException();

            _cpuMovements = [];
            _humanMovements = [];

            Contract.Assert(defaultCpuMovementDelay < defaultCpuMovementMinRest && defaultCpuMovementMinRest < defaultCpuMovementMaxRest);

            _cpuMovementDelay = gf.GetValue("Character/Movement", "CpuDelay", defaultCpuMovementDelay);
            _cpuMovementMinRest = gf.GetValue("Character/Movement", "CpuMinRest", defaultCpuMovementMinRest);
            _cpuMovementMaxRest = gf.GetValue("Character/Movement", "CpuMaxRest", defaultCpuMovementMaxRest);

            if (
                _cpuMovementDelay < 1 || _cpuMovementDelay > 3 ||
                _cpuMovementMinRest < defaultCpuMovementMinRest || _cpuMovementMinRest > defaultCpuMovementMaxRest ||
                _cpuMovementMaxRest < defaultCpuMovementMinRest || _cpuMovementMaxRest > defaultCpuMovementMaxRest ||
                _cpuMovementMinRest > _cpuMovementMaxRest
            )
                throw new NotSupportedException();

            _humanMovementDelay = gf.GetValue("Character/Movement", "HumanDelay", defaultHumanMovementDelay);

            if (_humanMovementDelay < 1 || _humanMovementDelay > 3)
                throw new NotSupportedException();

            // hex map

            _mapName = gf.GetValue("", "MapName", defaultMap + mapExtension);

            if (!_mapName.EndsWith(mapExtension, StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException();

            // ... home locations and census

            _homeLocations = new Location[(int)Races.kNumberOfRaces][];

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _homeLocations[i] = new Location[maxHomeLocations];

            _census = new PopulationCensus();

#if DEBUG
            for (int i = 0; i < _classTypeIcons.Length; i++)
            {
                Contract.Assert
                (
                    _classTypeIcons[i] == -1 && (1 << i & ClassTypeIconMask) == 0 ||
                    _classTypeIcons[i] >= 1 && (1 << i & ClassTypeIconMask) != 0
                );
            }
#endif

            // script map

            Contract.Assert(_mapTerrains.Length == 24);

            _terrainContents =
            [
                new TerrainContent(.98, .02, .00, .00, false, true, 0), // asteroids1
                new TerrainContent(.97, .03, .00, .00, false, true, 0), // asteroids2
                new TerrainContent(.95, .04, .01, .00, false, true, 0), // asteroids3
                new TerrainContent(.94, .05, .01, .00, false, false, 0), // asteroids4
                new TerrainContent(.92, .06, .02, .00, false, false, 0), // asteroids5
                new TerrainContent(.91, .07, .02, .01, false, false, 0), // asteroids6

                new TerrainContent(1.0, .00, .00, .00, false, true, 0), // space1
                new TerrainContent(.99, .00, .01, .00, false, false, 0), // space2
                new TerrainContent(),                                    // space3 (reserved for fog of war)
                new TerrainContent(),                                    // space4 (not used by the game)
                new TerrainContent(),                                    // space5 (not used by the game)
                new TerrainContent(),                                    // space6 (not used by the game)

                new TerrainContent(.92, .00, .06, .02, true, true, 0),  // nebula1
                new TerrainContent(.88, .02, .06, .04, true, true, 0),  // nebula2
                new TerrainContent(.81, .04, .11, .04, true, true, 0),  // nebula3
                new TerrainContent(.69, .07, .12, .06, true, false, 0),  // nebula4
                new TerrainContent(.57, .09, .16, .08, true, false, 0),  // nebula5
                new TerrainContent(.62, .11, .17, .10, true, false, 0),  // nebula6

                new TerrainContent(.90, .00, .05, .05, false, true, 1), // blackhole1
                new TerrainContent(.85, .00, .10, .05, false, true, 1), // blackhole2
                new TerrainContent(.80, .00, .10, .10, false, true, 2), // blackhole3
                new TerrainContent(.75, .00, .15, .10, false, false, 2), // blackhole4
                new TerrainContent(.70, .05, .15, .10, false, false, 3), // blackhole5
                new TerrainContent(.60, .10, .15, .15, false, false, 3)  // blackhole6
            ];

            Contract.Assert(_terrainContents.Length == 24);

            for (int i = 0; i < 24; i++)
            {
                ref TerrainContent content = ref _terrainContents[i];

                if (content.Space != 0.0)
                {
                    string path = "Map/Terrains/" + _mapTerrains[i];

                    content.Space = gf.GetValue(path, "Space", (float)content.Space);
                    content.Asteroids = gf.GetValue(path, "Asteroids", (float)content.Asteroids);
                    content.DustClouds = gf.GetValue(path, "DustClouds", (float)content.DustClouds);
                    content.IonStorms = gf.GetValue(path, "IonStorms", (float)content.IonStorms);

                    content.Nebulas = gf.GetValue(path, "Nebulas", content.Nebulas ? 1 : 0) != 0;
                    content.Sun = gf.GetValue(path, "Sun", content.Sun ? 1 : 0) != 0;

                    content.BlackHoles = gf.GetValue(path, "BlackHoles", content.BlackHoles);

                    // checks if the values are valid

                    if (
                        content.Space < 0.0 ||
                        content.Asteroids < 0.0 ||
                        content.DustClouds < 0.0 ||
                        content.IonStorms < 0.0 ||

                        content.BlackHoles < 0 ||
                        content.BlackHoles > 6
                    )
                        throw new NotSupportedException();

                    // normalizes the values

                    content.Normalize();
                }

#if VERBOSE
                Debug.WriteLine
                (
                    "new TerrainContent(" +
                    Math.Round(content.Space, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture)[1..] +
                    ", " +
                    Math.Round(content.Asteroids, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture)[1..] +
                    ", " +
                    Math.Round(content.DustClouds, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture)[1..] +
                    ", " +
                    Math.Round(content.IonStorms, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture)[1..] +
                    ", " +
                    content.Nebulas.ToString(CultureInfo.InvariantCulture).ToLowerInvariant() +
                    ", " +
                    content.BlackHoles.ToString(CultureInfo.InvariantCulture) +
                    "), // " +
                    _mapTerrains[i]
                );
#endif

            }

            // economy

            _expensesMultiplier = gf.GetValue("Economy", "ExpensesMultiplier", 1.0f);
            _maintenanceMultiplier = gf.GetValue("Economy", "MaintenanceMultiplier", 0.05f); // 5%
            _productionMultiplier = gf.GetValue("Economy", "ProductionMultiplier", 100.0f); // map dependent

            _curBudget = new double[(int)Races.kNumberOfRaces];
            _curExpenses = new double[(int)Races.kNumberOfRaces];
            _curMaintenance = new double[(int)Races.kNumberOfRaces];
            _curProduction = new double[(int)Races.kNumberOfRaces];

            _curPopulation = new int[(int)Races.kNumberOfRaces];
            _curSize = new int[(int)Races.kNumberOfRaces];

            _logBudget = new List<double>[(int)Races.kNumberOfRaces];
            _logExpenses = new List<double>[(int)Races.kNumberOfRaces];
            _logMaintenance = new List<double>[(int)Races.kNumberOfRaces];
            _logProduction = new List<double>[(int)Races.kNumberOfRaces];

            _logPopulation = new List<int>[(int)Races.kNumberOfRaces];
            _logSize = new List<int>[(int)Races.kNumberOfRaces];

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                _logBudget[i] = [];
                _logExpenses[i] = [];
                _logMaintenance[i] = [];
                _logProduction[i] = [];

                _logPopulation[i] = [];
                _logSize[i] = [];
            }

            // stardate

            _turnsPerYear = gf.GetValue("Clock", "TurnsPerYear", 52); // 52 weeks per year
            _millisecondsPerTurn = gf.GetValue("Clock", "MilliSecondsPerTurn", 1200000); // 20 minuts per turn

            if (_turnsPerYear < 1 || _millisecondsPerTurn < 10_000)
                throw new NotSupportedException();

            _baseYear = gf.GetValue("Clock/StartingDate", "BaseYear", defaultBaseYear);

            if (_baseYear < 2067) // the year humans met the vulcans :)
                throw new NotSupportedException();

            _earlyYears = gf.GetValue("Clock/StartingDate", "EarlyYears", defaultEarlyYears);
            _middleYears = gf.GetValue("Clock/StartingDate", "MiddleYears", defaultMiddleYears);
            _lateYears = gf.GetValue("Clock/StartingDate", "LateYears", defaultLateYears);
            _advancedYears = gf.GetValue("Clock/StartingDate", "AdvancedYears", defaultAdvancedYears);

            if (_earlyYears < 0 || _middleYears <= _earlyYears || _lateYears <= _middleYears || _advancedYears <= _lateYears)
                throw new NotSupportedException();

            switch (_startingEra)
            {
                case 0:
                    _turn = _earlyYears * _turnsPerYear; break;
                case 1:
                    _turn = _middleYears * _turnsPerYear; break;
                case 2:
                    _turn = _lateYears * _turnsPerYear; break;
                case 3:
                    _turn = _advancedYears * _turnsPerYear; break;
            }

            _mediumMissileSpeedDate = gf.GetValue("Clock/MissileSpeedDate", "Medium", defaultMediumMissileSpeedDate) * _turnsPerYear;
            _fastMissileSpeedDate = gf.GetValue("Clock/MissileSpeedDate", "Fast", defaultFastMissileSpeedDate) * _turnsPerYear;

            // specs

            _shiplist = new(StringComparer.OrdinalIgnoreCase);
            _ftrlist = new(StringComparer.OrdinalIgnoreCase);

            _supplyFtrList = new Dictionary<string, int>[(int)Races.kNumberOfRaces];

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _supplyFtrList[i] = [];

            _sparePartsMultiplier = 0.8; // used to be 5.0 in stock

            _initialPopulation = gf.GetValue("", "InitialPopulation", defaultInitialPopulation);
            _populationDistribution = gf.GetValue("", "PopulationDistribution", defaultPopulationDistribution);

            if (_populationDistribution < 1 || _populationDistribution > 6)
                throw new NotSupportedException();

            _neutralPopulationRatio = gf.GetValue("", "NeutralPopulationRatio", defaultNeutralPopulationRatio);

            if (_populationDistribution < 1.0 || _populationDistribution > 8.0)
                throw new NotSupportedException();

            // ... sets the alliances

            RaceMasks theGood = RaceMasks.kFederation | RaceMasks.kHydran | RaceMasks.kGorn | RaceMasks.kMirak;

            theGood |= (RaceMasks)((int)theGood << 8);

            RaceMasks theBad = (RaceMasks)((int)theGood ^ 0xffff);

            Contract.Assert((theGood & theBad) == RaceMasks.None);

            RaceMasks theUgly = RaceMasks.AllRaces ^ theGood ^ theBad;

            Contract.Assert((theUgly & (theGood | theBad)) == RaceMasks.None);

            _theGood = theGood;
            _theBad = theBad;
            _theUgly = theUgly;

            _alliances = new RaceMasks[(int)Races.kNumberOfRaces];

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                RaceMasks j = (RaceMasks)(1 << i);

                if ((j & theGood) != RaceMasks.None)
                    _alliances[i] = theGood;
                else if ((j & theBad) != RaceMasks.None)
                    _alliances[i] = theBad;
                else
                    _alliances[i] = theUgly;
            }

            _homeWorlds = new List<ShipData>[(int)Races.kNumberOfRaces][];
            _coreWorlds = new List<ShipData>[(int)Races.kNumberOfRaces][];
            _colonies = new List<ShipData>[(int)Races.kNumberOfRaces][];
            _orbitalStations = new List<ShipData>[(int)Races.kNumberOfRaces][];

            _starbases = new List<ShipData>[(int)Races.kNumberOfRaces];
            _battleStations = new List<ShipData>[(int)Races.kNumberOfRaces];
            _baseStations = new List<ShipData>[(int)Races.kNumberOfRaces];
            _weaponPlatforms = new List<ShipData>[(int)Races.kNumberOfRaces];
            _listeningPosts = new List<ShipData>[(int)Races.kNumberOfRaces];

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                _homeWorlds[i] = new List<ShipData>[3];
                _coreWorlds[i] = new List<ShipData>[3];
                _colonies[i] = new List<ShipData>[3];
                _orbitalStations[i] = new List<ShipData>[3];

                for (int j = 0; j < 3; j++)
                {
                    _homeWorlds[i][j] = [];
                    _coreWorlds[i][j] = [];
                    _colonies[i][j] = [];
                    _orbitalStations[i][j] = [];
                }

                _starbases[i] = [];
                _battleStations[i] = [];
                _baseStations[i] = [];
                _weaponPlatforms[i] = [];
                _listeningPosts[i] = [];
            }

            // ships

            _ships = [];

            _minStartingClass = new ClassTypes[4];
            _maxStartingClass = new ClassTypes[4];

            _minStartingClass[0] = (ClassTypes)(GetIndex(gf.GetValue("EarlyEra/Character", "MinClass", "FRIGATE"), _classTypes, StringComparison.OrdinalIgnoreCase, false) - 1);
            _maxStartingClass[0] = (ClassTypes)(GetIndex(gf.GetValue("EarlyEra/Character", "MaxClass", "DESTROYER"), _classTypes, StringComparison.OrdinalIgnoreCase, false) - 1);

            _minStartingClass[1] = (ClassTypes)(GetIndex(gf.GetValue("MiddleEra/Character", "MinClass", "DESTROYER"), _classTypes, StringComparison.OrdinalIgnoreCase, false) - 1);
            _maxStartingClass[1] = (ClassTypes)(GetIndex(gf.GetValue("MiddleEra/Character", "MaxClass", "LIGHT_CRUISER"), _classTypes, StringComparison.OrdinalIgnoreCase, false) - 1);

            _minStartingClass[2] = (ClassTypes)(GetIndex(gf.GetValue("LateEra/Character", "MinClass", "LIGHT_CRUISER"), _classTypes, StringComparison.OrdinalIgnoreCase, false) - 1);
            _maxStartingClass[2] = (ClassTypes)(GetIndex(gf.GetValue("LateEra/Character", "MaxClass", "HEAVY_CRUISER"), _classTypes, StringComparison.OrdinalIgnoreCase, false) - 1);

            _minStartingClass[3] = (ClassTypes)(GetIndex(gf.GetValue("AdvancedEra/Character", "MinClass", "HEAVY_CRUISER"), _classTypes, StringComparison.OrdinalIgnoreCase, false) - 1);
            _maxStartingClass[3] = (ClassTypes)(GetIndex(gf.GetValue("AdvancedEra/Character", "MaxClass", "DREADNOUGHT"), _classTypes, StringComparison.OrdinalIgnoreCase, false) - 1);

            if (
                _minStartingClass[0] < ClassTypes.kClassFreighter ||
                _maxStartingClass[3] > ClassTypes.kClassBattleship ||

                _minStartingClass[0] > _minStartingClass[1] ||
                _minStartingClass[1] > _minStartingClass[2] ||
                _minStartingClass[2] > _minStartingClass[3] ||

                _maxStartingClass[0] > _maxStartingClass[1] ||
                _maxStartingClass[1] > _maxStartingClass[2] ||
                _maxStartingClass[2] > _maxStartingClass[3] ||

                _minStartingClass[0] > _maxStartingClass[0] ||
                _minStartingClass[1] > _maxStartingClass[1] ||
                _minStartingClass[2] > _maxStartingClass[2] ||
                _minStartingClass[3] > _maxStartingClass[3]
            )
                throw new NotSupportedException();

            _cpuOfficerRank = OfficerRanks.kSenior + (_difficultyLevel >> 1);
            _cpuPowerBoost = gf.GetValue("AI", "PowerBoost", 0.05f) * (_difficultyLevel + 1); // 5%, 10%, 15%, 20%, 25%, 30% (percent of ship's total power added to APR)

            if (_cpuPowerBoost < 0.0 || _cpuPowerBoost > 1.0)
                throw new NotSupportedException();

            // ... initializes the average bpv, and the cost ratio, of each class

            _classAverageBpv = new double[(int)ClassTypes.kMaxClasses];

            gf.AddOrUpdate("Cost/Class", "SHUTTLE", "1.0", "readonly"); // adds the SHUTTLE entry just as a reference (its value will never change)

            _classCostRatio =
            [
                gf.GetValue("Cost/Class", "FIGHTER", 1.67f), // as a SHUTTLE is 1.0 by default, we can use its entry to store our missing fighter entry
                gf.GetValue("Cost/Class", "PSEUDO_FIGHTER", 5.0f),

                gf.GetValue("Cost/Class", "FREIGHTER", 15.0f),
                gf.GetValue("Cost/Class", "FRIGATE", 45.0f),
                gf.GetValue("Cost/Class", "DESTROYER", 67.5f),
                gf.GetValue("Cost/Class", "WAR_DESTROYER", 90.0f),
                gf.GetValue("Cost/Class", "LIGHT_CRUISER", 112.5f),
                gf.GetValue("Cost/Class", "HEAVY_CRUISER", 135.0f),
                gf.GetValue("Cost/Class", "NEW_HEAVY_CRUISER", 180.0f),
                gf.GetValue("Cost/Class", "HEAVY_BATTLECRUISER", 225.0f),
                gf.GetValue("Cost/Class", "CARRIER", 292.5f),
                gf.GetValue("Cost/Class", "DREADNOUGHT", 337.5f),
                gf.GetValue("Cost/Class", "BATTLESHIP", 382.5f),

                gf.GetValue("Cost/Class", "LISTENING_POST", 17.5f),
                gf.GetValue("Cost/Class", "BASE_STATION", 188.0f),
                gf.GetValue("Cost/Class", "BATTLE_STATION", 255.0f),
                gf.GetValue("Cost/Class", "STARBASE", 638.0f),

                gf.GetValue("Cost/Class", "MONSTER", 160.0f),
                gf.GetValue("Cost/Class", "PLANETS", 365.0f),
                gf.GetValue("Cost/Class", "SPECIAL", 325.0f)
            ];

            Contract.Assert(_classCostRatio.Length == (int)ClassTypes.kMaxClasses);

            for (int i = 0; i < (int)ClassTypes.kMaxClasses; i++)
            {
                if (_classCostRatio[i] < 1.0)
                    throw new NotSupportedException(); // no class should be cheaper than a shuttle
            }

            // ... repair and tradeIn costs

            _costRepair = gf.GetValue("Cost", "Repair", 0.15f);

            if (_costRepair <= 0.0 || _costRepair >= 1.0)
                throw new NotSupportedException();

            _costTradeIn = gf.GetValue("Cost", "TradeIn", 0.75f);

            if (_costTradeIn <= 0.0 || _costTradeIn >= 1.0)
                throw new NotSupportedException();

            // ... supply prices

            _costMissiles = gf.GetValue("Cost/Supply", "Missiles", 1.0f);
            _costShuttles = gf.GetValue("Cost/Supply", "Shuttles", 150.0f);
            _costMarines = gf.GetValue("Cost/Supply", "Marines", 20.0f);
            _costMines = gf.GetValue("Cost/Supply", "Mines", 10.0f);
            _costSpareParts = gf.GetValue("Cost/Supply", "SpareParts", 0.2f);

            if (
                _costMissiles <= 0.0 ||
                _costShuttles <= 0.0 ||
                _costMarines <= 0.0 ||
                _costMines <= 0.0 ||
                _costSpareParts <= 0.0
            )
                throw new NotSupportedException();

            _cpuAutomaticRepairMultiplier = gf.GetValue("Bonus", "CpuAutomaticRepair", 0.25f); // +25%

            if (_cpuAutomaticRepairMultiplier < 0.0 || _cpuAutomaticRepairMultiplier > 1.0)
                throw new NotSupportedException();

            _cpuAutomaticResupplyMultiplier = gf.GetValue("Bonus", "CpuAutomaticResupply", 0.25f); // +25%

            if (_cpuAutomaticResupplyMultiplier < 0.0 || _cpuAutomaticResupplyMultiplier > 1.0)
                throw new NotSupportedException();

            // shipyard

            _bidItems = new Dictionary<int, BidItem>[(int)Races.kNumberOfRaces];

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _bidItems[i] = [];

            _bidReplacements = [];

            _sellingAction = gf.GetValue("Shipyard", "SellingAction", 0);

            if (_sellingAction < 0 || _sellingAction > 2)
                throw new NotSupportedException();

            _turnsToClose =
            [
                gf.GetValue("Shipyard/TurnsForConstruction", "SizeClass1", 32),
                gf.GetValue("Shipyard/TurnsForConstruction", "SizeClass2", 16),
                gf.GetValue("Shipyard/TurnsForConstruction", "SizeClass3", 8),
                gf.GetValue("Shipyard/TurnsForConstruction", "SizeClass4", 4),
                gf.GetValue("Shipyard/TurnsForConstruction", "SizeClass5", 2), // pseudo-fighters
                gf.GetValue("Shipyard/TurnsForConstruction", "SizeClass6", 1)  // shuttles, fighters
            ];

            for (int i = 0; i < 6; i++)
            {
                if (_turnsToClose[i] < 1)
                    throw new NotSupportedException();
            }

            // chat

            _channels =
            [
                "@" + _hostName,
                "#SystemBroadcast",
                "#ServerBroadcast",
                "#General",
                "#Federation",
                "#Klingon",
                "#Romulan",
                "#Lyran",
                "#Hydran",
                "#Gorn",
                "#ISC",
                "#Mirak",
                "#Orion",
                "#Korgath",
                "#Prime",
                "#TigerHeart",
                "#BeastRaiders",
                "#Syndicate",
                "#Wyldefire",
                "#Camboro"
            ];

            _serverNick = "A" + Id;

            // draft

            _availableMissions = [];

            _drafts = [];

#if DEBUG
            Dictionary<string, object> d = [];

            for (int i = 1; i < _missionNames.Length; i++)
                d.Add(_missionNames[i], null);
#endif

            // maintenance

            Contract.Assert(savegameDirectory.EndsWith('/') && Directory.Exists(_root + savegameDirectory));

            _lastSavegame = null;
            _savegameState = 0;

            // functions

            LoadValidatedClientFiles();

            LoadMap();
            LoadMapTemplates();
            LoadSpaceBackgrounds();

            LoadShiplist();
            ClassifyPlanetsAndBases();

            ResetAvailableMissions();

            LoadCpuBattleSettings();

            // finalize the variables

            InitializeLocationIncrements();

            // finalize de local gf

#if !DEBUG_SETTINGS
            gf.Save(filename, -1, -1);
#endif

        }

        // server status

        private void UpdateRaceList()
        {
            //Contract.Assert(_raceList != 0u);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                Location location = _homeLocations[i][0];

                if (location == null)
                    _raceList &= ~(1u << i);
            }
        }

        // server files

        public void ReloadValidatedClientFiles()
        {
            _serverFiles.Clear();

            LoadValidatedClientFiles();
        }

        private void LoadValidatedClientFiles()
        {
            string path = _root + "Assets/ValidatedClientFiles/";

            if (Directory.Exists(path))
            {
                IEnumerable<string> e = Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly);

                foreach (string f in e)
                {
                    string filename = Path.GetFileName(f);

                    _serverFiles.Add(filename, 0);
                }
            }
        }

        private bool TryValidateClientFiles(Dictionary<string, uint> clientFiles, out string badOrMissing)
        {
            if (_serverFiles.Count > 0)
            {
                List<string> warnings = [];

                foreach (KeyValuePair<string, uint> p in _serverFiles)
                {
                    string filename = p.Key;
                    uint serverCRC = p.Value;

                    if (clientFiles.TryGetValue(filename, out uint clientCRC))
                    {
                        if (_serverFiles[filename] == 0)
                        {
                            _serverFiles[filename] = clientCRC;

                            Console.WriteLine("SUCCESS: A new file was added to the security check {" + filename + ", " + clientCRC.ToString("X8", CultureInfo.InvariantCulture) + "}");
                        }
                        else if (clientCRC != serverCRC)
                            warnings.Add(filename);
                    }
                    else
                        warnings.Add(filename);
                }

                int c = warnings.Count;

                if (c > 0)
                {
                    StringBuilder b = new(1024);

                    b.Append("Warning! The following files are either missing or incompatible:");
                    b.Append(warnings[0]);

                    for (int i = 1; i < c; i++)
                    {
                        b.Append(", ");
                        b.Append(warnings[i]);
                    }

                    b.Append('.');

                    badOrMissing = b.ToString();

                    return false;
                }
            }

            badOrMissing = null;

            return true;
        }

        // data counter

        private int GetNextDataId()
        {
            return Interlocked.Increment(ref _dataCounter);
        }

        // characters

        private bool TryInitializeCharacter(Client27000 client, string wonLogon, string name, Races race)
        {
            if (_ipAddresses.TryGetValue(client.IPAddress, out int characterId) && _characterNames.TryAdd(name, characterId))
            {
                Character character = client.Character;

                Contract.Assert(character.State == Character.States.IsHumanBusyConnecting);

                // updates the character

                character.WONLogon = wonLogon;
                character.Id = characterId;
                character.CharacterName = name;
                character.CharacterRace = race;
                character.CharacterPoliticalControl = race;
                character.CharacterRank = Ranks.Ensign;

                character.CharacterCurrentPrestige = _startingPrestige[CurrentEra];
                character.CharacterLifetimePrestige = _startingPrestige[CurrentEra];

                return true;
            }

            return false;
        }

        private void FinalizeCharacter(Client27000 client)
        {
            Character character = client.Character;

            Contract.Assert(character.State == Character.States.IsHumanBusyConnecting);

            // gets the starting location

            Location location = _homeLocations[(int)character.CharacterRace][0];

            Contract.Assert(location != null);

            // updates the character

            int era = CurrentEra;

            character.IPAddress = client.IPAddress;

            character.CharacterRank = _startingRank[era];

            character.CharacterLifetimePrestige = _startingPrestige[era] * 10;

            character.CharacterLocationX = location.X;
            character.CharacterLocationY = location.Y;

            character.HomeWorldLocationX = location.X;
            character.HomeWorldLocationY = location.Y;

            // creates the starting ship

            CreateShip(character.CharacterRace, _minStartingClass[era], _maxStartingClass[era], out Ship ship);

            UpdateCharacter(character, ship);

            // updates the map

            AddToHexPopulation(character);
        }

        private bool TryCreateOrUpdateCharacter(Client27000 client)
        {
            string ipAddress = client.IPAddress;

            Character character;

            if (_ipAddresses.TryGetValue(ipAddress, out int characterId))
            {
                character = _characters[characterId];

                if (character.State == Character.States.IsHumanBusyDisconnecting)
                    return false;

                Contract.Assert(character.State == Character.States.IsHuman && character.Client == null);

                // we need to check the character's fleet at this point
                // because he may have ALT+F4 during a mission
                // and the server destroyed all his ships

                RebuildShipList(character);
            }
            else
            {
                characterId = GetNextDataId();

                _ipAddresses.Add(ipAddress, characterId);

                character = new Character(true);

                _characters.Add(characterId, character);

                // updates the stats

                _numPlayers++;
            }

            // updates the character

            character.State = Character.States.IsHumanBusyConnecting;

            Contract.Assert(character.Client == null);

            character.Client = client;

            // updates the client

            Contract.Assert(client.Character == null);

            client.Character = character;

            // updates the stats

            _numLoggedOnPlayers++;

            if (_maxNumLoggedOnPlayers < _numLoggedOnPlayers)
                _maxNumLoggedOnPlayers = _numLoggedOnPlayers;

            return true;
        }

        private void RebuildShipList(Character character)
        {
            int c = character.ShipCount;

            if (c != 0)
            {
                List<Ship> list = [];

                for (int i = 0; i < c; i++)
                {
                    Ship ship = character.GetShipAt(i);

                    if (_ships.ContainsKey(ship.Id))
                        list.Add(ship);
                }

                c = list.Count;

                if (c != 0)
                {
                    character.ClearShipList();

                    for (int i = 0; i < c; i++)
                        character.AddShip(list[i]);

                    return;
                }
            }

            CreateTemporaryShip(character);
        }

        private void CreateTemporaryShip(Character character)
        {
            Contract.Assert(character.ShipCount == 0 && (character.State & Character.States.IsHuman) == Character.States.IsHuman);

            ClassTypes defaultClassType = _minStartingClass[CurrentEra];

            CreateShip(character.CharacterRace, defaultClassType, defaultClassType, out Ship ship);
            UpdateCharacter(character, ship);

            // the new ship is free for the player, but not for the empire
            // so we account it in the overall expenses

            _curExpenses[(int)character.CharacterRace] += GetShipCost(ship.ClassType, ship.BPV);
        }

        private void UpdateCharacter(Character character, int prestigeIncrement)
        {
            if (prestigeIncrement != 0)
            {
                int prestige = character.CharacterCurrentPrestige + prestigeIncrement;

                if (prestige < 0)
                    character.CharacterCurrentPrestige = 0;
                else
                {
                    character.CharacterCurrentPrestige = prestige;

                    if (character.CharacterLifetimePrestige < prestige)
                        character.CharacterLifetimePrestige = prestige;
                }

                // every time a player spends his credit

                if (prestigeIncrement < 0)
                    _curExpenses[(int)character.CharacterRace] -= prestigeIncrement;
            }
        }

        private void UpdateCharacter(Character character, Ship newShip)
        {
            Contract.Assert(newShip.OwnerID == 0);

            newShip.OwnerID = character.Id;

            if ((character.State & Character.States.IsCpu) == Character.States.IsCpu)
                AutomaticResupply(newShip, 1.0);

            character.AddShip(newShip);
        }

        private void BeginLoginClient(Client27000 client)
        {
            _lastLogin = _seconds;

            // updates the client

            client.LastTurn = _turn;
        }

        private void LoginClient(Client27000 client)
        {
            // tries to link the client to the launcher

            int address = GetEndPointAddress(client.RemoteEndPoint);

            //#if DEBUG
            //            if (address == 1140959424)
            //                address = 1073850560;
            //#endif

            if (_launchers.TryGetValue(address, out Client27001 launcher))
            {
                Contract.Assert(client.LauncherId == 0 && launcher.ClientId == 0 && launcher.Address == address);

                client.LauncherId = launcher.Address;
                launcher.ClientId = client.Id;

                Console.WriteLine("LAUNCHER: client " + client.Id + " is now linked to launcher " + launcher.Address);
            }

            // updates the character

            Character character = client.Character;

            Contract.Assert(character.State == Character.States.IsHumanBusyConnecting);

            character.State = Character.States.IsHumanOnline;

            character.LastLogin = DateTime.UtcNow;

            // logs the event

            Console.WriteLine("CLIENT: " + character.CharacterName + " joined the game server");
        }

        private void LogoutClient(int clientId)
        {
            if (!_clients.TryRemove(clientId, out Client27000 client))
                throw new NotSupportedException();

            client.State = Client27000.States.IsOffline;

            // sends a signal to stop the remaining musics in the playlist

            CmdMusic(client, string.Empty);

            // tries to unlink the client from its launcher

            if (_launchers.TryGetValue(client.LauncherId, out Client27001 launcher))
            {
                Contract.Assert(launcher.ClientId != 0 && launcher.Address != 0);

                client.LauncherId = 0;
                launcher.ClientId = 0;

                Console.WriteLine("LAUNCHER: client " + client.Id + " closed its link to launcher " + launcher.Address);
            }

            // checks if we have a character linked

            Character character = client.Character;

            if (character == null)
                return;

            // unlinks the client from its character

            client.Character = null;
            character.Client = null;

            // updates the character

            character.State = Character.States.IsHumanBusyDisconnecting;

            if (character.Id == 0)
            {
                if (!_ipAddresses.TryGetValue(client.IPAddress, out int characterId))
                    throw new NotSupportedException();

                _ipAddresses.Remove(client.IPAddress);
                _characters.Remove(characterId);

                // updates the stats

                _numPlayers--;

                Contract.Assert(_numPlayers >= 0);
            }
            else if (character.CharacterName.Length == 0)
            {
                _ipAddresses.Remove(character.IPAddress);
                _characters.Remove(character.Id);

                // updates the stats

                _numPlayers--;

                Contract.Assert(_numPlayers >= 0);
            }
            else
            {
                character.MoveDestinationX = -1;
                character.MoveDestinationY = -1;

                _humanMovements.Remove(character.Id);

                MapHex hex = _map[character.CharacterLocationX + character.CharacterLocationY * _mapWidth];

                RemoveFromHexPopulation(hex, character);

                // deletes all the ships of the character if he left the server during a draft process

                if (character.Mission != 0 && character.Mission == (hex.Mission & MissionFilter) && _drafts.TryGetValue(hex.X + hex.Y * _mapWidth, out _))
                {
                    int c = character.ShipCount;

                    for (int i = 0; i < c; i++)
                    {
                        Ship ship = character.GetShipAt(i);

                        _ships.Remove(ship.Id);
                    }

                    character.ClearShipList();
                }

                TryLeaveMission(character, hex);
            }

            // finalizes the character

            character.State = Character.States.IsHuman;

            character.LastLogout = DateTime.UtcNow;

            // updates the stats

            _numLoggedOnPlayers--;

            Contract.Assert(_numLoggedOnPlayers >= 0);

            // logs the event

            Console.WriteLine("CLIENT: " + character.CharacterName + " left the game server");
        }

        // ... IA

        private void CreateCharacter(Races race, int x, int y, ShipData newShipdata, out Character character)
        {
            CreateShip(newShipdata, out Ship ship);
            CreateCharacter(race, x, y, ship, out character);
        }

        private void CreateCharacter(Races race, int x, int y, Ship ship, out Character character)
        {
            int id = GetNextDataId();
            string name = _raceAbbreviations[(int)race] + (id & 0xffff).ToString("X4", CultureInfo.InvariantCulture) + "c";
            int era = CurrentEra;

            character = new Character(true)
            {
                Id = id,
                CharacterName = name,
                CharacterRace = race,
                CharacterPoliticalControl = race,
                CharacterRank = _startingRank[era],

                CharacterCurrentPrestige = _startingPrestige[era],
                CharacterLifetimePrestige = _startingPrestige[era] * 10,

                CharacterLocationX = x,
                CharacterLocationY = y,

                State = Character.States.IsCpuOnline
            };

            _characters.Add(id, character);

            if (ship.Race != race)
                ModifyShip(ship, race);

            UpdateCharacter(character, ship);

            AddToHexPopulation(character);
            AddOrUpdateCpuMovement(Environment.TickCount64, character);
        }

        private void UpdateCharacter(Character character, ShipData newShipData)
        {
            CreateShip(newShipData, out Ship ship);
            UpdateCharacter(character, ship);
        }

        // map

        private void LoadMap()
        {
            MetaVerseMap map = new();
            string path = _root + "Assets/Maps/" + _mapName;

            if (!map.Load(path))
                throw new FileNotFoundException(path);

            // initializes the hex map

            _mapWidth = map.Width;
            _mapHeight = map.Height;
            _map = new MapHex[map.Cells.Count];

            _earlyMap = _mapName;
            _middleMap = _mapName;
            _lateMap = _mapName;

            // loads the hex map

            int i = 0;

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    MetaVerseMap.tCell cell = map.Cells[i];

                    // creates a hex

                    MapHex hex = new(true)
                    {
                        Id = GetNextDataId(),

                        X = x,
                        Y = y
                    };

                    if (cell.Region == 0)
                        hex.EmpireControl = Races.kNeutralRace;
                    else
                        hex.EmpireControl = (Races)(cell.Region - 1);

                    if (cell.CartelRegion == 0)
                        hex.CartelControl = Races.kNeutralRace;
                    else
                        hex.CartelControl = (Races)(cell.CartelRegion + 7);

                    hex.Terrain = cell.Terrain;
                    hex.Planet = cell.Planet;
                    hex.Base = cell.Base;

                    hex.TerrainType = (TerrainTypes)(1 << cell.Terrain);

                    if ((hex.TerrainType & (TerrainTypes.kTerrainAsteroids4 | TerrainTypes.kTerrainAsteroids5 | TerrainTypes.kTerrainAsteroids6)) != 0)
                    {
#if DEBUG
                        Debugger.Break();
#else
                        throw new NotSupportedException();
#endif
                    }

                    if (cell.Planet == 0)
                        hex.PlanetType = PlanetTypes.kPlanetNone;
                    else
                        hex.PlanetType = (PlanetTypes)(1 << (cell.Planet - 1));

                    if (cell.Base == 0)
                        hex.BaseType = BaseTypes.kBaseNone;
                    else
                        hex.BaseType = (BaseTypes)(1 << (cell.Base - 1));

                    hex.BaseEconomicPoints = cell.Economic;
                    hex.CurrentEconomicPoints = cell.Economic;

                    hex.EmpireBaseVictoryPoints = cell.Strength;
                    hex.EmpireCurrentVictoryPoints = cell.Strength;

                    hex.CartelBaseVictoryPoints = cell.Strength;
                    hex.CartelCurrentVictoryPoints = cell.Strength;

                    hex.BaseSpeedPoints = cell.Impedence;
                    hex.CurrentSpeedPoints = cell.Impedence;

                    // helpers

                    hex.ControlPoints[(int)hex.EmpireControl] = cell.Strength;
                    hex.ControlPoints[(int)hex.CartelControl] = cell.Strength;

                    hex.IsEmpireHome = true;
                    hex.IsCartelHome = true;

                    // adds the hex to the map

                    _map[i] = hex;

                    i++;
                }
            }

            Contract.Assert(i == map.Cells.Count);
        }

        private void InitializeLocationIncrements()
        {
            Contract.Assert(_mapWidth != 0);

            _locationIncrements =
            [
                [0, -(_mapWidth + 1), -_mapWidth, -(_mapWidth - 1), -1, 1, _mapWidth],
                [0, -_mapWidth, -1, 1, _mapWidth - 1, _mapWidth, _mapWidth + 1]
            ];
        }

        private void LoadMapTemplates()
        {
            const string pattern = "*.ini";

            string path = _root + MapTemplate.SubPath;

            IEnumerable<string> e;

            // common map templates

            e = Directory.EnumerateFiles(path + "/generic", pattern);

            if (_genericMapTemplates == null)
                _genericMapTemplates = [];
            else
                _genericMapTemplates.Clear();

            foreach (string f in e)
            {
                MapTemplate template = new(f);

                if (template.IsValid)
                    _genericMapTemplates.Add(template);
            }

            if (_genericMapTemplates.Count == 0)
                throw new NotSupportedException("The server didn't found any map template at '" + path + "'!");

            // indexed map templates

            e = Directory.EnumerateFiles(path + "/indexed", pattern);

            if (_indexedMapTemplates == null)
                _indexedMapTemplates = [];
            else
                _indexedMapTemplates.Clear();

            foreach (string f in e)
            {
                string t = Path.GetFileNameWithoutExtension(f);
                int l = t.Length;

                if (
                    l == 0 ||
                    (l & 1) != 0 ||
                    !int.TryParse(t.AsSpan(0, l >> 1), NumberStyles.None, CultureInfo.InvariantCulture, out int x) ||
                    x < 0 ||
                    x >= _mapWidth ||
                    !int.TryParse(t.AsSpan(l >> 1), NumberStyles.None, CultureInfo.InvariantCulture, out int y) ||
                    y < 0 ||
                    y >= _mapHeight
                )
                    throw new NotSupportedException("The map template at '" + path + "' should be named using valid coordinates. Ex: (32, 36) => 3236.ini or 032036.ini");

                MapTemplate template = new(f);

                if (template.IsValid)
                    _indexedMapTemplates.Add(x + y * _mapWidth, template);
            }
        }

        private void LoadSpaceBackgrounds()
        {
            string path = _root + "Assets/Models/space";

            IEnumerable<string> e = Directory.EnumerateFiles(path, "*.mod");

            if (_spaceBackgrounds == null)
                _spaceBackgrounds = [];
            else
                _spaceBackgrounds.Clear();

            foreach (string f in e)
                _spaceBackgrounds.Add(Path.GetFileName(f));

            if (_spaceBackgrounds.Count == 0)
                throw new NotSupportedException("The server didn't found any space background model at '" + path + "'!");
        }

        private void AddToHexPopulation(Character character)
        {
            Contract.Assert(character.CharacterLocationX != -1 && character.CharacterLocationY != -1 && character.MoveDestinationX == -1 && character.MoveDestinationY == -1);

            MapHex hex = _map[character.CharacterLocationX + character.CharacterLocationY * _mapWidth];

            Contract.Assert(character.ShipCount != 0);

            AddToHexPopulation(hex, character);
        }

        private void AddToHexPopulation(MapHex hex, Character character)
        {
            hex.Population.Add(character.Id, null);

            AdjustPopulationCensus(hex.Census, (int)character.CharacterRace, character.ShipCount, character.ShipListBPV);
            BroadcastIcons(hex.X, hex.Y);
        }

        private void RemoveFromHexPopulation(Character character)
        {
            Contract.Assert(character.CharacterLocationX != -1 && character.CharacterLocationY != -1);

            MapHex hex = _map[character.CharacterLocationX + character.CharacterLocationY * _mapWidth];

            RemoveFromHexPopulation(hex, character);
        }

        private void RemoveFromHexPopulation(MapHex hex, Character character)
        {
            if (!hex.Population.Remove(character.Id))
                throw new NotSupportedException();

            AdjustPopulationCensus(hex.Census, (int)character.CharacterRace, -character.ShipCount, -character.ShipListBPV);
            BroadcastIcons(hex.X, hex.Y);
        }

        private void RefreshHex(MapHex hex)
        {
            ClearPopulationCensus(hex.Census);

            foreach (KeyValuePair<int, object> p in hex.Population)
            {
                Character character = _characters[p.Key];

                AdjustPopulationCensus(hex.Census, (int)character.CharacterRace, character.ShipCount, character.ShipListBPV);
            }
        }

        // economy

        private void CalculateInitialProduction()
        {
            // values of 1 turn

#if DEBUG
            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                Contract.Assert
                (
                    _curBudget[i] == 0.0 &&
                    _curExpenses[i] == 0.0 &&
                    _curMaintenance[i] == 0.0 &&
                    _curProduction[i] == 0.0 &&

                    _curPopulation[i] == 0 &&
                    _curSize[i] == 0
                );
            }
#endif

            CalculateMaintenance();
            CalculateProduction();

            // multiplied by an year

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                _curMaintenance[i] *= _turnsPerYear;
                _curProduction[i] *= _turnsPerYear;

                _curPopulation[i] *= _turnsPerYear;
                _curSize[i] *= _turnsPerYear;
            }
        }

        private void CalculateMaintenance()
        {
            // sums the BPV of all ships

            foreach (KeyValuePair<int, Character> p in _characters)
            {
                Character character = p.Value;

                _curMaintenance[(int)character.CharacterRace] += character.ShipListBPV;

                _curPopulation[(int)character.CharacterRace]++;
            }
        }

        private void CalculateProduction()
        {
            // sums the economic points of all hexes

            for (int i = 0; i < _map.Length; i++)
            {
                MapHex hex = _map[i];

                bool neutralPresence = false;

                if (hex.EmpireControl == Races.kNeutralRace)
                    neutralPresence = true;
                else
                {
                    _curProduction[(int)hex.EmpireControl] += hex.CurrentEconomicPoints;

                    _curSize[(int)hex.EmpireControl]++;
                }

                if (hex.CartelControl == Races.kNeutralRace)
                    neutralPresence = true;
                else
                {
                    _curProduction[(int)hex.CartelControl] += hex.CurrentEconomicPoints;

                    _curSize[(int)hex.CartelControl]++;
                }

                if (neutralPresence)
                {
                    _curProduction[(int)Races.kNeutralRace] += hex.CurrentEconomicPoints;

                    _curSize[(int)Races.kNeutralRace]++;
                }
            }
        }

        private void CalculateBudget()
        {
            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                // logs the values and averages

                _logBudget[i].Add(_curBudget[i]);

                _logExpenses[i].Add(_curExpenses[i]);
                _logMaintenance[i].Add(_curMaintenance[i]);
                _logProduction[i].Add(_curProduction[i]);

                _logPopulation[i].Add((int)Math.Round((double)_curPopulation[i] / _turnsPerYear, MidpointRounding.AwayFromZero));
                _logSize[i].Add((int)Math.Round((double)_curSize[i] / _turnsPerYear, MidpointRounding.AwayFromZero));

                // calculates the income and outcome from last year

                double income = _curProduction[i] * _productionMultiplier;
                double outcome = _curExpenses[i] * _expensesMultiplier + _curMaintenance[i] * _maintenanceMultiplier;

                // calculates the budget for the new year

                double budgetFromLastYear = _curBudget[i];
                double budgetForTheNewYear = income - outcome;

                _curBudget[i] = Math.Round(budgetFromLastYear * 0.25 + budgetForTheNewYear * 0.75, MidpointRounding.AwayFromZero);

                // resets the other values

                _curExpenses[i] = 0.0;
                _curMaintenance[i] = 0.0;
                _curProduction[i] = 0.0;

                _curPopulation[i] = 0;
                _curSize[i] = 0;
            }

            // merges the neutral budgets

            double neutralBudget = 0.0;

            for (int i = (int)Races.kFirstNPC; i < (int)Races.kNumberOfRaces; i++)
            {
                neutralBudget += _curBudget[i];

                _curBudget[i] = 0.0;
            }

            _curBudget[(int)Races.kNeutralRace] = Math.Round(neutralBudget, MidpointRounding.AwayFromZero);
        }

        // ships

        private void CreateInitialPlanetsAndBases()
        {
            // creates the queues that we will use

            var homeWorlds = new PriorityQueue<ShipData, int>[(int)Races.kNumberOfRaces][];
            var coreWorlds = new PriorityQueue<ShipData, int>[(int)Races.kNumberOfRaces][];
            var colonies = new PriorityQueue<ShipData, int>[(int)Races.kNumberOfRaces][];
            var orbitalStations = new PriorityQueue<ShipData, int>[(int)Races.kNumberOfRaces][];

            var starbases = new PriorityQueue<ShipData, int>[(int)Races.kNumberOfRaces];
            var battleStations = new PriorityQueue<ShipData, int>[(int)Races.kNumberOfRaces];
            var baseStations = new PriorityQueue<ShipData, int>[(int)Races.kNumberOfRaces];
            var weaponPlatforms = new PriorityQueue<ShipData, int>[(int)Races.kNumberOfRaces];
            var listeningPosts = new PriorityQueue<ShipData, int>[(int)Races.kNumberOfRaces];

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                homeWorlds[i] = new PriorityQueue<ShipData, int>[3];
                coreWorlds[i] = new PriorityQueue<ShipData, int>[3];
                colonies[i] = new PriorityQueue<ShipData, int>[3];
                orbitalStations[i] = new PriorityQueue<ShipData, int>[3];
            }

            // declares the function that we will use

            void Dequeue(Races race, List<ShipData> list, ref PriorityQueue<ShipData, int> queue, out ShipData data)
            {
                queue ??= new();

                if (queue.Count == 0)
                {
                    foreach (ShipData element in list)
                    {
                        if (IsAvailable(race, element))
                            queue.Enqueue(element, clsPcg.Shared.NextInt32());
                    }

                    if (queue.Count == 0)
                        throw new NotSupportedException();
                }

                data = queue.Dequeue();
            }

            bool TryCustomize(MapHex hex, List<string> list)
            {
                ShipData data = null;

                if (list.Count != 0)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (_shiplist.TryGetValue(list[i], out data))
                            CreateCharacter(hex.EmpireControl, hex.X, hex.Y, data, out _);
                    }
                }

                return data != null;
            }

            // tries to create the all the planets and bases

            for (int i = 0; i < _map.Length; i++)
            {
                MapHex hex = _map[i];

                _indexedMapTemplates.TryGetValue(hex.X + hex.Y * _mapWidth, out MapTemplate template);

                Races race = hex.EmpireControl;

                Contract.Assert(race >= Races.kFirstEmpire && race <= Races.kLastEmpire || race == Races.kNeutralRace);

                ShipData data;

                if (template == null || !TryCustomize(hex, template.Planets))
                {
                    if (hex.PlanetType != PlanetTypes.kPlanetNone)
                    {
                        if (hex.PlanetType == PlanetTypes.kPlanetHomeWorld1)
                            Dequeue(race, _homeWorlds[(int)race][0], ref homeWorlds[(int)race][0], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetHomeWorld2)
                            Dequeue(race, _homeWorlds[(int)race][1], ref homeWorlds[(int)race][1], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetHomeWorld3)
                            Dequeue(race, _homeWorlds[(int)race][2], ref homeWorlds[(int)race][2], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetCoreWorld1)
                            Dequeue(race, _coreWorlds[(int)race][0], ref coreWorlds[(int)race][0], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetCoreWorld2)
                            Dequeue(race, _coreWorlds[(int)race][1], ref coreWorlds[(int)race][1], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetCoreWorld3)
                            Dequeue(race, _coreWorlds[(int)race][2], ref coreWorlds[(int)race][2], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetColony1)
                            Dequeue(race, _colonies[(int)race][0], ref colonies[(int)race][0], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetColony2)
                            Dequeue(race, _colonies[(int)race][1], ref colonies[(int)race][1], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetColony3)
                            Dequeue(race, _colonies[(int)race][2], ref colonies[(int)race][2], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetAsteroidBase1)
                            Dequeue(race, _orbitalStations[(int)race][0], ref orbitalStations[(int)race][0], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetAsteroidBase2)
                            Dequeue(race, _orbitalStations[(int)race][1], ref orbitalStations[(int)race][1], out data);
                        else if (hex.PlanetType == PlanetTypes.kPlanetAsteroidBase3)
                            Dequeue(race, _orbitalStations[(int)race][2], ref orbitalStations[(int)race][2], out data);
                        else
                            throw new NotSupportedException();

                        CreateCharacter(race, hex.X, hex.Y, data, out _);
                    }
                }

                if (template == null || !TryCustomize(hex, template.Bases))
                {
                    if (hex.BaseType != BaseTypes.kBaseNone)
                    {
                        if (hex.BaseType == BaseTypes.kBaseStarbase)
                            Dequeue(race, _starbases[(int)race], ref starbases[(int)race], out data);
                        else if (hex.BaseType == BaseTypes.kBaseBattleStation)
                            Dequeue(race, _battleStations[(int)race], ref battleStations[(int)race], out data);
                        else if (hex.BaseType == BaseTypes.kBaseBaseStation)
                            Dequeue(race, _baseStations[(int)race], ref baseStations[(int)race], out data);
                        else if (hex.BaseType == BaseTypes.kBaseWeaponsPlatform)
                            Dequeue(race, _weaponPlatforms[(int)race], ref weaponPlatforms[(int)race], out data);
                        else if (hex.BaseType == BaseTypes.kBaseListeningPost)
                            Dequeue(race, _listeningPosts[(int)race], ref listeningPosts[(int)race], out data);
                        else
                            throw new NotSupportedException();

                        CreateCharacter(race, hex.X, hex.Y, data, out _);
                    }
                }

                // try add the specials

                if (template != null)
                    TryCustomize(hex, template.Specials);
            }
        }

        private bool IsAvailable(Races race, ShipData data)
        {
            int currentYear = CurrentYear;

            return
                data.Race == race &&
                (data.SpecialRole & SpecialRoles.Ignored) == 0 &&
                data.YearFirstAvailable <= currentYear &&
                data.YearLastAvailable >= currentYear;
        }

        private void CreateInitialPopulation()
        {
            List<Location> locations = [];

            List<ShipData> ships = [];
            List<ShipData> filter = [];

            for (int i = (int)Races.kFirstEmpire; i <= (int)Races.kLastNPC; i++)
            {
                // gets a list of all the available locations
                // where an AI of this race can spawn

                Races race = (Races)i;

                Contract.Assert(locations.Count == 0);

                Location location;

                for (int j = 0; j < _map.Length; j++)
                {
                    MapHex hex = _map[j];

                    if ((_raceList & (1u << i)) != 0u)
                    {
                        if (race <= Races.kLastEmpire)
                        {
                            if (hex.EmpireControl != race)
                                continue;
                        }
                        else if (race <= Races.kLastCartel)
                        {
                            if (hex.CartelControl != race)
                                continue;
                        }
                    }
                    else if (race <= Races.kLastEmpire)
                    {
                        if ((hex.EmpireControl != Races.kNeutralRace) && (_alliances[(int)race] & (RaceMasks)(1 << (int)hex.EmpireControl)) == 0)
                            continue;
                    }
                    else if (race <= Races.kLastCartel)
                    {
                        if ((hex.CartelControl != Races.kNeutralRace) && (_alliances[(int)race] & (RaceMasks)(1 << (int)hex.CartelControl)) == 0)
                            continue;
                    }
                    else
                    {
                        if (hex.EmpireControl != Races.kNeutralRace && hex.CartelControl != Races.kNeutralRace)
                            continue;
                    }

                    location = new Location(hex.X, hex.Y, 0);

                    locations.Add(location);
                }

                Contract.Assert(locations.Count > 0);

                // gets a list with all the ships available for this race

                Contract.Assert(ships.Count == 0);

                foreach (KeyValuePair<string, ShipData> p in _shiplist)
                {
                    ShipData data = p.Value;

                    if (
                        IsAvailable(race, data) &&
                        (race == Races.kMonster && data.ClassType == ClassTypes.kClassMonster) ||
                        (race < Races.kMonster && data.ClassType >= ClassTypes.kClassFreighter && data.ClassType <= ClassTypes.kClassBattleship)
                    )
                        ships.Add(data);
                }

                Contract.Assert(ships.Count > 0);

                // creates the initial population

                int initialPopulation = _initialPopulation;

                if (race >= Races.kFirstNPC)
                    initialPopulation = (int)Math.Round(initialPopulation * _neutralPopulationRatio, MidpointRounding.AwayFromZero);

                for (int j = 0; j < initialPopulation; j++)
                {
                    // tries to get a random location, which is not overcrowded
                    // and crashes if it is not sucessful, within a time interval

                    const double timeLimit = 1000.0; // ms

                    long t = Environment.TickCount64;

                    do
                    {
                        location = locations[_rand.NextInt32(locations.Count)];

                        if (Environment.TickCount64 - t >= timeLimit)
                            throw new NotSupportedException("Map too small?");
                    }
                    while (location.Z >= _populationDistribution);

                    location.Z++;

                    // gets a random entry from the list

                    ShipData data = ships[_rand.NextInt32(ships.Count)];

                    // if this is a support ship then we need to look for, or create, a pair for it

                    if (data.SpecialRole == SpecialRoles.D)
                    {
                        CreatePair(ships, filter, race, ref j, location, data);

                        continue;
                    }

                    // by default creates a character and a ship at a specific location

                    CreateCharacter(race, location.X, location.Y, data, out _);
                }

                // clears the lists

                locations.Clear();

                ships.Clear();
            }
        }

        private void CreatePair(List<ShipData> ships, List<ShipData> filter, Races race, ref int population, Location location, ShipData newShipData)
        {
            Character character;
            Ship ship;

            // tries to find a pair, that isn't a support ship itself, with +10% BPV than our support ship

            int minBPV = (int)Math.Round(newShipData.BPV * 1.1, MidpointRounding.AwayFromZero);

            ClassTypes minClassType = _minStartingClass[CurrentEra];
            ClassTypes maxClassType = _maxStartingClass[CurrentEra];

            foreach (KeyValuePair<int, Character> p in _characters)
            {
                character = p.Value;

                if (character.CharacterRace == race && character.ShipCount == 1)
                {
                    ship = character.GetFirstShip();

                    Contract.Assert(_ships.ContainsKey(ship.Id));

                    if (ship.BPV >= minBPV && ship.ClassType >= minClassType && ship.ClassType <= maxClassType)
                    {
                        UpdateCharacter(character, newShipData);
                        AdjustPopulationCensus(character, newShipData.BPV);

                        return;
                    }
                }
            }

            // if it didn't found a pair then we need to create one
            // this ship can't fly alone

            Contract.Assert(filter.Count == 0);

            foreach (ShipData d in ships)
            {
                if (d.SpecialRole != SpecialRoles.D && d.BPV >= minBPV)
                    filter.Add(d);
            }

            if (filter.Count == 0)
                throw new NotSupportedException();

            // creates a character and a command ship at a specific location

            ShipData commandShipData = filter[_rand.NextInt32(filter.Count)];

            filter.Clear();

            CreateCharacter(race, location.X, location.Y, commandShipData, out character);

            // creates the support ship and adds it to the current character

            UpdateCharacter(character, newShipData);
            AdjustPopulationCensus(character, newShipData.BPV);

            // increments the population counter (as we created an extra ship here)

            population++;
        }

        private void CreateShip(Races race, ClassTypes minClassType, ClassTypes maxClassType, out Ship ship)
        {
            if (!GetShipData(race, minClassType, maxClassType, 0, 32767, CurrentYear, out ShipData data))
                throw new NotSupportedException();

            CreateShip(data, out ship);
        }

        private void CreateShip(ShipData data, out Ship ship)
        {
            CopyShipData(data, out ship);

            ship.LockID = 0;
            ship.OwnerID = 0;
            ship.IsInAuction = 0;

            ship.Name = _raceAbbreviations[(int)ship.Race] + (ship.Id & 0xffff).ToString("X4", CultureInfo.InvariantCulture) + "s";
            ship.TurnCreated = _turn;

            _ships.Add(ship.Id, ship);
        }

        private void ModifyShip(Ship ship, Races race)
        {
            ship.Race = race;

            // tries to get the first fighter type

            string fighterType = string.Empty;

            foreach (KeyValuePair<string, int> p in _supplyFtrList[(int)race])
            {
                fighterType = p.Key;

                break;
            }

            // resets the fighter bays

            for (int i = 0; i < 4; i++)
            {
                if (ship.Stores.FighterBays[i].FightersMax > 0)
                {
                    ship.Stores.FighterBays[i].FightersCount = 0;
                    ship.Stores.FighterBays[i].FightersLoaded = 0;
                    ship.Stores.FighterBays[i].Unknown1 = 0;
                    ship.Stores.FighterBays[i].FighterType = fighterType;

                    Contract.Assert(ship.Stores.FighterBays[i].Unknown2 == 0);
                }
            }
        }

        private int GetShipTradeInValue(Ship ship)
        {
            int repairCost = GetShipRepairCost(ship);

            return (int)GetShipTradeInCost(ship.ClassType, ship.BPV) - repairCost;
        }

        private int GetShipRepairCost(Ship ship)
        {
            int max = 0;
            int cur = 0;

            for (int i = 0; i < Ship.SystemsSize; i += 2)
            {
                max += ship.Systems.Items[i];

                if (i == (int)SystemTypes.RightDamageControlMax || i == (int)SystemTypes.RepairMax)
                    cur += ship.Systems.Items[i]; // damaged ignored
                else
                    cur += ship.Systems.Items[i + 1];
            }

            if (max != cur)
            {
                double damageRatio = (double)(max - cur) / max;

                return (int)GetShipRepairCost(ship.ClassType, ship.BPV, damageRatio);
            }

            return 0;
        }

        private double GetShipRepairCost(ClassTypes classType, int bpv, double damageRatio)
        {
            Contract.Assert(damageRatio >= 0.0 && damageRatio <= 1.0);

            return Math.Truncate(GetRepairCost(classType) * bpv * damageRatio);
        }

        private double GetShipTradeInCost(ClassTypes classType, int bpv)
        {
            return Math.Truncate(GetTradeInCost(classType) * bpv);
        }

        private int GetShipStoresCost(Races race, ClassTypes t, ShipStores s)
        {
            // missiles (cost updated since 2.1u version)

            double v = _missileSizes[(int)s.MissilesType] * (int)s.MissilesDriveSystem * s.TotalMissilesReadyAndStored * 0.2 * _costMissiles;

            // shuttles

            v += s.General.CurrentQuantity * _costShuttles;

            // supplies

            v += s.BoardingParties.CurrentQuantity * _costMarines;
            v += s.TBombs.CurrentQuantity * _costMines;
            v += s.DamageControl.CurrentQuantity * GetSparePartsCost(t);

            // fighters

            for (int i = 0; i < 4; i++)
            {
                if (s.FighterBays[i].FightersCount > 0)
                {
                    string name = s.FighterBays[i].FighterType;
                    int bpv = _supplyFtrList[(int)race][name];

                    v += GetFighterBPV(name, bpv);
                }
            }

            return (int)Math.Truncate(v);
        }

        private static void RepairShip(Ship ship)
        {
            ref byte[] systems = ref ship.Systems.Items;

            for (int i = 0; i < Ship.SystemsSize; i += 2)
                systems[i + 1] = systems[i];

            NormalizeHardPointStates(ship);
        }

        private static void AutomaticRepair(Ship ship, double percentage)
        {
            ref byte[] systems = ref ship.Systems.Items;

            for (int i = 0; i < Ship.SystemsSize; i += 2)
            {
                if (systems[i] != 0)
                    systems[i + 1] = (byte)GetWithPercentageOrMinimum(systems[i + 1], systems[i], percentage, 1.0);
            }

            NormalizeHardPointStates(ship);
        }

        private static void NormalizeHardPointStates(Ship ship)
        {
            ref byte[] systems = ref ship.Systems.Items;
            ref WeaponHardpoint[] hardpoints = ref ship.Stores.WeaponHardpoints;

            for (int i = 0; i < 25; i++)
            {
                int max = systems[(i << 1) + (int)SystemTypes.NumHeavyWeaponMax1];

                if (max != 0)
                {
                    int cur = systems[(i << 1) + (int)SystemTypes.NumHeavyWeapon1];

                    Contract.Assert(cur <= max);

                    if (cur == max)
                        hardpoints[i].State = WeaponStates.Healthy;
                    else
                        hardpoints[i].State = WeaponStates.Damaged;
                }
            }
        }

        private void AutomaticResupply(Ship ship, double percentage)
        {
            ShipStores s = ship.Stores;

            // upgrades the missiles

            if (_turn >= _fastMissileSpeedDate)
                s.MissilesDriveSystem = MissileDriveSystems.Fast;
            else if (_turn >= _mediumMissileSpeedDate)
                s.MissilesDriveSystem = MissileDriveSystems.Medium;
            else
                Contract.Assert(s.MissilesDriveSystem == MissileDriveSystems.Slow);

            s.MissilesReloads = 4;

            // resupplies the missiles

            int c = 0;

            for (int i = 0; i < 25; i++)
            {
                int j = s.MissileHardpoints[i].TubesCount;

                if (j > 0)
                {
                    j = j * s.MissileHardpoints[i].TubesCapacity * (s.MissilesReloads + 1) / _missileSizes[(int)s.MissilesType];

                    //Contract.Assert(s.MissileHardpoints[i].MissilesReady == 0 && s.MissileHardpoints[i].MissilesStored <= j);

                    s.MissileHardpoints[i].MissilesStored = (short)GetWithPercentageOrMinimum(s.MissileHardpoints[i].MissilesStored, j, percentage, 2.0);

                    c += s.MissileHardpoints[i].MissilesStored;
                }
            }

            s.TotalMissilesReadyAndStored = (short)c;

            Contract.Assert(s.TotalMissilesReady == 0);

            s.TotalMissilesStored = (short)c;

            // resuplies the shuttles, boarding parties, mines and spare parts

            UpdateWithPercentageOrMinimum(ref s.General, percentage, 1.0);

            UpdateWithPercentageOrMinimum(ref s.BoardingParties, percentage, 1.0);
            UpdateWithPercentageOrMinimum(ref s.TBombs, percentage, 1.0);
            UpdateWithPercentageOrMinimum(ref s.DamageControl, percentage, 1.0);

            // resuplies the fighters

            for (int i = 0; i < 4; i++)
            {
                int j = s.FighterBays[i].FightersMax;

                if (j > 0)
                {
                    c = (int)GetWithPercentageOrMinimum(s.FighterBays[i].FightersCount, j, percentage, 1.0);

                    s.FighterBays[i].FightersCount = (byte)c;
                    s.FighterBays[i].FightersLoaded = (byte)c;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateWithPercentageOrMinimum(ref StoreItem item, double percentage, double minimum)
        {
            item.CurrentQuantity = (byte)GetWithPercentageOrMinimum(item.CurrentQuantity, item.MaxQuantity, percentage, minimum);
        }

        private static double GetWithPercentageOrMinimum(double cur, double max, double percentage, double minimum)
        {
            Contract.Assert(percentage <= 1.0);

            percentage *= max;

            if (percentage < minimum)
                percentage = minimum;

            cur = Math.Round(cur + percentage, MidpointRounding.AwayFromZero);

            if (cur > max)
                cur = max;

            return cur;
        }

        // ... report phase

        private void UpdateShipSystems(Ship ship, byte[] buffer, int index)
        {
            // first we copy the data to our ship

            ship.Systems = new ShipSystems(buffer, index);

            // then we do some validation

            ref byte[] systems = ref ship.Systems.Items;

            for (int i = 0; i < Ship.SystemsSize; i += 2)
            {
                if (systems[i] > 0)
                {
                    if (systems[i] > 127)
                        systems[i] = 127;

                    if (systems[i + 1] > systems[i])
                        systems[i + 1] = systems[i];
                }
            }

            // finally, in the end, we normalize any APR boost this ship could have received

            NormalizeAprPower(ship, systems[(int)SystemTypes.AprMax], _shiplist[ship.ShipClassName].Apr);
        }

        private void UpdateShipStores(Ship ship, byte[] buffer, int index, int size)
        {
            ShipStores a = ship.Stores;
            ShipStores b = new(buffer, index, size);

            // missile hardpoints

            int c = 0;

            for (int i = 0; i < 25; i++)
            {
                int max = b.MissileHardpoints[i].TubesCount;

                if (max > 0)
                {
                    max = max * b.MissileHardpoints[i].TubesCapacity * (b.MissilesReloads + 1) / _missileSizes[(int)b.MissilesType];

                    int cur = b.MissileHardpoints[i].MissilesReady + b.MissileHardpoints[i].MissilesStored;

                    if (cur > max)
                        cur = max;

                    Contract.Assert(a.MissileHardpoints[i].MissilesReady == 0);

                    a.MissileHardpoints[i].MissilesStored = (short)cur;

                    c += cur;
                }
            }

            // missiles (totals)

            a.TotalMissilesReadyAndStored = (short)c;

            Contract.Assert(a.TotalMissilesReady == 0);

            a.TotalMissilesStored = (short)c;

            // shuttles

            UpdateStoreItem(ref a.General, ref b.General);

            // unknown items

            Contract.Assert(
                b.Unknown3.MaxQuantity == 0 && b.Unknown3.BaseQuantity == 0 && b.Unknown3.CurrentQuantity == 0 &&
                b.Unknown4.MaxQuantity == 0 && b.Unknown4.BaseQuantity == 0 && b.Unknown4.CurrentQuantity == 0 &&
                b.Unknown5.MaxQuantity == 0 && b.Unknown5.BaseQuantity == 0 && b.Unknown5.CurrentQuantity == 0
            );

            // transport items

            Contract.Assert(!b.TransportItems.TryGetValue(TransportItems.kTransSpareParts, out int transSpareParts) || transSpareParts == 1);

            // weapon hardpoints

#if DEBUG
            for (int i = 0; i < 25; i++)
                Contract.Assert(Enum.IsDefined(b.WeaponHardpoints[i].State));
#endif

            // marines, mines and spare parts

            UpdateStoreItem(ref a.BoardingParties, ref b.BoardingParties);
            UpdateStoreItem(ref a.TBombs, ref b.TBombs);
            UpdateStoreItem(ref a.DamageControl, ref b.DamageControl);

            // fighter bays

            for (int i = 0; i < 4; i++)
            {
                byte cur = b.FighterBays[i].FightersCount;
                byte max = b.FighterBays[i].FightersMax;

                if (cur > max)
                    cur = max;

                a.FighterBays[i].FightersCount = cur;
                a.FighterBays[i].FightersLoaded = cur;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateStoreItem(ref StoreItem item1, ref StoreItem item2)
        {
            item1.CurrentQuantity = item2.CurrentQuantity < item1.MaxQuantity ? item2.CurrentQuantity : item1.MaxQuantity;
        }

        private static void UpdateShipOfficers(Ship ship, byte[] buffer, int index, int size)
        {
            // copies the data

            ship.Officers = new ShipOfficers(buffer, index, size);

            // normalizes the data

            UpgradeShipOfficers(ship, OfficerRanks.kSenior);
        }

        private static void UpgradeShipOfficers(Ship ship, OfficerRanks rank)
        {
            for (int i = 0; i < (int)OfficerTypes.kMaxOfficers; i++)
            {
                ref Officer officer = ref ship.Officers.Items[i];

                officer.Rank = rank;
                officer.Unknown1 = 0;
                officer.Unknown2 = _officerDefaults[(int)rank];
            }
        }

        // ... draft phase

        private static void ApplyAprPowerBoost(Ship ship, double powerBoost)
        {
            Contract.Assert(ship.ClassType >= ClassTypes.kClassFreighter && powerBoost >= 0.0 && powerBoost <= 1.0);

            // adjusts the power boost for the cartels

            if (ship.Race >= Races.kFirstCartel && ship.Race <= Races.kLastCartel)
                powerBoost = Math.Round(powerBoost * 0.33, MidpointRounding.AwayFromZero);

            // calculates the current power of the ship

            /*
                ship.Systems.Items[(int)SystemTypes.RightWarpMax]
                ship.Systems.Items[(int)SystemTypes.LeftWarpMax]
                ship.Systems.Items[(int)SystemTypes.CenterWarpMax]
                ship.Systems.Items[(int)SystemTypes.ImpulseMax];
            */

            int curAprMax = ship.Systems.Items[(int)SystemTypes.AprMax];

            // calculates the new power of the ship

            int newAprMax = (int)Math.Round(curAprMax * (powerBoost + 1.0), MidpointRounding.AwayFromZero);

            if (newAprMax > 127)
                newAprMax = 127;

            // adjusts the APR to the new 'reality'

            NormalizeAprPower(ship, curAprMax, newAprMax);
        }

        private static void NormalizeAprPower(Ship ship, int curAprMax, int newAprMax)
        {
            if (curAprMax != newAprMax)
            {
                ref byte[] systems = ref ship.Systems.Items;

                /*
                    curAprMax   newAprMax
                    curApr      ?
                */

                int curApr = systems[(int)SystemTypes.Apr];

                Contract.Assert(curApr <= curAprMax);

                int newApr = (int)Math.Round((double)(curApr * newAprMax) / curAprMax, MidpointRounding.AwayFromZero);

                if (newApr > newAprMax)
                    newApr = newAprMax;

                systems[(int)SystemTypes.AprMax] = (byte)newAprMax;
                systems[(int)SystemTypes.Apr] = (byte)newApr;
            }
        }

        // shipyard

        private void CreateShipyard()
        {
            int currentYear = CurrentYear;

            foreach (KeyValuePair<string, ShipData> p in _shiplist)
            {
                ShipData data = p.Value;

                if (
                    data.Race >= Races.kFirstEmpire &&
                    data.Race <= Races.kLastCartel &&
                    data.ClassType >= ClassTypes.kClassFreighter &&
                    data.ClassType <= ClassTypes.kClassBattleship &&
                    (data.SpecialRole & SpecialRoles.Ignored) == 0 &&
                    data.YearLastAvailable >= currentYear &&
                    data.YearFirstAvailable <= currentYear
                )
                {
                    CreateShip(data, out Ship ship);

                    AddBidItem(ship);
                }
            }

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                TrySortBidItems(i);
        }

        private void AddBidItem(Ship ship)
        {
            // updates the ship

            Contract.Assert(ship.OwnerID == 0 && ship.IsInAuction == 0);

            ship.IsInAuction = 1;

            // initializes the item

            int turnsToEndOfYear = _turnsPerYear - (_turn % _turnsPerYear);

            BidItem item = new()
            {
                Id = GetNextDataId(),
                LockID = 0,

                BiddingHasBegun = 0,

                ShipClassName = ship.ShipClassName,
                ShipId = ship.Id,
                ShipBPV = ship.BPV,

                AuctionValue = ship.BPV, // not used ?
                AuctionRate = 1.0,       // not used ?

                TurnOpened = _turn,
                TurnToClose = _turn + turnsToEndOfYear, // we want to renew the shipyard every year
                CurrentBid = (int)GetShipCost(ship.ClassType, ship.BPV),

                BidOwnerID = 0,
                TurnBidMade = 0,
                BidMaximum = 0
            };

            _bidItems[(int)ship.Race].Add(ship.Id, item);
        }

        private int GetFighterBPV(string name, int bpv)
        {
            ClassTypes classType;

            if (_ftrlist.ContainsKey(name))
            {
                // it is a fighter

                classType = ClassTypes.kClassShuttle;
            }
            else
            {
                Contract.Assert(_shiplist.TryGetValue(name, out ShipData data) && data.ClassType == ClassTypes.kClassPseudoFighter);

                classType = ClassTypes.kClassPseudoFighter;
            }

            /*
                _classAverageBpv[(int)classType] == _classCostRatio[(int)classType] * _costShuttles
                bpv                              == ?

                RETURNS: the relative bpv of this fighter
                         taking in account that each BPV point is worth exactly 1 prestige
            */

            Contract.Assert(int.IsPositive(bpv) && double.IsPositive(_classAverageBpv[(int)classType]));

            return (int)Math.Round(bpv * (_classCostRatio[(int)classType] * _costShuttles) / _classAverageBpv[(int)classType], MidpointRounding.AwayFromZero);
        }

        private double GetShipCost(ClassTypes classType, int bpv)
        {
            return Math.Round(_classCostRatio[(int)classType] * bpv, MidpointRounding.AwayFromZero);
        }

        private double GetRepairCost(ClassTypes classType)
        {
            return Math.Round(_classCostRatio[(int)classType] * _costRepair, MidpointRounding.AwayFromZero);
        }

        private double GetTradeInCost(ClassTypes classType)
        {
            return Math.Round(_classCostRatio[(int)classType] * _costTradeIn, MidpointRounding.AwayFromZero);
        }

        private double GetSparePartsCost(ClassTypes classType)
        {
            return Math.Round(_classCostRatio[(int)classType] * _costSpareParts, MidpointRounding.AwayFromZero);
        }

        private void TrySortBidItems(int race)
        {
            Dictionary<int, BidItem> items = _bidItems[race];

            if (items.Count <= 1)
                return;

            SortedDictionary<long, BidItem> sortedItems = [];

            foreach (KeyValuePair<int, BidItem> p in items)
            {
                Ship ship = _ships[p.Key];

                long key = ((long)(ClassTypes.kMaxClasses - ship.ClassType) << 48) + ((long)(32767 - ship.BPV) << 32) + ship.Id;

                sortedItems.Add(key, p.Value);
            }

            items.Clear();

            foreach (KeyValuePair<long, BidItem> p in sortedItems)
            {
                BidItem item = p.Value;

                items.Add(item.ShipId, item);
            }

            sortedItems.Clear();
        }

        private void ClearShipyard()
        {
            Dictionary<int, BidItem> d = [];

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                Dictionary<int, BidItem> items = _bidItems[i];

                if (items.Count == 0)
                    continue;

                // we want to preserve the bids that are still going on

                foreach (KeyValuePair<int, BidItem> p in items)
                {
                    BidItem item = p.Value;

                    if (item.BiddingHasBegun == 1)
                        d.Add(item.ShipId, item);
                    else if (!_ships.Remove(item.ShipId))
                        throw new NotImplementedException();
                }

                items.Clear();

                foreach (KeyValuePair<int, BidItem> p in d)
                {
                    BidItem item = p.Value;

                    items.Add(item.ShipId, item);
                }

                d.Clear();
            }
        }

        private bool TryUpdateBidItem(Character character, BidItem item, int bidType)
        {
            bool isFirstBid = item.BiddingHasBegun == 0;

            // updates the bid value

            int bidValue;

            if (isFirstBid)
                bidValue = item.CurrentBid;
            else
                bidValue = item.BidMaximum;

            switch (bidType)
            {
                case 2: // +5 prestige
                    bidValue += 5;
                    break;

                case 3: // +10 prestige
                    bidValue += 10;
                    break;

                case 5: // +5 %
                    bidValue = (int)Math.Truncate(bidValue * 1.05);
                    break;

                case 6: // +10%
                    bidValue = (int)Math.Truncate(bidValue * 1.10);
                    break;

#if DEBUG
                default:
                    throw new NotImplementedException();
#endif

            }

            // updates the character(s) and bid

            if (isFirstBid)
            {
                if (character.CharacterCurrentPrestige < bidValue || character.ShipCount + character.Bids >= maxHumanFleetSize)
                    return false;

                character.Bids++;

                // charges the full bid

                UpdateCharacter(character, -bidValue);

                ShipData data = _shiplist[item.ShipClassName];

                item.BiddingHasBegun = 1;
                item.TurnToClose = _turn + _turnsToClose[data.SizeClass - 1];
                item.BidOwnerID = character.Id;
            }
            else if (character.Id == item.BidOwnerID)
            {
                int inc = bidValue - item.BidMaximum;

                if (character.CharacterCurrentPrestige < inc)
                    return false;

                // charges the difference between the last bid and the current one

                UpdateCharacter(character, -inc);
            }
            else
            {
                if (character.CharacterCurrentPrestige < bidValue || character.ShipCount + character.Bids >= maxHumanFleetSize)
                    return false;

                // returns the last bid to its last owner

                Character lastOwner = _characters[item.BidOwnerID];

                lastOwner.Bids--;

                UpdateCharacter(lastOwner, item.BidMaximum);

                // charges the full bid to the new owner

                character.Bids++;

                UpdateCharacter(character, -bidValue);

                item.BidOwnerID = character.Id;

                // updates the last owner UI

                Write(lastOwner.Client, ClientRequests.PlayerRelayC_0x05_0x00_0x07, lastOwner.Id); // 14_12
            }

            // finalizes the bid

            item.CurrentBid = bidValue;

            item.TurnBidMade = _turn;
            item.BidMaximum = bidValue;

            // updates the current owner UI

            Write(character.Client, ClientRequests.PlayerRelayC_0x05_0x00_0x07, character.Id); // 14_12

            return true;
        }

        private void ProcessBids(Queue<int> queuedItems)
        {
            int mask = 0;

            if (_bidReplacements.Count > 0)
            {
                // there is no point in adding new replacements at the start of a new year
                // so we should check it before doing it

                if (_turn % _turnsPerYear != 0)
                {
                    foreach (KeyValuePair<string, int> p in _bidReplacements)
                    {
                        Ship ship = null;

                        for (int i = 0; i < p.Value; i++)
                        {
                            ShipData data = _shiplist[p.Key];

                            CreateShip(data, out ship);

                            AddBidItem(ship);
                        }

                        mask |= 1 << (int)ship.Race;
                    }
                }

                // the list is always cleared at the end

                _bidReplacements.Clear();
            }

            Contract.Assert(queuedItems.Count == 0);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                Dictionary<int, BidItem> items = _bidItems[i];

                if (items.Count > 0)
                {
                    foreach (KeyValuePair<int, BidItem> p in items)
                    {
                        BidItem item = p.Value;

                        if (item.TurnToClose <= _turn)
                        {
                            queuedItems.Enqueue(item.ShipId);

                            if (item.BiddingHasBegun == 0)
                            {
                                // deletes the ship

                                _ships.Remove(item.ShipId);
                            }
                            else
                            {
                                Ship ship = _ships[item.ShipId];

                                Contract.Assert(ship.IsInAuction == 1);

                                ship.IsInAuction = 0;

                                Character character = _characters[item.BidOwnerID];

                                character.Bids--;

                                UpdateCharacter(character, ship);

                                AdjustPopulationCensus(character, ship.BPV);

                                if ((character.State & Character.States.IsHumanOnline) == Character.States.IsHumanOnline)
                                {
                                    Write(character.Client, ClientRequests.PlayerRelayC_0x04_0x00_0x05, character.Id); // A_5
                                    Write(character.Client, ClientRequests.PlayerRelayC_0x06_0x00_0x08, character.Id); // 14_13
                                }

                                // queues a replacement for the next turn

                                if (_bidReplacements.ContainsKey(ship.ShipClassName))
                                    _bidReplacements[ship.ShipClassName] += 1;
                                else
                                    _bidReplacements.TryAdd(ship.ShipClassName, 1);
                            }
                        }
                    }

                    for (int j = queuedItems.Count; j > 0; j--)
                        items.Remove(queuedItems.Dequeue());

                    if ((mask & 1 << i) != 0)
                        TrySortBidItems(i);
                }
            }
        }

        // maintenance

        private void ProcessIO(long t0)
        {
            if (_savegameState == 0)
                return;

            const string lockMsg = "SUCCESS: Campaign locked";
            const string unlockMsg = "SUCCESS: Campaign unlocked";
            const string loadMsg = "SUCCESS: Campaign loaded";
            const string saveMsg = "SUCCESS: Campaign saved";

            Contract.Assert(_savegameState > 0);

            _savegameState--;

            switch (_savegameState)
            {
                case save3:
                    _savegameState = 0;
                    Console.WriteLine(unlockMsg);
                    break;
                case save2:
                    SaveCampaign(t0);
                    Console.WriteLine(saveMsg);
                    break;
                case save1:
                    CloseForMaintenance();
                    Console.WriteLine(lockMsg);
                    break;
                case load3:
                    _savegameState = 0;
                    Console.WriteLine(unlockMsg);
                    break;
                case load2:
                    LoadCampaign(t0);
                    Console.WriteLine(loadMsg);
                    break;
                case load1:
                    CloseForMaintenance();
                    Console.WriteLine(lockMsg);
                    break;
            }
        }

        private void CloseForMaintenance()
        {
            foreach (KeyValuePair<int, Client27000> p in _clients)
                p.Value.Dispose();
        }

        private void SaveCampaign(long t0)
        {
            Contract.Assert(_lastSavegame != null);

            // tries to create a new file and writer

            FileStream f = null;

            try
            {
                f = new FileStream(_lastSavegame + savegameExtension, FileMode.Create, FileAccess.Write);
            }
            catch (Exception e)
            {
                _lastSavegame = null;

                f?.Close();

                LogError("SaveCampaign()", e);

                return;
            }

            BinaryWriter w = new(f, Encoding.UTF8, true);

            // general

            w.Write(_savegameState);

            // server status

            Utils.Write(w, true, _administrator);
            Utils.Write(w, true, _nickSuffix);

            w.Write(_hostName);
            w.Write(_earlyMap);
            w.Write(_middleMap);
            w.Write(_lateMap);
            w.Write(_gameType);
            w.Write(_maxNumPlayers);
            w.Write(_numPlayers);
            w.Write(_maxNumLoggedOnPlayers);
            w.Write(_numLoggedOnPlayers);
            w.Write(_raceList);

            w.Write(_difficultyLevel);
            w.Write(_startingEra);

            // server files

            w.Write(_serverFiles.Count);

            foreach (KeyValuePair<string, uint> p in _serverFiles)
            {
                w.Write(p.Key);
                w.Write(p.Value);
            }

            // data counter

            w.Write(_dataCounter);

            // characters

            w.Write(_lastLogin - _seconds);

            w.Write(_logouts.Count);

            foreach (int i in _logouts)
                w.Write(i);

            w.Write(_ipAddresses.Count);

            foreach (KeyValuePair<string, int> p in _ipAddresses)
            {
                w.Write(p.Key);
                w.Write(p.Value);
            }

            w.Write(_characterNames.Count);

            foreach (KeyValuePair<string, int> p in _characterNames)
            {
                w.Write(p.Key);
                w.Write(p.Value);
            }

            w.Write(_characters.Count);

            foreach (KeyValuePair<int, Character> p in _characters)
            {
                w.Write(p.Key);
                p.Value.WriteTo(w);
            }

            for (int i = 0; i < 4; i++)
                w.Write((int)_startingRank[i]);

            for (int i = 0; i < 4; i++)
                w.Write(_startingPrestige[i]);

            w.Write(_cpuMovements.Count);

            foreach (KeyValuePair<int, long> p in _cpuMovements)
            {
                w.Write(p.Key);
                w.Write(p.Value - t0);
            }

            w.Write(_humanMovements.Count);

            foreach (KeyValuePair<int, long> p in _humanMovements)
            {
                w.Write(p.Key);
                w.Write(p.Value - t0);
            }

            w.Write(_cpuMovementDelay);
            w.Write(_cpuMovementMinRest);
            w.Write(_cpuMovementMaxRest);

            w.Write(_humanMovementDelay);

            // map

            w.Write(_mapName);

            w.Write(_mapWidth);
            w.Write(_mapHeight);

            w.Write(_map.Length);

            for (int i = 0; i < _map.Length; i++)
                _map[i].WriteTo(w);

            // ... skips _locationIncrements

            int c;

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                c = 0;

                while (c < maxHomeLocations && _homeLocations[i][c] != null)
                    c++;

                w.Write(c);

                for (int j = 0; j < c; j++)
                    _homeLocations[i][j].WriteTo(w);
            }

            _census.WriteTo(w);

            // script map

            for (int i = 0; i < 24; i++)
                _terrainContents[i].WriteTo(w);

            // economy

            w.Write(_expensesMultiplier);
            w.Write(_maintenanceMultiplier);
            w.Write(_productionMultiplier);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                w.Write(_curBudget[i]);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                w.Write(_curExpenses[i]);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                w.Write(_curMaintenance[i]);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                w.Write(_curProduction[i]);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                w.Write(_curPopulation[i]);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                w.Write(_curSize[i]);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                c = _logBudget[i].Count;

                w.Write(c);

                for (int j = 0; j < c; j++)
                    w.Write(_logBudget[i][j]);

                c = _logExpenses[i].Count;

                w.Write(c);

                for (int j = 0; j < c; j++)
                    w.Write(_logExpenses[i][j]);

                c = _logMaintenance[i].Count;

                w.Write(c);

                for (int j = 0; j < c; j++)
                    w.Write(_logMaintenance[i][j]);

                c = _logProduction[i].Count;

                w.Write(c);

                for (int j = 0; j < c; j++)
                    w.Write(_logProduction[i][j]);

                c = _logPopulation[i].Count;

                w.Write(c);

                for (int j = 0; j < c; j++)
                    w.Write(_logPopulation[i][j]);

                c = _logSize[i].Count;

                w.Write(c);

                for (int j = 0; j < c; j++)
                    w.Write(_logSize[i][j]);
            }

            // stardate

            w.Write(_turn);

            w.Write(_turnsPerYear);
            w.Write(_millisecondsPerTurn);

            w.Write(_earlyYears);
            w.Write(_middleYears);
            w.Write(_lateYears);
            w.Write(_advancedYears);

            w.Write(_mediumMissileSpeedDate);
            w.Write(_fastMissileSpeedDate);

            // specs

            Contract.Assert(_missileSizes.Length == 7);

            for (int i = 0; i < 7; i++)
                w.Write(_missileSizes[i]);

            w.Write(_sparePartsMultiplier);

            w.Write(_initialPopulation);
            w.Write(_populationDistribution);
            w.Write(_neutralPopulationRatio);

            w.Write((int)_theGood);
            w.Write((int)_theBad);
            w.Write((int)_theUgly);

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                w.Write((int)_alliances[i]);

            // ships

            w.Write(_ships.Count);

            foreach (KeyValuePair<int, Ship> p in _ships)
            {
                w.Write(p.Key);
                p.Value.WriteTo(w);
            }

            for (int i = 0; i < 4; i++)
            {
                w.Write((int)_minStartingClass[i]);
                w.Write((int)_maxStartingClass[i]);
            }

            w.Write((int)_cpuOfficerRank);
            w.Write(_cpuPowerBoost);

            for (int i = 0; i < (int)ClassTypes.kMaxClasses; i++)
                w.Write(_classCostRatio[i]);

            w.Write(_costRepair);
            w.Write(_costTradeIn);

            w.Write(_costMissiles);
            w.Write(_costShuttles);
            w.Write(_costMarines);
            w.Write(_costMines);
            w.Write(_costSpareParts);

            w.Write(_cpuAutomaticRepairMultiplier);
            w.Write(_cpuAutomaticResupplyMultiplier);

            // shipyard

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                w.Write(_bidItems[i].Count);

                foreach (KeyValuePair<int, BidItem> p in _bidItems[i])
                {
                    w.Write(p.Key);
                    p.Value.WriteTo(w);
                }
            }

            w.Write(_bidReplacements.Count);

            foreach (KeyValuePair<string, int> p in _bidReplacements)
            {
                w.Write(p.Key);
                w.Write(p.Value);
            }

            w.Write(_sellingAction);

            for (int i = 0; i < 6; i++)
                w.Write(_turnsToClose[i]);

            // chat

            w.Write(_serverNick);

            // drafts

            c = _availableMissions.Count;

            w.Write(c);

            for (int i = 0; i < c; i++)
                w.Write(_availableMissions[i]);

            w.Write(_drafts.Count);

            foreach (KeyValuePair<int, Draft> p in _drafts)
            {
                w.Write(p.Key);
                p.Value.WriteTo(w);
            }

            // gs service

            GamespyService.WriteTo(w);

            // eol

            w.Write(0x12345678);

            // closes the writer and file

            w.Close();

            f.Flush();
            f.Close();
        }

        private void LoadCampaign(long t0)
        {
            Contract.Assert(_lastSavegame != null);

            // tries to open the file and creates a reader

            FileStream f = null;

            try
            {
                f = new FileStream(_lastSavegame + savegameExtension, FileMode.Open, FileAccess.Read);
            }
            catch (Exception e)
            {
                f?.Close();

                LogError("LoadCampaign()", e);

                return;
            }

            BinaryReader r = new(f, Encoding.UTF8, true);
            int c;

            // general

            _savegameState = r.ReadInt32();

            Contract.Assert(_savegameState == save2);

            // server status

            Utils.Read(r, true, out _administrator);
            Utils.Read(r, true, out _nickSuffix);

            _hostName = r.ReadString();
            _earlyMap = r.ReadString();
            _middleMap = r.ReadString();
            _lateMap = r.ReadString();
            _gameType = r.ReadString();
            _maxNumPlayers = r.ReadInt32();
            _numPlayers = r.ReadInt32();
            _maxNumLoggedOnPlayers = r.ReadInt32();
            _numLoggedOnPlayers = r.ReadInt32();
            _raceList = r.ReadUInt32();

            _difficultyLevel = r.ReadInt32();
            _startingEra = r.ReadInt32();

            // server files

            _serverFiles.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
            {
                string filename = r.ReadString();
                uint hash = r.ReadUInt32();

#if RESET_VALIDADATED_CLIENT_FILES
                _serverFiles.Add(filename, 0);
#else
                _serverFiles.Add(filename, hash);
#endif

            }

            // data counter

            _dataCounter = r.ReadInt32();

            // characters

            _lastLogin = r.ReadInt64();

            _logouts.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
                _logouts.Enqueue(r.ReadInt32());

            _ipAddresses.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
                _ipAddresses.Add(r.ReadString(), r.ReadInt32());

            _characterNames.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
                _characterNames.Add(r.ReadString(), r.ReadInt32());

            _characters.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
                _characters.Add(r.ReadInt32(), new Character(r));

            for (int i = 0; i < 4; i++)
                _startingRank[i] = (Ranks)r.ReadInt32();

            for (int i = 0; i < 4; i++)
                _startingPrestige[i] = r.ReadInt32();

            _cpuMovements.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
                _cpuMovements.Add(r.ReadInt32(), r.ReadInt64() + t0);

            _humanMovements.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
                _humanMovements.Add(r.ReadInt32(), r.ReadInt64() + t0);

            Contract.Assert(_humanMovements.Count == 0);

            _cpuMovementDelay = r.ReadInt32();
            _cpuMovementMinRest = r.ReadInt32();
            _cpuMovementMaxRest = r.ReadInt32();

            _humanMovementDelay = r.ReadInt32();

            // map

            _mapName = r.ReadString();

            _mapWidth = r.ReadInt32();
            _mapHeight = r.ReadInt32();

            c = r.ReadInt32();

            _map = new MapHex[c];

            for (int i = 0; i < c; i++)
                _map[i] = new MapHex(r);

            InitializeLocationIncrements();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                c = r.ReadInt32();

                for (int j = 0; j < c; j++)
                    _homeLocations[i][j] = new Location(r);

                for (int j = c; j < maxHomeLocations; j++)
                    _homeLocations[i][j] = null;
            }

            _census.ReadFrom(r);

            // script map

            for (int i = 0; i < 24; i++)
                _terrainContents[i].ReadFrom(r);

            LoadMapTemplates();

            // economy

            _expensesMultiplier = r.ReadDouble();
            _maintenanceMultiplier = r.ReadDouble();
            _productionMultiplier = r.ReadDouble();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _curBudget[i] = r.ReadDouble();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _curExpenses[i] = r.ReadDouble();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _curMaintenance[i] = r.ReadDouble();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _curProduction[i] = r.ReadDouble();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _curPopulation[i] = r.ReadInt32();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _curSize[i] = r.ReadInt32();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                _logBudget[i].Clear();

                c = r.ReadInt32();

                for (int j = 0; j < c; j++)
                    _logBudget[i].Add(r.ReadDouble());

                _logExpenses[i].Clear();

                c = r.ReadInt32();

                for (int j = 0; j < c; j++)
                    _logExpenses[i].Add(r.ReadDouble());

                _logMaintenance[i].Clear();

                c = r.ReadInt32();

                for (int j = 0; j < c; j++)
                    _logMaintenance[i].Add(r.ReadDouble());

                _logProduction[i].Clear();

                c = r.ReadInt32();

                for (int j = 0; j < c; j++)
                    _logProduction[i].Add(r.ReadDouble());

                _logPopulation[i].Clear();

                c = r.ReadInt32();

                for (int j = 0; j < c; j++)
                    _logPopulation[i].Add(r.ReadInt32());

                _logSize[i].Clear();

                c = r.ReadInt32();

                for (int j = 0; j < c; j++)
                    _logSize[i].Add(r.ReadInt32());
            }

            // stardate

            _turn = r.ReadInt32();
            _turnsPerYear = r.ReadInt32();
            _millisecondsPerTurn = r.ReadInt32();

            _earlyYears = r.ReadInt32();
            _middleYears = r.ReadInt32();
            _lateYears = r.ReadInt32();
            _advancedYears = r.ReadInt32();

            _mediumMissileSpeedDate = r.ReadInt32();
            _fastMissileSpeedDate = r.ReadInt32();

            // specs

            _shiplist.Clear();
            _ftrlist.Clear();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _supplyFtrList[i].Clear();

            LoadShiplist();

            Contract.Assert(_missileSizes.Length == 7);

            for (int i = 0; i < 7; i++)
                _missileSizes[i] = r.ReadInt32();

            _sparePartsMultiplier = r.ReadDouble();

            _initialPopulation = r.ReadInt32();
            _populationDistribution = r.ReadInt32();
            _neutralPopulationRatio = r.ReadDouble();

            _theGood = (RaceMasks)r.ReadInt32();
            _theBad = (RaceMasks)r.ReadInt32();
            _theUgly = (RaceMasks)r.ReadInt32();

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
                _alliances[i] = (RaceMasks)r.ReadInt32();

            // ships

            _ships.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
            {
                int shipId = r.ReadInt32();
                Ship ship = new(r);

                if (ship.OwnerID != 0)
                {
                    if (!_characters.TryGetValue(ship.OwnerID, out Character owner))
                        throw new NotSupportedException();
                }

                _ships.Add(shipId, ship);
            }

            for (int i = 0; i < 4; i++)
            {
                _minStartingClass[i] = (ClassTypes)r.ReadInt32();
                _maxStartingClass[i] = (ClassTypes)r.ReadInt32();
            }

            _cpuOfficerRank = (OfficerRanks)r.ReadInt32();
            _cpuPowerBoost = r.ReadDouble();

            for (int i = 0; i < (int)ClassTypes.kMaxClasses; i++)
                _classCostRatio[i] = r.ReadDouble();

            _costRepair = r.ReadDouble();
            _costTradeIn = r.ReadDouble();

            _costMissiles = r.ReadDouble();
            _costShuttles = r.ReadDouble();
            _costMarines = r.ReadDouble();
            _costMines = r.ReadDouble();
            _costSpareParts = r.ReadDouble();

            _cpuAutomaticRepairMultiplier = r.ReadDouble();
            _cpuAutomaticResupplyMultiplier = r.ReadDouble();

            // shipyard

            for (int i = 0; i < (int)Races.kNumberOfRaces; i++)
            {
                _bidItems[i].Clear();

                c = r.ReadInt32();

                for (int j = 0; j < c; j++)
                    _bidItems[i].Add(r.ReadInt32(), new BidItem(r));
            }

            _bidReplacements.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
                _bidReplacements.Add(r.ReadString(), r.ReadInt32());

            _sellingAction = r.ReadInt32();

            for (int i = 0; i < 6; i++)
                _turnsToClose[i] = r.ReadInt32();

            // chat

            _serverNick = r.ReadString();

            // drafts

            _availableMissions.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
                _availableMissions.Add(r.ReadInt32());

            _drafts.Clear();

            c = r.ReadInt32();

            for (int i = 0; i < c; i++)
                _drafts.Add(r.ReadInt32(), new Draft(r));

            // gamespy service

            GamespyService.ReadFrom(r);

            // eol

            if (r.ReadInt32() != 0x12345678)
                throw new NotSupportedException();

            // closes the reader and file

            r.Close();

            f.Flush();
            f.Close();

            // replaces the placeholders in the characters by the real ships

            foreach (var p in _characters)
                p.Value.Update(_ships);

#if DEBUG
            // it is a really bad sign if there is any draft still going on
            // because no players are meant to be online at this point

            Contract.Assert(_clients.IsEmpty);

            if (_drafts.Count > 0)
                DebugDrafts();

            DebugMapCharactersAndShips();
#endif

        }

#if DEBUG
        private void DebugDrafts()
        {
            // tries to restore the indexed data for debugging reasons

            foreach (KeyValuePair<int, Draft> d in _drafts)
            {
                Draft draft = d.Value;

                if (_characters.ContainsKey(draft.Mission.HostId))
                {
                    Debug.WriteLine("draft.Mission.Host.Id = " + draft.Mission.HostId);
                }
                else
                    Debug.WriteLine("draft.Mission.Host.Id = " + draft.Mission.HostId + " (missing)");

                foreach (KeyValuePair<int, Team> t in draft.Mission.Teams)
                {
                    Team team = t.Value;

                    if (_characters.ContainsKey(team.OwnerId))
                    {
                        Debug.WriteLine("\tteam.Owner.Id = " + team.OwnerId);
                    }
                    else
                        Debug.WriteLine("\tteam.Owner.Id = " + team.OwnerId + " (missing)");

                    foreach (KeyValuePair<int, object> s in team.Ships)
                    {
                        int shipId = s.Key;

                        if (_ships.TryGetValue(shipId, out Ship ship))
                        {
                            team.Ships[s.Key] = ship;

                            Debug.WriteLine("\t\tship.Id = " + shipId);
                        }
                        else
                            Debug.WriteLine("\t\tship.Id = " + shipId + " (missing)");
                    }
                }
            }
        }

        private void DebugMapCharactersAndShips()
        {
            int bugs = 0;

            // checks the characters

            Dictionary<int, int> owner = [];

            foreach (KeyValuePair<int, Character> p in _characters)
            {
                Character character = p.Value;

                int c = character.ShipCount;
                int i;

                for (i = 0; i < c; i++)
                {
                    Ship ship = character.GetShipAt(i);

                    if (_ships.ContainsKey(ship.Id))
                    {
                        if (ship.OwnerID == character.Id)
                        {
                            if (ship.Systems.Items[(int)SystemTypes.ExtraDamage] == 0)
                            {
                                bugs++;

                                Contract.Assert(ship.Systems.Items[(int)SystemTypes.ExtraDamageMax] != 0);

                                Debug.WriteLine("_characters[" + character.Id + "].Ships[" + i + "] is invalid (was already destroyed)");
                            }

                            owner.Add(ship.Id, character.Id); // we flag the ship here, temporarily, as being owned by someone
                        }
                        else
                        {
                            bugs++;

                            Debug.WriteLine("_ships[" + ship.Id + "].OwnerID == " + ship.OwnerID + "; // should be " + character.Id);
                        }
                    }
                    else
                    {
                        bugs++;

                        Debug.WriteLine("_characters[" + character.Id + "].Ships[" + i + "] doesn't exist!");
                    }
                }

                if (character.Mission != 0)
                {
                    bugs++;

                    Debug.WriteLine("_characters[" + character.Id + "].Mission = " + (character.Mission & MissionFilter));
                }

                /*
                    if (character.State != Character.States.IsHumanAfkOnline && character.State != Character.States.IsCpuOnline)
                    {
                        bugs++;

                        Debug.WriteLine("_characters[" + character.Id + "].State = " + character.State);
                    }
                */

                i = character.CharacterLocationX + character.CharacterLocationY * _mapWidth;

                if (i >= 0 && i < _map.Length)
                {
                    MapHex hex = _map[i];

                    if (!hex.Population.ContainsKey(character.Id) && character.State != Character.States.IsHuman)
                    {
                        bugs++;

                        Debug.WriteLine("_map[" + i + "].Population[" + character.Id + "] doesn't exist!");
                    }
                }
                else
                {
                    bugs++;

                    Debug.WriteLine("_characters[" + character.Id + "] location is invalid!");
                }
            }

            //Contract.Assert(bugs == 0);

            // checks the map

            bugs += DebugMapPopulation();

            //Contract.Assert(bugs == 0);

            for (int i = 0; i < _map.Length; i++)
            {
                MapHex hex = _map[i];

                if (hex.Mission != 0)
                {
                    bugs++;

                    Debug.WriteLine("_map[" + i + "].Mission = " + (hex.Mission & MissionFilter));
                }
            }

            //Contract.Assert(bugs == 0);

            // checks the ships

            foreach (KeyValuePair<int, Ship> p in _ships)
            {
                Ship ship = p.Value;

                if (!owner.Remove(ship.Id) && ship.IsInAuction == 0)
                {
                    bugs++;

                    Debug.Write("_ships[" + ship.Id + "] of type " + Enum.GetName(ship.ClassType) + " (" + Enum.GetName(ship.Race) + ") doesn't belong to any fleet. ");

                    if (_characters.ContainsKey(ship.OwnerID))
                        Debug.Write("The owner is");
                    else
                        Debug.Write("Last owner was");

                    Debug.WriteLine(" " + ship.OwnerID + ". And the ship has " + ship.Systems.Items[(int)SystemTypes.ExtraDamage] + " of " + ship.Systems.Items[(int)SystemTypes.ExtraDamageMax] + " health remaining.");
                }
            }

            //Contract.Assert(bugs == 0);
        }

        private int DebugMapPopulation()
        {
            MapHex temp = new(true);
            int bugs = 0;

            for (int i = 0; i < _map.Length; i++)
            {
                ClearPopulationCensus(temp.Census);

                MapHex hex = _map[i];

                foreach (KeyValuePair<int, object> p in hex.Population)
                {
                    int characterId = p.Key;

                    if (_characters.TryGetValue(characterId, out Character character))
                    {
                        if (character.CharacterLocationX == hex.X && character.CharacterLocationY == hex.Y)
                            AdjustPopulationCensus(temp.Census, (int)character.CharacterRace, character.ShipCount, character.ShipListBPV);
                        else
                        {
                            bugs++;

                            Debug.WriteLine("_map[" + i + "].Population[" + characterId + "] is in a different location!");
                        }
                    }
                    else
                    {
                        bugs++;

                        Debug.WriteLine("_map[" + i + "].Population[" + characterId + "] doesn't exist!");
                    }
                }

                for (int j = 0; j < (int)Races.kNumberOfRaces; j++)
                {
                    if
                    (
                        hex.Census.RaceCount[j] != temp.Census.RaceCount[j] ||
                        hex.Census.RaceBPV[j] != temp.Census.RaceBPV[j] ||

                        hex.Census.AllyCount[j] != temp.Census.AllyCount[j] ||
                        hex.Census.AllyBPV[j] != temp.Census.AllyBPV[j] ||

                        hex.Census.EnemyCount[j] != temp.Census.EnemyCount[j] ||
                        hex.Census.EnemyBPV[j] != temp.Census.EnemyBPV[j] ||

                        hex.Census.NeutralCount[j] != temp.Census.NeutralCount[j] ||
                        hex.Census.NeutralBPV[j] != temp.Census.NeutralBPV[j]
                    )
                    {
                        bugs++;

                        Debug.WriteLine("_map[" + i + "] statistics doesn't match!");
                    }
                }
            }

            return bugs;
        }
#endif

    }
}
