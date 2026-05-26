//#define USING_SHIP_TEAM_STATES

using shrServices;

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace SfcOpServer
{
    public partial class GameServer
    {
        public const int DraftCooldown = 60; // s

        // mission details

        private const string defaultMissionTitle = "- Patrol -";

        private const int defaultMissionSpeed = 8;

        // shift constants

        private const int HostShift = 0;
        private const int MissionShift = 31;
        private const int IsMustPlayShift = 40;

        private const int CounterShift = 41;
        private const int IsClosedShift = 44;

        // mask constants

        private const long HostMask = 0x7fffffffL << HostShift;
        private const long MissionMask = 0x1ffL << MissionShift;
        private const long IsMustPlayMask = 0x1L << IsMustPlayShift;

        private const long MissionFilter = HostMask | MissionMask | IsMustPlayMask;

        private const long CounterMask = 0x7L << CounterShift;
        private const long IsClosedMask = 0x1L << IsClosedShift;

        private enum Phases
        {
            // phase 1

            a0 = 0x0,

            a1 = a0 + 1, // cooldown interrupt
            a2 = a0 + (DraftCooldown * smallTicksPerSecond),

            // phase 2

            b0 = 0x1000,

            b1 = b0 + (smallTicksPerSecond - 1), // ~1s

            // phase 3

            c0 = 0x2000,

            c1 = c0 + ((smallTicksPerSecond * 2) - (smallTicksPerSecond / 4)) // ~2s
        }

        // private functions

        private void ResetAvailableMissions()
        {
            Contract.Assert(_availableMissions.Count == 0);

            for (int i = 0; i < _missionNames.Length; i++)
                _availableMissions.Add(i);
        }

        private void TryGetMission(Character character)
        {
            Contract.Assert(character.Mission == 0);

            MapHex destination = _map[character.MoveDestinationX + character.MoveDestinationY * _mapWidth];

            if (destination.Mission == 0)
            {
                // checks if we meet the minimum requirements for a mission

                if (destination.Population.Count == 0)
                    return;

                Contract.Assert(!destination.Population.ContainsKey(character.Id));

                // grabs a mission from the list

                int i = _rand.NextInt32(_availableMissions.Count);

                long hostId = character.Id;
                long missionId = _availableMissions[i];
                long isMustPlay = 0; // _rand.NextInt32(2);

                _availableMissions.RemoveAt(i);

                if (_availableMissions.Count == 0)
                    ResetAvailableMissions();

                long mission = (hostId << HostShift) | (missionId << MissionShift) | (isMustPlay << IsMustPlayShift);

                // updates the hex

                destination.Mission = mission | (1L << CounterShift);

                // updates the character

                character.Mission = mission;
            }
            else if ((destination.Mission & IsClosedMask) != IsClosedMask && ((destination.Mission & CounterMask) >> CounterShift) < 6)
            {
                // updates the hex

                destination.Mission += (1L << CounterShift);

                // updates the character

                character.Mission = destination.Mission & MissionFilter;
            }
        }

        private void TryLeaveMission(Character character, MapHex hex)
        {

#if DEBUG
            bool isDone = false;
#endif

            bool hexUpdated = false;

            if (character.Mission != 0)
            {
                Contract.Assert((character.Mission & MissionFilter) == character.Mission);

                if (character.Mission == (hex.Mission & MissionFilter))
                {
                    Contract.Assert((hex.Mission & CounterMask) != 0);

                    hex.Mission -= (1L << CounterShift);

                    //Debug.WriteLine((hex.Mission & CounterMask) >> CounterShift);

                    if ((hex.Mission & CounterMask) == 0)
                    {
                        int draftId = hex.X + hex.Y * _mapWidth;

                        if (_drafts.TryGetValue(draftId, out Draft draft))
                        {
                            //if ((draft.Flags & 1) == 1)
                            //    TryFixReportIssues(draft);

                            foreach (KeyValuePair<int, Team> t in draft.Mission.Teams)
                            {
                                Team team = t.Value;

                                if (_characters.TryGetValue(team.OwnerId, out Character owner))
                                {
                                    Contract.Assert((owner.Mission & MissionFilter) == owner.Mission);

                                    if (owner.Mission == (hex.Mission & MissionFilter))
                                    {
                                        // releases the AI from the mission

                                        if ((owner.State & Character.States.IsCpu) == Character.States.IsCpu)
                                        {
                                            if (_characters.ContainsKey(owner.Id))
                                            {
                                                Contract.Assert(owner.ShipCount > 0 && owner.Mission != 0 && owner.State == Character.States.IsCpuAfkBusyOnline);

                                                owner.Mission = 0;
                                                owner.State = Character.States.IsCpuOnline;
                                            }
                                        }
                                    }
                                }

                                // deletes all the ships that were destroyed

                                foreach (KeyValuePair<int, object> s in team.Ships)
                                {
                                    if (_ships.TryGetValue(s.Key, out Ship ship))
                                    {
                                        if (ship.Systems.Items[(int)SystemTypes.ExtraDamage] == 0)
                                        {
                                            Contract.Assert(ship.Systems.Items[(int)SystemTypes.ExtraDamageMax] != 0);

                                            if (ship.OwnerID == team.OwnerId)
                                            {
                                                // the ship was destroyed

                                                _ships.Remove(ship.Id);
                                            }
                                            else
                                            {
                                                // the ship was captured (falsely reported as being destroyed)

                                                ship.Systems.Items[(int)SystemTypes.ExtraDamage] = 1;
                                            }
                                        }
                                    }

#if DEBUG
                                    else
                                    {
                                        /*
                                            this happened while i was in a mission, when i ALT+F4.
                                            my ship was missing from the _ships, so an error was being thrown here
                                            don't remember if it was intended or not...
                                        */

                                        //Debugger.Break();
                                    }
#endif

                                }
                            }

#if DEBUG
                            isDone = true;
#endif

                            hexUpdated = TryUpdateHexTerrain(hex);

                            _drafts.Remove(draftId);
                        }

                        hex.Mission = 0;
                    }
                }

                character.Mission = 0;
            }

#if DEBUG
            if (isDone)
                DebugMapCharactersAndShips();
#endif

            //if (hexUpdated)
            //    BroadcastHexAndIcons(hex.X, hex.Y);
            //else
            //    BroadcastIcons(hex.X, hex.Y);
        }

        private bool TryUpdateHexTerrain(MapHex hex)
        {
            int hexBase = hex.Base;

            foreach (KeyValuePair<int, object> p in hex.Population)
            {
                if (_characters.TryGetValue(p.Key, out Character character) && character.ShipCount == 1)
                {
                    Ship ship = character.GetFirstShip();

                    Contract.Assert(_ships.ContainsKey(ship.Id));

                    switch (ship.ClassType)
                    {
                        case ClassTypes.kClassListeningPost:
                            hexBase = 5;
                            break;

                        case ClassTypes.kClassBaseStation:
                            hexBase = 3;
                            break;

                        case ClassTypes.kClassBattleStation:
                            hexBase = 2;
                            break;

                        case ClassTypes.kClassStarBase:
                            hexBase = 1;
                            break;

                        case ClassTypes.kClassSpecial:
                            ShipData data = _shiplist[ship.ShipClassName];

                            if (data.HullType == HullTypes.kHullDefensePlatform)
                                hexBase = 4;

                            break;
                    }
                }
            }

            if (hex.Base != hexBase)
            {
                hex.Base = hexBase;
                hex.BaseType = (BaseTypes)(1 << (hexBase - 1));

                return true;
            }

            return false;
        }

        /*
            private void TryFixReportIssues(Draft draft)
            {
                //apparently one of the players experienced some kind of issue during the mission
                //but there are some things in their favour:
                //    1. they didn't disconnected
                //    2. they sent a report
                //    3. their ships were reported, meaning:
                //        - if it is alive, and the owner is the same, probably it is ok to give him back the ships;
                //        - if it is alive, and the owner is not the same, probably his ship was captured, and then repaired by the server in a previous report;
                //        - if it is dead, then the ship was destroyed, and can be removed from the server;

                Contract.Assert(_dictIntObj.Count == 0);

                foreach (KeyValuePair<int, Character> p in _characters)
                {
                    Character character = p.Value;

                    for (int i = 0; i < character.ShipCount; i++)
                    {
                        if (_ships.ContainsKey(character.Ships[i]))
                        {
                            Ship ship = _ships[character.Ships[i]];

                            if (ship.OwnerID == character.Id)
                                ship.Flags = character.Id;
                        }
                    }
                }

                Dictionary<int, object> d = new Dictionary<int, object>();

                foreach (KeyValuePair<int, Ship> p in _ships)
                {
                    Ship ship = p.Value;

                    if (ship.Flags != 0)
                    {
                        // apparently everything is ok

                        ship.Flags = 0;
                    }
                    else if (_characters.TryGetValue(ship.OwnerID, out Character character))
                    {
                        Contract.Assert(ship.IsInAuction == 0 && ship.Damage.Items[(int)SystemTypes.ExtraDamage] != 0);

                        // apparently the player sent a bad report, but his ship survived
                        // what the heck, he even got a bonus (temporary ship)
                        // what can go wrong if we give him the ship back?

                        UpdateCharacter(character, ship);
                        RefreshCharacter(character);

                        d.Add(ship.Id, null);
                    }
                }

                foreach (KeyValuePair<int, Team> t in draft.Mission.Teams)
                {
                    Team team = t.Value;

                    Dictionary<int, Ship> e = new Dictionary<int, Ship>();

                    foreach (KeyValuePair<int, Ship> s in team.Ships)
                    {
                        Ship ship = s.Value;

                        if (!d.ContainsKey(ship.Id))
                            e.Add(ship.Id, ship);
                    }

                    team.Ships = e;
                }
            }
        */

        private void ProcessDrafts(Queue<int> queueInt)
        {
            if (_drafts.Count != 0)
            {
                Contract.Assert(queueInt.Count == 0);

                foreach (KeyValuePair<int, Draft> p in _drafts)
                {
                    Draft draft = p.Value;

                    draft.Countdown--;

                    if (draft.Countdown >= 0)
                    {
                        switch (draft.Countdown)
                        {
                            // phase 1

                            case (int)Phases.a0:
                                {
                                    // tries to create a mission

                                    MapHex hex = _map[p.Key];

                                    if (TryCreateMission(draft, hex))
                                    {
                                        // closes the hex

                                        hex.Mission |= IsClosedMask;

                                        // tries to send the configuration to everyone that accepted

                                        foreach (KeyValuePair<int, Team> q in draft.Mission.Teams)
                                        {
                                            Team team = q.Value;

                                            if (draft.Accepted.ContainsKey(team.OwnerId))
                                            {
                                                Character character = _characters[team.OwnerId];

                                                try
                                                {
                                                    R_SendConfig(character, draft.Mission);
                                                }
                                                catch (Exception)
                                                { }
                                            }
                                        }

                                        draft.Countdown = (int)Phases.b1;
                                    }
                                    else
                                        draft.Countdown = (int)Phases.a0;

                                    break;
                                }
                            case (int)Phases.a1:
                                {
                                    // at this point the cooldown is over. so any player that didn’t accepted or forfeited is counted as "having forfeited"

                                    foreach (KeyValuePair<int, Team> q in draft.Mission.Teams)
                                    {
                                        Team team = q.Value;

                                        if (draft.Expected.ContainsKey(team.OwnerId) && !draft.Accepted.ContainsKey(team.OwnerId))
                                            draft.Forfeited.TryAdd(team.OwnerId, null);
                                    }

                                    break;
                                }
                            case (int)Phases.a2:
                                throw new NotSupportedException();

                            // phase 2

                            case (int)Phases.b0:
                                {
                                    // tries to send the mission to everyone that accepted

                                    draft.TimeStamp = DateTime.Now;

                                    foreach (KeyValuePair<int, Team> q in draft.Mission.Teams)
                                    {
                                        Team team = q.Value;

                                        if (draft.Accepted.ContainsKey(team.OwnerId))
                                        {
                                            Character character = _characters[team.OwnerId];

                                            try
                                            {
                                                WriteMissionSetup(draft, character);
                                            }
                                            catch (Exception)
                                            { }
                                        }
                                    }

                                    draft.Countdown = (int)Phases.c1;

                                    break;
                                }
                            case (int)Phases.b1:
                                throw new NotSupportedException();

                            // phase 3

                            case (int)Phases.c0:
                                {
                                    // tries to send the ready signal to every player, one by one

                                    foreach (KeyValuePair<int, Team> q in draft.Mission.Teams)
                                    {
                                        Team team = q.Value;

                                        if (draft.Accepted.ContainsKey(team.OwnerId) && draft.Confirmed.ContainsKey(team.OwnerId))
                                        {
                                            if (draft.Ready.TryAdd(team.OwnerId, null))
                                            {
                                                Character character = _characters[team.OwnerId];

                                                try
                                                {
                                                    WriteMissionReady(character);

                                                    //CmdSound(character.Client, "suspense" + _rand.NextInt32(1, 4));

                                                    string[] playlist = draft.Mission.Musics;

                                                    for (int i = 0; i < playlist.Length; i++)
                                                        CmdMusic(character.Client, playlist[i]);
                                                }
                                                catch (Exception)
                                                {
                                                    CmdMusic(character.Client, string.Empty);
                                                }

                                                goto tryNextPlayer;
                                            }
                                        }
                                    }

                                    // resets the host

                                    draft.Mission.HostId = 0;

                                    // ends the draft

                                    draft.Countdown = (int)Phases.a0;

                                    break;

                                tryNextPlayer:

                                    draft.Countdown = (int)Phases.c1;

                                    break;
                                }

                            case (int)Phases.c1:
                                throw new NotSupportedException();

                            // common

                            default:
                                {
                                    // checks if everyone has already made a decision

                                    if (draft.Countdown > (int)Phases.a1 && draft.Countdown < (int)Phases.a2)
                                    {
                                        if (draft.Expected.Count == draft.Accepted.Count + draft.Forfeited.Count)
                                            draft.Countdown = (int)Phases.a1;
                                    }

                                    break;
                                }
                        }
                    }
                    else if (draft.Mission == null)
                    {
                        Contract.Assert(draft.Expected.Count == 1);

                        // the mission was not created for some reason
                        // so we can discard it safelly in the end

                        queueInt.Enqueue(p.Key);
                    }
                }

                // tries to remove any draft from which we were unable to create a mission

                while (queueInt.TryDequeue(out int draftId))
                    _drafts.Remove(draftId);
            }
        }

        private bool TryCreateMission(Draft draft, MapHex hex)
        {
            Character host = _characters[(int)((hex.Mission & HostMask) >> HostShift)];

            Contract.Assert(hex.Population.ContainsKey(host.Id));

            // creates the mission

            Mission mission = new()
            {
                HostId = host.Id,

                Map = 0,
                Background = _spaceBackgrounds[hex.Id % _spaceBackgrounds.Count],
                Speed = defaultMissionSpeed,

                Teams = [],

                CustomIds = [],
                Config = null
            };

            // initializes the race masks

            uint alliedRaces = (uint)_alliances[(int)host.CharacterRace];
            uint neutralRaces = (uint)_alliances[(int)Races.kNeutralRace];

            Contract.Assert((alliedRaces & neutralRaces) == 0);

            // initializes the team queues

            Queue<Character> alliedHuman = new();
            Queue<Character> alliedAI = new();
            Queue<Character> enemyHuman = new();
            Queue<Character> enemyAI = new();
            Queue<Character> neutralAI = new();

            // initializes the slots

            const int slotsSupported = 64; // HARD BOUND: number of 'tActor' and 'tState' supported by the game
            const int shipsSupported = 32; // SOFT BOUND: number of 'AI ships' supported by the game (tested with 1 player)

            int slotsAvailable = slotsSupported - 1; // 1 slot for each 'tScript'
            int shipsAvailable = shipsSupported;     // all the other ships will spawn as if they had no crew on board

            // adds the host team

            TryEnqueue(alliedHuman, host, ref slotsAvailable, ref shipsAvailable);

            foreach (KeyValuePair<int, object> p in hex.Population)
            {
                Character character = _characters[p.Key];

                if (character.Id != host.Id && character.Mission == host.Mission)
                {
                    uint mask = 1u << (int)character.CharacterRace;

                    if ((alliedRaces & mask) != 0)
                    {
                        if
                        (
                            draft.Accepted.ContainsKey(character.Id) &&
                            (character.State & Character.States.IsHumanBusyOnline) == Character.States.IsHumanBusyOnline &&
                            character.Client.LauncherId != 0
                        )
                        {
                            if (!TryEnqueue(alliedHuman, character, ref slotsAvailable, ref shipsAvailable))
                                goto notSupported;
                        }
                        else if (character.State == Character.States.IsCpuAfkBusyOnline)
                        {
                            if (!TryEnqueue(alliedAI, character, ref slotsAvailable, ref shipsAvailable))
                                goto notSupported;
                        }
                    }
                    else if ((neutralRaces & mask) != 0)
                    {
                        if (character.State == Character.States.IsCpuAfkBusyOnline)
                        {
                            if (!TryEnqueue(neutralAI, character, ref slotsAvailable, ref shipsAvailable))
                                goto notSupported;
                        }
                    }
                    else
                    {
                        if
                        (
                            draft.Accepted.ContainsKey(character.Id) &&
                            (character.State & Character.States.IsHumanBusyOnline) == Character.States.IsHumanBusyOnline &&
                            character.Client.LauncherId != 0
                        )
                        {
                            if (!TryEnqueue(enemyHuman, character, ref slotsAvailable, ref shipsAvailable))
                                goto notSupported;
                        }
                        else if (character.State == Character.States.IsCpuAfkBusyOnline)
                        {
                            if (!TryEnqueue(enemyAI, character, ref slotsAvailable, ref shipsAvailable))
                                goto notSupported;
                        }
                    }
                }
            }

            // checks the number of teams

            if (alliedHuman.Count + enemyHuman.Count > 6) // the game only supports 6 human players
                goto notSupported;

            int totalAllied = alliedHuman.Count + alliedAI.Count;
            int totalEnemy = enemyHuman.Count + enemyAI.Count;
            int totalNeutral = neutralAI.Count;

            if (totalAllied + totalEnemy + totalNeutral > 20) // the game only supports 20 teams
                goto notSupported;

            // creates the human teams

            while (alliedHuman.Count > 0)
                AddTeam(mission, alliedHuman.Dequeue(), TeamTags.kTagA);

            while (enemyHuman.Count > 0)
                AddTeam(mission, enemyHuman.Dequeue(), TeamTags.kTagB);

            // creates the AI teams

            while (alliedAI.Count > 0)
                AddTeam(mission, alliedAI.Dequeue(), TeamTags.kTagA);

            while (enemyAI.Count > 0)
                AddTeam(mission, enemyAI.Dequeue(), TeamTags.kTagB);

            // creates the neutral teams

            while (neutralAI.Count > 0)
                AddTeam(mission, neutralAI.Dequeue(), TeamTags.kTagC);

            // tries to create the map

            CreateScriptMap(hex, mission, out int planetBits, out int baseBits, out int specialBits, out MapTemplate mapTemplate);

            // tries to create the configuration

            if (!TryCreateScriptINI(mission, in totalAllied, in totalEnemy, in totalNeutral, in planetBits, in baseBits, in specialBits, mapTemplate))
                goto notSupported;

            // assigns the mission to the draft

            draft.Mission = mission;

            return true;

        notSupported:

            Contract.Assert(draft.Mission == null);

            return false;
        }

        private bool TryEnqueue(Queue<Character> team, Character character, ref int slotsAvailable, ref int shipsAvailable)
        {
            team.Enqueue(character);

            slotsAvailable -= character.ShipCount + 2; // 1 slot for each 'tShip', 'tTeam' and 'tVictoryCondition'
            shipsAvailable -= character.ShipCount;

            return slotsAvailable >= 0 && shipsAvailable >= 0;
        }

        private void AddTeam(Mission mission, Character character, TeamTags teamTag)
        {
            TeamIds teamId = (TeamIds)mission.Teams.Count;
            TeamTypes teamType = (teamId == TeamIds.kTeam1) ? TeamTypes.kPrimaryTeam : TeamTypes.kPlayableTeam;

            // creates a new team

            Team team = new(character, teamId, teamType, teamTag);

            // adds the ships

            int c = character.ShipCount;

            for (int i = 0; i < c; i++)
            {
                Ship ship = character.GetShipAt(i);

                Contract.Assert(_ships.ContainsKey(ship.Id));

                team.Ships.Add(ship.Id, ship);
            }

            // adds the team

            mission.Teams.Add(character.Id, team);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void R_SendConfig(Character character, Mission mission)
        {
            if (_launchers.TryGetValue(character.Client.LauncherId, out Client27001 launcher))
            {
                string data = mission.Config.Replace("%ti%", ((int)mission.Teams[character.Id].Id).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

                Clear();

                Push(data); // file data
                Push(0x00); // file name
                Push((byte)0x00); // opcode

                Write(launcher);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void PushHostMissions(Client27000 client)
        {
            long mission = client.Character.Mission;

            Contract.Assert(mission != 0);

            Push((byte)0x00);
            Push(DraftCooldown * 1000); // cooldown (ms)

            Push((byte)((mission & IsMustPlayMask) >> IsMustPlayShift));
            Push(_missionNames[(int)((mission & MissionMask) >> MissionShift)]);

            Push(0x01); // count

            Push((byte)0x00);
            Push(client.Id, client.Relays[(int)ClientRelays.MissionRelayNameC], 0x04);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void PushGuestMissions(Client27000 client)
        {
            long mission = client.Character.Mission;

            Contract.Assert(mission != 0);

            Push((byte)0x01);
            Push(DraftCooldown * 1000); // cooldown (ms)

            Push((byte)0x01);
            Push(_missionNames[(int)((mission & MissionMask) >> MissionShift)]);

            Push(0x01); // count

            // reply header

            Push(0x07);
            Push((int)ServerRelays.AVtMissionMatcherRelayS);
            Push(0x00);

            Push((byte)0x01);

            // msg header

            Push(client.Id, client.Relays[(int)ClientRelays.MissionRelayNameC], 0x04);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void WriteMissionSetup(Draft draft, Character character)
        {
            /*
                [Q] 22000000 00 00000000_0f000000_03000000 0d000000 01000000 00000000 55010000 01
                [R] 7c070000 00 01000000_10000000_02000000 67070000 01 00000000_0f000000_0c000100 (...)
            */

            Mission mission = draft.Mission;
            Character host = _characters[mission.HostId];

            // sorts the teams and counts the human players

            Stack<Team> stack = new();
            int count = 0;

            Team team;
            Character owner;

            foreach (KeyValuePair<int, Team> p in mission.Teams)
            {
                team = p.Value;

                stack.Push(team);

                owner = _characters[p.Key];

                if ((owner.State & Character.States.IsHumanOnline) == Character.States.IsHumanOnline)
                    count++;
            }

            Contract.Assert(count > 0);

            //--------------------------------------------------------------------------------------------------------------

            Clear();

            // undefined hex (12 bytes) ?

            Push(MapHex.Empty);

            // human players

            Push(count);

            // era

            Push(CurrentEra);

            // difficulty level

            Push(_difficultyLevel >> 1);

            //--------------------------------------------------------------------------------------------------------------

            while (stack.Count > 0)
            {
                // gets the current team and owner

                team = stack.Pop();
                owner = _characters[team.OwnerId];

                // checks if the team is controlled by the CPU

                bool isHuman = (owner.State & Character.States.IsHumanOnline) == Character.States.IsHumanOnline;

                if (isHuman)
                    Push((byte)0x00);
                else
                    Push((byte)0x01);

                // eol

                Push(0x00);

                // ships

                count = owner.ShipCount;

                Utils.Rent(4096, out byte[] b, out MemoryStream m, out BinaryWriter w, out BinaryReader r);

                for (int i = count - 1; i >= 0; i--)
                {
                    Ship original = owner.GetShipAt(i);

                    Contract.Assert(_ships.ContainsKey(original.Id));

                    m.Seek(0, SeekOrigin.Begin);

                    original.WriteTo(w);

                    m.Seek(0, SeekOrigin.Begin);

                    Ship clone = new(r);

                    // the spare parts are not sent at this moment
                    // they are added later in the mission script

                    clone.Stores.DamageControl.CurrentQuantity = 0;

                    if (!isHuman)
                    {
                        ApplyAprPowerBoost(clone, _cpuPowerBoost);
                        UpgradeShipOfficers(clone, _cpuOfficerRank);
                    }

                    Push(clone);
                }

                Utils.Return(b, m, w, r);

                // ship count

                Push(count);

                // team

                Push((int)team.Type);
                Push(owner.Id);
                Push((int)team.Id);
                Push(owner.CharacterRating);
                Push((int)owner.CharacterRank);
                Push((int)owner.CharacterRace);
                Push(owner.CharacterName);
            }

            // team count

            Push(mission.Teams.Count);

            //--------------------------------------------------------------------------------------------------------------

            // political tension matrice
            // (...)

            Push(0x00);               // size of the political tension matrice
            Push((int)Races.kNoRace); // race of the primary opponent team

            //--------------------------------------------------------------------------------------------------------------

            Races primaryTeamRace = host.CharacterRace;

            Races alliedRace = Races.kNoRace;
            Races enemyRace = Races.kNoRace;

            // political tension matrice

            for (Races race = Races.kLastNeutral; race >= Races.kFirstEmpire; race--)
            {
                if (race == primaryTeamRace)
                {
                    // we love our people

                    Push(0.0f);
                    Push(0);
                }
                else if (((1 << (int)race) & (int)_alliances[(int)primaryTeamRace]) != 0)
                {
                    alliedRace = race;

                    // we care about our allies

                    Push(0.25f);
                    Push(250);
                }
                else
                {
                    enemyRace = race;

                    // we hate our enemies

                    Push(1.0f);
                    Push(1000);
                }

                Push((int)race);
            }

            Push((int)Races.kNumberOfRaces); // size of the political tension matrice
            Push((int)primaryTeamRace);      // race of the primary team

            //--------------------------------------------------------------------------------------------------------------

            // allied race

            Push((int)alliedRace);

            // enemy race

            Push((int)enemyRace);

            // game speed

            Push(mission.Speed);

            // host name (shuffled)

            Push(string.Empty); // host.CharacterName

            // Metaverse (shuffled)

            Push(string.Empty); // "Metaverse"

            // host IP

            Push(host.IPAddress);

            // space background

            Push(mission.Background);

            // mission location

            Push(host.CharacterLocationY);
            Push(host.CharacterLocationX);

            // stardate

            PushStardate();

            // mission map

            Push(mission.Map);

            // mission title

            Push(defaultMissionTitle);

            // mission time

            Push(TimeService.NewsTime(draft.TimeStamp));

            //--------------------------------------------------------------------------------------------------------------

            Client27000 client = character.Client;

            count = (character.Id == host.Id) ? 2 : 3;

            // reply header

            Push(0x0c);
            Push((int)ServerRelays.AVtMissionMatcherRelayS);
            Push(0x00);

            Push((byte)0x01);

            // msg header

            Push(client.Id, client.Relays[(int)ClientRelays.MissionRelayNameC], count);

            Write(client);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void WriteMissionReady(Character character)
        {
            Client27000 client = character.Client;

            /*
                [Q] 26000000 00 00000000_0f000000_0c000100 11000000 01 06000000_01000000_10000000 55010000
                [R] 2a000000 00 01000000_10000000_06000000 15000000 01 00000000_0f000000_0d000100 00000000 00000000
            */

            Clear();

            Push(0x00);
            Push(0x00);

            // reply header

            Push(0x0d);
            Push((int)ServerRelays.AVtMissionMatcherRelayS);
            Push(0x00);

            Push((byte)0x01);

            // msg header

            Push(client.Id, client.Relays[(int)ClientRelays.MissionRelayNameC], 0x06);

            Write(client);
        }
    }
}
