#pragma warning disable IDE0028

using shrServices;

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Text;

namespace SfcOpServer
{
    public partial class GameServer
    {
        private void LoadShiplist()
        {
            SortedDictionary<string, object> d = new(StringComparer.OrdinalIgnoreCase);
            string t;
            string[] a;
            int i, j, k;

            // reads the shiplist

            string path = _root + "Assets/Specs/shiplist.txt";

            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            StreamReader r = new(path, Encoding.ASCII);

            while (!r.EndOfStream)
            {
                t = r.ReadLine();

                if (t.Length == 0 || t.StartsWith('\t') || t.StartsWith("Race", StringComparison.Ordinal))
                    continue;

                a = t.Split('\t', StringSplitOptions.None);

                ShipData data = new()
                {
                    Race = (Races)GetIndex(a[0], _races, StringComparison.OrdinalIgnoreCase, false),
                    HullType = (HullTypes)(GetIndex(a[1], _hullTypes, StringComparison.Ordinal, false)),
                    ClassName = a[2],
                    ClassType = (ClassTypes)(GetIndex(a[3], _classTypes, StringComparison.OrdinalIgnoreCase, false) - 1),
                    BPV = GetInteger(a[4]),
                    SpecialRole = (SpecialRoles)GetIndex(a[5], _specialRoles, StringComparison.Ordinal, false),
                    YearFirstAvailable = GetInteger(a[6]),
                    YearLastAvailable = GetInteger(a[7]),
                    SizeClass = GetInteger(a[8]),
                    TurnMode = a[9],
                    MoveCost = GetFloat(a[10]),
                    HetAndNimble = GetInteger(a[11]),
                    HetBreakdown = GetInteger(a[12]),
                    StealthOrECM = GetInteger(a[13]),
                    RegularCrew = GetFloat(a[14]),
                    BoardingPartiesBase = GetInteger(a[15]),
                    BoardingPartiesMax = GetInteger(a[16]),
                    DeckCrews = GetInteger(a[17]),
                    TotalCrew = GetFloat(a[18]),
                    MinCrew = GetInteger(a[19]),
                    Shield1 = GetInteger(a[20]),
                    Shield2And6 = GetInteger(a[21]),
                    Shield3And5 = GetInteger(a[22]),
                    Shield4 = GetInteger(a[23]),
                    ShieldTotal = GetInteger(a[24]),
                    Cloak = GetInteger(a[25]),

                    // weapons (26 - 100)

                    Probes = GetInteger(a[101]),
                    T_BombsBase = GetInteger(a[102]),
                    T_BombsMax = GetInteger(a[103]),
                    NuclearMineBase = GetInteger(a[104]),
                    NuclearMineMax = GetInteger(a[105]),
                    DroneControl = GetInteger(a[106]),
                    ADD_6 = GetInteger(a[107]),
                    ADD_12 = GetInteger(a[108]),
                    ShuttlesSize = GetInteger(a[109]),
                    LaunchRate = GetInteger(a[110]),
                    GeneralBase = GetInteger(a[111]),
                    GeneralMax = GetInteger(a[112]),
                    FighterBay1 = GetInteger(a[113]),
                    FighterType1 = a[114],
                    FighterBay2 = GetInteger(a[115]),
                    FighterType2 = a[116],
                    FighterBay3 = GetInteger(a[117]),
                    FighterType3 = a[118],
                    FighterBay4 = GetInteger(a[119]),
                    FighterType4 = a[120],
                    Armor = GetInteger(a[121]),
                    ForwardHull = GetInteger(a[122]),
                    CenterHull = GetInteger(a[123]),
                    AftHull = GetInteger(a[124]),
                    Cargo = GetInteger(a[125]),
                    Barracks = GetInteger(a[126]),
                    Repair = GetInteger(a[127]),
                    R_L_Warp = GetInteger(a[128]),
                    C_Warp = GetInteger(a[129]),
                    Impulse = GetInteger(a[130]),
                    Apr = GetInteger(a[131]),
                    Battery = GetInteger(a[132]),
                    Bridge = GetInteger(a[133]),
                    Security = GetInteger(a[134]),
                    Lab = GetInteger(a[135]),
                    Transporters = GetInteger(a[136]),
                    Tractors = GetInteger(a[137]),
                    MechTractors = GetInteger(a[138]),
                    SpecialSensors = GetInteger(a[139]),
                    Sensors = GetInteger(a[140]),
                    Scanners = GetInteger(a[141]),
                    ExplosionStrength = GetInteger(a[142]),
                    Acceleration = GetInteger(a[143]),
                    DamageControl = GetInteger(a[144]),
                    ExtraDamage = GetInteger(a[145]),
                    ShipCost = GetInteger(a[146]),
                    RefitBaseClass = a[147],
                    Geometry = a[148],
                    UI = a[149],
                    FullName = a[150],
                    Refits = a[151],
                    Balance = GetInteger(a[152])
                };

                for (i = 0, j = 26; i < 25; i++, j += 3)
                {
                    data.Weapons[i].Num = GetInteger(a[j]);
                    data.Weapons[i].Type = (WeaponTypes)(GetIndex(a[j + 1], _weaponTypes, StringComparison.OrdinalIgnoreCase, false) - 1);
                    data.Weapons[i].Arc = (WeaponArcs)(GetIndex(a[j + 2], _weaponArcs, StringComparison.OrdinalIgnoreCase, false) - 1);
                }

                // checks if the data is valid

                static void ThrowError(ShipData data, string msg)
                {
                    throw new NotSupportedException($"'{data.ClassName}' has an unsupported '{msg}'.");
                }

                if (data.HullType > HullTypes.kFighter && data.HullType != HullTypes.kHullPlanet)
                    ThrowError(data, "Hull Type");

                if (data.ClassName.StartsWith("O-", StringComparison.OrdinalIgnoreCase))
                    ThrowError(data, "Class names starting with 'O-' are not supported");

                if ((int)data.SpecialRole >= 0)
                    data.SpecialRole = (SpecialRoles)(1 << (int)data.SpecialRole);
                else
                    ThrowError(data, "Special Role");

                if (data.YearFirstAvailable > data.YearLastAvailable)
                    ThrowError(data, "Year First\\Last Available");

                if (
                    ((data.ClassType < ClassTypes.kClassPlanets || data.ClassType > ClassTypes.kClassSpecial) && (data.SizeClass < 1 || data.SizeClass > 6)) ||
                    (data.ClassType == ClassTypes.kClassPlanets && data.SizeClass != 0) ||
                    (data.ClassType == ClassTypes.kClassSpecial && (data.SizeClass < 0 || data.SizeClass > 6))
                )
                    ThrowError(data, "Size Class");

                if (data.Shield1 + (data.Shield2And6 + data.Shield3And5 << 1) + data.Shield4 != data.ShieldTotal)
                    ThrowError(data, "Shield 1\\2_6\\3_5\\4\\Total");

                if (!IsGeometryValid(data.Geometry))
                    ThrowError(data, "Geometry");

                if (data.UI.Length == 0)
                    ThrowError(data, "UI");

                // adds the data sorted by <Race>, <ClassType>, <BPV> and <ClassName>

                t = ((int)data.Race).ToString("D2", CultureInfo.InvariantCulture) +
                    ((int)data.ClassType).ToString("D2", CultureInfo.InvariantCulture) +
                    data.BPV.ToString("D5", CultureInfo.InvariantCulture) +
                    data.ClassName;

                d.Add(t, data);
            }

            r.Close();

            // creates the ship list, using <ClassName> as key

            Contract.Assert(_shiplist.Count == 0);

            foreach (KeyValuePair<string, object> p in d)
            {
                ShipData data = (ShipData)p.Value;

                _shiplist.Add(data.ClassName, data);
            }

            // reads the ftrlist

            path = _root + "Assets/Specs/ftrlist.txt";

            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            d.Clear();

            r = new StreamReader(path, Encoding.ASCII);

            while (!r.EndOfStream)
            {
                t = r.ReadLine();

                if (t.Length == 0 || t.StartsWith('\t') || t.StartsWith("Race", StringComparison.Ordinal))
                    continue;

                a = t.Split('\t', StringSplitOptions.None);

                FighterData data = new()
                {
                    Race = (Races)GetIndex(a[0], _races, StringComparison.OrdinalIgnoreCase, false),
                    HullType = a[1],
                    Speed = GetInteger(a[2]),

                    // weapons (3 - 22)

                    Damage = GetInteger(a[23]),
                    ADD_6 = GetInteger(a[24]),
                    GroundAttackBonus = GetInteger(a[25]),
                    ECM = GetInteger(a[26]),
                    ECCM = GetInteger(a[27]),
                    BPV = GetInteger(a[28]),
                    CarrierSizeClass = GetInteger(a[29]),
                    FirstYearAvailable = GetInteger(a[30]),
                    LastYearAvailable = GetInteger(a[31]),
                    Size = GetInteger(a[32]),
                    UI = a[33],
                    Geometry = a[34]
                };

                for (i = 0, j = 3; i < 5; i++, j += 4)
                {
                    data.Weapons[i].Num = GetInteger(a[j]);
                    data.Weapons[i].Type = (WeaponTypes)(GetIndex(a[j + 1], _weaponTypes, StringComparison.OrdinalIgnoreCase, false) - 1);
                    data.Weapons[i].Arc = (WeaponArcs)(GetIndex(a[j + 2], _weaponArcs, StringComparison.OrdinalIgnoreCase, false) - 1);
                    data.Weapons[i].Shots = GetInteger(a[j + 3]);
                }

                // checks if the data is valid

                static void ThrowError(FighterData data, string msg)
                {
                    throw new NotSupportedException($"'{data.HullType}' has an unsupported '{msg}'.");
                }

                if (data.Race == Races.kNoRace)
                    ThrowError(data, "Race");

                t = $"{_raceAbbreviations[(int)data.Race]}-SH";

                if (!data.HullType.Equals(t, StringComparison.OrdinalIgnoreCase) && data.HullType.Length < 7)
                    ThrowError(data, "HullType (length must be >= 7)"); // the 'squadrons' will not be spawned otherwise

                if (data.UI.Length == 0)
                    ThrowError(data, "UI");

                if (!IsGeometryValid(data.Geometry))
                    ThrowError(data, "Geometry");

                // sorts the fighters by <Race>, the <BPV> reversed and <HullType>

                t = ((int)data.Race).ToString("D2", CultureInfo.InvariantCulture) +
                    (99999 - data.BPV).ToString("D5", CultureInfo.InvariantCulture) +
                    data.HullType;

                d.Add(t, data);
            }

            r.Close();

            // creates the fighter list, using <HullType> as key

            Contract.Assert(_ftrlist.Count == 0);

            foreach (KeyValuePair<string, object> p in d)
            {
                FighterData data = (FighterData)p.Value;

                _ftrlist.Add(data.HullType, data);
            }

            // creates the pseudo\fighter lists of each race (as they are used in 'AVtShipRelay')

            Span<long> totalBpv = stackalloc long[(int)ClassTypes.kMaxClasses];
            Span<int> totalElements = stackalloc int[(int)ClassTypes.kMaxClasses];

            foreach (KeyValuePair<string, ShipData> p in _shiplist)
            {
                ShipData ship = p.Value;

                if (ship.ClassType == ClassTypes.kClassPseudoFighter)
                    _supplyFtrList[(int)ship.Race].Add(ship.ClassName, ship.BPV);

                totalBpv[(int)ship.ClassType] += ship.BPV;
                totalElements[(int)ship.ClassType]++;
            }

            foreach (KeyValuePair<string, FighterData> p in _ftrlist)
            {
                FighterData fighter = p.Value;

                _supplyFtrList[(int)fighter.Race].Add(fighter.HullType, fighter.BPV);

                totalBpv[(int)ClassTypes.kClassShuttle] += fighter.BPV;
                totalElements[(int)ClassTypes.kClassShuttle]++;
            }

            // ... calculates the average bpv of each class

            for (i = 0; i < (int)ClassTypes.kMaxClasses; i++)
            {
                if (totalElements[i] > 0)
                    _classAverageBpv[i] = Math.Round((double)totalBpv[i] / totalElements[i], 2, MidpointRounding.AwayFromZero);
                else
                    _classAverageBpv[i] = 0.0;
            }

            // ... calculates the cost ratio of each class

            for (i = (int)ClassTypes.kClassFreighter; i < (int)ClassTypes.kMaxClasses; i++)
            {
                if (_classAverageBpv[i] > 0.0)
                    _classCostRatio[i] = Math.Round(_classCostRatio[i] * _costShuttles / _classAverageBpv[i], 2, MidpointRounding.AwayFromZero);
            }

            // ... does a sanity check of the prices

            foreach (KeyValuePair<string, ShipData> p in _shiplist)
            {
                ShipData ship = p.Value;

                if (ship.ClassType != ClassTypes.kClassPseudoFighter)
                {
                    double shipCost = GetShipCost(ship.ClassType, ship.BPV);

                    // tests the trade in value (0% damage)

                    double value = GetShipTradeInCost(ship.ClassType, ship.BPV);

                    if (value >= shipCost)
                        throw new NotSupportedException();

                    // tests the repair cost (100% damage)

                    value = GetShipRepairCost(ship.ClassType, ship.BPV, 1.0);

                    if (value >= shipCost)
                        throw new NotSupportedException();
                }
            }
        }

