#pragma warning disable IDE0130

using shrGF;
using shrPcg;
using shrServices;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;

namespace SfcOpServer
{
    public sealed class MapTemplate
    {
        public const string SubPath = "d4v1ks/sfcopserver/templates";

        private const string freeChar = ".";
        private const string emptyChar = " ";
        private const string nebulaChar = "?";
        private const string sunChar = "!";
        private const string planetChar = "+";
        private const string baseChar = "-";
        private const string specialChar = "'";

        private const string objectChars =
            "1234567890" + // neutral planets (PL1 -> PL10)
            "%" +          // worm hole
            "$" +          // fissure
            "~";           // pulsar

        private const string alliedChars = "abcdefghijklmnopqrst"; // allied starting positions
        private const string enemyChars = "ABCDEFGHIJKLMNOPQRST";  // enemy starting positions (including bases and planets)

        /*
            Shared.h

            dll = last - first + 1 + 4
            ini = last - first + 1
            txt = last - first - 1

            |dll|ini|txt|
            | 0 |   |   | "Met_Common",
            | 1 |   |   | "// Met_Common - Map",
            | 2 | 0 |   | " +___+",
            | 3 | 1 | 0 | " |.A.|",
            | 4 | 2 | 1 | " |.?.|",
            | 5 | 3 | 2 | " |.a.|",
            | 6 | 4 |   | " +---+",
            | 7 |   |   | "///",
            | 8 |   |   | NULL
        */

        private const int txtMinSize = 3;
        private const int txtMaxSize = 254;

        // private static variables

        private static readonly clsPcg _rand;
        private static readonly Dictionary<string, string> _icons; // "planet", "8"

        private static readonly bool[] _isObject;
        private static readonly bool[] _canBeCleared;
        private static readonly bool[] _canBeAround;
        private static readonly bool[] _isValid;

        private static readonly char[] _asteroids;
        private static readonly char[] _blackHoles;
        private static readonly char[] _dustClouds;
        private static readonly char[] _ionStorms;
        private static readonly char[] _nebulas;
        private static readonly char[] _suns;

        // public properties

        public string Name => _name;

        public int Width => _width;
        public int Height => _height;

        public bool IsValid => _map != null;

        // ... map overrides

        public string MapOverrides => _mapOverrides.ToString();

        // ... customizations

        public string Background => _background;
        public List<string> Musics => _musics;
        public List<string> Planets => _planets;
        public List<string> Bases => _bases;
        public List<string> Specials => _specials;

        // private variables

        private string _name;

        private int _width;
        private int _height;

        private char[][] _map;

        private char[] _nextPosition;

        // ... positions

        private int _freeCount;
        private int _sunCount;
        private int _alliedCount;
        private int _enemyCount;
        private int _planetCount;
        private int _baseCount;
        private int _specialCount;
        private int _objectCount;

        private uint[] _freePositions;
        private uint[] _sunPositions;
        private uint[] _alliedPositions;
        private ulong[] _enemyPositions;
        private ulong[] _planetPositions;
        private uint[] _basePositions;
        private uint[] _specialPositions;
        private ulong[] _objectPositions;

        // ... map overrides

        private StringBuilder _mapOverrides;

        private Dictionary<char, string> _modelFilenames;
        private Dictionary<char, string> _tacticalMapIcons;

        // ... points of interest

        private string _background;
        private List<string> _musics;
        private List<string> _planets;
        private List<string> _bases;
        private List<string> _specials;

