using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace SfcOpServer
{
    public partial class GameServer
    {
        private void CreateScriptMap(MapHex hex, Mission mission, out int planetBits, out int baseBits, out int specialBits, out MapTemplate mapTemplate)
        {
            planetBits = 0;
            baseBits = 0;
            specialBits = 0;

            // the template randomizer is seeded here, or 'locked', so we can replicate the same setup each time we visit the same hex
            // in the end we 'unlock' it to be able to randomize what cames next

            MapTemplate.LockRnd((ulong)hex.Id);

            int i = hex.X + hex.Y * _mapWidth;
            int j = MapTemplate.NextInt32(_genericMapTemplates.Count); // ... this allows us to keep the same setup even if we later index a random template (skips one random number)

            if (!_indexedMapTemplates.TryGetValue(i, out mapTemplate))
                mapTemplate = _genericMapTemplates[j];

            mapTemplate.Update(hex, _terrainContents);

            MapTemplate.UnlockRnd();

            // populates the template with the mission ships, bases and planets

            i = 1;

            foreach (KeyValuePair<int, Team> p in mission.Teams)
            {
                Character owner = _characters[p.Key];
                Team team = p.Value;
                Ship ship = owner.GetFirstShip();

                if (ship.ClassType == ClassTypes.kClassPlanets)
                {
                    planetBits |= i;

                    mapTemplate.AddPlanet();
                }
                else if (ship.ClassType >= ClassTypes.kClassListeningPost && ship.ClassType <= ClassTypes.kClassStarBase)
                {
                    baseBits |= i;

                    mapTemplate.AddBase();
                }
                else if (ship.ClassType == ClassTypes.kClassSpecial)
                {
                    specialBits |= i;

                    mapTemplate.AddSpecial();
                }
                else if (team.Tag == TeamTags.kTagA)
                    mapTemplate.AddAlliedShip();
                else if (team.Tag == TeamTags.kTagB)
                    mapTemplate.AddEnemyShip();
                else
                {
                    Contract.Assert(team.Tag == TeamTags.kTagC);

                    mapTemplate.AddNeutralShip();
                }

                i <<= 1;
            }
        }
    }
}