        private static int GetIndex(string key, string[] keys, StringComparison comparisonType, bool failSoft)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] != null && keys[i].Equals(key, comparisonType))
                    return i;
            }

            if (failSoft)
                return -1;

            throw new NotSupportedException();
        }

        private static int GetInteger(string t)
        {
            if (t.Length == 0)
                return 0;

            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                return result;

            throw new NotSupportedException();
        }

        private static float GetFloat(string t)
        {
            if (t.Length == 0)
                return 0f;

            if (float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;

            throw new NotSupportedException();
        }

        private bool IsGeometryValid(string t)
        {
            if (t.Length == 0)
                return false;

            t = Utils.LowerCasePath(t);

            if (t.EndsWith(".mod", StringComparison.Ordinal))
            {
                t = $"{_root}{t}";

                return File.Exists(t);
            }

            // ./a/1

            int i = t.LastIndexOf('/');

            if (i < 3 || i > t.Length - 2 || !int.TryParse(t[(i + 1)..], out int j) || j <= 0)
                return false;

            int k = t.LastIndexOf('/', i - 1);

            if (k <= 0)
                return false;

            t = $"{_root}{t[..i]}{t[k..i]}";

            while (j > 0)
            {
                string p = $"{t}{j}.mod";

                if (!File.Exists(p))
                    return false;

                j--;
            }

            return true;
        }

        private static void WriteShipData(BinaryWriter w, int value)
        {
            if (value >= 0 && value <= 127)
                w.Write((ushort)(value | value << 8));
            else
                throw new NotSupportedException();
        }

        private void WriteShipData(BinaryWriter w, int droneCount, WeaponTypes droneType, ref int totalTubes, ref int totalMissiles)
        {
            if (droneCount > 0 && _droneCapacities.TryGetValue(droneType, out int droneCapacity))
            {
                // default AI values

                const int defaultReloads = 1;
                const int defaultMissilesSize = 1;

                int missilesStored = droneCapacity * droneCount * (1 + defaultReloads) / defaultMissilesSize;

                totalTubes += droneCount;
                totalMissiles += missilesStored;

                // 0000 0800 0100 0400

                w.Write((ushort)0x00); // missilesReady
                w.Write((ushort)missilesStored);
                w.Write((ushort)droneCount);
                w.Write((ushort)droneCapacity);
            }
            else if ((droneCount > 0 && droneType != WeaponTypes.None) || (droneCount == 0 && droneType == WeaponTypes.None))
                w.Write((ulong)0x00);
            else
                throw new NotSupportedException();
        }

        private static void WriteShipData(BinaryWriter w, int maxValue, int baseValue)
        {
            if (maxValue <= 127 && baseValue >= 0 && maxValue >= baseValue)
            {
                w.Write((byte)maxValue);
                w.Write((byte)baseValue);
                w.Write((byte)baseValue);
            }
            else
                throw new NotSupportedException();
        }

        private void WriteShipData(BinaryWriter w, int fighterBay, string fighterType)
        {
            if (fighterBay == 0 && fighterType.Length == 0)
            {
                w.Write(0);
                w.Write((ulong)0);
            }
            else if (fighterBay > 0 && (_shiplist.ContainsKey(fighterType) || _ftrlist.ContainsKey(fighterType)))
            {
                w.Write(fighterBay | (fighterBay << 8) | (fighterBay << 16));

                // type

                Utils.Write(w, false, fighterType);

                // sub type

                w.Write(0);
            }
            else
                throw new NotSupportedException();
        }

        private void ClassifyPlanetsAndBases()
        {
            static void ThrowError(ShipData data)
            {
                throw new NotSupportedException($"'{data.ClassName}' failed the classification process.");
            }

            string t;
            int i;

            foreach (KeyValuePair<string, ShipData> p in _shiplist)
            {
                ShipData data = p.Value;

                switch (data.ClassType)
                {
                    case ClassTypes.kClassListeningPost:
                        if (data.HullType != HullTypes.kHullListeningPost)
                            ThrowError(data);

                        _listeningPosts[(int)data.Race].Add(data);

                        break;

                    case ClassTypes.kClassBaseStation:
                        if (data.HullType != HullTypes.kBS)
                            ThrowError(data);

                        _baseStations[(int)data.Race].Add(data);

                        break;

                    case ClassTypes.kClassBattleStation:
                        if (data.HullType != HullTypes.kBT)
                            ThrowError(data);

                        _battleStations[(int)data.Race].Add(data);

                        break;

                    case ClassTypes.kClassStarBase:
                        if (data.HullType != HullTypes.kSB)
                            ThrowError(data);

                        _starbases[(int)data.Race].Add(data);

                        break;

                    case ClassTypes.kClassPlanets:
                        if (data.HullType != HullTypes.kHullPlanet)
                            ThrowError(data);

                        t = data.FullName;
                        i = t.IndexOf('|');

                        if (i > 0)
                            t = t[..i];

                        t = t.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();

                        switch (t)
                        {
                            // home worlds

                            case "hw":
                            case "hw1":
                            case "homeworld":
                            case "homeworld1":
                                _homeWorlds[(int)data.Race][0].Add(data);
                                break;

                            case "hw2":
                            case "homeworld2":
                                _homeWorlds[(int)data.Race][1].Add(data);
                                break;

                            case "hw3":
                            case "homeworld3":
                                _homeWorlds[(int)data.Race][2].Add(data);
                                break;

                            // core worlds

                            case "cw":
                            case "cw1":
                            case "coreworld":
                            case "coreworld1":
                                _coreWorlds[(int)data.Race][0].Add(data);
                                break;

                            case "cw2":
                            case "coreworld2":
                                _coreWorlds[(int)data.Race][1].Add(data);
                                break;

                            case "cw3":
                            case "coreworld3":
                                _coreWorlds[(int)data.Race][2].Add(data);
                                break;

                            // colonies

                            case "col":
                            case "col1":
                            case "colony":
                            case "colony1":
                                _colonies[(int)data.Race][0].Add(data);
                                break;

                            case "col2":
                            case "colony2":
                                _colonies[(int)data.Race][1].Add(data);
                                break;

                            case "col3":
                            case "colony3":
                                _colonies[(int)data.Race][2].Add(data);
                                break;

                            // uninhabited worlds

                            case "uw":
                            case "uw1":
                            case "uninhabitedworld":
                            case "uninhabitedworld1":
                                _orbitalStations[(int)data.Race][0].Add(data);
                                break;

                            case "uw2":
                            case "uninhabitedworld2":
                                _orbitalStations[(int)data.Race][1].Add(data);
                                break;

                            case "uw3":
                            case "uninhabitedworld3":
                                _orbitalStations[(int)data.Race][2].Add(data);
                                break;

                            default:
                                ThrowError(data);
                                break;
                        }

                        break;

                    case ClassTypes.kClassSpecial:
                        t = data.FullName.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();

                        switch (data.HullType)
                        {
                            case HullTypes.kHullDefensePlatform:
                                _weaponPlatforms[(int)data.Race].Add(data);
                                break;

                            case HullTypes.kHullAsteroidBase: // mining station
                            case HullTypes.kHullStarDock:     // fleet repair dock
                            case HullTypes.kBox:              // repair box
                            case HullTypes.kMineHull:         // player buoy
                                break;

                            default:
                                ThrowError(data);
                                break;
                        }

                        break;
                }
            }
        }

        private void CopyShipData(ShipData data, out Ship ship)
        {
            Rent(2048, out byte[] b, out MemoryStream m, out BinaryWriter w, out BinaryReader r);

            // ----------------------------------------------------------------------------------------------------------------------------------------------------
            // header

            ship = new()
            {
                Id = GetNextDataId(),
                Race = data.Race,
                ClassType = data.ClassType,
                BPV = data.BPV,
                EPV = data.BPV,
                ShipClassName = data.ClassName
            };

            // ----------------------------------------------------------------------------------------------------------------------------------------------------
            // damage chunk

            WriteShipData(w, data.R_L_Warp);
            WriteShipData(w, data.R_L_Warp);
            WriteShipData(w, data.C_Warp);
            WriteShipData(w, data.Impulse);
            WriteShipData(w, data.Apr);
            WriteShipData(w, data.Bridge);
            WriteShipData(w, data.Sensors);
            WriteShipData(w, data.Scanners);
            WriteShipData(w, data.DamageControl);
            WriteShipData(w, data.Repair);
            WriteShipData(w, data.ForwardHull);
            WriteShipData(w, data.AftHull);
            WriteShipData(w, data.CenterHull);
            WriteShipData(w, data.Tractors);
            WriteShipData(w, data.ExtraDamage);
            WriteShipData(w, data.Transporters);
            WriteShipData(w, data.Transporters);
            WriteShipData(w, data.Battery);
            WriteShipData(w, data.Lab);
            WriteShipData(w, data.Cargo);
            WriteShipData(w, data.Armor);
            WriteShipData(w, data.Cloak);
            WriteShipData(w, data.DamageControl);
            WriteShipData(w, data.Probes);
            WriteShipData(w, data.Barracks);

            for (int i = 0; i < 25; i++)
                WriteShipData(w, data.Weapons[i].Num);

            // reads the result

            m.Seek(0L, SeekOrigin.Begin);

            ship.Systems = new ShipSystems(r);

            m.Seek(0L, SeekOrigin.Begin);

            // 0

            w.Write((ushort)0x0101);

            w.Write(0x00);
            w.Write((ulong)0x00);

            // 12

            int droneCount = 0;
            int droneAmmo = 0;

            for (int i = 0; i < 25; i++)
                WriteShipData(w, data.Weapons[i].Num, data.Weapons[i].Type, ref droneCount, ref droneAmmo);

            Contract.Assert(m.Position == 214L);

            if (droneCount > 0)
            {
                w.Seek(2, SeekOrigin.Begin);

                // 2

                w.Write(0x_0001_00_00);      // type 1, slow, one

                w.Write((ushort)droneCount); // total hardpoints
                w.Write(droneAmmo);          // total ammo loaded and used
                w.Write((ushort)droneAmmo);  // total ammo remaining

                w.Seek(214, SeekOrigin.Begin);
            }

            // 214

            WriteShipData(w, data.ShuttlesSize, data.ShuttlesSize);

            WriteShipData(w, 0, 0); // unknown3
            WriteShipData(w, 0, 0); // unknown4
            WriteShipData(w, 0, 0); // unknown5

            w.Write(0x00); // transporter items

            // 230

            Contract.Assert(m.Position == 230L);

            for (int i = 0; i < 25; i++)
            {
                w.Write((short)WeaponStates.Uninitialized);
                w.Write((short)WeaponArcs.Uninitialized);
            }

            // 330

            Contract.Assert(m.Position == 330L);

            WriteShipData(w, data.BoardingPartiesMax, data.BoardingPartiesBase);
            WriteShipData(w, data.T_BombsMax, data.T_BombsBase);

            int defaultSpareParts = (int)Math.Round(data.DamageControl * _sparePartsMultiplier, MidpointRounding.AwayFromZero);

            WriteShipData(w, defaultSpareParts, defaultSpareParts);

            // 339

            WriteShipData(w, data.FighterBay1, data.FighterType1);
            WriteShipData(w, data.FighterBay2, data.FighterType2);
            WriteShipData(w, data.FighterBay3, data.FighterType3);
            WriteShipData(w, data.FighterBay4, data.FighterType4);

            // reads the result

            m.Seek(0L, SeekOrigin.Begin);

            ship.Stores = new ShipStores(r);

            // ----------------------------------------------------------------------------------------------------------------------------------------------------
            // Officers chunk

            m.Seek(0L, SeekOrigin.Begin);

            for (int i = 0; i < (int)OfficerTypes.kMaxOfficers; i++)
            {
                const int rank = (int)OfficerRanks.kSenior;

                string name = Enum.GetName(typeof(OfficerTypes), i);

                Utils.Write(w, false, name[1..]);

                w.Write(rank);
                w.Write(0x00);
                w.Write(_officerDefaults[rank]);
            }

            // reads the result

            m.Seek(0L, SeekOrigin.Begin);

            ship.Officers = new ShipOfficers(r);

            // ----------------------------------------------------------------------------------------------------------------------------------------------------

            Return(b, m, w, r);
        }

        private bool GetShipData(Races race, ClassTypes minClassType, ClassTypes maxClassType, int minBPV, int maxBPV, int yearAvailable, out ShipData shipData)
        {
            List<ShipData> list = [];

            foreach (KeyValuePair<string, ShipData> p in _shiplist)
            {
                ShipData data = p.Value;

                if (
                    data.Race == race &&
                    data.ClassType >= minClassType && data.ClassType <= maxClassType &&
                    data.BPV >= minBPV && data.BPV <= maxBPV &&
                    (data.SpecialRole & SpecialRoles.Ignored) == 0 &&
                    data.YearFirstAvailable <= yearAvailable && data.YearLastAvailable >= yearAvailable
                )
                    list.Add(data);
            }

            int c = list.Count;

            if (c > 0)
            {
                shipData = list[_rand.NextInt32(c)];

                return true;
            }

            shipData = null;

            return false;
        }
    }
}