        static MapTemplate()
        {
            Contract.Assert(SubPath.Equals(SubPath.ToLowerInvariant(), StringComparison.Ordinal)); // TrySortTemplate()

            const string canBeClearedChars =
                freeChar +
                planetChar +
                baseChar +
                specialChar;

            const string canBeAroundChars =
                emptyChar +
                nebulaChar +
                alliedChars +
                enemyChars;

            const string validChars =
                sunChar +
                objectChars +
                canBeClearedChars +
                canBeAroundChars;

            int i;

            _rand = new();
            _icons = [];

            for (i = 0; i < (int)MapObjectTypes.kMaxObjects; i++)
            {
                string k = Enum.GetName(typeof(MapObjectTypes), (MapObjectTypes)i)[7..].ToLowerInvariant();
                string v = $"{i}";

                _icons.Add(k, v);
            }

            _isObject = new bool[256];
            _canBeCleared = new bool[256];
            _canBeAround = new bool[256];
            _isValid = new bool[256];

            for (i = 0; i < objectChars.Length; i++)
                _isObject[objectChars[i]] = true;

            for (i = 0; i < canBeClearedChars.Length; i++)
                _canBeCleared[canBeClearedChars[i]] = true;

            for (i = 0; i < canBeAroundChars.Length; i++)
                _canBeAround[canBeAroundChars[i]] = true;

            for (i = 0; i < validChars.Length; i++)
                _isValid[validChars[i]] = true;

            _asteroids = ['[', ']', '*', 'a', 'A'];
            _blackHoles = [':', ',', 'b', 'B'];
            _dustClouds = ['<', '>', 'd', 'D'];
            _ionStorms = ['{'];
            _nebulas = ['&', '?', 'c', 'C'];
            _suns = ['(', ')', '!'];
        }

        public MapTemplate(string filename)
        {
            filename = Utils.NormalizePath(filename);

            if (TryLoadTemplate(filename, out int width, out int height, out byte[][] map))
            {
                if (TrySortTemplate(filename, width, height, map))
                    return;
            }

            _name = null;

            _width = 0;
            _height = 0;

            _map = null;

            _nextPosition = null;

            ClearIndexes();

            _freePositions = null;
            _sunPositions = null;
            _alliedPositions = null;
            _enemyPositions = null;
            _planetPositions = null;
            _basePositions = null;
            _specialPositions = null;
            _objectPositions = null;

            _mapOverrides = null;

            _modelFilenames = null;
            _tacticalMapIcons = null;

            _background = null;
            _musics = null;
            _planets = null;
            _bases = null;
            _specials = null;
        }

        public static void LockRnd(ulong value)
        {
            _rand.Seed(value);
        }

        public static void UnlockRnd()
        {
            _rand.Seed();
        }

        public static int NextInt32(int count)
        {
            return _rand.NextInt32(count);
        }

        public void Update(MapHex hex, TerrainContent[] terrains)
        {
            int i = BitOperations.TrailingZeroCount((uint)hex.TerrainType);

            if (i >= 9 && i <= 11)
                throw new NotSupportedException();

            // makes a copy of the terrain we want to use

            TerrainContent content = terrains[i];

            // checks if we need to sort the free positions back to their original state
            // obviously we need to do this before clearing the indexes  :>

            if (_freeCount != 0)
                Array.Sort(_freePositions);

            // clears the variables we want to use

            ClearMap();
            ClearIndexes();

            // populates the template with the current terrain content
            // (apparently the client can only display 74 icons at once in the minimap so we should be careful when setting it. it includes the ships, planets and suns)

            if (_freePositions.Length != 0)
            {
                double ratio = _freePositions.Length;

                content.Asteroids *= ratio;
                content.DustClouds *= ratio;
                content.IonStorms *= ratio;

                PopulateFreePositionsWith(_asteroids, (int)Math.Truncate(content.Asteroids));
                PopulateFreePositionsWith(_dustClouds, (int)Math.Truncate(content.DustClouds));
                PopulateFreePositionsWith(_ionStorms, (int)Math.Truncate(content.IonStorms));

                PopulateFreePositionsWith(_blackHoles, content.BlackHoles);
            }

            // nebula

            if (content.Nebulas)
            {
                Contract.Assert(_map[1][1] == '.');

                _map[1][1] = _nebulas[_rand.NextInt32(_nebulas.Length)];
            }

            // sun

            if (content.Sun && _sunPositions.Length != 0)
                PopulateSpecificPositionWith(ref _sunCount, _sunPositions, _suns[_rand.NextInt32(_suns.Length)]);

            // objects

            _mapOverrides.Clear();

            for (i = 0; i < _objectPositions.Length; i++)
            {
                GetCoordinates(_objectPositions[_objectCount], out int x1, out int y1, out int x2, out _);

                _objectCount++;

                Contract.Assert(_map[y1][x1] == freeChar[0]);

                char c = (char)x2;

                _map[y1][x1] = c;

                // ... model override

                if (_modelFilenames.TryGetValue(c, out string value))
                {
                    _mapOverrides.Append("PL");

                    if (c == '0')
                        _mapOverrides.Append('1');

                    _mapOverrides.Append(c);
                    _mapOverrides.Append(" = ");
                    _mapOverrides.AppendLine(value);
                }

                // ... icon override

                TryAppendMapOverride(c);
            }

            if (_objectCount != _objectPositions.Length)
                throw new NotSupportedException();
        }

