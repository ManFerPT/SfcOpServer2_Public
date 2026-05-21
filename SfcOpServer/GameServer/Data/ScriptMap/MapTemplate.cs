#pragma warning disable IDE0057, IDE0130

using shrGF;
using shrPcg;
using shrServices;

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SfcOpServer
{
    public sealed class MapTemplate
    {
        private enum Entities
        {
            Planets,
            Bases,
            Specials,
            Allied,
            Enemy,
            Neutral,

            Total
        }

        private readonly struct Info
        {
            public readonly int X1;
            public readonly int Y1;

            public readonly int X2;
            public readonly int Y2;

            public bool ContainsStart => X1 >= 0 && Y1 >= 0;
            public bool ContainsEnd => X2 >= 0 && Y2 >= 0;

            public Info()
            {
                X1 = -1;
                Y1 = -1;

                X2 = -1;
                Y2 = -1;
            }

            public Info(int x1, int y1)
            {
                X1 = x1;
                Y1 = y1;

                X2 = -1;
                Y2 = -1;
            }

            public Info(int x1, int y1, int x2, int y2)
            {
                X1 = x1;
                Y1 = y1;

                X2 = x2;
                Y2 = y2;
            }
        }

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

        public const string SubPath = "d4v1ks/sfcopserver/templates";

        // ... object chars

        private const char objectChar0 = '0';
        private const char objectChar1 = '9';

        private const char wormChar = '%';
        private const char fissureChar = '$';
        private const char pulsarChar = '~';

        // ... valid chars

        private const char startChar0 = 'G';
        private const char startChar1 = 'Z';

        private const char endChar0 = 'g';
        private const char endChar1 = 'z';

        private const char blackHoleChar0 = 'A';
        private const char blackHoleChar1 = 'C';

        private const char emptyChar = ' ';
        private const char freeChar = '.';
        private const char nebulaChar = '?';
        private const char sunChar = '!';

        // private static variables

        private static readonly clsPcg _rand;
        private static readonly Dictionary<string, string> _icons; // "planet", "8"

        private static readonly byte[] _isObject;
        private static readonly byte[] _isValid;

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

        public string MapOverrides => _mapOverrides.ToString();

        public string Background => _background;
        public List<string> Musics => _musics;

        public List<string> Planets => _planets;
        public List<string> Bases => _bases;
        public List<string> Specials => _specials;

        // private variables

        private readonly string _name;

        private readonly int _width;
        private readonly int _height;

        private readonly Info _sunInfo;
        private readonly Info _nebulaInfo;
        private readonly List<Info> _freeInfo;
        private readonly Dictionary<char, List<Info>> _objectInfo;
        private readonly SortedDictionary<char, Info> _blackHoleInfo;
        private readonly SortedDictionary<char, Info>[] _entityInfo;

        private readonly Dictionary<char, string> _modelFilenames;
        private readonly Dictionary<char, string> _tacticalMapIcons;
        private readonly StringBuilder _mapOverrides;

        private readonly string _background;
        private readonly List<string> _musics;

        private readonly List<string> _planets;
        private readonly List<string> _bases;
        private readonly List<string> _specials;

        private readonly PriorityQueue<Info, ulong> _freeQueue;
        private readonly Queue<Info> _planetQueue;
        private readonly Queue<Info> _baseQueue;
        private readonly Queue<Info> _specialQueue;
        private readonly Queue<Info> _alliedQueue;
        private readonly Queue<Info> _enemyQueue;
        private readonly Queue<Info> _neutralQueue;

        private readonly char[][] _map;
        private char _position;

        static MapTemplate()
        {
            Contract.Assert(SubPath.Equals(Utils.NormalizePath(SubPath), StringComparison.Ordinal));
            Contract.Assert(blackHoleChar1 - blackHoleChar0 + 1 == TerrainContent.MaxBlackHoles);

            _rand = new();

            Dictionary<string, string> icons = _icons = new(StringComparer.OrdinalIgnoreCase);
            int i;

            for (i = 0; i < (int)MapObjectTypes.kMaxObjects; i++)
                icons.Add(Enum.GetName(typeof(MapObjectTypes), (MapObjectTypes)i).Substring(7), i.ToString(CultureInfo.InvariantCulture));

            // ... object chars

            _isObject = new byte[256];

            for (i = objectChar0; i <= objectChar1; i++)
                _isObject[i] = 1;

            _isObject[wormChar] = 1;
            _isObject[fissureChar] = 1;
            _isObject[pulsarChar] = 1;

            // ... valid chars

            _isValid = new byte[256];

            Unsafe.CopyBlock(ref MemoryMarshal.GetArrayDataReference(_isValid), ref MemoryMarshal.GetArrayDataReference(_isObject), 256u);

            for (i = startChar0; i <= startChar1; i++)
                _isValid[i] = 1;

            for (i = endChar0; i <= endChar1; i++)
                _isValid[i] = 1;

            for (i = blackHoleChar0; i <= blackHoleChar1; i++)
                _isValid[i] = 1;

            _isValid[emptyChar] = 1;
            _isValid[freeChar] = 1;
            _isValid[nebulaChar] = 1;
            _isValid[sunChar] = 1;

            _asteroids = ['[', ']', '*', 'a', 'A'];
            _blackHoles = [':', ',', 'b', 'B'];
            _dustClouds = ['<', '>', 'd', 'D'];
            _ionStorms = ['{'];
            _nebulas = ['&', '?', 'c', 'C'];
            _suns = ['(', ')', '!'];
        }

        public MapTemplate(string filename)
        {
            try
            {
                filename = Utils.NormalizePath(filename);

                // tries to read the file

                byte[] b = File.ReadAllBytes(filename);

                // tries to find the header

                ReadOnlySpan<byte> s = new(b);
                int i = s.IndexOf("[Objects]\r\n"u8);

                if (i == -1)
                    throw new NotSupportedException("The file doesn't contain any [Objects]!");

                s = s.Slice(i + 11);

                // tries to get the width

                const int minSize = 3;
                const int maxSize = 254;

                int width = s.IndexOf("\r\n"u8);

                if (width < minSize || width > maxSize)
                    throw new NotSupportedException($"The lines must have between {minSize} to {maxSize} chars");

                // tries to get the height

                int height = s.Length / (width + 2);

                if (height < minSize || height > maxSize)
                    throw new NotSupportedException($"The template must have between {minSize} to {maxSize} lines");

                // initializes the template

                _name = Path.GetFileNameWithoutExtension(filename);

                _width = width;
                _height = height;

                _sunInfo = new();
                _nebulaInfo = new();
                _freeInfo = [];
                _objectInfo = [];
                _blackHoleInfo = [];
                _entityInfo = new SortedDictionary<char, Info>[(int)Entities.Total];

                for (i = 0; i < (int)Entities.Total; i++)
                    _entityInfo[i] = [];

                _modelFilenames = [];
                _tacticalMapIcons = [];
                _mapOverrides = new(1024);

                Contract.Assert(_background == null);

                _musics = [];

                _planets = [];
                _bases = [];
                _specials = [];

                _freeQueue = new();
                _planetQueue = new();
                _baseQueue = new();
                _specialQueue = new();
                _alliedQueue = new();
                _enemyQueue = new();
                _neutralQueue = new();

                _map = new char[height][];

                for (i = 0; i < height; i++)
                    _map[i] = new char[width];

                Contract.Assert(_position == char.MinValue);

                // tries to load the settings

                GFFile ini = new();

                if (!ini.Load(filename))
                    throw new FileNotFoundException();

                // tries to set the background and musics

                string key;

                if (ini.TryGetValue(string.Empty, "Background", out string value, out bool quotes))
                {
                    value = value.ToLowerInvariant();

                    if (!value.EndsWith(".mod", StringComparison.Ordinal))
                        value += ".mod";

                    key = filename[..filename.IndexOf($"/{SubPath}/")] + "/assets/models/space/" + value;

                    if (File.Exists(key))
                        _background = value;
                }

                for (i = 0; ini.TryGetValue("Musics", i.ToString(CultureInfo.InvariantCulture), out value, out _); i++)
                    _musics.Add(value);

                // tries to set the type of the entities

                Dictionary<char, int> e = [];
                char c;

                for (c = startChar0; c <= startChar1; c++)
                {
                    key = c.ToString();

                    if (ini.ContainsKey("Planets", key))
                    {
                        e.Add(c, (int)Entities.Planets);

                        _planets.Add(ini.GetValue("Planets", key, null));
                    }
                    else if (ini.ContainsKey("Bases", key))
                    {
                        e.Add(c, (int)Entities.Bases);

                        _bases.Add(ini.GetValue("Bases", key, null));
                    }
                    else if (ini.ContainsKey("Specials", key))
                    {
                        e.Add(c, (int)Entities.Specials);

                        _specials.Add(ini.GetValue("Specials", key, null));
                    }
                    else if (ini.TryGetValue("Ships", key, out value, out quotes))
                    {
                        if (value.StartsWith("A", StringComparison.OrdinalIgnoreCase))
                            e.Add(c, (int)Entities.Allied);
                        else if (value.StartsWith("E", StringComparison.OrdinalIgnoreCase))
                            e.Add(c, (int)Entities.Enemy);
                        else if (value.StartsWith("N", StringComparison.OrdinalIgnoreCase))
                            e.Add(c, (int)Entities.Neutral);
                    }
                }

                // tries to parse the map

                Unsafe.SkipInit(out Info info);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        c = (char)s[x];

                        if (c == sunChar)
                        {
                            if (!_sunInfo.ContainsStart && x == 0)
                            {
                                _sunInfo = new(x, y);

                                continue;
                            }
                        }
                        else if (c == nebulaChar)
                        {
                            if (!_nebulaInfo.ContainsStart && x == 1 && y == 1)
                            {
                                _nebulaInfo = new(x, y);

                                continue;
                            }
                        }
                        else if (c == freeChar)
                        {
                            _freeInfo.Add(new(x, y));

                            continue;
                        }
                        else if (c == emptyChar)
                        {
                            continue;
                        }
                        else if (_isObject[c] != 0)
                        {
                            if (!_objectInfo.TryGetValue(c, out List<Info> list))
                            {
                                list = [];
                                _objectInfo.Add(c, list);
                            }

                            list.Add(new(x, y));

                            continue;
                        }
                        else if (c >= blackHoleChar0 && c <= blackHoleChar1)
                        {
                            _blackHoleInfo.Add(c, new(x, y));

                            continue;
                        }
                        else if (c >= endChar0 && c <= endChar1)
                        {
                            c = char.ToUpper(c);

                            if (e.TryGetValue(c, out i))
                            {
                                if (!_entityInfo[i].TryGetValue(c, out info))
                                {
                                    _entityInfo[i].Add(c, new(-1, -1, x, y));

                                    continue;
                                }

                                if (!info.ContainsEnd)
                                {
                                    _entityInfo[i][c] = new(info.X1, info.Y1, x, y);

                                    continue;
                                }
                            }
                        }
                        else if (c >= startChar0 && c <= startChar1)
                        {
                            if (e.TryGetValue(c, out i))
                            {
                                if (!_entityInfo[i].TryGetValue(c, out info))
                                {
                                    _entityInfo[i].Add(c, new(x, y, -1, -1));

                                    continue;
                                }

                                if (!info.ContainsStart)
                                {
                                    _entityInfo[i][c] = new(x, y, info.X2, info.Y2);

                                    continue;
                                }
                            }
                        }

                        throw new NotSupportedException($"Invalid char at {x}, {y}");
                    }

                    s = s.Slice(width + 2);

                    if (s.Length == 0)
                        break;

                    if (s[width] == 13 && s[width + 1] == 10)
                        continue;

                    throw new NotSupportedException("All lines must have the same width");
                }

                // checks the sun and nebula positions

                if (!_sunInfo.ContainsStart)
                    throw new NotSupportedException($"You don't have a '{sunChar}' at the left side (0, y)");

                if (!_nebulaInfo.ContainsStart)
                    throw new NotSupportedException($"You don't have a '{nebulaChar}' at 1, 1");

                // checks if we have enough positions to allocate all the assets

                const string notEnough = "Not enough positions to allocate all ";

                if (_planets.Count > _entityInfo[(int)Entities.Planets].Count)
                    throw new NotSupportedException($"{notEnough}[planets]");

                if (_bases.Count > _entityInfo[(int)Entities.Bases].Count)
                    throw new NotSupportedException($"{notEnough}[bases]");

                if (_specials.Count > _entityInfo[(int)Entities.Specials].Count)
                    throw new NotSupportedException($"{notEnough}[specials]");

                // checks if we have at least one starting position

                if (_entityInfo[(int)Entities.Allied].Count == 0)
                    throw new NotSupportedException("You don't have a starting position");

                // checks the entity vectors

                void CheckVector(SortedDictionary<char, Info> entities)
                {
                    foreach (var p in entities)
                    {
                        if (!p.Value.ContainsStart || !p.Value.ContainsEnd)
                            throw new NotSupportedException($"'{p.Key}' needs to have a start and end position");
                    }
                }

                CheckVector(_entityInfo[(int)Entities.Planets]);

                CheckVector(_entityInfo[(int)Entities.Allied]);
                CheckVector(_entityInfo[(int)Entities.Enemy]);
                CheckVector(_entityInfo[(int)Entities.Neutral]);

                // checks the entity positions

                void CheckPosition(SortedDictionary<char, Info> entities)
                {
                    foreach (var p in entities)
                    {
                        if (!p.Value.ContainsStart || p.Value.ContainsEnd)
                            throw new NotSupportedException($"'{p.Key}' only needs a start position");
                    }
                }

                CheckPosition(_entityInfo[(int)Entities.Bases]);
                CheckPosition(_entityInfo[(int)Entities.Specials]);

                // checks the blackhole positions

                if (_blackHoleInfo.Count != TerrainContent.MaxBlackHoles)
                    throw new NotSupportedException("You didn't set the 3 'black hole' spawn positions (A to C)");

                CheckPosition(_blackHoleInfo);

                // initializes the remaining variables

                for (c = '0'; c <= '9'; c++)
                {
                    if (_objectInfo.ContainsKey(c))
                    {
                        key = $"PL{(c - 38) % 10}";

                        if (ini.TryGetValue("Map/Overrides", key, out value, out _))
                            _modelFilenames.Add(c, value);

                        key = c.ToString();

                        if (ini.TryGetValue("Map/Overrides", key, out value, out quotes))
                        {
                            key = value.Replace(" ", string.Empty).ToLowerInvariant();

                            if (
                                _icons.TryGetValue(key, out value) ||
                                (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int j) && j >= 0 && j < (int)MapObjectTypes.kMaxObjects)
                            )
                                _tacticalMapIcons.Add(c, value);
                        }
                    }
                }
            }
            catch (Exception)
            {
                _name = null;

                _width = 0;
                _height = 0;

                _sunInfo = new();
                _nebulaInfo = new();
                _freeInfo = null;
                _objectInfo = null;
                _blackHoleInfo = null;
                _entityInfo = null;

                _modelFilenames = null;
                _tacticalMapIcons = null;
                _mapOverrides = null;

                _background = null;
                _musics = null;

                _planets = null;
                _bases = null;
                _specials = null;

                _freeQueue = null;
                _planetQueue = null;
                _baseQueue = null;
                _specialQueue = null;
                _alliedQueue = null;
                _enemyQueue = null;
                _neutralQueue = null;

                _map = null;
                _position = char.MinValue;
            }
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

            // makes a copy of the terrain

            TerrainContent content = terrains[i];

            // initializes the queues

            Populate(_freeQueue, _freeInfo);

            Populate(_planetQueue, _entityInfo[(int)Entities.Planets]);
            Populate(_baseQueue, _entityInfo[(int)Entities.Bases]);
            Populate(_specialQueue, _entityInfo[(int)Entities.Specials]);
            Populate(_alliedQueue, _entityInfo[(int)Entities.Allied]);
            Populate(_enemyQueue, _entityInfo[(int)Entities.Enemy]);
            Populate(_neutralQueue, _entityInfo[(int)Entities.Neutral]);

            // clears the map

            for (i = 0; i < _height; i++)
                Array.Fill(_map[i], '.');

            _position = 'G';

            // populates the template with the current terrain content
            // (apparently the client can only display 74 icons at once in the minimap so we should be careful when setting it. it includes the ships, planets and suns)

            const int maxIcons = 40;

            i = content.BlackHoles;

            if (i > 0)
            {
                var e = _blackHoleInfo.GetEnumerator();

                do
                {
                    e.MoveNext();

                    Populate(e.Current.Value, _blackHoles);

                    i--;
                }
                while (i > 0);
            }

            i = _freeInfo.Count;

            if (i > 0)
            {
                double ratio = Math.Min(maxIcons, i);

                content.Asteroids *= ratio;
                content.DustClouds *= ratio;
                content.IonStorms *= ratio;

                for (i = (int)Math.Truncate(content.Asteroids); i > 0 && _freeQueue.Count > 0; i--)
                    Populate(_freeQueue.Dequeue(), _asteroids);

                for (i = (int)Math.Truncate(content.DustClouds); i > 0 && _freeQueue.Count > 0; i--)
                    Populate(_freeQueue.Dequeue(), _dustClouds);

                for (i = (int)Math.Truncate(content.IonStorms); i > 0 && _freeQueue.Count > 0; i--)
                    Populate(_freeQueue.Dequeue(), _ionStorms);
            }

            if (content.Nebulas)
                Populate(_nebulaInfo, _nebulas);

            Populate(_sunInfo, _suns);

            // objects and overrides

            _mapOverrides.Clear();

            foreach (var p in _objectInfo)
            {
                char c = p.Key;

                foreach (Info info in p.Value)
                {
                    Contract.Assert(_map[info.Y1][info.X1] == freeChar);

                    _map[info.Y1][info.X1] = c;
                }

                // ... icon override

                if (_tacticalMapIcons.TryGetValue(c, out string v))
                {
                    _mapOverrides.Append(c);
                    _mapOverrides.Append(" = ");
                    _mapOverrides.AppendLine(v);
                }

                // ... model override

                if (_modelFilenames.TryGetValue(c, out v))
                {
                    _mapOverrides.Append("PL");

                    if (c == '0')
                        _mapOverrides.Append('1');

                    _mapOverrides.Append(c);
                    _mapOverrides.Append(" = ");
                    _mapOverrides.AppendLine(v);
                }
            }
        }

        public void AddPlanet()
        {
            Populate2(_planetQueue);
        }

        public void AddBase()
        {
            Populate1(_baseQueue);
        }

        public void AddSpecial()
        {
            Populate1(_specialQueue);
        }

        public void AddAlliedShip()
        {
            Populate2(_alliedQueue);
        }

        public void AddEnemyShip()
        {
            Populate2(_enemyQueue);
        }

        public void AddNeutralShip()
        {
            Populate2(_neutralQueue);
        }

        public ReadOnlySpan<char> GetLine(int index)
        {
            Contract.Assert(index >= 0 && index < _height);

            return new(_map[index]);
        }

        private static void Populate(PriorityQueue<Info, ulong> destination, List<Info> source)
        {
            destination.Clear();

            foreach (var value in source)
                destination.Enqueue(value, _rand.NextUInt64());
        }

        private static void Populate(Queue<Info> destination, SortedDictionary<char, Info> source)
        {
            destination.Clear();

            foreach (var p in source)
                destination.Enqueue(p.Value);
        }

        private void Populate(Info info, char[] array)
        {
            Contract.Assert(_map[info.Y1][info.X1] == '.');

            _map[info.Y1][info.X1] = array[_rand.NextInt32(array.Length)];
        }

        private void Populate1(Queue<Info> queue)
        {
            GetCoordinates(queue, out Info info);

            Contract.Assert(
                _map[info.Y1][info.X1] == '.' &&
                _position >= 'G' && _position <= 'Z'
            );

            _map[info.Y1][info.X1] = _position;

            _position++;
        }

        private void Populate2(Queue<Info> queue)
        {
            GetCoordinates(queue, out Info info);

            Contract.Assert(
                _map[info.Y1][info.X1] == '.' &&
                _map[info.Y2][info.X2] == '.' &&
                _position >= 'G' && _position <= 'Z'
            );

            _map[info.Y1][info.X1] = _position;
            _map[info.Y2][info.X2] = char.ToLower(_position);

            _position++;
        }

        private void GetCoordinates(Queue<Info> queue, out Info coordinates)
        {
            if (queue.TryDequeue(out coordinates))
                return;

            if (_freeQueue.TryDequeue(out Info start, out _) && _freeQueue.TryDequeue(out Info end, out _))
            {
                coordinates = new(start.X1, start.Y1, end.X1, end.Y1);

                return;
            }

            throw new NotSupportedException("The template doesn't contain any spawn positions");
        }
    }
}
