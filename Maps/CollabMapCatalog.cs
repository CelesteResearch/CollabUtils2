using Celeste.Core.Display;
using Celeste.Core.Maps;
using Celeste.Everest.Content.Maps.Binary;
using Celeste.Formats.Map.Binary.Source;
using Celeste.Everest.Content.Maps.Processing;
using Celeste.Graphics.Display.Colors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mods.CollabUtils2.Maps
{
    public sealed class CollabMapCatalog
    {
        // GymLevels: maps map SIDs to their gym level info
        // GymTech: maps collab IDs and tech names to gym tech info
        public Dictionary<string, GymLevelInfo> GymLevels = new Dictionary<string, GymLevelInfo>();

        public Dictionary<string, Dictionary<string, GymTechInfo>> GymTech =
            new Dictionary<string, Dictionary<string, GymTechInfo>>();

        // the structure here is: SilverBerries[LevelSet][SID] = ID of the silver berry in that map.
        // so, to check if all silvers in a levelset have been unlocked, go through all entries in SilverBerries[levelset].
        public Dictionary<string, Dictionary<string, MapEntityId>> SilverBerries =
            new Dictionary<string, Dictionary<string, MapEntityId>>();

        public Dictionary<string, SpeedBerryInfo> SpeedBerries = new Dictionary<string, SpeedBerryInfo>();

        public HashSet<string> MapsWithSilverBerries = new HashSet<string>();
        public HashSet<string> MapsWithRainbowBerries = new HashSet<string>();

        public void Clear()
        {
            GymLevels.Clear();
            GymTech.Clear();
            SilverBerries.Clear();
            SpeedBerries.Clear();
            MapsWithSilverBerries.Clear();
            MapsWithRainbowBerries.Clear();
        }
    }
}