        public void AddAlliedShip()
        {
            PopulateSpecificPositionWith(ref _alliedCount, _alliedPositions, GetAndAdvancePosition());
        }

        public void AddEnemyShip()
        {
            PopulateSpecificPositionWith(ref _enemyCount, _enemyPositions, GetAndAdvancePosition());
        }

        public void AddNeutralShip()
        {
            PopulateFreePositionsWith(_nextPosition, 1);
            GetAndAdvancePosition();
        }

        public void AddPlanet()
        {
            PopulateSpecificPositionWith(ref _planetCount, _planetPositions, GetAndAdvancePosition());
        }

        public void AddBase()
        {
            PopulateSpecificPositionWith(ref _baseCount, _basePositions, GetAndAdvancePosition());
        }

        public void AddSpecial()
        {
            PopulateSpecificPositionWith(ref _specialCount, _specialPositions, GetAndAdvancePosition());
        }

        public ReadOnlySpan<char> GetLine(int index)
        {
            if (index < 0 || index >= _height)
                throw new NotSupportedException();

            return new(_map[index]);
        }

        private bool TryLoadTemplate(string filename, out int width, out int height, out byte[][] map)
        {
            FileStream file = null;
            byte[] buffer = null;

            try
            {
                file = new(filename, FileMode.Open, FileAccess.Read, FileShare.None);

                int length = (int)file.Length;

                if (length == 0)
                    throw new NotSupportedException("the template can't be empty");

                buffer = ArrayPool<byte>.Shared.Rent(length);

                file.ReadExactly(buffer, 0, length);

                ReadOnlySpan<byte> span = new(buffer, 0, length);

                // looks for the [Objects] section

                const string objectsHeader = "[Objects]";

                int start = span.IndexOf("[Objects]"u8);

                if (start == -1)
                    throw new NotSupportedException($"The file doesn't contain any {objectsHeader}!");

                start += objectsHeader.Length;

                while (span[start] == 10 || span[start] == 13)
                    start++;

                span = span[start..];

                // checks the width

                width = 1;

                while (width < span.Length && span[width] != 10 && span[width] != 13)
                    width++;

                if (width < txtMinSize || width > txtMaxSize)
                    throw new NotSupportedException($"The lines must have between {txtMinSize} to {txtMaxSize} chars");

                // tries to read all the lines

                List<ReadOnlyMemory<byte>> lines = [];

                int i = 0;
                int j = width;

                while (true)
                {
                    lines.Add(new ReadOnlyMemory<byte>(buffer, start + i, j - i));

                    while (true)
                    {
                        if (j >= span.Length)
                            goto tryGetHeight;

                        if (span[j] != 10 && span[j] != 13)
                            break;

                        j++;
                    }

                    i = j;

                    while (j < span.Length && span[j] != 10 && span[j] != 13)
                        j++;

                    if (j - i != width)
                        throw new NotSupportedException("All lines must be the same size");
                }

            tryGetHeight:

                height = lines.Count;

                if (height < txtMinSize || height > txtMaxSize)
                    throw new NotSupportedException($"The template must have between {txtMinSize} to {txtMaxSize} lines");

                // initializes the map template

                _name = Path.GetFileNameWithoutExtension(filename);

                _width = width;
                _height = height;

                _map = new char[height][];

                for (int y = 0; y < height; y++)
                    _map[y] = new char[width];

                _nextPosition = new char[1];

                // ... creates the map

                map = new byte[height][];

                for (int y = 0; y < height; y++)
                {
                    map[y] = new byte[width];

                    lines[y].CopyTo(new Memory<byte>(map[y], 0, width));

                    // ... checks the current line

                    for (int x = 0; x < width; x++)
                    {
                        if (!_isValid[map[y][x]])
                            throw new NotSupportedException($"The char at {y}, {x} is invalid");
                    }
                }

                // checks the nebula position

                if (map[1][1] != (byte)nebulaChar[0])
                    throw new NotSupportedException($"The char at 1, 1 must be a '{nebulaChar}'");

                return true;
            }
            catch (Exception)
            {
                width = 0;
                height = 0;

                map = null;

                return false;
            }
            finally
            {
                file?.Dispose();

                if (buffer != null)
                    ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private bool TrySortTemplate(string filename, int width, int height, byte[][] map)
        {
            try
            {
                // sorts the content of the map template

                uint sunPosition = uint.MaxValue;

                SortedDictionary<int, uint> alliedPositions = [];
                SortedDictionary<int, uint> enemyPositions = [];
                SortedDictionary<int, uint> planetPositions = [];
                SortedDictionary<int, uint> basesPositions = [];
                SortedDictionary<int, uint> specialPositions = [];

                Dictionary<int, uint> objectPositions = [];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int c = map[y][x];
                        uint k = (uint)((y << 16) | x);

                        if (c == nebulaChar[0])
                        {
                            if (y != 1 || x != 1)
                                throw new NotSupportedException();
                        }
                        else if (c == sunChar[0])
                        {
                            if (sunPosition != uint.MaxValue)
                                throw new NotSupportedException();

                            sunPosition = k;
                        }
                        else if (c >= alliedChars[0] && c <= alliedChars[^1])
                        {
                            if (alliedPositions.ContainsKey(c))
                                throw new NotSupportedException();

                            alliedPositions.Add(c, k);
                        }
                        else if (c >= enemyChars[0] && c <= enemyChars[^1])
                        {
                            if (enemyPositions.ContainsKey(c) || planetPositions.ContainsKey(c) || basesPositions.ContainsKey(c) || specialPositions.ContainsKey(c))
                                throw new NotSupportedException();

                            if (x < width - 1 && map[y][x + 1] == planetChar[0])
                                planetPositions.Add(c, k);
                            else if (x < width - 1 && map[y][x + 1] == baseChar[0])
                                basesPositions.Add(c, k);
                            else if (x < width - 1 && map[y][x + 1] == specialChar[0])
                                specialPositions.Add(c, k);
                            else
                                enemyPositions.Add(c, k);
                        }
                        else if (_isObject[c])
                            objectPositions.Add(c, k);
                    }
                }

                // checks if we have any starting positions

                if (alliedPositions.Count == 0)
                    throw new NotSupportedException();

                /*
                    // clears the scenario around certain positions

                    if (sunPosition != uint.MaxValue)
                    {
                        GetCoordinates(sunPosition, out int x, out int y);
                        ClearPositions(map, x, y, 4);
                    }

                    ClearPositions(map, alliedPositions, 1);
                    ClearPositions(map, enemyPositions, 1);
                    ClearPositions(map, planetPositions, 3);
                    ClearPositions(map, basesPositions, 2);
                    ClearPositions(map, specialPositions, 1);
                */

                // sorts the free spaces

                List<uint> freeSpaces = [];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        uint k = (uint)((y << 16) | x);

                        if (map[y][x] == (byte)freeChar[0])
                            freeSpaces.Add(k);
                        else if (_canBeCleared[map[y][x]])
                        {
                            map[y][x] = (byte)freeChar[0];

                            freeSpaces.Add(k);
                        }
                    }
                }

