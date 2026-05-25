using shrNet;
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
        private void JoinIrcServer()
        {
            StringBuilder s = new(1024);

            // does the handshake

            s.Append("NICK ");
            s.Append(_serverNick);

            WriteLine(s);

            s.Append("USER ");
            s.Append(_serverNick.Replace(" ", string.Empty).ToLowerInvariant());
            s.Append("@fake.net 127.0.0.1 ");
            s.Append(_hostAddress);
            s.Append(" :");
            s.Append(_serverNick);

            WriteLine(s);

            // adds the server to the irc's whitelist

            s.Append("MODE ");
            s.Append(_serverNick);
            s.Append(" +w");

            WriteLine(s);

            // joins the first channel

            s.Append("JOIN ");
            s.Append(_channels[1]);

            WriteLine(s);

            // then joins all the others

            for (int i = 2; i < _channels.Length; i++)
            {
                s.Append("JOIN ");
                s.Append(_channels[i]);
                s.Append(_channels[0]);

                WriteLine(s);
            }
        }

        private void WriteLine(StringBuilder msg)
        {
            msg.AppendLine();

            WriteTo(_client6667, msg);

            msg.Clear();
        }

        private void WriteTo(IIrcClient client, StringBuilder msg)
        {
            Span<byte> span = stackalloc byte[msg.Length];

            bool isConverted = Utils.TryConvert(msg, span);

            Contract.Assert(isConverted);

            client.TryWrite(span);
        }

        private void ProcessServerChat()
        {
            while (_client6667.TryRead(out DuplexMessage message))
            {
                try
                {
                    string line = Encoding.UTF8.GetString(message.AsReadOnlySpan());

                    if (line.Length >= 36)
                    {
                        Character source;
                        IIrcClient client;

                        /*
                            :D4v1ks3074930439!d4v1ks@192.168.1.71 JOIN :#ServerBroadcast@New_Server
                            :*0123456789!*@*.*.*.* JOIN :#ServerBroadcast@*
                        */

                        int i = line.IndexOf(" JOIN :#ServerBroadcast@", 22, StringComparison.Ordinal);

                        if (i >= 0)
                        {
                            if (_administrator == null && !line.StartsWith(":" + _serverNick + "!", StringComparison.Ordinal))
                            {
                                int j = line.IndexOf('!', StringComparison.Ordinal);

                                if (j <= 0 || j >= i)
                                    throw new NotSupportedException();

                                _administrator = line[1..j];

                                Contract.Assert(_nickSuffix == null || _nickSuffix.Equals(line.Substring(j - 10, 10), StringComparison.Ordinal));

                                _nickSuffix ??= line.Substring(j - 10, 10);
                            }
                        }

                        /*
                            :D4v1ks3074930439!c41c@192.168.1.71 NOTICE #ServerBroadcast@New_Server :AfkOn
                            :*0123456789!*@*.*.*.* NOTICE #*@* :AfkOn\r\n

                            :D4v1ks3074930439!c427@192.168.1.71 NOTICE #ServerBroadcast@New_Server :AfkOff
                            :*0123456789!*@*.*.*.* NOTICE #*@* :AfkOff\r\n
                        */

                        i = line.IndexOf(" NOTICE #ServerBroadcast@", 22, StringComparison.Ordinal);

                        if (i >= 0)
                        {
                            if (line.IndexOf(":AfkOn", 35, StringComparison.Ordinal) >= 0)
                            {
                                if (TryGetCharacter(line, i, out source, out client))
                                    source.State |= Character.States.IsAfk;
                            }
                            else if (line.IndexOf(":AfkOff", 35, StringComparison.Ordinal) >= 0)
                            {
                                if (TryGetCharacter(line, i, out source, out client))
                                    source.State &= ~Character.States.IsAfk;
                            }

                            return;
                        }

                        /*
                            :D4v1ks3074930439!e00e@192.168.1.71 PRIVMSG #General@New_Server :!scan
                            :*0123456789!*@*.*.*.* PRIVMSG #*@* :!*
                        */

                        i = line.IndexOf(" PRIVMSG #", 22, StringComparison.Ordinal);

                        if (i >= 0)
                        {
                            int j = line.IndexOf(":!", 36, StringComparison.Ordinal);

                            if (j >= 0 && TryGetCharacter(line, i, out source, out client))
                            {
                                string cmd = line.Substring(j + 2, line.Length - j - 4);

                                if (cmd.Length == 0)
                                    return;

                                string[] a = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                                if (a.Length < 1)
                                    return;

                                // initialize

                                StringBuilder msg = new(1024);
                                bool isDraftable = source.Client.LauncherId != 0;

                                // process command

                                if (isDraftable && (a[0].Equals("scan", StringComparison.Ordinal) || a[0].Equals("s", StringComparison.Ordinal)))
                                {
                                    int locationX;
                                    int locationY;

                                    switch (a.Length)
                                    {
                                        case 1:
                                            locationX = source.CharacterLocationX;
                                            locationY = source.CharacterLocationY;

                                            break;

                                        case 3:
                                            if
                                            (
                                                !int.TryParse(a[1], NumberStyles.None, CultureInfo.InvariantCulture, out locationX) ||
                                                !int.TryParse(a[2], NumberStyles.None, CultureInfo.InvariantCulture, out locationY) ||
                                                locationX < 0 ||
                                                locationY < 0 ||
                                                locationX >= _mapWidth ||
                                                locationY >= _mapHeight ||
                                                !MovementValid(source.CharacterLocationX, source.CharacterLocationY, locationX, locationY)
                                             )
                                                return;

                                            break;

                                        default:
                                            return;
                                    }

                                    CmdScan(source, client.Nick, locationX, locationY, msg);
                                }
                                else if (a[0].Equals("info", StringComparison.Ordinal) || a[0].Equals("i", StringComparison.Ordinal))
                                {
                                    if (a.Length == 1)
                                    {
                                        CmdSound(source.Client, "sensorping");

                                        i = source.CharacterLocationX + source.CharacterLocationY * _mapWidth;

                                        MapHex hex = _map[i];

                                        msg.Append(":L.R.S PRIVMSG ");
                                        msg.Append(client.Nick);
                                        msg.Append(" :You are in sector (");
                                        msg.Append(source.CharacterLocationX);
                                        msg.Append(", ");
                                        msg.Append(source.CharacterLocationY);
                                        msg.Append(") of type '");
                                        msg.Append(_mapTerrains[hex.Terrain]);
                                        msg.Append('\'');
                                        msg.AppendLine();
                                    }
                                }
                                else if (a[0].Equals("turn", StringComparison.Ordinal) || a[0].Equals("t", StringComparison.Ordinal))
                                {
                                    if (a.Length == 1)
                                    {
                                        double timeRemaining = double.NaN;

                                        if (timeRemaining > 0.0)
                                            timeRemaining = Math.Round(timeRemaining / 1000.0, MidpointRounding.AwayFromZero);
                                        else
                                            timeRemaining = 0.0;

                                        msg.Append(":Computer PRIVMSG ");
                                        msg.Append(client.Nick);
                                        msg.Append(" :");
                                        msg.Append(timeRemaining);
                                        msg.Append(" seconds until next turn");
                                        msg.AppendLine();
                                    }
                                }
                                else // if (client.Nick.Equals(_administrator, StringComparison.Ordinal))
                                {
                                    if (a[0].Equals("add", StringComparison.Ordinal))
                                    {
                                        ShipData data = null;
                                        int race;

                                        switch (a.Length)
                                        {
                                            case 2:
                                                if (source.ShipCount < maxHumanFleetSize && _shiplist.TryGetValue(a[1], out data))
                                                {
                                                    CreateShip(data, out Ship ship);

                                                    if (ship.Race != source.CharacterRace)
                                                        ModifyShip(ship, source.CharacterRace);

                                                    UpdateCharacter(source, ship);
                                                    AdjustPopulationCensus(source, ship.BPV);

                                                    Write(source.Client, ClientRequests.PlayerRelayC_0x04_0x00_0x06, source.Id); // A_5

                                                    BroadcastIcons(source.CharacterLocationX, source.CharacterLocationY);
                                                }

                                                break;

                                            case 3:
                                                race = GetIndex(a[1], _realAbbreviations, StringComparison.OrdinalIgnoreCase, true);

                                                if (race != -1 && _shiplist.TryGetValue(a[2], out data))
                                                {
                                                    CreateCharacter((Races)race, source.CharacterLocationX, source.CharacterLocationY, data, out Character character);

                                                    BroadcastIcons(source.CharacterLocationX, source.CharacterLocationY);
                                                }

                                                break;

                                            case 4:
                                                race = GetIndex(a[1], _realAbbreviations, StringComparison.OrdinalIgnoreCase, true);

                                                if (race != -1 && _shiplist.TryGetValue(a[2], out data))
                                                {
                                                    if (a[3].Equals("-m", StringComparison.Ordinal))
                                                    {
                                                        CreateCharacter((Races)race, source.CharacterLocationX, source.CharacterLocationY, data, out Character character);

                                                        _cpuMovements.Remove(character.Id);
                                                    }
                                                    else if (int.TryParse(a[3], NumberStyles.None, CultureInfo.InvariantCulture, out int count))
                                                    {
                                                        while (count > 0)
                                                        {
                                                            CreateCharacter((Races)race, source.CharacterLocationX, source.CharacterLocationY, data, out Character character);

                                                            _cpuMovements.Remove(character.Id);

                                                            for (count -= character.ShipCount; count > 0 && character.ShipCount < GameServer.MaxFleetSize; count--)
                                                            {
                                                                CreateShip(data, out Ship ship);

                                                                if (ship.Race != character.CharacterRace)
                                                                    ModifyShip(ship, character.CharacterRace);

                                                                UpdateCharacter(character, ship);
                                                                AdjustPopulationCensus(character, ship.BPV);
                                                            }
                                                        }
                                                    }

                                                    BroadcastIcons(source.CharacterLocationX, source.CharacterLocationY);
                                                }

                                                break;
                                        }
                                    }
                                    else if (a[0].Equals("go", StringComparison.Ordinal))
                                    {
                                        int locationX;
                                        int locationY;

                                        switch (a.Length)
                                        {
                                            case 2:
                                                if (
                                                    !_characterNames.TryGetValue(a[1], out int characterId) ||
                                                    !_characters.TryGetValue(characterId, out Character character)
                                                )
                                                    return;

                                                locationX = character.CharacterLocationX;
                                                locationY = character.CharacterLocationY;

                                                break;

                                            case 3:
                                                if (
                                                    !int.TryParse(a[1], NumberStyles.None, CultureInfo.InvariantCulture, out locationX) ||
                                                    !int.TryParse(a[2], NumberStyles.None, CultureInfo.InvariantCulture, out locationY) ||
                                                    locationX < 0 ||
                                                    locationY < 0 ||
                                                    locationX >= _mapWidth ||
                                                    locationY >= _mapHeight
                                                )
                                                    return;

                                                break;

                                            default:
                                                return;
                                        }

                                        if (source.CharacterLocationX != locationX || source.CharacterLocationY != locationY)
                                        {
                                            RemoveFromHexPopulation(source);

                                            AddHexRequests(source);
                                            BroadcastIcons(source.CharacterLocationX, source.CharacterLocationY);

                                            source.CharacterLocationX = locationX;
                                            source.CharacterLocationY = locationY;

                                            Write(source.Client, ClientRequests.PlayerRelayC_0x02_0x00_0x02, source.Id); // 14_8

                                            AddToHexPopulation(source);

                                            AddHexRequests(source);
                                            BroadcastIcons(source.CharacterLocationX, source.CharacterLocationY);
                                        }
                                    }
                                    else if (a[0].Equals("items", StringComparison.Ordinal))
                                    {
                                        int index, count;
                                        Ship ship;

                                        switch (a.Length)
                                        {
                                            case 2:
                                                if (int.TryParse(a[1], NumberStyles.None, CultureInfo.InvariantCulture, out index) && index > (int)TransportItems.kTransNothing && index < (int)TransportItems.Total)
                                                {
                                                    ship = source.GetFirstShip();
                                                    TransportItems item = (TransportItems)index;

                                                    ship.Stores.TransportItems.TryGetValue(item, out count);

                                                    msg.Append(":Storage PRIVMSG ");
                                                    msg.Append(client.Nick);
                                                    msg.Append(" :");
                                                    msg.Append(Enum.GetName(item));
                                                    msg.Append(" is set to ");
                                                    msg.Append(count);
                                                    msg.AppendLine();
                                                }

                                                break;

                                            case 3:
                                                if (int.TryParse(a[2], NumberStyles.None, CultureInfo.InvariantCulture, out count) && count >= 0 && count <= 255)
                                                {
                                                    if (int.TryParse(a[1], NumberStyles.None, CultureInfo.InvariantCulture, out index) && index > (int)TransportItems.kTransNothing && index < (int)TransportItems.Total && index != (int)TransportItems.kTransSpareParts)
                                                    {
                                                        TransportItems item = (TransportItems)index;

                                                        ship = source.GetFirstShip();

                                                        Contract.Assert(_ships.ContainsKey(ship.Id));

                                                        if (count == 0)
                                                            ship.Stores.TransportItems.Remove(item);
                                                        else if (!ship.Stores.TransportItems.TryAdd(item, count))
                                                            ship.Stores.TransportItems[item] = count;

                                                        msg.Append(":Storage PRIVMSG ");
                                                        msg.Append(client.Nick);
                                                        msg.Append(" :");
                                                        msg.Append(Enum.GetName(item));
                                                        msg.Append(" was set to ");
                                                        msg.Append(count);
                                                        msg.AppendLine();
                                                    }
                                                    else if (a[1].Equals("all", StringComparison.Ordinal))
                                                    {
                                                        ship = source.GetFirstShip();

                                                        for (TransportItems item = TransportItems.kTransNothing + 1; item < TransportItems.Total; item++)
                                                        {
                                                            if (item != TransportItems.kTransSpareParts)
                                                            {
                                                                if (count == 0)
                                                                    ship.Stores.TransportItems.Remove(item);
                                                                else if (!ship.Stores.TransportItems.TryAdd(item, count))
                                                                    ship.Stores.TransportItems[item] = count;
                                                            }
                                                        }

                                                        msg.Append(":Storage PRIVMSG ");
                                                        msg.Append(client.Nick);
                                                        msg.Append(" :all items are set to ");
                                                        msg.Append(count);
                                                        msg.AppendLine();
                                                    }
                                                }

                                                break;
                                        }
                                    }
                                    else if (a[0].Equals("medals", StringComparison.Ordinal))
                                    {
                                        if (a.Length == 2 && int.TryParse(a[1], NumberStyles.None, CultureInfo.InvariantCulture, out int medals) && medals >= (int)Medals.kNoMedals && medals <= (int)Medals.kAllMedals)
                                        {
                                            source.Awards = (Medals)medals;

                                            Write(source.Client, ClientRequests.PlayerRelayC_0x08_0x00_0x0c, source.Id); // 14_15 (not used)
                                        }
                                    }
                                    else if (a[0].Equals("prestige", StringComparison.Ordinal))
                                    {
                                        if (a.Length == 2 && int.TryParse(a[1], NumberStyles.None, CultureInfo.InvariantCulture, out int prestige) && prestige >= 0 && prestige <= 1_000_000_000)
                                        {
                                            source.CharacterCurrentPrestige = prestige;

                                            if (source.CharacterLifetimePrestige < prestige)
                                                source.CharacterLifetimePrestige = prestige;

                                            Write(source.Client, ClientRequests.PlayerRelayC_0x05_0x00_0x07, source.Id); // 14_12
                                        }
                                    }
                                    else if (a[0].Equals("rank", StringComparison.Ordinal))
                                    {
                                        if (a.Length == 2 && int.TryParse(a[1], NumberStyles.None, CultureInfo.InvariantCulture, out int rank) && rank > (int)Ranks.None && rank < (int)Ranks.Total)
                                        {
                                            source.CharacterRank = (Ranks)rank;

                                            Write(source.Client, ClientRequests.PlayerRelayC_0x07_0x00_0x0b, source.Id); // 14_14
                                        }
                                    }
                                    else if (a[0].Equals("save", StringComparison.Ordinal))
                                    {
                                        if (_savegameState == 0)
                                        {
                                            switch (a.Length)
                                            {
                                                case 1:
                                                    DateTime ts = DateTime.Now;

                                                    _savegameState = save0;
                                                    _lastSavegame = _root + savegameDirectory + _hostName + "_" + _turn + "t_" + ts.Hour + "h" + ts.Minute + "m" + ts.Second + "s";

                                                    MsgMaintenance(msg);

                                                    break;
                                            }
                                        }
                                    }
                                    else if (a[0].Equals("load", StringComparison.Ordinal))
                                    {
                                        if (_savegameState == 0)
                                        {
                                            switch (a.Length)
                                            {
                                                case 1:
                                                    if (File.Exists(_lastSavegame + savegameExtension))
                                                    {
                                                        _savegameState = load0;

                                                        MsgMaintenance(msg);
                                                    }

                                                    break;

                                                case 2:
                                                    string filenameWithoutExtension = _root + savegameDirectory + a[1];

                                                    if (File.Exists(filenameWithoutExtension + savegameExtension))
                                                    {
                                                        _savegameState = load0;
                                                        _lastSavegame = filenameWithoutExtension;

                                                        MsgMaintenance(msg);
                                                    }

                                                    break;
                                            }
                                        }
                                    }
                                    else if (a[0].Equals("reload", StringComparison.Ordinal))
                                    {
                                        if (a.Length == 1)
                                        {
                                            msg.Append(":Computer PRIVMSG ");
                                            msg.Append(client.Nick);

                                            try
                                            {
                                                LoadMapTemplates();
                                                LoadSpaceBackgrounds();

                                                msg.Append(" :The existing assets were reloaded with success");
                                            }
                                            catch (Exception)
                                            {
                                                msg.Append(" :A problem ocorred while reloading the existing assets...");
                                            }

                                            msg.AppendLine();
                                        }
                                    }

#if DEBUG
                                    else if (a[0].Equals("damage", StringComparison.Ordinal))
                                    {
                                        Ship ship = source.GetFirstShip();

                                        switch (a.Length)
                                        {
                                            case 1:
                                                // random damage

                                                for (i = 0; i < Ship.SystemsSize; i += 2)
                                                {
                                                    j = ship.Systems.Items[i];

                                                    if (j != 0)
                                                        ship.Systems.Items[i + 1] = (byte)_rand.NextInt32(j);
                                                }

                                                msg.Append(":Systems PRIVMSG ");
                                                msg.Append(client.Nick);
                                                msg.Append(" :The systems were damaged randomly");

                                                break;

                                            case 2:
                                                // specific damage

                                                if (int.TryParse(a[1], NumberStyles.None, CultureInfo.InvariantCulture, out i) && i >= 0 && i <= 49)
                                                {
                                                    for (j = 0; j < 50; j++)
                                                        ship.Systems.Items[j] = 1;

                                                    ship.Systems.Items[(i << 1) + 1] = 0;

                                                    msg.Append(":Systems PRIVMSG ");
                                                    msg.Append(client.Nick);
                                                    msg.Append(" :");
                                                    msg.Append(Enum.GetName(typeof(SystemTypes), (SystemTypes)(i << 1)));
                                                    msg.Append(" was damaged by 1");
                                                }

                                                break;

                                            default:
                                                return;
                                        }

                                        msg.Append(" ( repair cost estimated at ");
                                        msg.Append(GetShipRepairCost(ship));
                                        msg.AppendLine(" )");
                                    }
#endif

                                }

                                // finalize

                                if (msg.Length > 0)
                                    WriteTo(client, msg);
                            }

                            return;
                        }
                    }
                    else if (line.StartsWith("PING :", StringComparison.Ordinal))
                    {
                        StringBuilder msg = new(1024);

                        msg.Append("PO");
                        msg.Append(line, 2, line.Length - 4);

                        WriteLine(msg);
                    }
                }
                catch (Exception e)
                {
                    LogError("ProcessServerChat()", e);
                }
                finally
                {
                    message.Release();
                }
            }
        }

        private bool TryGetCharacter(string line, int limit, out Character character, out IIrcClient client)
        {
            // :*0123456789!*@*.*.*.* PRIVMSG #*@* :!*

            int i = line.IndexOf(_nickSuffix, 2, StringComparison.Ordinal);

            if (i >= 0 && i < limit && _characterNames.TryGetValue(line[1..i], out int id) && _characters.TryGetValue(id, out character) && _ircService.TryGetClient(line[1..(i + 10)], out client))
                return true;

            character = null;
            client = null;

            return false;
        }

        private void CmdMusic(Client27000 client, string filename)
        {
            Contract.Assert(filename != null);

            if (_launchers.TryGetValue(client.LauncherId, out Client27001 launcher))
            {
                if (filename.Length != 0)
                    filename = $"D4v1ks/SfcOpClient/Musics/{filename}";

                Clear();

                Push(0x00); // file data
                Push(filename);
                Push((byte)0x02); // opcode

                Write(launcher);
            }
        }

        private void CmdSound(Client27000 client, string filename)
        {
            Contract.Assert(filename != null);

            if (_launchers.TryGetValue(client.LauncherId, out Client27001 launcher))
            {
                if (filename.Length != 0)
                    filename = $"D4v1ks/SfcOpClient/Sounds/{filename}.wav";

                Clear();

                Push(0x00); // file data
                Push(filename);
                Push((byte)0x03); // opcode

                Write(launcher);
            }
        }

        private void CmdScan(Character source, string nick, int locationX, int locationY, StringBuilder msg)
        {
            const string nickFrom = "S.R.S";

            Dictionary<int, object> population = _map[locationX + locationY * _mapWidth].Population;

            Contract.Assert(msg.Length == 0);

            StringBuilder line = new(1024);

            if (population.Count > 1)
            {
                CmdSound(source.Client, "scan");

                StringBuilder arg = new(1024);

                foreach (KeyValuePair<int, object> p in population)
                {
                    Character character = _characters[p.Key];

                    if (character.Id != source.Id && (character.State & (Character.States.IsAfk | Character.States.IsBusy)) == Character.States.None)
                    {
                        int c = character.ShipCount - 1;

                        for (int i = 0; i <= c; i++)
                        {
                            Ship ship = character.GetShipAt(i);

                            Contract.Assert(_ships.ContainsKey(ship.Id));

                            arg.Append(ship.ShipClassName);

                            /*
                                arg.Append('(');
                                arg.Append(ship.BPV);
                                arg.Append(')');
                            */

                            if ((character.State & Character.States.IsHuman) == Character.States.IsHuman)
                                arg.Append('*');

                            if (i < c)
                                arg.Append(", ");
                            else
                                arg.Append("; ");

                            if (line.Length + arg.Length >= 80)
                            {
                                AppendMsgLine(nickFrom, nick, msg, line);

                                line.Clear();
                            }

                            line.Append(arg);

                            arg.Clear();
                        }
                    }
                }

                if (msg.Length == 0 && line.Length == 0)
                    line.Append("interference detected");
            }
            else
                line.Append("no signal detected");

            AppendMsgLine(nickFrom, nick, msg, line);
        }

        private static void AppendMsgLine(string nickFrom, string nickTo, StringBuilder msg, StringBuilder line)
        {
            msg.Append(':');
            msg.Append(nickFrom);
            msg.Append(" PRIVMSG ");
            msg.Append(nickTo);
            msg.Append(" :");
            msg.Append(line);
            msg.AppendLine();
        }

        private void MsgMaintenance(StringBuilder msg)
        {
            msg.Append("PRIVMSG ");
            msg.Append(_channels[3]);
            msg.Append(_channels[0]);
            msg.Append(" :");
            msg.Append(defaultClosingWarning);

            WriteLine(msg);
        }
    }
}
