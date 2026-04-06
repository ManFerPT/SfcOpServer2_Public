#pragma warning disable IDE0028

using shrNet;
using shrServices;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace SfcOpServer
{
    public partial class GameServer
    {
        private static readonly byte[][] _clientStrings =
        [
            "CharacterLogOnRelayNameC"u8.ToArray(),
            "MessengerRelayNameC"u8.ToArray(),
            "MetaViewPortHandlerNameC"u8.ToArray(),
            "MissionRelayNameC"u8.ToArray(),
            "PlayerRelayC"u8.ToArray(),

            "MedalsPanel"u8.ToArray(),
            "MetaClientChatPanel"u8.ToArray(),
            "MetaClientHelpListPanel"u8.ToArray(),
            "MetaClientMissionPanel"u8.ToArray(),
            "MetaClientNewsPanel"u8.ToArray(),
            "MetaClientPlayerListPanel"u8.ToArray(),
            "MetaClientShipPanel"u8.ToArray(),
            "MetaClientSupplyDockPanel"u8.ToArray(),
            "PlayerInfoPanel"u8.ToArray()
        ];

        private static readonly byte[][] _serverStrings =
        [
            "\u0022\u0000\u0000\u0000 *~Server~* .?AVtMessengerRelayS@@"u8.ToArray(),
            "\u001d\u0000\u0000\u0000 *~Server~* .?AVtInfoRelayS@@"u8.ToArray(),

            "\u0022\u0000\u0000\u0000 *~Server~* .?AVtCharacterRelayS@@"u8.ToArray(),
            "\u001d\u0000\u0000\u0000 *~Server~* .?AVtChatRelayS@@"u8.ToArray(),
            "\u001e\u0000\u0000\u0000 *~Server~* .?AVtClockRelayS@@"u8.ToArray(),
            "\u0021\u0000\u0000\u0000 *~Server~* .?AVtDataValidatorS@@"u8.ToArray(),
            "\u0020\u0000\u0000\u0000 *~Server~* .?AVtEconomyRelayS@@"u8.ToArray(),
            "\u001c\u0000\u0000\u0000 *~Server~* .?AVtMapRelayS@@"u8.ToArray(),
            "\u0027\u0000\u0000\u0000 *~Server~* .?AVtMissionMatcherRelayS@@"u8.ToArray(),
            "\u001d\u0000\u0000\u0000 *~Server~* .?AVtNewsRelayS@@"u8.ToArray(),
            "\u001f\u0000\u0000\u0000 *~Server~* .?AVtNotifyRelayS@@"u8.ToArray(),
            "\u0021\u0000\u0000\u0000 *~Server~* .?AVtSecurityRelayS@@"u8.ToArray(),
            "\u001d\u0000\u0000\u0000 *~Server~* .?AVtShipRelayS@@"u8.ToArray(),
        ];

        // private functions

        private void Process(Client27000 client, byte[] buffer, int length)
        {
            /*
                00       04
                00000000 00

                00       04 05       09       13       17       21
                00000000 00 00000000_00000000_00000000 00000000 ...

                00       04 05       09       13       17       21 22       26       30       34
                00000000 00 00000000_00000000_00000000 00000000 01 00000000_00000000_00000000 ...
            */

            switch (buffer[4])
            {
                case (byte)0x00:
                    byte i3 = buffer[13];

                    // tries the get the response header

                    int i4, i5, i6;

                    if (length >= 34 && buffer[21] == 1)
                    {
                        i4 = Unsafe.ReadUnaligned<int>(ref buffer[22]);
                        i5 = Unsafe.ReadUnaligned<int>(ref buffer[26]);
                        i6 = Unsafe.ReadUnaligned<int>(ref buffer[30]);
                    }
                    else
                    {
                        i4 = 0;
                        i5 = 0;
                        i6 = 0;
                    }

                    switch (buffer[9])
                    {
                        case (byte)ServerRelays.AVtMessengerRelayS:
                            switch (i3)
                            {
                                case (byte)0x05:
                                    Q_0_5(client);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtInfoRelayS:
                            switch (i3)
                            {
                                case (byte)0x00:
                                    Q_1_0(client, buffer, length);
                                    return;

                                case (byte)0x01:
                                    Q_1_1(client);
                                    return;

                                case (byte)0x02:
                                    Q_1_2(client, i4, i5, i6, buffer, length);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtCharacterRelayS:
                            switch (i3)
                            {
                                case (byte)0x02:
                                    Q_14_2(client, i4, i5, i6);
                                    return;

                                case (byte)0x03:
                                    Q_14_3(client, i4, i5, i6, buffer);
                                    return;

                                case (byte)0x08:
                                    Q_14_8(client, i4, i5, i6, buffer, length);
                                    return;

                                case (byte)0x0f:
                                    Q_14_F(client, i4, i5, i6);
                                    return;

                                case (byte)0x10:
                                    Q_14_10(client, i4, i5, i6);
                                    return;

                                case (byte)0x12:
                                    Q_14_12(client, i4, i5, i6);
                                    return;

                                case (byte)0x13:
                                    Q_14_13(client, i4, i5, i6);
                                    return;

                                case (byte)0x14:
                                    Q_14_14(client, i4, i5, i6);
                                    return;

                                case (byte)0x15:
                                    Q_14_15(client, i4, i5, i6);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtChatRelayS:
                            switch (i3)
                            {
                                case (byte)0x02:
                                    Q_11_2(client, i4, i5, i6);
                                    return;

                                case (byte)0x04:
                                    Q_11_4(client, i4, i5, i6);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtClockRelayS:
                            switch (i3)
                            {
                                case (byte)0x02:
                                    Q_4_2(client, i4, i5, i6, buffer, length);
                                    return;

                                case (byte)0x05:
                                    Q_4_5();
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtDataValidatorS:
                            switch (i3)
                            {
                                case (byte)0x02:
                                    Q_5_2(client, buffer);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtEconomyRelayS:
                            switch (i3)
                            {
                                case (byte)0x02:
                                    Q_7_2(client, i4, i5, i6);
                                    return;

                                case (byte)0x03:
                                    Q_7_3(client, i4, i5, i6, buffer);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtMapRelayS:
                            switch (i3)
                            {
                                case (byte)0x02:
                                    Q_D_2(client, i4, i5, i6);
                                    return;

                                case (byte)0x03:
                                    Q_D_3(client, i4, i5, i6, buffer);
                                    return;

                                case (byte)0x06:
                                    Q_D_6(client, i4, i5, i6, buffer);
                                    return;

                                case (byte)0x12:
                                    Q_D_12(client, i4, i5, i6, buffer);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtMissionMatcherRelayS:
                            switch (i3)
                            {
                                case (byte)0x03:
                                    Q_E_3(client, buffer);
                                    return;

                                case (byte)0x04:
                                    Q_E_4(client, buffer);
                                    return;

                                case (byte)0x07:
                                    Q_E_7(client, buffer);
                                    return;

                                case (byte)0x0c:
                                    Q_E_C(client, buffer);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtNewsRelayS:
                            switch (i3)
                            {
                                case (byte)0x02:
                                    Q_F_2(client, i4, i5, i6, buffer);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtNotifyRelayS:
                            switch (i3)
                            {
                                case (byte)0x02:
                                    Q_15_2(client, buffer, length);
                                    return;

                                case (byte)0x03:
                                    Q_15_3(client, buffer);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtSecurityRelayS:
                            switch (i3)
                            {
                                case (byte)0x02:
                                    Q_19_2(client, i4, i5, i6, buffer, length);
                                    return;

                                case (byte)0x03:
                                    Q_19_3(client, i4, i5, i6);
                                    return;

                                case (byte)0x04:
                                    Q_19_4(client, buffer);
                                    return;
                            }
                            break;

                        case (byte)ServerRelays.AVtShipRelayS:
                            switch (i3)
                            {
                                case (byte)0x04:
                                    Q_A_4(client, buffer);
                                    return;

                                case (byte)0x05:
                                    Q_A_5(client, i4, i5, i6);
                                    return;

                                case (byte)0x06:
                                    Q_A_6(client, i4, i5, i6, buffer);
                                    return;

                                case (byte)0x07:
                                    Q_A_7(client, i4, i5, i6, buffer);
                                    return;
                            }
                            break;
                    }
                    break;

                case (byte)0x01:
                    Q(client);
                    return;
            }

            Console.WriteLine("SERVER OPCODE: " + DuplexMessage.GetHex(buffer, 0, BitConverter.ToInt32(buffer, 0)));

            throw new NotSupportedException();
        }

        // AVtMessengerRelayS

        private void Q(Client27000 client)
        {
            // [Q] 05000000 01

            if (client.State == Client27000.States.IsOffline)
            {
                client.State |= Client27000.States.Accepted;

                // [R] 19000000 00 ffffffff_00000000_00000000 04000000 01000000
                // [R] 21000000 00 ffffffff_00000000_01000000 0c000000 41577e77 00000000 01000000
                // [R] 05000000 02

                Clear();

                Push((byte)0x02);
                Push(0x05);

                Flush();

                Push((int)ServerRelays.AVtInfoRelayS);
                Push(0x00);
                Push(0x00); // undefined string ?
                Push(-1, 0x00, 0x01);

                Flush();

                Push(client.Id);
                Push(-1, 0x00, 0x00);

                Write(client);
            }
        }

        private static void Q_0_5(Client27000 client)
        {
            // [Q] 15000000 00 00000000_00000000_05000000 00000000

            client.LastPingReply = Environment.TickCount64;
        }

        // AVtInfoRelayS

        private void Q_1_0(Client27000 client, byte[] buffer, int size)
        {
            // [Q] 4b000000 00 00000000_01000000_00000000 36000000 2a000000_643476316b7340686f746d61696c2e636f6d4368617261637465724c6f674f6e52656c61794e616d6543 03000000 04000000

            int relayCount = 0;
            int relayIndex = -1;

            ReadOnlySpan<byte> b = new(buffer, 0, size - 8);

            for (int i = 0; i < (int)ClientRelays.Total; i++)
            {
                if (client.Relays[i] != -1)
                    relayCount++;
                else if (b.EndsWith(_clientStrings[i]))
                    relayIndex = i;
            }

            if (relayIndex == -1)
                throw new NotSupportedException();

            // registers the relay

            Contract.Assert(BitConverter.ToInt32(buffer, size - 8) == client.Id);

            client.Relays[relayIndex] = BitConverter.ToInt32(buffer, size - 4);

            Contract.Assert(client.Relays[relayIndex] > 0);

            relayCount++;

            // checks if we registered everything

            if (relayCount == (int)ClientRelays.Total)
            {
                Contract.Assert(client.State == Client27000.States.IsAccepted);

                client.State |= Client27000.States.Registered;
            }

            // checks if we are reconnecting

            if (relayIndex == (int)ClientRelays.CharacterLogOnRelayNameC)
            {
                Character character = client.Character;

                if ((character.State & Character.States.IsHumanBusyReconnecting) == Character.States.IsHumanBusyReconnecting)
                {
                    character.State = Character.States.IsHumanBusyConnecting;

                    // starts the login process

                    BeginLoginClient(client);

                    // sends the full character

                    Clear();

                    Push(character, isHexCacheSet: false, isFlagSet: true);
                    Push(0x00);
                    Push(client.Id, client.Relays[(int)ClientRelays.CharacterLogOnRelayNameC], 0x02);

                    Write(client);
                }

                Contract.Assert(character.State == Character.States.IsHumanBusyConnecting);
            }
        }

        private void Q_1_1(Client27000 client)
        {
            /*
                // logout phase
                
                [Q] 51000000 00 00000000_01000000_01000000 3c000000 28000000_643476316b7340686f746d61696c2e636f6d4d657461436c69656e744d697373696f6e50616e656c 4f000000 11000000 4f000000 01000000                   
                    (all 14 client relays)

                [R] 1e000000 00 05000000_0d000000_03000000 09000000 00000000 01 940b1234
                [R] 1e000000 00 05000000_11000000_06000000 09000000 00000000 0f 00000000
            */

            Contract.Assert(client.Other[0] != -1);

            Clear();

            Push(client.Character.Id);
            Push((byte)0x01);
            Push(0x00);
            Push(client.Id, client.Other[0], 0x03);

            Write(client);

            Contract.Assert(client.Other[1] != -1);

            Clear();

            Push(0x00);
            Push((byte)0x0f);
            Push(0x00);
            Push(client.Id, client.Other[1], 0x06);

            Write(client);
        }

        private void Q_1_2(Client27000 client, int i4, int i5, int i6, byte[] buffer, int size)
        {
            // [Q] 47000000 00 00000000_01000000_02000000 32000000 01 01000000_01000000_03000000 21000000_202a7e5365727665727e2a202e3f415674536563757269747952656c6179534040

            ReadOnlySpan<byte> b = new(buffer, 0, size);

            for (int i = 0x02; i < (int)ServerRelays.Total; i++)
            {
                if (b.EndsWith(_serverStrings[i]))
                {
                    Clear();

                    Push(i);
                    Push(0x00);
                    Push(_serverStrings[i]);
                    Push(i4, i5, i6);

                    Write(client);

                    return;
                }
            }

            throw new NotSupportedException();
        }

        // AVtClockRelayS

        private void Q_4_2(Client27000 client, int i4, int i5, int i6, byte[] buffer, int size)
        {
            ReadOnlySpan<byte> b = new(buffer, 0, size - 4);

            if (b.EndsWith(_clientStrings[(int)ClientRelays.MetaViewPortHandlerNameC]) || b.EndsWith(_clientStrings[(int)ClientRelays.PlayerInfoPanel]))
            {
                Clear();

                PushStardate();

                Push(0x00);
                Push(i4, i5, i6);

                Write(client);
            }
        }

        private static void Q_4_5()
        {
            Debugger.Break(); // !?  
        }

        // AVtDataValidatorS

        private void Q_5_2(Client27000 client, byte[] buffer)
        {
            // sends a signal to stop the remaining musics in the playlist

            CmdMusic(client, string.Empty);

            // gets the current character and checks if it is in a mission

            Character character = client.Character;

            Contract.Assert(character.Mission != 0);

            // skips the character id

            Contract.Assert(BitConverter.ToInt32(buffer, 21) == character.Id);

            // skips the character name

            int c = BitConverter.ToInt32(buffer, 25);

            Contract.Assert(Encoding.UTF8.GetString(buffer, 29, c).Equals(character.CharacterName, StringComparison.Ordinal));

            int p = 29 + c;

            // checks if the character was the host

            bool isHost = buffer[p] != 0;

            p++;

            // gets the X coordinate

            int locationX = BitConverter.ToInt32(buffer, p);

            Contract.Assert(locationX == character.CharacterLocationX);

            p += 4;

            // gets the Y coordinate

            int locationY = BitConverter.ToInt32(buffer, p);

            Contract.Assert(locationY == character.CharacterLocationY);

            p += 4;

            // gets the number of teams reported

            int teamsReported = BitConverter.ToInt32(buffer, p);

            p += 4;

            // gets the current draft 

            c = locationX + locationY * _mapWidth;

            Draft draft = _drafts[c];

            // reports the character

            draft.Reported.Add(character.Id, null);

            // updates the mission host in case it changed during the mission

            if (isHost)
            {
                Contract.Assert(draft.Mission.HostId == 0);

                draft.Mission.HostId = character.Id;
            }

            // gets the current hex

            MapHex hex = _map[c];

            Contract.Assert(hex.Mission != 0);

            // processes the reported teams

            SortedDictionary<long, int> sortedItems = [];
            Queue<int> queuedItems = new();

            for (int i = 0; i < teamsReported; i++)
            {
                // gets the team id

                TeamIds teamID = (TeamIds)BitConverter.ToInt32(buffer, p);

                p += 4;

                // gets the owner

                Character owner = _characters[BitConverter.ToInt32(buffer, p)];

                p += 4;

                Contract.Assert(owner.CharacterLocationX == locationX && owner.CharacterLocationY == locationY);

                // gets the number of ships reported

                int shipsReported = BitConverter.ToInt32(buffer, p);

                p += 4;

                // defines some flags

                bool IsCpu = (owner.State & Character.States.IsCpu) == Character.States.IsCpu;
                bool IsUpdatable = (IsCpu || owner.Id == character.Id) && owner.Mission == (hex.Mission & MissionFilter);

                // processes the reported ships

                int shipId;

                for (int j = 0; j < shipsReported; j++)
                {
                    // gets the ship id

                    shipId = BitConverter.ToInt32(buffer, p);

                    // tries to get the ship

                    bool shipCanBeUpdated = _ships.TryGetValue(shipId, out Ship ship) && IsUpdatable;

                    // skips the header

                    c = Ship.GetHeaderSize(buffer, p);
                    p += c;

                    // tries to update the damage chunk

                    if (shipCanBeUpdated)
                        UpdateShipSystems(ship, buffer, p);

                    p += Ship.SystemsSize;

                    // tries to update the stores chunk

                    c = Ship.GetStoresSize(buffer, p);

                    if (shipCanBeUpdated)
                        UpdateShipStores(ship, buffer, p, c);

                    p += c;

                    // tries to update the officers chunk

                    c = Ship.GetOfficersSize(buffer, p);

                    if (shipCanBeUpdated)
                        UpdateShipOfficers(ship, buffer, p, c);

                    p += c;

                    // skips the ship flag

                    Contract.Assert(BitConverter.ToInt32(buffer, p) == 0);

                    p += 4;
                }

                // gets the VictoryLevel

                VictoryLevels victoryLevel = (VictoryLevels)BitConverter.ToInt32(buffer, p);

                p += 4;

                // gets the Prestige

                int prestige = BitConverter.ToInt32(buffer, p);

                p += 4;

                // gets the BonusPrestige

                int bonusPrestige = BitConverter.ToInt32(buffer, p);

                p += 4;

                // gets the length of the NextMissionTitle

                c = BitConverter.ToInt32(buffer, p);

                p += 4;

                // gets the NextMissionTitle

                if (c != 0)
                {
                    foreach (KeyValuePair<int, Team> e in draft.Mission.Teams)
                    {
                        while (true)
                        {
                            // reported custom id

                            int customId = (int)Utils.HexToByte(buffer, p);

                            p += 2;

                            if (customId == 0)
                                break;

                            // reported ship damage percentage

                            int shipDamagePercentage = (int)Utils.HexToByte(buffer, p);

                            p += 2;

                            Contract.Assert(shipDamagePercentage >= 0 && shipDamagePercentage <= 100);

                            // reported ship stores

                            Contract.Assert(customId >= Mission.FirstCustomId && customId < (Mission.FirstCustomId + 60));

                            shipId = draft.Mission.CustomIds[customId];

                            Ship ship = _ships[shipId];

                            ship.Stores.TransportItems.Clear();
                            ship.Stores.DamageControl.CurrentQuantity = 0;

                            while (true)
                            {
                                TransportItems item = (TransportItems)Utils.HexToByte(buffer, p);

                                p += 2;

                                if (item == TransportItems.kTransNothing)
                                    break;

                                Contract.Assert(item > TransportItems.kTransNothing && item < TransportItems.Total);

                                int count = (int)Utils.HexToByte(buffer, p);

                                p += 2;

                                Contract.Assert(count > 0 && count <= 255);

                                if (item == TransportItems.kTransSpareParts)
                                {
                                    Contract.Assert(!ship.Stores.TransportItems.ContainsKey(TransportItems.kTransSpareParts) && ship.Stores.DamageControl.CurrentQuantity == 0);

                                    /*
                                        during a mission, we may receive more spare parts than our ship can actually use
                                        (this will cause the 'UI STORES' to malfunction when the ship returns to the campaign map)
                                        so, to avoid this issue we sell the extra spare parts here
                                    */

                                    if (ship.Stores.DamageControl.MaxQuantity >= count)
                                        ship.Stores.DamageControl.CurrentQuantity = (byte)count;
                                    else
                                    {
                                        ship.Stores.DamageControl.CurrentQuantity = ship.Stores.DamageControl.MaxQuantity;

                                        UpdateCharacter(owner, (int)((count - ship.Stores.DamageControl.MaxQuantity) * GetSparePartsCost(ship.ClassType)));
                                    }
                                }
                                else
                                    ship.Stores.TransportItems.Add(item, count);
                            }

                            // ... makes sure we report the ship

                            e.Value.Reported.TryAdd(shipId, shipDamagePercentage);
                        }
                    }
                }

                // gets the NextMissionScore

                int nextMissionScore = BitConverter.ToInt32(buffer, p);

                p += 4;

                // gets the Medal

                Medals medal = (Medals)BitConverter.ToInt32(buffer, p);

                p += 4;

                // gets the CampaignEvent

                CampaignEvents campaignEvent = (CampaignEvents)BitConverter.ToInt32(buffer, p);

                p += 4;

                // -------------------------------------------------------------------------------

                const int originalBPV = 32;
                const int capturedBPV = 47;

                if (IsUpdatable)
                {
                    // processes the victory level

                    Contract.Assert(Enum.IsDefined(victoryLevel));

                    // processes the prestige rewards

                    c = prestige + bonusPrestige;

                    UpdateCharacter(owner, c);

                    // processes the next mission title

                    Contract.Assert(sortedItems.Count == 0);

                    Team team = draft.Mission.Teams[owner.Id];

                    Contract.Assert(team.Id == teamID);

                    foreach (KeyValuePair<int, int> q in team.Reported)
                    {
                        shipId = q.Key;

                        if (_ships.TryGetValue(shipId, out Ship ship))
                        {
                            Contract.Assert(ship.Systems.Items[(int)SystemTypes.ExtraDamageMax] != 0 && ship.Systems.Items[(int)SystemTypes.ExtraDamage] != 0);

                            if (team.Ships.ContainsKey(shipId))
                                sortedItems.Add((long)ship.BPV << originalBPV | (long)ship.Id, shipId);
                            else
                                sortedItems.Add((long)ship.BPV << capturedBPV | (long)ship.Id, shipId);
                        }
                    }

                    // processes the next mission score

                    Contract.Assert(nextMissionScore == 0);

                    // processes the medal

                    Contract.Assert(medal >= Medals.kNoMedals && medal <= Medals.kAllMedals);

                    character.Awards |= medal;

                    // processes the campaign event

                    Contract.Assert(Enum.IsDefined(campaignEvent));

                    // checks if the owner, while playing, received any ship from the shipyard
                    // and adds it to the current list

                    if (!IsCpu)
                    {
                        c = owner.ShipCount;

                        for (int j = 0; j < c; j++)
                        {
                            Ship ship = owner.GetShipAt(j);

                            if (!team.Ships.ContainsKey(ship.Id))
                            {
                                Contract.Assert(ship.OwnerID == owner.Id);

                                sortedItems.Add((long)ship.BPV << originalBPV | (long)ship.Id, ship.Id);
                            }
                        }
                    }

                    // clears the owner's fleet

                    owner.ClearShipList();

                    // checks if the owner has any ships to rebuild his fleet

                    if (sortedItems.Count == 0)
                    {
                        // as we can't have any character without ships
                        // we must do something about it

                        if (IsCpu)
                        {
                            // deletes the AI permanently

                            _characters.Remove(owner.Id);
                            _cpuMovements.Remove(owner.Id);

                            // we just remove the AI from the hex population here
                            // because the hex will be refreshed at the end of this report

                            hex.Population.Remove(owner.Id);
                        }
                        else
                        {
                            // creates a temporary ship for the player

                            CreateTemporaryShip(owner);
                        }
                    }
                    else
                    {
                        // checks if the owner's fleet surpassed its cap and needs to sell any 'extra' ships

                        if (IsCpu)
                            c = MaxFleetSize;
                        else
                        {
                            // a human character can have up to 3 ships in his fleet
                            // but, here, we need to give room for any bids that are still pending

                            Contract.Assert(owner.Bids >= 0 && owner.Bids < maxHumanFleetSize);

                            c = maxHumanFleetSize - owner.Bids;
                        }

                        if (sortedItems.Count > c)
                        {
                            Contract.Assert(queuedItems.Count == 0);

                            foreach (KeyValuePair<long, int> q in sortedItems)
                            {
                                Ship ship = _ships[q.Value];

                                if (c > 0)
                                {
                                    // enqueues the 'best' ships

                                    queuedItems.Enqueue(ship.Id);

                                    // decrements the number of ships remaining

                                    c--;
                                }
                                else if (ship.ClassType >= ClassTypes.kClassListeningPost && ship.ClassType <= ClassTypes.kClassStarBase || ship.ClassType == ClassTypes.kClassPlanets)
                                {
                                    Contract.Assert(!IsCpu);

                                    // always keeps the bases or planets

                                    queuedItems.Enqueue(ship.Id);
                                }
                                else
                                {
                                    // sells the 'extra' ship

                                    int profit = GetShipTradeInValue(ship);

                                    UpdateCharacter(owner, profit);

                                    // deletes the 'extra' ship

                                    _ships.Remove(ship.Id);
                                }
                            }

                            sortedItems.Clear();

                            c = queuedItems.Count;

                            while (c > 0)
                            {
                                sortedItems.Add(c, queuedItems.Dequeue());

                                c--;
                            }

                            Contract.Assert(queuedItems.Count == 0);
                        }

                        // rebuilds the owner's fleet

                        foreach (KeyValuePair<long, int> q in sortedItems)
                        {
                            Ship ship = _ships[q.Value];

                            // checks if the ship was captured and modifies it if necessary

                            if (ship.Race != owner.CharacterRace)
                            {
                                Contract.Assert(ship.OwnerID != owner.Id);

                                ModifyShip(ship, owner.CharacterRace);
                            }

                            ship.OwnerID = 0;

                            // does the automatic repairs and resupplies
                            // and decides what to do with the ship

                            if (IsCpu)
                            {
                                int expenses = 0;

                                // the automatic repair is mostly done by the crew
                                // but the AI 'supports' any differences

                                int oldCost = GetShipRepairCost(ship);

                                AutomaticRepair(ship, _cpuAutomaticRepairMultiplier);

                                int newCost = GetShipRepairCost(ship);

                                Contract.Assert(newCost <= oldCost);

                                expenses += (int)Math.Round((newCost - oldCost) * _cpuAutomaticRepairMultiplier, MidpointRounding.AwayFromZero);

                                // the automatic resupply is entirely 'supported' by the AI

                                oldCost = GetShipStoresCost(ship.Race, ship.ClassType, ship.Stores);

                                AutomaticResupply(ship, _cpuAutomaticResupplyMultiplier);

                                newCost = GetShipStoresCost(ship.Race, ship.ClassType, ship.Stores);

                                Contract.Assert(oldCost <= newCost);

                                expenses += (int)Math.Round((oldCost - newCost) * _cpuAutomaticResupplyMultiplier, MidpointRounding.AwayFromZero);

                                // registers the expense

                                Contract.Assert(expenses <= 0);

                                UpdateCharacter(owner, expenses);

                                _curExpenses[(int)owner.CharacterRace] -= expenses;

                                // adds the ship to the fleet

                                UpdateCharacter(owner, ship);
                            }
                            else
                            {
                                // checks if the ship is a base or planet that we brought or was captured

                                if (ship.ClassType >= ClassTypes.kClassListeningPost && ship.ClassType <= ClassTypes.kClassStarBase || ship.ClassType == ClassTypes.kClassPlanets)
                                {
                                    // creates an AI character to take care of it

                                    CreateCharacter(owner.CharacterRace, locationX, locationY, ship, out _);
                                }
                                else
                                {
                                    // adds the ship to the fleet

                                    UpdateCharacter(owner, ship);
                                }
                            }
                        }

                        sortedItems.Clear();

                        Contract.Assert((IsCpu && owner.ShipCount <= MaxFleetSize) || (!IsCpu && owner.ShipCount <= maxHumanFleetSize));
                    }
                }
            }

            // ----------------------------------------------------------------------------------------------------------------------------------------------

            Write(client, ClientRequests.PlayerRelayC_0x05_0x00_0x07, character.Id); // 14_12
            Write(client, ClientRequests.PlayerRelayC_0x04_0x00_0x06, character.Id); // A_5
            Write(client, ClientRequests.PlayerRelayC_0x08_0x00_0x0c, character.Id); // 14_15

            // ----------------------------------------------------------------------------------------------------------------------------------------------

            RefreshHex(hex);

            // tries to leave the current mission

            Contract.Assert((character.State & Character.States.IsBusy) == Character.States.IsBusy);

            character.State &= ~Character.States.IsBusy;

            TryLeaveMission(character, hex);
        }

        // AVtEconomyRelayS

        private void Q_7_2(Client27000 client, int i4, int i5, int i6)
        {
            // [Q] 2e000000000000000006000000020000001900000001210000001600000002000000e20100000000803f00000000

            Character character = client.Character;

            Clear();

            int c = 0;

            foreach (KeyValuePair<int, BidItem> p in _bidItems[(int)character.CharacterRace])
            {
                BidItem item = p.Value;

                Push(character, item);
                Push(item.ShipId);

                c++;

                if (c == 40) // the UI only supports 40 entries
                    break;
            }

            Push(c);
            Push(0x01);

            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_7_3(Client27000 client, int i4, int i5, int i6, byte[] buffer)
        {
            int shipId = BitConverter.ToInt32(buffer, 34);

            if (_ships[shipId].IsInAuction != 1)
                return;

            Character character = client.Character;

            Contract.Assert(BitConverter.ToInt32(buffer, 38) == character.Id && BitConverter.ToInt32(buffer, 42) == (int)character.CharacterRace);

            BidItem item = _bidItems[(int)character.CharacterRace][shipId];
            int bidType = BitConverter.ToInt32(buffer, 46);

            Clear();

            if (TryUpdateBidItem(character, item, bidType))
            {
                Contract.Assert(item.BiddingHasBegun == 1);

                Push(0x02);
                Push(0x00);
                Push(character.Id);
                Push(character, item);

                Push(0x00);
            }
            else
            {
                Push(0x00);
                Push(0x00);
                Push(character.Id);
                Push(character, BidItem.Empty);

                Push(0x05);
            }

            Push(i4, i5, i6);

            Write(client);
        }

        // AVtShipRelayS

        private void Q_A_4(Client27000 client, byte[] buffer)
        {
            Character character = client.Character;

            Contract.Assert(BitConverter.ToInt32(buffer, 34) == -1);

            int shipId = BitConverter.ToInt32(buffer, 38);

            Contract.Assert(BitConverter.ToInt32(buffer, 42) == character.Id);

            int index = character.TryGetShip(shipId, out Ship ship);

            Contract.Assert(index >= 0 && _ships.ContainsKey(ship.Id));

            character.RemoveShipAt(index);

            Contract.Assert(ship.OwnerID == character.Id);

            ship.OwnerID = 0;

            // calculates the value won and updates the character

            int tradeInValue = GetShipTradeInValue(ship);

            UpdateCharacter(character, tradeInValue);

            // repairs the ship (its price is included in the tradeInValue above)

            RepairShip(ship);

            // updates the hex

            AdjustPopulationCensus(character, -ship.BPV);

            // decides what to do with the ship after we sell it in the shipyard

            switch (_sellingAction)
            {
                case 1:
                    {
                        // puts the ship in the shipyard

                        AddBidItem(ship);

                        Contract.Assert(ship.Race == character.CharacterRace);

                        TrySortBidItems((int)ship.Race);

                        break;
                    }
                case 2:
                    {
                        // gives the ship to a new character

                        CreateCharacter(character.CharacterRace, character.CharacterLocationX, character.CharacterLocationY, ship, out _);

                        break;
                    }
                default:
                    {
                        // removes the ship from the game

                        _ships.Remove(ship.Id);

                        break;
                    }
            }

            Write(client, ClientRequests.PlayerRelayC_0x04_0x00_0x06, character.Id); // A_5
            Write(client, ClientRequests.PlayerRelayC_0x05_0x00_0x07, character.Id); // 14_12
        }

        private void Q_A_5(Client27000 client, int i4, int i5, int i6)
        {
            Character character = client.Character;

            Clear();

            int c = character.ShipCount;

            Contract.Assert(c > 0);

            // supply costs

            for (int i = c - 1; i >= 0; i--)
            {
                Ship ship = character.GetShipAt(i);

                Contract.Assert(_ships.ContainsKey(ship.Id));

                if (ship.Stores.ContainsFighters)
                {
                    foreach (KeyValuePair<string, int> p in _supplyFtrList[(int)character.CharacterRace])
                    {
                        Push(GetFighterBPV(p.Key, p.Value));
                        Push(p.Key);
                    }

                    Push(_supplyFtrList[(int)character.CharacterRace].Count);
                }
                else
                    Push(0x00);

                Push(GetSparePartsCost(ship.ClassType));
                Push(_costMines);
                Push(_costMarines);
                Push(_costShuttles);
                Push(1.0); // fighters cost
                Push(_costMissiles);
                Push(1.0); // unknown cost

                Push(ship.Id);
            }

            Push(c);

            // trade in costs

            for (int i = c - 1; i >= 0; i--)
            {
                Ship ship = character.GetShipAt(i);

                Push(GetTradeInCost(ship.ClassType));

                Push(ship.Id);
            }

            Push(c);

            // repair costs

            for (int i = c - 1; i >= 0; i--)
            {
                Ship ship = character.GetShipAt(i);

                Push(GetRepairCost(ship.ClassType));

                Push(ship.Id);
            }

            Push(c);

            // ships

            for (int i = c - 1; i >= 0; i--)
            {
                Ship ship = character.GetShipAt(i);

                Push(ship);
            }

            Push(c);

            // header

            Push(0x01);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_A_6(Client27000 client, int i4, int i5, int i6, byte[] buffer)
        {
            Character character = client.Character;

            Contract.Assert(BitConverter.ToInt32(buffer, 34) == character.Id);
            Contract.Assert(BitConverter.ToInt32(buffer, 38) == 1);

            int shipId = BitConverter.ToInt32(buffer, 42);

            Ship ship = _ships[shipId];

            int repairCost = GetShipRepairCost(ship);

            if (character.CharacterCurrentPrestige >= repairCost)
            {
                UpdateCharacter(character, -repairCost);

                RepairShip(ship);
            }

            Write(client, ClientRequests.PlayerRelayC_0x05_0x00_0x07, character.Id); // 14_12

            // 19000000 00 010000001400000002000000 04000000 00000000

            Clear();

            Push(0x00);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_A_7(Client27000 client, int i4, int i5, int i6, byte[] buffer)
        {
            Character character = client.Character;

            Contract.Assert(BitConverter.ToInt32(buffer, 34) == character.Id);

            // gets the indexes of the ships

            int c = BitConverter.ToInt32(buffer, 38);

            Contract.Assert(c == character.ShipCount);

            Span<int> shipIndex = stackalloc int[c];

            int p = 42;

            for (int i = 0; i < c; i++)
            {
                int shipId = BitConverter.ToInt32(buffer, p);

                p += 4;

                shipIndex[i] = character.TryGetShip(shipId, out _);

                Contract.Assert(shipIndex[i] != -1);
            }

            // tries to update the names of the ships

            Contract.Assert(BitConverter.ToInt32(buffer, p) == c);

            p += 4;

            for (int i = 0; i < c; i++)
            {
                Ship ship = character.GetShipAt(shipIndex[i]);

                Contract.Assert(BitConverter.ToInt32(buffer, p) == ship.Id);

                p += 4;

                int nameSize = BitConverter.ToInt32(buffer, p);

                p += 4;

                ship.Name = Encoding.UTF8.GetString(buffer, p, nameSize);

                p += nameSize;
            }

            // tries to update the stores of the ships

            Contract.Assert(BitConverter.ToInt32(buffer, p) == c);

            p += 4;

            for (int i = 0; i < c; i++)
            {
                Ship ship = character.GetShipAt(shipIndex[i]);

                Contract.Assert(BitConverter.ToInt32(buffer, p) == ship.Id);

                p += 4;

                int storesSize = Ship.GetStoresSize(buffer, p);

                ShipStores stores = new(buffer, p, storesSize);

                // calculates the cost of updating the current stores

                int currentValue = GetShipStoresCost(ship.Race, ship.ClassType, ship.Stores);
                int newValue = GetShipStoresCost(ship.Race, ship.ClassType, stores);

                // checks if we have enough prestige to make the update

                int cost = newValue - currentValue;

                if (cost < 0)
                    cost = 0;

                if (character.CharacterCurrentPrestige >= cost)
                {
                    UpdateCharacter(character, -cost);

                    ship.Stores = stores;
                }

                p += storesSize;
            }

            // 1e00000000020000000600000005000000 09000000 00000000 07 73021234

            Write(client, ClientRequests.PlayerRelayC_0x05_0x00_0x07, character.Id); //  14_12

            /*
                3e050000 00 020000001400000003000000 29050000 01000000

                02000000

                e9011234 e901123473021234000000000007000000810000008100000005000000462d434152080000005553532056656761020000000f0f0f0f000004040000060606060606060600000c0c040400000202060603030303040408080000000000000606020200000202020200000000000000000000000000000000020202020202000000000202000000000000000000000000000000000000 0101000001000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000004040400000000000000000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff140a140804041e06060000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000010000000000000000000000000000000100000000000000000000000000000001000000000000000000000000000000010000000000000000000000000000000100000000000000000000000000000001000000000000000000000000000000         
                74021234 7402123473021234000000000003000000470000004700000004000000..462d46460800000055535320566567610200000006060606000003030000040405050505040400000000000006060202040402020202020202020000000000000404010100000101010100000000000000000000000000000000010101010101010101010000000000000000000000000000000000000000 0101000001000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000002020200000000000000000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff0c06080402031404040000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001000000000000000000000000000000010000000000000000000000000000000100000000000000000000000000000001000000000000000000000000000000010000000000000000000000000000000100000000000000000000000000000001000000000000000000000000000000
            */

            Clear();

            for (int i = c - 1; i >= 0; i--)
            {
                Ship ship = character.GetShipAt(shipIndex[i]);

                Push(ship);
                Push(ship.Id);
            }

            // header

            Push(c);
            Push(0x01);

            Push(i4, i5, i6);

            Write(client);
        }

        // AVtMapRelayS

        private void Q_D_2(Client27000 client, int i4, int i5, int i6)
        {
            Contract.Assert(client.Other[1] == -1);

            client.Other[1] = i5;

            Clear();

            Push(_mapHeight);
            Push(_mapWidth);

            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_D_3(Client27000 client, int i4, int i5, int i6, byte[] buffer)
        {
            int c = BitConverter.ToInt16(buffer, 34);

            Clear();

            Character character = client.Character;

            if (c == -1)
            {
                /*
                    [Q] 24000000 00 000000000d00000003000000 0f000000 01 010000000e00000002000000 ffff

                    [R] 1a030000 00 010000001100000002000000 05030000
                        40000000
                        000801000000000040400164
                        000840000000010038380964
                        000800000100000130301165
                        (...)
                        070f01000000000011113068
                        171701000000000009093869
                        17170100000000000101406b
                        01

                        // are the messages capped to 32768 bytes or 2728 hexes!?

                        04800000 00 010000000e00000002000000 d58f0000
                        fc0b0000
                        1717010000000000141414c8
                        171704000000000014141ec8
                        (...)
                        171701000000000014141464
                        171700000100000014142364
                        000a800000000000646403
                */

                Push((byte)0x01);

                c = _map.Length;

                for (int i = c - 1; i >= 0; i--)
                {
                    MapHex hex = _map[i];

                    if (
                        (hex.X != character.CharacterLocationX || hex.Y != character.CharacterLocationY) &&
                        (!MovementValid(hex.X, hex.Y, character.CharacterLocationX, character.CharacterLocationY))
                    )
                        hex = MapHex.FogOfWar;

                    Push(hex);
                }
            }
            else
            {
                /*
                    [Q] 24000000 00 000000000d00000003000000 0f000000 01 010000000e00000002000000 5000
                */

                int x = c & 255;
                int y = c >> 8;

                MapHex hex = _map[x + y * _mapWidth];

                if (
                    (hex.X != character.CharacterLocationX || hex.Y != character.CharacterLocationY) &&
                    (!MovementValid(hex.X, hex.Y, character.CharacterLocationX, character.CharacterLocationY))
                )
                    hex = MapHex.FogOfWar;

                Push((byte)y);
                Push((byte)x);
                Push((byte)0x00);

                Push(hex);

                c = 1;
            }

            Push(c);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_D_6(Client27000 client, int i4, int i5, int i6, byte[] buffer)
        {
            // [Q] 32000000 00 000000000d00000006000000 1d000000 01 010000000e00000000000000 55010000 09000000 03000000 00000000

            Contract.Assert(i4 == client.Id && i5 == client.Relays[(int)ClientRelays.MetaViewPortHandlerNameC] && i6 == 0);

            Character character = client.Character;

            Contract.Assert(BitConverter.ToInt32(buffer, 34) == character.Id);

            int destinationX = BitConverter.ToInt32(buffer, 38);
            int destinationY = BitConverter.ToInt32(buffer, 42);

            int moveDelay = 0;
            int moveOpcode;

            if ((character.State & Character.States.IsBusy) != Character.States.IsBusy && MovementValid(character.CharacterLocationX, character.CharacterLocationY, destinationX, destinationY))
            {
                if ((character.Mission & IsMustPlayMask) != IsMustPlayMask)
                {
                    // [R] 2d000000 00 010000000e00000004000000 18000000 02000000 55010000 09000000 03000000 03000000 00000000

                    moveDelay = _humanMovementDelay;
                    moveOpcode = 0x02;

                    BeginHumanMovement(character, destinationX, destinationY);

                    Write(client, ClientRequests.PlayerRelayC_0x03_0x00_0x04, character.Id); // 14_10
                }
                else
                {
                    // [R] 2d0000000 00 10000000e00000004000000 18000000 05000000 55010000 08000000 03000000 00000000 00000000

                    moveOpcode = 0x05;
                }
            }
            else
            {
                // [R] 2d0000000 00 10000000e00000004000000 18000000 06000000 55010000 0a000000 04000000 00000000 00000000

                moveOpcode = 0x06;
            }

            // begins the movement

            Clear();

            Push(0x00);
            Push(moveDelay);
            Push(destinationY);
            Push(destinationX);
            Push(character.Id);
            Push(moveOpcode);
            Push(i4, i5, 0x04);

            Write(client);
        }

        private void Q_D_12(Client27000 client, int i4, int i5, int i6, byte[] buffer)
        {
            CmdSound(client, "computer" + _rand.NextInt32(1, 5));

            // [Q] 2a000000 00 000000000d00000012000000 15000000 01 010000000e00000009000200 00000000 08000000

            int race1 = BitConverter.ToInt32(buffer, 34);
            int race2 = BitConverter.ToInt32(buffer, 38);

            StringBuilder status = new(1024);

            status.Append('(');

            if (race2 <= (int)Races.kLastCartel)
            {
                status.Append("We are at ");

                if ((_alliances[race1] & (RaceMasks)(1 << race2)) != RaceMasks.None)
                    status.Append("peace");
                else
                    status.Append("war");

                status.Append(" with ");
                status.Append(_races[race2]);
            }
            else if (race2 == (int)Races.kNeutralRace)
                status.Append("Contested sector");
            else
                status.Append("Beyond sensor range");

            status.Append(')');

            // [R] 39000000 00 010000000e00000009000200 24000000 01000000 1c000000576520646973747275737420746865204f72696f6e2043617274656c

            Clear();

            Push(status.ToString());
            Push(0x01);
            Push(i4, i5, i6);

            Write(client);
        }

        // AVtMissionMatcherRelayS

        private void Q_E_3(Client27000 client, byte[] buffer)
        {
            Character character = client.Character;

            Contract.Assert(BitConverter.ToInt32(buffer, 21) == 1);

            int missionIndex = BitConverter.ToInt32(buffer, 25);

            Contract.Assert(BitConverter.ToInt32(buffer, 29) == character.Id);

            int mapIndex = character.CharacterLocationX + character.CharacterLocationY * _mapWidth;

            MapHex hex = _map[mapIndex];

            if (buffer[33] == 0)
            {
                if (missionIndex == -1)
                {
                    /*
                        // the character moved or abandoned his missions

                        22000000 00 000000000f00000003000000 0d000000 01000000 ffffffff 42050000 00
                    */

                    Contract.Assert(character.Mission == 0 || (character.Mission & IsMustPlayMask) != IsMustPlayMask);

                    TryLeaveMission(character, hex);
                }
                else
                {
                    /*
                        // the character forfeited a mission

                        22000000 00 000000000f00000003000000 0d000000 01000000 00000000 42050000 00
                    */

                    Contract.Assert((character.Mission & IsMustPlayMask) == IsMustPlayMask);

                    TryLeaveMission(character, hex);
                }

                return;
            }

            /*
                // the character accepted a mission

                22000000 00 000000000f00000003000000 0d000000 01000000 00000000 42050000 01
            */

            Contract.Assert((character.State & Character.States.IsBusy) != Character.States.IsBusy);

            character.State |= Character.States.IsBusy;

            Draft draft = new()
            {
                Countdown = (int)Phases.a2
            };

            if (!_drafts.TryAdd(mapIndex, draft))
                throw new NotSupportedException();

            draft.Expected.Add(character.Id, null);
            draft.Accepted.Add(character.Id, null);

            foreach (KeyValuePair<int, object> p in hex.Population)
            {
                Character target = _characters[p.Key];

                if (target.Id == character.Id)
                    continue;

                if (target.State == Character.States.IsCpuOnline)
                {
                    // AI character

                    Contract.Assert(target.Mission == 0);

                    target.Mission = character.Mission;
                    target.State = Character.States.IsCpuAfkBusyOnline;
                }
                else if (target.State == Character.States.IsHumanOnline && target.Client.LauncherId != 0)
                {
                    // human character with a valid address

                    if (target.Mission == 0)
                    {
                        target.Mission = character.Mission;

                        hex.Mission += (1L << CounterShift);
                    }

                    Contract.Assert(target.Mission == character.Mission);

                    target.State |= Character.States.IsBusy;

                    Clear();

                    PushGuestMissions(target.Client);

                    Write(target.Client);

                    draft.Expected.Add(target.Id, null);
                }
            }
        }

        private void Q_E_4(Client27000 client, byte[] buffer)
        {
            /*
                // a mission failed to start (ex: draft with 7 human players, or bad connectivity)

                21000000 00 000000000f00000004000000 0c000000 48050000 ffffffff ffffffff
            */

            Contract.Assert(BitConverter.ToInt32(buffer, 21) == client.Character.Id);

            // sends a signal to stop the remaining musics in the playlist

            CmdMusic(client, string.Empty);
        }

        private void Q_E_7(Client27000 client, byte[] buffer)
        {
            Character character = client.Character;

            Contract.Assert(character.Mission != 0);

            int draftId = character.CharacterLocationX + character.CharacterLocationY * _mapWidth;

            Draft draft = _drafts[draftId];

            if (buffer[33] == 0)
            {
                /*
                    // the character forfeited a mission

                    22000000 00 000000000f00000007000000 0d000000 01000000 00000000 42050000 00
                */

                draft.Forfeited.Add(character.Id, null);

                Contract.Assert((character.State & Character.States.IsBusy) == Character.States.IsBusy);

                character.State &= ~Character.States.IsBusy;

                MapHex hex = _map[draftId];

                TryLeaveMission(character, hex);

                return;
            }

            /*
                // the character accepted a mission

                22000000 00 000000000f00000007000000 0d000000 01000000 00000000 42050000 01
            */

            draft.Accepted.Add(character.Id, null);
        }

        private void Q_E_C(Client27000 client, byte[] buffer)
        {
            /*
                // a mission was received with success

                26000000 00 000000000e0000000c000000 11000000 01 060000000400000010000000 42050000
            */

            Character character = client.Character;

            Contract.Assert(BitConverter.ToInt32(buffer, 34) == character.Id);

            Character host = _characters[(int)((character.Mission & HostMask) >> HostShift)];
            Draft draft = _drafts[host.CharacterLocationX + host.CharacterLocationY * _mapWidth];

            Contract.Assert(draft.Accepted.ContainsKey(character.Id));

            draft.Confirmed.Add(character.Id, null);

            if (character.Id == host.Id)
                draft.Countdown = (int)Phases.c0 + 1;
        }

        // AVtNewsRelayS

        private void Q_F_2(Client27000 client, int i4, int i5, int i6, byte[] buffer)
        {
            /*
                //********************************************************************************************************************************************************
                // requests news by id

                [Client1] 26000000 00 000000001100000002000000 11000000 01 010000001200000002000000 9a040000
                [Server1] 5a000000 00 010000001200000002000000 45000000

                01000000 // news count

                9a040000 // news id
                00000000
                04
                04       // urgency
                01       // category (0 - universal, 1 - empire, 2 - personal)
                26000000 4e657720676f7665726e6f7220656c656374656420746f2073797374656d202831302c33292e
                2f27e95e
                e9c4c2cc
                ae200a00 // rgb color

                //********************************************************************************************************************************************************
                // requests all news

                [Client1] 26000000 00 000000000f00000002000000 11000000 01 010000001200000002000000 ffffffff
                [Server1] 280a0000 00 010000001200000002000000 130a0000

                1c000000

                e7090000
                00000000
                03
                03
                01
                3f000000 546865204d6972616b2053746172204c65616775652072616e6b732061742032382077697468203020746f74616c2065636f6e6f6d696320706f696e74732e
                032ce95e
                0e4dadad
                bd3a1700

                e6090000
                00000000
                03
                03
                01
                45000000 54686520496e7465727374656c6c617220436f6e636f726469756d2072616e6b732061742032372077697468203020746f74616c2065636f6e6f6d696320706f696e74732e
                032ce95e
                c147adad
                bd3a1700

                (...)

            */

            int info = BitConverter.ToInt32(buffer, 34);
            Character character = client.Character;

            int newsColor;
            string newsText;

            switch (info)
            {
                case -1:
                    newsColor = 0x7f7f7f; // gray
                    newsText = "Welcome " + Enum.GetName(character.CharacterRank) + " " + character.CharacterName + "!";

                    break;

                case 0:
                    newsColor = 0xffffff; // white
                    newsText = "This text is white!";

                    break;

                default:
                    throw new NotImplementedException();
            }

            Clear();

            Push(newsColor);                       // Color
            Push(TimeService.TimeDetail());        // TimeDetail
            Push(TimeService.NewsTime());          // NewsTime
            Push(newsText);                        // IdiomSize, Idiom
            Push((byte)Categories.PlayerSpecific); // Category
            Push((byte)UrgencyLevels.Ultra);       // UrgencyLevel
            Push((byte)PersistenceLevels.Default); // PersistenceLevel
            Push(0x00);                            // LockID
            Push(GetNextDataId());                 // ID

            // count

            Push(0x01);

            Push(i4, i5, i6);

            Write(client);
        }

        // AVtChatRelayS

        private void Q_11_2(Client27000 client, int i4, int i5, int i6)
        {
            Contract.Assert(_channels.Length == 20);

            Clear();

            // @Standard

            Push(_channels[0]);

            // #General, #ServerBroadcast

            for (int i = 3; i >= 2; i--)
            {
                Push(_channels[0]);
                Push(_channels[i]);
            }

            // #SystemBroadcast

            Push(0x00);
            Push(_channels[1]);

            // # empires and cartels

            for (int i = 19; i >= 4; i--)
            {
                Push(_channels[0]);
                Push(_channels[i]);
            }

            Push(19);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_11_4(Client27000 client, int i4, int i5, int i6)
        {
            Clear();

            Push(0x00);

            Push(defaultIrcChannel); // default IRC server channel
            Push(_serverNick);       // VerboseName
            Push(_serverNick);       // Name
            Push(_serverNick);       // NickName
            Push(defaultIrcPort);    // default IRC server port
            Push(_hostAddress);      // default IRC server address

            Push(0x01);
            Push(i4, i5, i6);

            Write(client);
        }

        // AVtCharacterRelayS

        private void Q_14_2(Client27000 client, int i4, int i5, int i6)
        {
            // checks if we are creating a new character
            // or reconnecting with an existing one

            Character character = client.Character;

            if (character.CharacterName.Length == 0)
            {
                /*
                    0x01 - there was an unknown error while attempting to connect 
                    0x03 - this server is currently full. please try again later
                    0x04 - the password you supplied is incorrect
                    0x08 - <creates a new char>
                    0x0a - the character you are specifying is already logged on
                */

                Clear();

                Push(Character.Empty, isHexCacheSet: false, isFlagSet: false);
                Push(0x08);
                Push(i4, i5, i6);

                Write(client);

                return;
            }

            Contract.Assert(character.State == Character.States.IsHumanBusyConnecting);

            character.State = Character.States.IsHumanBusyReconnecting;

            AddToHexPopulation(character);

            /*
                0x01 - there was an unknown error while attempting to connect 
                0x02 - <reconnecting with existing char>
                0x03 - this server is currently full. please try again later
                0x04 - the password you supplied is incorrect
                0x08 - <creates a new char>
                0x0a - the character you are specifying is already logged on
            */

            Clear();

            Push(character, isHexCacheSet: false, isFlagSet: true);
            Push(0x02);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_14_3(Client27000 client, int i4, int i5, int i6, byte[] buffer)
        {
            // checks if the server is currently locked

            if (_savegameState > 0)
            {
                Clear();

                Push(Character.Empty, isHexCacheSet: false, isFlagSet: false);
                Push(0x03);
                Push(i4, i5, i6);

                Write(client);

                return;
            }

            /*
                [Q] cd00000000000000001500000003000000b8000000010c000000040000000300000000000000
                13000000 643476316b733740686f746d61696c2e636f6d
                00000000
                07000000 443476316b7336
                06000000 
                00000000ffffffffdc0500000000000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffff0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
            */

            // gets the won logon

            int c = BitConverter.ToInt32(buffer, 38);

            string wonLogon = Encoding.UTF8.GetString(buffer, 42, c);

            int p = 42 + c;

            // skips the id

            Contract.Assert(BitConverter.ToInt32(buffer, p) == 0);

            p += 4;

            // reads the name

            c = BitConverter.ToInt32(buffer, p);
            p += 4;

            string name = Encoding.UTF8.GetString(buffer, p, c);

            p += c;

            // reads the race

            Races race = (Races)BitConverter.ToInt32(buffer, p);

            // checks if a character with this name already exists
            // if not, then finishes its creation

            if (TryInitializeCharacter(client, wonLogon, name, race))
            {
                // send the partial character

                Clear();

                Push(client.Character, isHexCacheSet: false, isFlagSet: false);
                Push(0x00);
                Push(i4, i5, i6);

                Write(client);

                // finalizes the character

                FinalizeCharacter(client);

                // starts the login process

                BeginLoginClient(client);

                // sends the full character

                Contract.Assert(i6 == 0x03);

                Clear();

                Push(client.Character, isHexCacheSet: false, isFlagSet: true);
                Push(0x00);
                Push(i4, i5, i6 - 1);

                Write(client);
            }
            else
            {
                Clear();

                Push(Character.Empty, isHexCacheSet: false, isFlagSet: false);
                Push(0x05);
                Push(i4, i5, i6);

                Write(client);
            }
        }

        private void Q_14_8(Client27000 client, int i4, int i5, int i6, byte[] buffer, int size)
        {
            Character character;

            Clear();

            int c = 0;

            if (size == 39)
            {
                // [Q] 27000000 00 000000001500000008000000 12000000 01 010000000b00000001000100 00000000 01

                Contract.Assert(BitConverter.ToInt32(buffer, 34) == 0 && buffer[38] == 1);

                // apparently, the i5 from here is used in Q_1_1() as i2

                Contract.Assert(client.Other[0] == -1);

                client.Other[0] = i5;

                // creates a list of all the online characters

                Contract.Assert(client.IdList.Count == 0);

                foreach (KeyValuePair<string, int> p in _characterNames)
                {
                    if (_characters.TryGetValue(p.Value, out character) && (character.State & Character.States.IsHumanOnline) == Character.States.IsHumanOnline)
                    {
                        client.IdList.Add(character.Id, null);

                        Push(character, isHexCacheSet: true, isFlagSet: true);

                        c++;
                    }
                }

                if (c == 0)
                {
                    character = client.Character;

                    Contract.Assert(character.State == Character.States.IsHumanBusyConnecting);

                    client.IdList.TryAdd(character.Id, null);

                    Push(character, isHexCacheSet: true, isFlagSet: true);

                    c++;
                }

                /*
                    // tries to add the current character

                    character = client.Character;

                    client.IdList.TryAdd(character.Id, null);

                    Push(character, isHexCacheSet: true, isFlagSet: true);

                    c++;

                    // tries to add all the other characters

                    //int allied = (int)_alliances[(int)client.Character.CharacterRace];

                    foreach (KeyValuePair<int, Character> p in _characters)
                    {
                        character = p.Value;

                        if ((character.State & Character.States.IsOnline) != 0) // && ((1 << (int)character.CharacterRace) & allied) != 0)
                        {
                            client.IdList.TryAdd(character.Id, null);

                            Contract.Assert((character.CharacterRace >= Races.kFirstNPC) || (character.IPAddress.Length != 0 && character.WONLogon.Length != 0));

                            Push(character, isHexCacheSet: true, isFlagSet: true);

                            c++;

                            if (c >= 900) // relative cap per race (due to how many we can send in a msg at once)
                                break;
                        }
                    }
                */
            }
            else
            {
                // [Q] 2b000000 00 000000001500000008000000 16000000 01 010000000600000003000100 01000000 d2000000 00
                //     2b000000 00 000000001400000008000000 16000000 01 300000000d00000004000000 01000000 f2200000 01

                Contract.Assert(BitConverter.ToInt32(buffer, 34) == 1);

                // gets the character id

                int characterId = BitConverter.ToInt32(buffer, 38);

                Contract.Assert(characterId != 0);

                // checks if the id is valid and the character is online

                if (_characters.TryGetValue(characterId, out character) && (character.State & Character.States.IsOnline) == Character.States.IsOnline)
                {
                    Push(character, isHexCacheSet: true, isFlagSet: true);

                    c++;
                }
                else
                {
                    character = client.Character;

                    Contract.Assert(character.Id == characterId && character.State == Character.States.IsHumanBusyConnecting);

                    Push(character, isHexCacheSet: true, isFlagSet: true);

                    c++;
                }
            }

            Push(c);
            Push(0x00);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_14_F(Client27000 client, int i4, int i5, int i6)
        {
            /*
                [Q] 26000000 00 00000000150000000f000000 11000000 01 010000000e00000007000200 55010000
                [R] 36000000 00 010000000e00000007000200 21000000 00000000
                    01000000
                    55010000 56010000 03000000 0a000000 04000000 01 01000000
            */

            Character character = client.Character;

            Contract.Assert(character.BestShipId != 0);

            // adds the current character to a list

            Span<int> idList = stackalloc int[22]; // 7 * 3 + 1

            idList[0] = character.Id;

            Span<int> ids = idList[1..];
            int c = 1;

            // sorts the remaining characters by bpv

            int teamMask = 1 << (int)character.CharacterRace;
            int allyMask = (int)_alliances[(int)character.CharacterRace];
            int neutralMask = (int)_alliances[(int)Races.kNeutralRace];

            int[] locationIncrements = _locationIncrements[character.CharacterLocationX & 1];
            int i1 = character.CharacterLocationX + character.CharacterLocationY * _mapWidth;

            Span<int> list = stackalloc int[48]; // { target.Id1, target.BestShipBPV1 }, { target.Id2, target.BestShipBPV2 }, { target.Id3, target.BestShipBPV3 }, ...

            for (int i = 0; i < 7; i++)
            {
                int i2 = i1 + locationIncrements[i];

                if (!MovementValid(i1, i2))
                    continue;

                Dictionary<int, object> hexPopulation = _map[i2].Population;

                if (hexPopulation.Count == 0)
                    continue;

                int teammates = 0;  // list
                int allies = 0;     // list[12..]
                int neutrals = 0;   // list[24..]
                int enemies = 0;    // list[36..]

                list.Clear();

                foreach (KeyValuePair<int, object> p in hexPopulation)
                {
                    Character target = _characters[p.Key];

                    if (target.Id != character.Id && ((1 << (int)target.BestShipClass) & ClassTypeIconMask) != 0)
                    {
                        int mask = 1 << (int)target.CharacterRace;

                        if (mask == teamMask)
                            FilterIcons(target.Id, target.BestShipBPV, list, ref teammates);
                        else if ((mask & allyMask) != 0)
                            FilterIcons(target.Id, target.BestShipBPV, list[12..], ref allies);
                        else if ((mask & neutralMask) != 0)
                            FilterIcons(target.Id, target.BestShipBPV, list[24..], ref neutrals);
                        else // enemy mask
                            FilterIcons(target.Id, target.BestShipBPV, list[36..], ref enemies);
                    }
                }

                while (teammates + allies + neutrals + enemies > 3)
                {
                    if (neutrals > 1)
                        neutrals--;
                    else if (enemies > 1)
                        enemies--;
                    else if (allies > 1)
                        allies--;
                    else if (teammates > 1)
                        teammates--;
                    else
                    {
                        Contract.Assert(allies > 0);

                        allies--;
                    }
                }

                if (teammates > 0)
                {
                    c += teammates;

                    PushIds(list, teammates, ref ids);
                }

                if (allies > 0)
                {
                    c += allies;

                    PushIds(list[12..], allies, ref ids);
                }

                if (enemies > 0)
                {
                    c += enemies;

                    PushIds(list[36..], enemies, ref ids);
                }

                if (neutrals > 0)
                {
                    c += neutrals;

                    PushIds(list[24..], neutrals, ref ids);
                }
            }

            idList.Sort();

            Clear();

            for (int i = 21, j = 22 - c; i >= j; i--)
            {
                Character owner = _characters[idList[i]];
                byte location = owner.Id == character.Id ? (byte)1 : (byte)0;
                int icon = _classTypeIcons[(int)owner.BestShipClass];

                Contract.Assert(icon != -1);

                Push((int)owner.CharacterRace);
                Push(location);
                Push(icon);
                Push(owner.CharacterLocationY);
                Push(owner.CharacterLocationX);
                Push(owner.BestShipId);
                Push(owner.Id);
            }

            Push(c);
            Push(0x00);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_14_10(Client27000 client, int i4, int i5, int i6)
        {
            Character character = client.Character;

            Contract.Assert(character.CharacterLocationX != character.MoveDestinationX || character.CharacterLocationY != character.MoveDestinationY);

            MapHex hex = _map[character.CharacterLocationX + character.CharacterLocationY * _mapWidth];

            Contract.Assert(hex.X == character.CharacterLocationX && hex.Y == character.CharacterLocationY);

            // updates the character details, supply and shipyard availability, in the UI

            Clear();

            Push(character.MoveDestinationY);
            Push(character.MoveDestinationX);

            PushHexCache(hex, 1);

            Push(0x00);
            Push(i4, i5, i6);

            Write(client);

            // checks if we need to continue a movement

            if (character.MoveDestinationX != -1 && character.MoveDestinationY != -1)
                ContinueHumanMovement(character);
        }

        private void Q_14_12(Client27000 client, int i4, int i5, int i6)
        {
            Clear();

            Push(client.Character.CharacterCurrentPrestige);
            Push(0x00);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_14_13(Client27000 client, int i4, int i5, int i6)
        {
            /*
                // never found how to use this in this server, so we just return an empty list, if the client ends up requesting this data for some reason
                // apparently in the stock server this is used to remove an entry from the shipyard list before a global request is done (each 8 seconds)
                // but in this server the client seems to ignore any request i make to it (see void CurrentlyKnownRequests())

                Q: 26000000 00 000000001500000013000000 11000000 01 020000000600000007000200 73021234

                // no bids

                R: 1d000000 00 020000000600000007000200 08000000 00000000
                   00000000 // bid count

                // one bid

                R: 21000000 00 020000000600000007000200 0c000000 00000000
                   01000000 // bid count
                   9e1f1234 // bid id
            */

            Clear();

            Push(0x00);
            Push(0x00);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_14_14(Client27000 client, int i4, int i5, int i6)
        {
            Clear();

            Push((int)client.Character.CharacterRank);
            Push(0x00);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_14_15(Client27000 client, int i4, int i5, int i6)
        {
            Clear();

            Push((int)client.Character.Awards);
            Push(0x00);
            Push(i4, i5, i6);

            Write(client);
        }

        // AVtNotifyRelayS

        private static void Q_15_2(Client27000 client, byte[] buffer, int size)
        {
            // 5a000000 00 000000001600000002000000 45000000 00000000 00000000 00000000 0d e2010000 2b000000643476316b7340686f746d61696c2e636f6d4d657461436c69656e74537570706c79446f636b50616e656c 04000000 00

            if (client.State != Client27000.States.IsOnline)
            {
                ReadOnlySpan<byte> b = new(buffer, 0, size - 5);

                int i;

                for (i = 0; i < (int)ClientRelays.Total; i++)
                {
                    if (b.EndsWith(_clientStrings[i]))
                        break;
                }

                if (i < (int)ClientRelays.Total)
                {
                    int flag = buffer[33];
                    int i3 = BitConverter.ToInt32(buffer, size - 5);

                    string name = Encoding.UTF8.GetString(_clientStrings[i]) + "_0x" + i3.ToString("x2") + "_0x00_0x" + flag.ToString("x2");

                    if (Enum.IsDefined(typeof(ClientRequests), name))
                    {
                        for (int j = 0; j < (int)ClientRequests.Total; j++)
                        {
                            int[] requests = client.Requests[j];

                            if (requests[0] == i && requests[3] == flag)
                            {
                                requests[1] = i3;

                                return;
                            }
                        }
                    }

                    throw new NotSupportedException("CLIENT OPCODES: " + name + " is not defined!");
                }

                throw new NotSupportedException("CLIENT OPCODES: received an unknown relay notification!");
            }

            throw new NotSupportedException("CLIENT OPCODES: the client is already online!");
        }

        private void Q_15_3(Client27000 client, byte[] buffer)
        {
            Character character = client.Character;

            switch (buffer[25])
            {
                case 0x03:
                    {
                        Write(client, ClientRequests.PlayerRelayC_0x03_0x00_0x03, character.Id); // 14_10

                        break;
                    }
                case 0x0d:
                    {
                        if (client.Relays[(int)ClientRelays.MetaViewPortHandlerNameC] != -1)
                            Write(client, ClientRequests.MetaViewPortHandlerNameC_0x07_0x00_0x0d, character.Id); // nothing

                        Clear();

                        Push(client, ClientRequests.MetaClientShipPanel_0x05_0x00_0x0d, character.Id); // nothing
                        Push(client, ClientRequests.MetaClientSupplyDockPanel_0x04_0x00_0x0d, character.Id); // nothing

                        Write(client);

                        // checks if we are in the end of the login phase

                        if (client.State == Client27000.States.IsRegistered)
                        {
                            for (int i = 0; i < (int)ClientRequests.Total; i++)
                            {
                                int[] requests = client.Requests[i];

                                string name1 = Enum.GetName(typeof(ClientRequests), (ClientRequests)i);
                                string name2 = Encoding.UTF8.GetString(_clientStrings[requests[0]]) + "_0x" + requests[1].ToString("x2") + "_0x" + requests[2].ToString("x2") + "_0x" + requests[3].ToString("x2");

                                if (!name1.Equals(name2, StringComparison.Ordinal))
                                    throw new NotSupportedException();
                            }

                            // refreshes the medals

                            Write(client, ClientRequests.PlayerRelayC_0x08_0x00_0x0c, character.Id); // 14_15 (not used)

                            // finalizes the login

                            LoginClient(client);

                            client.State |= Client27000.States.Online;
                        }

                        break;
                    }
                case 0x0e:
                    {
                        Write(client, ClientRequests.MetaClientSupplyDockPanel_0x05_0x00_0x0e, character.Id); // nothing

                        break;
                    }

#if DEBUG
                default:
                    {
                        Debugger.Break();

                        break;
                    }
#endif

            }
        }

        // AVtSecurityRelayS

        private void Q_19_2(Client27000 client, int i4, int i5, int i6, byte[] buffer, int size)
        {
            /*
                [Q] 630a0000000000000018000000020000004e0a0000010100000002000000020001005c0000000e000000626f6e5f6865617465722e736372e83bc09a0b000000626f6e5f6869742e736372196b775713000000626f6e5f7069656365616374696f6e2e7363727ddfabd30b0000006674726c6973742e7478744d3265951000000067656e5f706c61796261636b2e736372a0a2b5cc100000006d65745f3130706174726f6c2e7363729e260a1e140000006d65745f3131636f6e766f79726169642e736372c0dce4fd160000006d65745f3132636f6e766f796573636f72742e736372b52b9168110000006d65745f31336d6f6e737465722e736372b521cc4f100000006d65745f3134656e69676d612e736372b23d3c76150000006d65745f313562617365646566656e73652e736372f8c26743150000006d65745f313673686970646566656e73652e7363724ae988b6100000006d65745f3137706174726f6c2e736372bdd893041a0000006d65745f3138686f6d65776f726c6461737361756c742e736372200dacf90e0000006d65745f31397363616e2e7363727498a0910e0000006d65745f3173636f75742e736372dd990f59120000006d65745f323073757072697365722e736372024fea8d160000006d65745f3231646973747265737363616c6c2e7363724e2e1641120000006d
                65745f32326469706c6f6d61742e736372980a1c0d120000006d65745f323371756172746572732e73637279a2f2ca110000006d65745f3234616e6f6d616c792e73637208705068140000006d65745f32357375706572666c6565742e73637211702d6c190000006d65745f323661737465726f696461737375616c742e73637287c77cd4190000006d65745f323761737465726f6964646566656e73652e73637268c3d5ac150000006d65745f32386e65676f74696174696f6e2e7363723bc805e0120000006d65745f323972656368617267652e73637251b1aa26160000006d65745f32686f6c64696e67616374696f6e2e7363720de77e23110000006d65745f333073616c766167652e736372d3b9283c130000006d65745f333165706963656e7465722e736372d35a0b6a110000006d65745f33616d6275736865652e73637206a18259110000006d65745f34616d6275736865722e736372875dc415140000006d65745f35666c656574616374696f6e2e7363728ca218c90f0000006d65745f36706174726f6c2e7363727fcbd8e1140000006d65745f376261736561737361756c742e736372356ae6d8140000006d65745f387368697061737361756c742e7363724db299f5190000006d65745f39706c616e657461727961737361756c742e73637213843b960e0000006d65745f636f6d6d6f6e2e736372556
                bed861c0000006d65745f7374617262617365636f6e737472756374696f6e2e73637231bb6ee8100000006d756c5f3130686f636b65792e7363720a325a0d120000006d756c5f313174696e79666573742e736372a97df698120000006d756c5f3132736c7567666573742e73637281d8832a160000006d756c5f313372616e646f6d626174746c652e7363724a144fa4150000006d756c5f313474696d6564626174746c652e7363729df35bdd140000006d756c5f31367375706572666c6565742e7363724a659b25110000006d756c5f31376d6f6e737465722e7363723d062bcc150000006d756c5f31386d6f6e737465726d6173682e736372638abdd4130000006d756c5f31397363616e68617070792e73637257fa404e110000006d756c5f316672656534616c6c2e7363725ece7273120000006d756c5f323073746172626173652e736372d538fce8140000006d756c5f326261736561737361756c742e736372ea1e6e38140000006d756c5f33626174746c6566657374732e73637242b3ba66140000006d756c5f34746f75726e6579666573742e736372be41b943170000006d756c5f35626174746c65666573746c6974652e736372d946f470180000006d756c5f36746f75726e6579666573746c6974652e736372912d1ddc100000006d756c5f37746f75726e65792e7363728f1204f8140000006d756c5f
                397465616d61737361756c742e736372962bd709100000006d756c5f696e7472756465722e7363722f001a800c0000006d756c5f74776f6b2e736372790c5af60c000000736869706c6973742e747874250bd91211000000736b695f316672656534616c6c2e736372629bc32512000000736b695f677265656e6e676f6c642e736372944711570c000000736b695f686f6f642e7363722a04186d12000000736b695f7375706572666c6565742e7363720ed362c917000000736b695f7375707269736572657665727365642e7363725a3d8e5d0e000000736b695f7473686f6f742e7363729794f6940c000000736b695f74776f6b2e736372eac0928a12000000736b695f7761726f66726f7365732e7363723adca6580d000000736b695f777265636b2e73637220389df90f00000073746172666c6565746f702e657865a095ca290d0000007475745f7832355f312e736372a7c445df0d0000007475745f7832355f322e7363721de79be60d0000007475745f7832355f332e7363724e228cfd0d0000007475745f7832355f342e736372dc73c82b160000007475745f7832355f636f6d6d616e643139302e736372c896b06e160000007475745f7832355f636f6d6d616e643239302e7363726c86468d160000007475745f7832355f636f6d6d616e643539302e736372d1be6059160000007475745f7832355f73636
                9656e63653331302e7363725dc1d2ee160000007475745f7832355f776561706f6e733338302e736372bb321300160000007475745f7832355f776561706f6e733438302e7363723ef547000f0000007832355f62756768756e742e7363720e28e7e2120000007832355f636174636874686965662e73637203414f790f0000007832355f64656164656e642e736372ddd6260a0c0000007832355f666163652e736372893d0d37100000007832355f68656c6c6261636b2e736372cb788cba0c0000007832355f686972652e7363729af482d30d0000007832355f686f6e6f722e7363723cb7518c110000007832355f6c61776e6f726465722e736372d2a82e110e0000007832355f6c6567656e642e736372ad571ac9110000007832355f6c6567656e646172792e7363726bb660830c0000007832355f727573652e736372c8241a630f0000007832355f746865746573742e7363727d08d5750e0000007832355f74726176656c2e73637260cc0214480000003338316261396338313661353765623265333531346562333562396432663235306363353333643030373835653230396632373730303064396438343766376630663066323339310c0000003139322e3136382e312e373112000000643476316b7340686f746d61696c2e636f6d00000000000000000000000000000000ffffffffdc0500000000000000
                00000040fc3503ffffffffffffffffffffffffffffffffffffffffffffffff0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000
            */

            string warning;

            // checks if the server is currently locked

            if (_savegameState > 0)
            {
                warning = defaultMaintenanceWarning;

                goto sendWarning;
            }

            // gets the number of files

            int c = BitConverter.ToInt32(buffer, 34);
            int p = 38;

            // gets the names and CRCs of the files

            Dictionary<string, uint> clientFiles = new(StringComparer.OrdinalIgnoreCase);

            for (int i = c; i > 0; i--)
            {
                c = BitConverter.ToInt32(buffer, p);
                p += 4;

                string file = Encoding.UTF8.GetString(buffer, p, c);

                p += c;

                uint crc = BitConverter.ToUInt32(buffer, p);

                p += 4;

                clientFiles.Add(file, crc);
            }

            // gets the hash string

            c = BitConverter.ToInt32(buffer, p);
            p += 4;

            string hash = Encoding.UTF8.GetString(buffer, p, c);

            p += c;

            Contract.Assert(hash.Length == 72);

#if DEBUG
            MemoryStream m = null;
            BinaryReader r = null;

            try
            {
                m = new(buffer);
                r = new(m, Encoding.UTF8, true);

                m.Seek(p, SeekOrigin.Begin);

                // the client keeps a cache between sessions

                CharacterCache characterCache = new(r);
                HexCache hexCache = new(r);

                Contract.Assert(r.ReadByte() == Math.Sign(characterCache.CharacterName.Length));
                Contract.Assert(r.ReadInt32() == 0);
                Contract.Assert(r.ReadInt32() == 0);

                // end of the cache

                Contract.Assert(m.Position == size);
            }
            catch (Exception)
            {
                Debugger.Break(); // !? 
            }
            finally
            {
                r?.Dispose();
                m?.Dispose();
            }
#endif

            // gets the ip address

            c = BitConverter.ToInt32(buffer, p);
            p += 4;

            string ipAddress = Encoding.UTF8.GetString(buffer, p, c);

            p += c;

            Console.WriteLine("  IpAddress = " + ipAddress);

            // checks if the security check was successful

            if (client.IPAddress.Equals(ipAddress, StringComparison.Ordinal))
            {
                if (TryValidateClientFiles(clientFiles, out warning))
                {
                    // checks if the client is reconnecting

                    client.IsReconnecting = buffer[size - 9] == 1;

                    if (!TryCreateOrUpdateCharacter(client))
                    {
                        warning = defaultBusyWarning;

                        goto sendWarning;
                    }

                    // [R] 3a0000000001000000020000000200010025000000 0100000000000000 190000005375636365737366756c20736563757269747920636865636b

                    Clear();

                    Push("Successful security check");
                    Push(0x00);
                    Push(0x01);
                    Push(i4, i5, i6);

                    Write(client);

                    return;
                }
            }
            else
                warning = "The IPAddress reported by your game doesn't match the IPAddress seen by the server!";

        sendWarning:

            // [R] d300000000020000000200000002000100be000000 0100000001000000 b20000005468697320736572766572206861732064657465637465642074686174206f6e65206f72206d6f7265206f6620746865206e65636573736172792066696c657320726571756972656420746f20636f6e6e656374206f7220656974686572206d697373696e67206f7220696e636f6d70617469626c652e20204c697374206f66206f6666656e64696e672066696c65733a206d65745f78706174726f6c2e7363727c204d697373696e672046696c65207c20

            Clear();

            Push(warning);
            Push(0x01);
            Push(0x01);
            Push(i4, i5, i6);

            Write(client);
        }

        private void Q_19_3(Client27000 client, int i4, int i5, int i6)
        {
            // [Q] 22000000 00 000000001800000003000000 0d000000 01 010000000200000001000100
            // [R] 19000000 00 010000000200000001000100 04000000 01000000

            Clear();

            Push(0x01);
            Push(i4, i5, i6);

            Write(client);
        }

        private static void Q_19_4(Client27000 client, byte[] buffer)
        {
            // 25000000 00 000000001800000004000000 10000000 0c0000003139322e3136382e312e3731

            Contract.Assert(client.IPAddress.Equals(Encoding.UTF8.GetString(buffer, 25, BitConverter.ToInt32(buffer, 21)), StringComparison.Ordinal));
        }

        // Requests

#if DEBUG
        private void CurrentlyKnownRequests(Client27000 client, Character character)
        {
            // for exclusive use --------------------------------------------------------------------------------------------------------------------------------------------------

            Write(client, ClientRequests.MetaViewPortHandlerNameC_0x07_0x00_0x0d, character.Id); // nothing (Q_15_3)

            Write(client, ClientRequests.PlayerRelayC_0x03_0x00_0x03, character.Id); // 14_10 (Q_15_3)
            Write(client, ClientRequests.PlayerRelayC_0x04_0x00_0x05, character.Id); // A_5 (called in the end of a ship purchase)
            Write(client, ClientRequests.PlayerRelayC_0x06_0x00_0x08, character.Id); // 14_13 (called in the end of a ship purchase)

            Write(client, ClientRequests.MetaClientPlayerListPanel_0x02_0x00_0x00, character.Id); // 14_8 (called to add a name)
            Write(client, ClientRequests.MetaClientPlayerListPanel_0x03_0x00_0x01, character.Id); // nothing (called to remove a name)

            Write(client, ClientRequests.MetaClientShipPanel_0x05_0x00_0x0d, character.Id); // nothing (Q_15_3)

            Write(client, ClientRequests.MetaClientSupplyDockPanel_0x04_0x00_0x0d, character.Id); // nothing (Q_15_3)
            Write(client, ClientRequests.MetaClientSupplyDockPanel_0x05_0x00_0x0e, character.Id); // nothing (Q_15_3)

            // for general use ----------------------------------------------------------------------------------------------------------------------------------------------------

            // hex data (partial or complete)
            Write(client, ClientRequests.MetaViewPortHandlerNameC_0x03_0x00_0x00, (character.CharacterLocationX << 16) + character.CharacterLocationY); // D_3 (or -1)
            // hex icons
            Write(client, ClientRequests.MetaViewPortHandlerNameC_0x06_0x00_0x0f, 0x00); // 14_F

            // character data
            Write(client, ClientRequests.PlayerRelayC_0x02_0x00_0x02, character.Id); // 14_8
            // hex data + character destination
            Write(client, ClientRequests.PlayerRelayC_0x03_0x00_0x04, character.Id); // 14_10
            // ship data (entire fleet)
            Write(client, ClientRequests.PlayerRelayC_0x04_0x00_0x06, character.Id); // A_5
            // character.CharacterCurrentPrestige
            Write(client, ClientRequests.PlayerRelayC_0x05_0x00_0x07, character.Id); // 14_12
            // character.CharacterRank  
            Write(client, ClientRequests.PlayerRelayC_0x07_0x00_0x0b, character.Id); // 14_14
            // character.Awards 
            Write(client, ClientRequests.PlayerRelayC_0x08_0x00_0x0c, character.Id); // 14_15

            // news query
            Write(client, ClientRequests.MetaClientNewsPanel_0x03_0x00_0x00, -1); // f_2 (info >= 0 to request a specific news)
        }
#endif

    }
}