                // calculates all the planet end positions (aka their rotation)

                Dictionary<int, uint> planetRotations = [];

                if (planetPositions.Count != 0)
                {
                    foreach (KeyValuePair<int, uint> p in planetPositions)
                    {
                        GetCoordinates(p.Value, out double x1, out double y1);

                        double x2, y2;

                        if (sunPosition != uint.MaxValue)
                            GetCoordinates(sunPosition, out x2, out y2);
                        else
                        {
                            x2 = x1;
                            y2 = y1 + 1;
                        }

                        double angle = NormalizeAngle(GetAngle(x1, y1, x2, y2) - (Math.Tau * 0.25)); // 90º

                        int index = GetClosestIndex(freeSpaces, x1, y1, angle);

#if DEBUG
                        GetCoordinates(freeSpaces[index], out int x, out int y);

                        map[y][x] = (byte)(p.Key + 32);
#endif

                        planetRotations.Add(p.Key, freeSpaces[index]);
                        freeSpaces.RemoveAt(index);
                    }
                }

                // calculates all the enemy end positions

                Dictionary<int, uint> enemyEndPositions = [];

                if (enemyPositions.Count != 0)
                {
                    foreach (KeyValuePair<int, uint> p in enemyPositions)
                    {
                        GetCoordinates(p.Value, out double x1, out double y1);

                        double angle = Math.Tau * 0.25; // 90º

                        int index = GetClosestIndex(freeSpaces, x1, y1, angle);

#if DEBUG
                        GetCoordinates(freeSpaces[index], out int x, out int y);

                        map[y][x] = (byte)p.Key;
#endif

                        enemyEndPositions.Add(p.Key, freeSpaces[index]);
                        freeSpaces.RemoveAt(index);
                    }
                }

                // checks if all the planets are correctly aligned

#if VERBOSE
                StringBuilder s = new();

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                        s.Append((char)map[y][x]);

                    s.AppendLine();
                }

                s.AppendLine();

                //Debug.Write(s.ToString());
#endif

                foreach (KeyValuePair<int, uint> p in planetPositions)
                {
                    GetCoordinates(p.Value, out double x1, out double y1);
                    GetCoordinates(planetRotations[p.Key], out double x2, out double y2);

                    double x3, y3;

                    if (sunPosition != uint.MaxValue)
                        GetCoordinates(sunPosition, out x3, out y3);
                    else
                    {
                        x3 = x1;
                        y3 = y1 + 1;
                    }

                    double bias = NormalizeAngle(GetAngle(x1, y1, x2, y2) - GetAngle(x1, y1, x3, y3));

#if VERBOSE
                    s.Append((char)p.Key);
                    s.Append(" -> ");
                    s.AppendLine(Math.Round(bias, 5, MidpointRounding.AwayFromZero).ToString());
#endif

                    const double expectation = 1.5708;

                    Contract.Assert(bias >= expectation * 0.98 && bias <= expectation * 1.02);
                }

#if VERBOSE
                Debug.WriteLine(s.ToString());
#endif

                // creates the final lists

#pragma warning disable IDE0305
                _freePositions = freeSpaces.ToArray();
#pragma warning restore IDE0305

                Array.Sort(_freePositions); // must be sorted for the process to be repeatable

                if (sunPosition != uint.MaxValue)
                    _sunPositions = [sunPosition];
                else
                    _sunPositions = [];

                InitializePositions(alliedPositions, ref _alliedPositions);
                InitializePositions(enemyPositions, enemyEndPositions, ref _enemyPositions);
                InitializePositions(planetPositions, planetRotations, ref _planetPositions);
                InitializePositions(basesPositions, ref _basePositions);
                InitializePositions(specialPositions, ref _specialPositions);
                InitializePositions(objectPositions, ref _objectPositions);

                // map overrides

                GFFile ini = new();

                _mapOverrides = new(1024);

                _tacticalMapIcons = [];
                _modelFilenames = [];

                Contract.Assert(_background == null);

                _musics = [];
                _planets = [];
                _bases = [];
                _specials = [];

                if (ini.Load(filename))
                {
                    string key, value;

                    for (char i = '0'; i <= '9'; i++)
                    {
                        if (objectPositions.ContainsKey(i))
                        {
                            key = $"{i}";

                            if (ini.TryGetValue("Objects/Icons", key, out value, out _))
                            {
                                key = value.Replace(" ", string.Empty).ToLowerInvariant();

                                if (
                                    _icons.TryGetValue(key, out value) ||
                                    (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int j) && j >= 0 && j < (int)MapObjectTypes.kMaxObjects)
                                )
                                    _tacticalMapIcons.Add(i, value);
                            }

                            key = $"{(i - 38) % 10}";

                            if (ini.TryGetValue("Objects/Names", key, out value, out _))
                                _modelFilenames.Add(i, value);
                        }
                    }

                    if (ini.TryGetValue(string.Empty, "Background", out value, out _))
                    {
                        value = value.ToLowerInvariant();

                        if (!value.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                            value += ".mod";

                        key = filename[..filename.IndexOf($"/{SubPath}/")] + "/assets/models/space/" + value;

                        if (File.Exists(key))
                            _background = value;
                    }

                    for (int i = 0; ini.TryGetValue("Musics", i.ToString(CultureInfo.InvariantCulture), out value, out _); i++)
                        _musics.Add(value);

                    for (int i = 0; ini.TryGetValue("POI/Planets", i.ToString(CultureInfo.InvariantCulture), out value, out _); i++)
                        _planets.Add(value);

                    for (int i = 0; ini.TryGetValue("POI/Bases", i.ToString(CultureInfo.InvariantCulture), out value, out _); i++)
                        _bases.Add(value);

                    for (int i = 0; ini.TryGetValue("POI/Specials", i.ToString(CultureInfo.InvariantCulture), out value, out _); i++)
                        _specials.Add(value);

                    // checks if we have enough positions to allocate all the assets

                    const string notEnough = "Not enough positions to allocate all ";

                    if (_planets.Count > _planetPositions.Length)
                        throw new NotSupportedException($"{notEnough}[planets]");

                    if (_bases.Count > _basePositions.Length)
                        throw new NotSupportedException($"{notEnough}[bases]");

                    if (_specials.Count > _specialPositions.Length)
                        throw new NotSupportedException($"{notEnough}[specials]");
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /*
            private void ClearPositions(byte[][] map, SortedDictionary<int, uint> positions, int r)
            {
                foreach (KeyValuePair<int, uint> p in positions)
                {
                    GetCoordinates(p.Value, out int x, out int y);
                    ClearPositions(map, x, y, r);
                }
            }

            private void ClearPositions(byte[][] map, int x, int y, int r)
            {
                int y1 = y - r;
                int y2 = y + r;

                if (y1 < 0) y1 = 0;
                if (y2 >= _height) y2 = _height - 1;

                int x1 = x - r;
                int x2 = x + r;

                if (x1 < 0) x1 = 0;
                if (x2 >= _width) x2 = _width - 1;

                int cx = x;
                int cy = y;

                for (y = y1; y <= y2; y++)
                {
                    for (x = x1; x <= x2; x++)
                    {
                        if (y != cy || x != cx)
                        {
                            if (_canBeCleared[map[y][x]])
                                map[y][x] = (byte)' ';
                            else if (!_canBeAround[map[y][x]])
                                throw new NotSupportedException("The char at " + y + ", " + x + " is not allowed around a '" + (char)map[cx][cy] + "'");
                        }
                    }
                }
            }
        */

        private static int GetClosestIndex(List<uint> freeSpaces, double x1, double y1, double angle)
        {
            double bias = double.MaxValue;
            double distance = double.MaxValue;
            int index = -1;

            for (int i = 0; i < freeSpaces.Count; i++)
            {
                GetCoordinates(freeSpaces[i], out double x2, out double y2);

                double b = NormalizeAngle(GetAngle(x2, y2, x1, y1) - angle);

                if (bias >= b)
                {
                    double d = GetDistance(x2, y2, x1, y1);

                    if (bias > b || distance > d)
                    {
                        bias = b;
                        distance = d;
                        index = i;
                    }
                }
            }

            if (index == -1)
                throw new NotSupportedException();

            return index;
        }

        private void ClearIndexes()
        {
            _freeCount = 0;
            _sunCount = 0;
            _alliedCount = 0;
            _enemyCount = 0;
            _planetCount = 0;
            _baseCount = 0;
            _specialCount = 0;
            _objectCount = 0;
        }

        private void ClearMap()
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                    _map[y][x] = '.';
            }

            _nextPosition[0] = 'G';
        }

        private void PopulateFreePositionsWith(char[] items, int count)
        {
            if (count <= 0)
                return;

            if (count >= _freePositions.Length - _freeCount)
                throw new NotSupportedException();

            int lastFreePosition = _freePositions.Length - 1;
            int numberOfItems = items.Length;

            for (int i = 0; i < count; i++)
            {
                int index = _rand.NextInt32(_freeCount, lastFreePosition);

                GetCoordinates(_freePositions[index], out int x, out int y);

                // free positions are not really discarded. we just 'shuffle' the data
                // (to repeat this process we must sort the data again)

                if (_freeCount != index)
                    (_freePositions[_freeCount], _freePositions[index]) = (_freePositions[index], _freePositions[_freeCount]);

                _freeCount++;

                char item;

                if (numberOfItems == 1)
                    item = items[0];
                else
                    item = items[_rand.NextInt32(numberOfItems)];

                Contract.Assert(_map[y][x] == '.');

                _map[y][x] = item;
            }
        }

        private void PopulateSpecificPositionWith(ref int count, uint[] positions, char nextPosition)
        {
            GetCoordinates(positions[count], out int x, out int y);

            count++;

            Contract.Assert(_map[y][x] == '.');

            _map[y][x] = nextPosition;
        }

        private void PopulateSpecificPositionWith(ref int count, ulong[] positions, char nextPosition)
        {
            GetCoordinates(positions[count], out int x1, out int y1, out int x2, out int y2);

            count++;

            Contract.Assert(_map[y1][x1] == '.');

            _map[y1][x1] = nextPosition;

            Contract.Assert(_map[y2][x2] == '.');

            _map[y2][x2] = (char)(nextPosition + 32); // AZ -> az
        }

        private char GetAndAdvancePosition()
        {
            char c = _nextPosition[0];

            if (c > 'T')
                throw new NotSupportedException();

            _nextPosition[0]++;

            TryAppendMapOverride(c);

            return c;
        }

        private void TryAppendMapOverride(char c)
        {
            if (_tacticalMapIcons.TryGetValue(c, out string v))
            {
                _mapOverrides.Append(c);
                _mapOverrides.Append(" = ");
                _mapOverrides.AppendLine(v);
            }
        }

        /*
            private static void GetFirstCoordinates(SortedDictionary<int, uint> positions, out double x, out double y)
            {
                GetFirstCoordinates(positions, out int _x, out int _y);

                x = _x;
                y = _y;
            }

            private static void GetFirstCoordinates(SortedDictionary<int, uint> positions, out int x, out int y)
            {
                using SortedDictionary<int, uint>.Enumerator e = positions.GetEnumerator();

                e.MoveNext();

                GetCoordinates(e.Current.Value, out x, out y);
            }
        */

        private static void GetCoordinates(uint position, out double x, out double y)
        {
            GetCoordinates(position, out int _x, out int _y);

            x = _x;
            y = _y;
        }

        private static void GetCoordinates(uint position, out int x, out int y)
        {
            x = (int)(position & 0xffff);
            y = (int)(position >> 16);
        }

        private static void GetCoordinates(ulong position, out int x1, out int y1, out int x2, out int y2)
        {
            x2 = (ushort)position;
            y2 = (ushort)(position >> 16);
            x1 = (ushort)(position >> 32);
            y1 = (ushort)(position >> 48);
        }

        private static double GetAngle(double x1, double y1, double x2, double y2)
        {
            return NormalizeAngle(Math.Atan2(y1 - y2, x1 - x2));
        }

        private static double NormalizeAngle(double a)
        {
            while (a < 0.0)
                a += Math.Tau;

            while (a >= Math.Tau)
                a -= Math.Tau;

            return a;
        }

        private static double GetDistance(double x1, double y1, double x2, double y2)
        {
            double x = x1 - x2;
            double y = y1 - y2;

            return x * x + y * y;
        }

        private static void InitializePositions(SortedDictionary<int, uint> source, ref uint[] destination)
        {
            destination = new uint[source.Count];

            int i = 0;

            foreach (KeyValuePair<int, uint> p in source)
            {
                destination[i] = p.Value;

                i++;
            }
        }

        private static void InitializePositions(SortedDictionary<int, uint> source1, Dictionary<int, uint> source2, ref ulong[] destination)
        {
            destination = new ulong[source1.Count];

            int i = 0;

            foreach (KeyValuePair<int, uint> p in source1)
            {
                Contract.Assert(source2.ContainsKey(p.Key));

                destination[i] = (uint)source2[p.Key] | ((ulong)p.Value << 32);

                i++;
            }
        }

        private static void InitializePositions(Dictionary<int, uint> source, ref ulong[] destination)
        {
            destination = new ulong[source.Count];

            int i = 0;

            foreach (KeyValuePair<int, uint> p in source)
            {
                destination[i] = (uint)p.Key | ((ulong)p.Value << 32);

                i++;
            }
        }
    }
}
